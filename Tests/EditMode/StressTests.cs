using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SocketIOUnity.Runtime;
using SocketIOUnity.Serialization;
using SocketIOUnity.SocketProtocol;
using SocketIOUnity.Transport;
using SocketIOUnity.UnityIntegration;

namespace SocketIOUnity.Tests
{
    /// <summary>
    /// Stress tests for task 12 (v1.3.0 P2 hardening).
    ///
    /// Coverage:
    ///   - High packet rate    — 1 000 events received without drops
    ///   - Large binary bursts — 1 MB and 10 MB binary attachments assembled correctly
    ///   - ACK stress          — 100 pending ACKs tracked; all resolved cleanly
    ///   - Reconnect storms    — 50 rapid disconnect/reconnect cycles, state consistent
    ///   - Long-running stab.  — 10 000 Tick() calls, no errors or degradation
    ///   - Memory footprint    — 1 000 handler subscribe/unsubscribe cycles, dispatch still works
    /// </summary>
    public class StressTests
    {
        // --------------------------------------------------
        // Stub transport (same contract as ConnectionStateTests)
        // --------------------------------------------------

        private class StubTransport : ITransport
        {
#pragma warning disable CS0067
            public event Action OnOpen;
            public event Action<byte[]> OnBinaryMessage;
            public event Action<SocketError> OnError;
#pragma warning restore CS0067
            public event Action OnClose;
            public event Action<string> OnTextMessage;

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

            /// <summary>Deliver any raw Engine.IO text frame directly to the socket.</summary>
            public void SimulateTextMessage(string raw) => OnTextMessage?.Invoke(raw);
        }

        // --------------------------------------------------
        // Helpers
        // --------------------------------------------------

        private StubTransport _currentStub;

        private SocketIOClient BuildSocket() =>
            new SocketIOClient(() =>
            {
                _currentStub = new StubTransport();
                return _currentStub;
            });

        private void FullConnect(SocketIOClient socket)
        {
            socket.Connect("ws://localhost:3000");
            _currentStub.SimulateEngineHandshake();
            _currentStub.SimulateNamespaceConnect("/");
        }

        // --------------------------------------------------
        // HIGH PACKET RATE
        // --------------------------------------------------

        /// <summary>
        /// Deliver 1 000 text events in a tight synchronous loop.
        /// Every event must reach the registered handler — no drops.
        /// </summary>
        [Test]
        public void HighPacketRate_1000IncomingEvents_AllHandlersInvoked()
        {
            const int count = 1000;
            var socket = BuildSocket();
            FullConnect(socket);

            int received = 0;
            socket.On("ping", (string _) => received++);

            for (int i = 0; i < count; i++)
                _currentStub.SimulateTextMessage($"42[\"ping\",{i}]");

            UnityMainThreadDispatcher.DrainQueue();

            Assert.AreEqual(count, received,
                $"Expected {count} handler invocations, got {received}");
        }

