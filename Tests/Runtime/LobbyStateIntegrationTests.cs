using System.Collections.Generic;
using NUnit.Framework;
using SocketIOUnity.Runtime;
using SocketIOUnity.Transport;

namespace SocketIOUnity.Tests
{
    /// <summary>
    /// Validates the socket-level invariants that LobbyStateStore.SetSocket() relies on.
    ///
    /// LobbyStateStore cannot be tested directly from this assembly (it is a MonoBehaviour
    /// in Assembly-CSharp, which is not referenced here). These tests instead verify the
    /// SocketIOClient / NamespaceSocket contract that the store's derived properties depend on:
    ///
    ///   SOT-13  IsConnected = (socket.State == Connected) — timing relative to lobby namespace
    ///   SOT-15  store.OnConnected fires from lobbyNamespace.OnConnected (not root Connected)
    ///           store.OnDisconnected fires from socket.OnStateChanged → Disconnected
    /// </summary>
    public class LobbyStateIntegrationTests
    {
        // ----------------------------------------------------------------
        // Stub transport (shared with ConnectionStateTests pattern)
        // ----------------------------------------------------------------

        private class StubTransport : ITransport
        {
#pragma warning disable CS0067
            public event System.Action OnOpen;
            public event System.Action<byte[]> OnBinaryMessage;
            public event System.Action<SocketError> OnError;
#pragma warning restore CS0067
            public event System.Action OnClose;
            public event System.Action<string> OnTextMessage;

            public void Connect(string url) { }
            public void Close() { }
            public void Dispatch() { }
            public void SendText(string message) { }
            public void SendBinary(byte[] data) { }

            public void SimulateEngineHandshake() =>
                OnTextMessage?.Invoke(
                    "0{\"sid\":\"stub-sid\",\"upgrades\":[],\"pingInterval\":25000,\"pingTimeout\":5000}");

            public void SimulateNamespaceConnect(string ns = "/") =>
                OnTextMessage?.Invoke(ns == "/" ? "40" : $"40{ns},");

            public void SimulateUnexpectedClose() => OnClose?.Invoke();
        }

        private StubTransport _stub;

        private SocketIOClient BuildSocket()
        {
            return new SocketIOClient(() =>
            {
                _stub = new StubTransport();
                return _stub;
            });
        }

        // ----------------------------------------------------------------
        // SOT-13: IsConnected timing
        // ----------------------------------------------------------------

        /// <summary>
        /// Documents the intentional gap: socket.State becomes Connected when "/" connects,
        /// which is BEFORE the /lobby namespace confirms. LobbyStateStore.IsConnected reflects
        /// this — true slightly before OnConnected fires. UI buttons are gated by OnConnected
        /// (fired from lobbyNamespace.OnConnected), so this window is unreachable in normal flow.
        /// </summary>
        [Test]
        public void SocketState_Connected_BeforeLobbyNamespace_Connects()
        {
            var socket = BuildSocket();
            var lobby  = socket.Of("/lobby");

            bool lobbyOnConnectedFired = false;
            lobby.OnConnected += () => lobbyOnConnectedFired = true;

            socket.Connect("ws://localhost:3001");
            _stub.SimulateEngineHandshake();
            _stub.SimulateNamespaceConnect("/");   // root "/" connects

            Assert.AreEqual(ConnectionState.Connected, socket.State,
                "socket.State is Connected once '/' namespace acks");
            Assert.IsFalse(lobbyOnConnectedFired,
                "lobby.OnConnected has NOT fired yet — /lobby namespace hasn't connected");

            _stub.SimulateNamespaceConnect("/lobby");  // /lobby connects

            Assert.IsTrue(lobbyOnConnectedFired,
                "lobby.OnConnected fires after /lobby namespace acks");
        }

        [Test]
        public void SocketState_NotConnected_BeforeAnyConnect()
        {
            var socket = BuildSocket();
            Assert.AreNotEqual(ConnectionState.Connected, socket.State);
        }

        [Test]
        public void SocketState_NotConnected_DuringConnecting()
        {
            var socket = BuildSocket();
            socket.Connect("ws://localhost:3001");
            Assert.AreNotEqual(ConnectionState.Connected, socket.State);
        }

        [Test]
        public void SocketState_NotConnected_AfterDisconnect()
        {
            var socket = BuildSocket();
            socket.ReconnectConfig = new ReconnectConfig { autoReconnect = false };
            socket.Connect("ws://localhost:3001");
            _stub.SimulateEngineHandshake();
            _stub.SimulateNamespaceConnect("/");

            Assert.AreEqual(ConnectionState.Connected, socket.State);

            _stub.SimulateUnexpectedClose();
            Assert.AreNotEqual(ConnectionState.Connected, socket.State,
                "socket.State must not be Connected after disconnect");
        }

        [Test]
        public void SocketState_NotConnected_DuringReconnecting()
        {
            var socket = BuildSocket();
            // autoReconnect = true (default) → state becomes Reconnecting on close
            socket.Connect("ws://localhost:3001");
            _stub.SimulateEngineHandshake();
            _stub.SimulateNamespaceConnect("/");

            _stub.SimulateUnexpectedClose();

            Assert.AreEqual(ConnectionState.Reconnecting, socket.State,
                "State is Reconnecting during backoff");
            Assert.AreNotEqual(ConnectionState.Connected, socket.State,
                "IsConnected (derived) must be false during Reconnecting");
        }

        // ----------------------------------------------------------------
        // SOT-15: Event sources — which socket event drives which store event
        // ----------------------------------------------------------------

