using System;
using System.Collections.Generic;
using NUnit.Framework;
using SocketIOUnity.Runtime;
using SocketIOUnity.Transport;

namespace SocketIOUnity.Tests
{
    /// <summary>
    /// Tests for ConnectionState transitions and OnStateChanged event (v1.2.1).
    ///
    /// Coverage:
    ///   SOT-03  State transitions through connect, disconnect, and reconnect cycles
    ///   SOT-04  OnStateChanged event fires on every state change (and not spuriously)
    ///   SOT-02  Namespace event handlers survive a reconnect (regression guard)
    /// </summary>
    public class ConnectionStateTests
    {
        // --------------------------------------------------
        // Stub transport — manually controls message delivery
        // --------------------------------------------------

        /// <summary>
        /// A fully synchronous fake transport. Connect() and Close() are no-ops;
        /// tests drive the connection lifecycle by calling SimulateX() helpers.
        /// </summary>
        private class StubTransport : ITransport
        {
#pragma warning disable CS0067 // interface members required but never raised by this stub
            public event Action OnOpen;
            public event Action<byte[]> OnBinaryMessage;
            public event Action<SocketError> OnError;
#pragma warning restore CS0067
            public event Action OnClose;
            public event Action<string> OnTextMessage;

            // Called by EngineIOClient.Connect() — no-op; test controls timing
            public void Connect(string url) { }

            // Called by EngineIOClient.Disconnect() — no-op; we don't want a spurious OnClose
            public void Close() { }

            // Called by EngineIOClient.Tick() — no-op; tests fire messages directly
            public void Dispatch() { }

            public void SendText(string message) { }
            public void SendBinary(byte[] data) { }

            // --------------------------------------------------
            // Test helpers
            // --------------------------------------------------

            /// <summary>
            /// Deliver a valid Engine.IO handshake (type 0).
            /// Causes EngineIOClient to set IsConnected = true and fire OnOpen → default namespace SendConnect.
            /// </summary>
            public void SimulateEngineHandshake() =>
                OnTextMessage?.Invoke(
                    "0{\"sid\":\"stub-sid\",\"upgrades\":[],\"pingInterval\":25000,\"pingTimeout\":5000}");

            /// <summary>
            /// Deliver a Socket.IO CONNECT ack for the given namespace.
            /// After "/" connects, SocketIOClient transitions to Connected.
            /// </summary>
            public void SimulateNamespaceConnect(string ns = "/") =>
                OnTextMessage?.Invoke(ns == "/" ? "40" : $"40{ns},");

            /// <summary>
            /// Simulate an unexpected transport close (e.g. server crash / network drop).
            /// Fires EngineIOClient's HandleTransportClose → SocketIOClient.HandleEngineClose.
            /// </summary>
            public void SimulateUnexpectedClose() => OnClose?.Invoke();
        }

        // --------------------------------------------------
        // Helpers
        // --------------------------------------------------

        private StubTransport _currentStub;

        /// <summary>
        /// Build a SocketIOClient whose factory always captures the latest stub into _currentStub.
        /// </summary>
        private SocketIOClient BuildSocket()
        {
            return new SocketIOClient(() =>
            {
                _currentStub = new StubTransport();
                return _currentStub;
            });
        }

        /// <summary>
        /// Drive a full successful connection: engine handshake → Socket.IO CONNECT for "/".
        /// After this call socket.State == Connected.
        /// </summary>
        private void CompleteConnection(SocketIOClient socket)
        {
            _currentStub.SimulateEngineHandshake();
            _currentStub.SimulateNamespaceConnect("/");
        }

        // --------------------------------------------------
        // SOT-03: State transitions
        // --------------------------------------------------

        [Test]
        public void InitialState_IsDisconnected()
        {
            var socket = BuildSocket();
            Assert.AreEqual(ConnectionState.Disconnected, socket.State);
        }