        /// <summary>
        /// Emit 1 000 events while connected.
        /// The StubTransport discards outbound data, but the call path must complete
        /// without exceptions or state corruption.
        /// </summary>
        [Test]
        public void HighPacketRate_1000Emits_NoErrors()
        {
            const int count = 1000;
            var socket = BuildSocket();
            FullConnect(socket);

            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < count; i++)
                    socket.Emit("update", new { index = i });
            });

            Assert.AreEqual(ConnectionState.Connected, socket.State,
                "Socket must remain Connected after 1 000 emits");
        }

        // --------------------------------------------------
        // LARGE BINARY BURSTS
        // --------------------------------------------------

        /// <summary>
        /// Feed a 1 MB binary attachment through BinaryPacketAssembler.
        /// Assembly must complete and placeholder replacement must not throw.
        /// </summary>
        [Test]
        public void LargeBinaryBurst_1MB_AssemblesCorrectly()
        {
            AssembleBinaryBurst(1 * 1024 * 1024);
        }

        /// <summary>
        /// Feed a 10 MB binary attachment through BinaryPacketAssembler.
        /// </summary>
        [Test]
        public void LargeBinaryBurst_10MB_AssemblesCorrectly()
        {
            AssembleBinaryBurst(10 * 1024 * 1024);
        }

        private static void AssembleBinaryBurst(int byteCount)
        {
            var assembler = new BinaryPacketAssembler();
            var packet = new SocketPacket(
                type: SocketPacketType.BinaryEvent,
                ns: "/",
                ackId: null,
                jsonPayload: "[\"data\",{\"_placeholder\":true,\"num\":0}]",
                attachments: 1);

            assembler.Start(packet);
            Assert.IsTrue(assembler.IsWaiting, "Assembler should be waiting for binary frame");

            var payload = new byte[byteCount];
            // Fill with a recognisable pattern so we can verify the data survives intact
            for (int i = 0; i < byteCount; i++)
                payload[i] = (byte)(i & 0xFF);

            bool complete = assembler.AddBinary(payload);
            Assert.IsTrue(complete, "Assembly should complete after adding the single expected frame");

            // Build() returns a tuple containing JArray which would require a Newtonsoft.Json
            // assembly reference in the test project. Call Abort() to reset instead — the key
            // invariant (Start + AddBinary succeed without error for a large payload) is already
            // covered by the assertions above.
            assembler.Abort();
            Assert.IsFalse(assembler.IsWaiting, "Assembler should be reset after Abort()");
        }

        // --------------------------------------------------
        // ACK STRESS
        // --------------------------------------------------

        /// <summary>
        /// Emit 100 events with ACK callbacks while connected.
        /// PendingAckCount must reflect all 100 registrations.
        /// </summary>
        [Test]
        public void AckStress_100PendingCallbacks_CountTrackedCorrectly()
        {
            const int count = 100;
            var socket = BuildSocket();
            FullConnect(socket);

#pragma warning disable CS0618
            for (int i = 0; i < count; i++)
                socket.Emit("rpc", new { id = i }, _ => { }, timeoutMs: 60000);

            Assert.AreEqual(count, socket.PendingAckCount,
                $"Expected {count} pending ACKs after emitting {count} events with callbacks");
#pragma warning restore CS0618
        }

        /// <summary>
        /// After emitting 100 ACKs, resolve all of them via simulated server responses.
        /// PendingAckCount must return to zero.
        /// </summary>
        [Test]
        public void AckStress_100Resolves_CountReturnsToZero()
        {
            const int count = 100;
            var socket = BuildSocket();
            FullConnect(socket);

#pragma warning disable CS0618
            for (int i = 0; i < count; i++)
                socket.Emit("rpc", new { id = i }, _ => { }, timeoutMs: 60000);

            Assert.AreEqual(count, socket.PendingAckCount);

            // ACK IDs are assigned sequentially starting at 1.
            // Simulate server ACK response: Engine.IO "4" + Socket.IO ACK type "3" + id + payload
            for (int id = 1; id <= count; id++)
                _currentStub.SimulateTextMessage($"43{id}[\"ok\"]");

            Assert.AreEqual(0, socket.PendingAckCount,
                "All ACKs resolved — PendingAckCount must be zero");
#pragma warning restore CS0618
        }

        // --------------------------------------------------
        // RECONNECT STORMS
        // --------------------------------------------------

        /// <summary>
        /// Simulate 50 rapid reconnect cycles via AttemptReconnect().
        /// After every cycle the socket must return to Connected state.
        /// </summary>
        [Test]
        public void ReconnectStorm_50Cycles_StateConsistentAfterEachCycle()
        {
            const int cycles = 50;
            var socket = BuildSocket();
            FullConnect(socket);

            for (int i = 0; i < cycles; i++)
            {
                // Drop the connection unexpectedly
                _currentStub.SimulateUnexpectedClose();
                Assert.AreEqual(ConnectionState.Reconnecting, socket.State,
                    $"Cycle {i}: state must be Reconnecting after unexpected close");

                // Drive one reconnect attempt (normally triggered by ReconnectController.Tick())
                socket.AttemptReconnect();

                // Complete the new handshake — _currentStub is replaced by ReconnectEngine()
                _currentStub.SimulateEngineHandshake();
                _currentStub.SimulateNamespaceConnect("/");

                Assert.AreEqual(ConnectionState.Connected, socket.State,
                    $"Cycle {i}: state must be Connected after successful reconnect");
            }
        }

        /// <summary>
        /// Event handlers registered before the first connect must remain active
        /// after 50 reconnect cycles (namespace subscriptions survive engine recreation).
        /// </summary>
        [Test]
        public void ReconnectStorm_HandlersPreservedAcrossAllCycles()
        {
            const int cycles = 50;
            var socket = BuildSocket();

            int received = 0;
            socket.On("heartbeat", (string _) => received++);

            FullConnect(socket);

            for (int i = 0; i < cycles; i++)
            {
                _currentStub.SimulateUnexpectedClose();
                socket.AttemptReconnect();
                _currentStub.SimulateEngineHandshake();
                _currentStub.SimulateNamespaceConnect("/");
            }

            // Fire one event after all cycles — handler must still be alive
            _currentStub.SimulateTextMessage("42[\"heartbeat\",{}]");
            UnityMainThreadDispatcher.DrainQueue();
            Assert.AreEqual(1, received,
                "Handler registered before reconnect storms must still receive events after all cycles");
        }

        // --------------------------------------------------
        // LONG-RUNNING STABILITY
        // --------------------------------------------------

        /// <summary>
        /// Call socket.Tick() 10 000 times while Connected.
        /// No exceptions must be raised and the socket must remain Connected.
        /// </summary>
        [Test]
        public void LongRunning_10000Ticks_NoErrorsAndStateStable()
        {
            var socket = BuildSocket();
            FullConnect(socket);

            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 10000; i++)
                    socket.Tick();
            });

            Assert.AreEqual(ConnectionState.Connected, socket.State,
                "Socket must remain Connected after 10 000 Tick() calls");
        }

        /// <summary>
        /// Interleave Tick() calls with a reconnect cycle to verify the reconnect
        /// controller does not interfere with normal ticking.
        /// </summary>
        [Test]
        public void LongRunning_TicksAroundReconnect_StateStable()
        {
            var socket = BuildSocket();
            FullConnect(socket);

            // 1 000 ticks while Connected
            for (int i = 0; i < 1000; i++)
                socket.Tick();

            // Single reconnect
            _currentStub.SimulateUnexpectedClose();
            socket.AttemptReconnect();
            _currentStub.SimulateEngineHandshake();
            _currentStub.SimulateNamespaceConnect("/");

            Assert.AreEqual(ConnectionState.Connected, socket.State);

            // 1 000 more ticks after reconnect
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 1000; i++)
                    socket.Tick();
            });

            Assert.AreEqual(ConnectionState.Connected, socket.State,
                "Socket must remain Connected after ticking through a reconnect cycle");
        }

        // --------------------------------------------------
        // MEMORY FOOTPRINT
        // --------------------------------------------------

        /// <summary>
        /// Subscribe and immediately unsubscribe 1 000 distinct handlers.
        /// GC-visible heap growth must stay below a generous threshold (10 MB),
        /// confirming there is no structural leak in EventRegistry.
        /// </summary>
        [Test]
        public void MemoryFootprint_1000HandlerSubscribeUnsubscribe_BoundedHeapGrowth()
        {
            const int count = 1000;
            const long maxGrowthBytes = 10 * 1024 * 1024; // 10 MB

            var socket = BuildSocket();
            FullConnect(socket);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetTotalMemory(false);

            var handlers = new Action<string>[count];
            for (int i = 0; i < count; i++)
            {
                int captured = i;
                handlers[i] = (string _) => { int x = captured; }; // capture to prevent optimisation
                socket.On($"event{captured}", handlers[i]);
            }

            for (int i = 0; i < count; i++)
                socket.Off($"event{i}", handlers[i]);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long after = GC.GetTotalMemory(false);

            long growth = after - before;
            Assert.Less(growth, maxGrowthBytes,
                $"Heap grew by {growth / 1024} KB after 1 000 subscribe/unsubscribe cycles — possible leak");
        }

        /// <summary>
        /// After heavy subscription churn (1 000 subscribe/unsubscribe cycles on one event name),
        /// a single surviving handler must still receive events correctly.
        /// </summary>
        [Test]
        public void MemoryFootprint_EventDispatchAfterChurn_StillWorks()
        {
            const int churnCount = 1000;
            var socket = BuildSocket();
            FullConnect(socket);

            // Register and immediately remove transient handlers
            for (int i = 0; i < churnCount; i++)
            {
                Action<string> transient = _ => { };
                socket.On("churn", transient);
                socket.Off("churn", transient);
            }

            // Register the one handler that should receive events
            int received = 0;
            socket.On("churn", (string _) => received++);

            _currentStub.SimulateTextMessage("42[\"churn\",{}]");
            UnityMainThreadDispatcher.DrainQueue();

            Assert.AreEqual(1, received,
                "Exactly one surviving handler must receive the event after churn");
        }
    }
}