        /// <summary>
        /// store.OnConnected must derive from lobbyNamespace.OnConnected, not root OnConnected
        /// or socket.OnStateChanged(Connected). This test pins the correct source.
        /// </summary>
        [Test]
        public void LobbyNamespace_OnConnected_FiresAfterNamespaceAck_NotAfterRoot()
        {
            var socket = BuildSocket();
            var lobby  = socket.Of("/lobby");

            var connectedSequence = new List<string>();
            socket.OnConnected             += () => connectedSequence.Add("root.OnConnected");
            socket.OnStateChanged          += s  => { if (s == ConnectionState.Connected) connectedSequence.Add("socket.OnStateChanged(Connected)"); };
            lobby.OnConnected              += () => connectedSequence.Add("lobby.OnConnected");

            socket.Connect("ws://localhost:3001");
            _stub.SimulateEngineHandshake();
            _stub.SimulateNamespaceConnect("/");

            // At this point: root.OnConnected + OnStateChanged(Connected) have fired
            // lobby.OnConnected has NOT fired yet
            Assert.IsFalse(connectedSequence.Contains("lobby.OnConnected"),
                "lobby.OnConnected must not fire before /lobby namespace acks");

            _stub.SimulateNamespaceConnect("/lobby");

            Assert.IsTrue(connectedSequence.Contains("lobby.OnConnected"),
                "lobby.OnConnected must fire after /lobby namespace acks");

            // Verify ordering: root fires first, then lobby
            int rootIdx  = connectedSequence.IndexOf("root.OnConnected");
            int lobbyIdx = connectedSequence.IndexOf("lobby.OnConnected");
            Assert.Less(rootIdx, lobbyIdx,
                "root.OnConnected must fire before lobby.OnConnected");
        }

        /// <summary>
        /// store.OnDisconnected derives from socket.OnStateChanged → Disconnected.
        /// Verify that OnStateChanged fires Disconnected on unexpected close with autoReconnect=false.
        /// </summary>
        [Test]
        public void SocketOnStateChanged_Disconnected_FiresOnUnexpectedClose_WhenNoAutoReconnect()
        {
            var socket = BuildSocket();
            socket.ReconnectConfig = new ReconnectConfig { autoReconnect = false };

            var states = new List<ConnectionState>();
            socket.OnStateChanged += s => states.Add(s);

            socket.Connect("ws://localhost:3001");
            _stub.SimulateEngineHandshake();
            _stub.SimulateNamespaceConnect("/");
            _stub.SimulateUnexpectedClose();

            Assert.IsTrue(states.Contains(ConnectionState.Disconnected),
                "OnStateChanged must fire Disconnected on unexpected close");
        }

        /// <summary>
        /// With autoReconnect=true, unexpected close transitions to Reconnecting — not Disconnected.
        /// store.OnDisconnected should NOT fire until reconnect is fully exhausted or Disconnect() called.
        /// </summary>
        [Test]
        public void SocketOnStateChanged_Reconnecting_NotDisconnected_WhenAutoReconnectOn()
        {
            var socket = BuildSocket();
            // autoReconnect = true (default)

            bool disconnectedFired = false;
            socket.OnStateChanged += s =>
            {
                if (s == ConnectionState.Disconnected) disconnectedFired = true;
            };

            socket.Connect("ws://localhost:3001");
            _stub.SimulateEngineHandshake();
            _stub.SimulateNamespaceConnect("/");
            _stub.SimulateUnexpectedClose();

            Assert.AreEqual(ConnectionState.Reconnecting, socket.State);
            Assert.IsFalse(disconnectedFired,
                "OnStateChanged(Disconnected) must NOT fire while reconnect backoff is active — " +
                "store.OnDisconnected would incorrectly wipe room state during a transient disconnect");
        }

        // ----------------------------------------------------------------
        // SOT-15: Manual Reconnect() path — socket.Connect() re-entry
        // ----------------------------------------------------------------

        /// <summary>
        /// LobbyNetworkManager.Reconnect() calls _root.Connect(url) after a disconnect.
        /// Verify state transitions are correct on re-connect after disconnect.
        /// </summary>
        [Test]
        public void Connect_AfterDisconnect_TransitionsToConnecting()
        {
            var socket = BuildSocket();
            socket.ReconnectConfig = new ReconnectConfig { autoReconnect = false };

            socket.Connect("ws://localhost:3001");
            _stub.SimulateEngineHandshake();
            _stub.SimulateNamespaceConnect("/");
            _stub.SimulateUnexpectedClose();

            Assert.AreEqual(ConnectionState.Disconnected, socket.State);

            // Simulate Reconnect() button click
            socket.Connect("ws://localhost:3001");
            Assert.AreEqual(ConnectionState.Connecting, socket.State,
                "Re-calling Connect() after disconnect must transition to Connecting");
        }

        [Test]
        public void Connect_AfterDisconnect_CanReachConnected_Again()
        {
            var socket = BuildSocket();
            socket.ReconnectConfig = new ReconnectConfig { autoReconnect = false };

            socket.Connect("ws://localhost:3001");
            _stub.SimulateEngineHandshake();
            _stub.SimulateNamespaceConnect("/");
            _stub.SimulateUnexpectedClose();

            // Reconnect
            socket.Connect("ws://localhost:3001");
            _stub.SimulateEngineHandshake();
            _stub.SimulateNamespaceConnect("/");

            Assert.AreEqual(ConnectionState.Connected, socket.State,
                "Full reconnect cycle must end in Connected state");
        }
    }
}