        [Test]
        public void Connect_SetsState_ToConnecting()
        {
            var socket = BuildSocket();
            socket.Connect("ws://localhost:3000");
            Assert.AreEqual(ConnectionState.Connecting, socket.State);
        }

        [Test]
        public void AfterHandshakeAndNamespaceConnect_StateIsConnected()
        {
            var socket = BuildSocket();
            socket.Connect("ws://localhost:3000");
            CompleteConnection(socket);
            Assert.AreEqual(ConnectionState.Connected, socket.State);
        }

        [Test]
        public void Disconnect_FromConnected_SetsState_ToDisconnected()
        {
            var socket = BuildSocket();
            socket.Connect("ws://localhost:3000");
            CompleteConnection(socket);

            socket.Disconnect();
            Assert.AreEqual(ConnectionState.Disconnected, socket.State);
        }

        [Test]
        public void UnexpectedClose_WithAutoReconnect_SetsState_ToReconnecting()
        {
            var socket = BuildSocket();
            socket.Connect("ws://localhost:3000");
            CompleteConnection(socket);

            // Default config has autoReconnect = true
            _currentStub.SimulateUnexpectedClose();
            Assert.AreEqual(ConnectionState.Reconnecting, socket.State);
        }

        [Test]
        public void UnexpectedClose_WithAutoReconnectDisabled_SetsState_ToDisconnected()
        {
            var socket = BuildSocket();
            socket.ReconnectConfig = new ReconnectConfig { autoReconnect = false };
            socket.Connect("ws://localhost:3000");
            CompleteConnection(socket);

            _currentStub.SimulateUnexpectedClose();
            Assert.AreEqual(ConnectionState.Disconnected, socket.State);
        }

        [Test]
        public void AfterReconnect_StateReturnsToConnected()
        {
            var socket = BuildSocket();
            socket.Connect("ws://localhost:3000");
            CompleteConnection(socket);

            // Drop connection — triggers reconnect
            _currentStub.SimulateUnexpectedClose();
            Assert.AreEqual(ConnectionState.Reconnecting, socket.State);

            // Manually fire one reconnect attempt (normally driven by tick loop)
            socket.AttemptReconnect();

            // _currentStub is now the NEW stub created by ReconnectEngine()
            CompleteConnection(socket);
            Assert.AreEqual(ConnectionState.Connected, socket.State);
        }

        [Test]
        public void ReconnectExhausted_SetsState_ToDisconnected()
        {
            var socket = BuildSocket();
            socket.ReconnectConfig = new ReconnectConfig
            {
                autoReconnect = true,
                maxAttempts = 1,
                initialDelay = 0f
            };

            socket.Connect("ws://localhost:3000");
            CompleteConnection(socket);
            _currentStub.SimulateUnexpectedClose();

            // Grab ReconnectController via reflection (internal, but InternalsVisibleTo covers test assembly)
            var reconnectField = typeof(SocketIOClient).GetField("_reconnect",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var reconnect = (ReconnectController)reconnectField.GetValue(socket);

            // Force _attempt to maxAttempts so the next Tick fires the exhaustion path
            var attemptField = typeof(ReconnectController).GetField("_attempt",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            attemptField.SetValue(reconnect, 1); // maxAttempts=1, _attempt=1 → exhausted on next Tick

            // Force _nextAttemptTime in the past so Tick proceeds past the timing check
            var nextAttemptField = typeof(ReconnectController).GetField("_nextAttemptTime",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            nextAttemptField.SetValue(reconnect, -1f);

            reconnect.Tick();

            Assert.AreEqual(ConnectionState.Disconnected, socket.State,
                "State must be Disconnected once reconnect attempts are exhausted");
        }

        // --------------------------------------------------
        // SOT-04: OnStateChanged event
        // --------------------------------------------------

        [Test]
        public void OnStateChanged_FiresWithConnecting_OnConnect()
        {
            var socket = BuildSocket();
            var received = new List<ConnectionState>();
            socket.OnStateChanged += s => received.Add(s);

            socket.Connect("ws://localhost:3000");

            Assert.Contains(ConnectionState.Connecting, received);
        }

        [Test]
        public void OnStateChanged_FiresWithConnected_AfterHandshake()
        {
            var socket = BuildSocket();
            var received = new List<ConnectionState>();
            socket.OnStateChanged += s => received.Add(s);

            socket.Connect("ws://localhost:3000");
            CompleteConnection(socket);

            Assert.Contains(ConnectionState.Connected, received);
        }

        [Test]
        public void OnStateChanged_FiresWithDisconnected_OnDisconnect()
        {
            var socket = BuildSocket();
            var received = new List<ConnectionState>();
            socket.OnStateChanged += s => received.Add(s);

            socket.Connect("ws://localhost:3000");
            CompleteConnection(socket);
            socket.Disconnect();

            Assert.Contains(ConnectionState.Disconnected, received);
        }

        [Test]
        public void OnStateChanged_NotFiredWhenStateUnchanged()
        {
            var socket = BuildSocket();
            int fireCount = 0;
            socket.OnStateChanged += _ => fireCount++;

            // Connect once → should fire Connecting then Connected
            socket.Connect("ws://localhost:3000");
            CompleteConnection(socket);
            int countAfterFirstConnect = fireCount;

            // "Connect" again with same state — should NOT fire because state doesn't change
            // (State is already Connected; re-calling Connect sets Connecting which IS a change,
            // so instead we verify that identical AttemptReconnect calls don't double-fire Reconnecting)
            _currentStub.SimulateUnexpectedClose(); // → Reconnecting (fires once)
            int afterClose = fireCount;

            // Fire AttemptReconnect twice in a row — second SetState(Reconnecting) should be no-op
            socket.AttemptReconnect();
            socket.AttemptReconnect();
            int afterDoubleAttempt = fireCount;

            Assert.AreEqual(afterClose, afterDoubleAttempt,
                "Repeated AttemptReconnect should not re-fire OnStateChanged for already-Reconnecting state");
        }

        // --------------------------------------------------
        // SOT-02: Namespace handler survival (regression guard)
        // --------------------------------------------------

        [Test]
        public void NamespaceHandlers_SurviveReconnect()
        {
            var socket = BuildSocket();
            socket.Connect("ws://localhost:3000");
            CompleteConnection(socket);

            // Grab the default namespace socket and register a handler
            var nsBefore = socket.Of("/");
            nsBefore.On("player_moved", (string _) => { });

            // Simulate unexpected disconnect → reconnect
            _currentStub.SimulateUnexpectedClose();
            socket.AttemptReconnect();
            CompleteConnection(socket); // completes on the new stub

            // The namespace socket reference must be the same instance
            var nsAfter = socket.Of("/");
            Assert.AreSame(nsBefore, nsAfter,
                "NamespaceSocket instance must survive reconnect — recreating it loses all On() handlers");

            // The handler must still be present in the event registry
            var eventsField = typeof(NamespaceSocket).GetField("_events",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var eventRegistry = eventsField.GetValue(nsAfter);
            var handlersField = typeof(EventRegistry).GetField("_handlers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var handlers = (Dictionary<string, List<Action<string>>>)handlersField.GetValue(eventRegistry);

            Assert.IsTrue(handlers.ContainsKey("player_moved"),
                "Event handler must still be registered after reconnect");
            Assert.AreEqual(1, handlers["player_moved"].Count,
                "Exactly one handler should be registered (no duplicates)");
        }

        [Test]
        public void NonDefaultNamespaceHandlers_SurviveReconnect()
        {
            var socket = BuildSocket();
            socket.Connect("ws://localhost:3000");
            CompleteConnection(socket);

            // Get /lobby namespace and register handlers
            var lobby = socket.Of("/lobby");
            int handlerCount = 0;
            lobby.On("room_state", (string _) => handlerCount++);
            lobby.On("player_joined", (string _) => handlerCount++);

            // Simulate unexpected disconnect → reconnect
            _currentStub.SimulateUnexpectedClose();
            socket.AttemptReconnect();
            CompleteConnection(socket);
            _currentStub.SimulateNamespaceConnect("/lobby");

            // Namespace instance must be identical
            Assert.AreSame(lobby, socket.Of("/lobby"),
                "/lobby NamespaceSocket must survive reconnect");

            // Verify both handlers are still registered
            var eventsField = typeof(NamespaceSocket).GetField("_events",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var eventRegistry = eventsField.GetValue(lobby);
            var handlersField = typeof(EventRegistry).GetField("_handlers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var handlers = (Dictionary<string, List<Action<string>>>)handlersField.GetValue(eventRegistry);

            Assert.IsTrue(handlers.ContainsKey("room_state"), "room_state handler must survive");
            Assert.IsTrue(handlers.ContainsKey("player_joined"), "player_joined handler must survive");
        }

        [Test]
        public void NamespaceSocket_SendsConnect_AfterReconnect()
        {
            var socket = BuildSocket();
            socket.Connect("ws://localhost:3000");
            CompleteConnection(socket);

            var lobby = socket.Of("/lobby");
            _currentStub.SimulateNamespaceConnect("/lobby"); // connect /lobby before drop

            // Drop + reconnect
            _currentStub.SimulateUnexpectedClose();
            socket.AttemptReconnect();
            CompleteConnection(socket); // "/" connects → triggers SendConnect for all other namespaces

            // After "/" reconnects, /lobby should get SendConnect automatically.
            // Simulate the server's response:
            _currentStub.SimulateNamespaceConnect("/lobby");

            // The lobby socket must be connected again after reconnect
            var isConnectedField = typeof(NamespaceSocket).GetField("_connected",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool lobbyConnected = (bool)isConnectedField.GetValue(lobby);

            Assert.IsTrue(lobbyConnected,
                "/lobby should be re-connected after a full reconnect cycle");
        }

        // --------------------------------------------------
        // Connection-establishment timeout (v1.6.0)
        // --------------------------------------------------

        // Reflection helper: reach the internal EngineIOClient and force its connect
        // deadline into the past, then drive one engine tick. StubTransport never sends
        // OPEN, and Time.time does not advance within a synchronous test, so this is the
        // only way to exercise the timeout deterministically.
        private static void ForceEngineConnectDeadlineElapsedAndTick(SocketIOClient socket)
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var engine = typeof(SocketIOClient).GetField("_engine", flags).GetValue(socket);
            engine.GetType().GetField("_connectDeadline", flags).SetValue(engine, -1f);
            engine.GetType().GetMethod("Tick").Invoke(engine, null);
        }

        [Test]
        public void ConnectTimeout_FiresOnError_WhenOpenNeverArrives()
        {
            var socket = BuildSocket();
            SocketError? captured = null;
            socket.OnError += e => captured = e;

            // StubTransport.Connect() is a no-op, so the server never sends OPEN.
            socket.Connect("ws://localhost:3000");

            ForceEngineConnectDeadlineElapsedAndTick(socket);

            Assert.IsTrue(captured.HasValue, "OnError should fire when the connect timeout elapses");
            Assert.AreEqual(ErrorType.Timeout, captured.Value.Type,
                "Connect timeout must surface as ErrorType.Timeout");
        }

        [Test]
        public void ConnectTimeout_DoesNotFire_AfterHandshakeCompletes()
        {
            var socket = BuildSocket();
            bool errored = false;
            socket.OnError += _ => errored = true;

            socket.Connect("ws://localhost:3000");
            CompleteConnection(socket); // engine OPEN disarms the connect timer

            ForceEngineConnectDeadlineElapsedAndTick(socket);

            Assert.IsFalse(errored, "Connect timeout must not fire once the handshake has completed");
            Assert.AreEqual(ConnectionState.Connected, socket.State);
        }
    }
}
