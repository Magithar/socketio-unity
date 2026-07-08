using System;
using System.Diagnostics;
using NUnit.Framework;
using SocketIOUnity.Runtime;
using SocketIOUnity.Serialization;
using SocketIOUnity.SocketProtocol;
using SocketIOUnity.Transport;
using SocketIOUnity.UnityIntegration;

namespace SocketIOUnity.Tests
{
    /// <summary>
    /// Timed counterpart to StressTests: same load patterns, but reports
    /// throughput / allocation numbers instead of only asserting correctness.
    ///
    /// Every benchmark takes multiple samples (Repetitions) and reports
    /// min/median/max instead of a single Stopwatch reading — a single sample
    /// bakes in JIT/GC noise and isn't reproducible enough to publish.
    ///
    /// Numbers are printed via Debug.Log in a single-line, greppable
    /// "BENCHMARK <name> key=value ..." format so they show up in the same log
    /// run-tests.sh already captures. (TestContext.Progress/Out are NOT captured
    /// by Unity's batchmode test runner — confirmed by running it — so Debug.Log
    /// is used instead, same as everything else in this codebase's log output.)
    ///
    /// These are report-only: assertions here are generous upper-bound ceilings
    /// meant to catch a catastrophic regression (e.g. an accidental O(n^2) loop),
    /// not to gate on a specific throughput target. CI hardware is too noisy for
    /// that — publishable numbers should come from a local run on hardware
    /// representative of an actual player's machine, not this dev box.
    /// </summary>
    public class BenchmarkTests
    {
        private const int Repetitions = 5;

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

            public void SimulateTextMessage(string raw) => OnTextMessage?.Invoke(raw);
        }

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

        private static void Report(string name, params (string key, object value)[] fields)
        {
            var sb = new System.Text.StringBuilder("BENCHMARK ").Append(name);
            foreach (var (key, value) in fields)
                sb.Append(' ').Append(key).Append('=').Append(value);

            // Unity's batchmode test runner doesn't route TestContext.Progress /
            // TestContext.Out into -logFile or the results XML's <output>
            // element — Debug.Log is what actually shows up in the captured log.
            UnityEngine.Debug.Log(sb.ToString());
        }

        /// <summary>Sorts a copy of <paramref name="samples"/> and returns (min, median, max).</summary>
        private static (double min, double median, double max) Stats(double[] samples)
        {
            var sorted = (double[])samples.Clone();
            Array.Sort(sorted);
            int n = sorted.Length;
            double median = n % 2 == 0
                ? (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0
                : sorted[n / 2];
            return (sorted[0], median, sorted[n - 1]);
        }

        // --------------------------------------------------
        // SUSTAINED INCOMING THROUGHPUT
        // --------------------------------------------------

        [Test]
        public void Benchmark_SustainedIncomingThroughput()
        {
            const int count = 1000;
            var socket = BuildSocket();
            FullConnect(socket);

            int received = 0;
            socket.On("ping", (string _) => received++);

            // Pre-build messages outside the timed region — interpolating them
            // inline would bake test-harness string-construction cost into the
            // client-throughput number this benchmark is meant to isolate.
            var messages = new string[count];
            for (int i = 0; i < count; i++)
                messages[i] = $"42[\"ping\",{i}]";

            var samplesMs = new double[Repetitions];
            for (int rep = 0; rep < Repetitions; rep++)
            {
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < count; i++)
                    _currentStub.SimulateTextMessage(messages[i]);
                UnityMainThreadDispatcher.DrainQueue();
                sw.Stop();
                samplesMs[rep] = sw.Elapsed.TotalMilliseconds;
            }

            Assert.AreEqual(count * Repetitions, received);

            var (minMs, medianMs, maxMs) = Stats(samplesMs);
            double eventsPerSec = count / (medianMs / 1000.0);
            Report("sustained_incoming_throughput",
                ("events_per_sec_median", (long)eventsPerSec),
                ("median_ms", medianMs.ToString("F3")),
                ("min_ms", minMs.ToString("F3")),
                ("max_ms", maxMs.ToString("F3")));

            Assert.Less(maxMs, 5000.0,
                $"Slowest of {Repetitions} reps ({count} events each) took {maxMs:F2}ms — regression ceiling exceeded");
        }

        // --------------------------------------------------
        // SUSTAINED EMIT THROUGHPUT
        // --------------------------------------------------

        [Test]
        public void Benchmark_SustainedEmitThroughput()
        {
            const int count = 1000;
            var socket = BuildSocket();
            FullConnect(socket);

            var samplesMs = new double[Repetitions];
            for (int rep = 0; rep < Repetitions; rep++)
            {
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < count; i++)
                    socket.Emit("update", new { index = i });
                sw.Stop();
                samplesMs[rep] = sw.Elapsed.TotalMilliseconds;
            }

            var (minMs, medianMs, maxMs) = Stats(samplesMs);
            double eventsPerSec = count / (medianMs / 1000.0);
            Report("sustained_emit_throughput",
                ("events_per_sec_median", (long)eventsPerSec),
                ("median_ms", medianMs.ToString("F3")),
                ("min_ms", minMs.ToString("F3")),
                ("max_ms", maxMs.ToString("F3")));

            Assert.Less(maxMs, 5000.0,
                $"Slowest of {Repetitions} reps ({count} emits each) took {maxMs:F2}ms — regression ceiling exceeded");
        }

        // --------------------------------------------------
        // BINARY ASSEMBLY THROUGHPUT
        // --------------------------------------------------

        [TestCase(1 * 1024 * 1024, "1mb")]
        [TestCase(10 * 1024 * 1024, "10mb")]
        public void Benchmark_BinaryAssemblyThroughput(int byteCount, string label)
        {
            var payload = new byte[byteCount];
            for (int i = 0; i < byteCount; i++)
                payload[i] = (byte)(i & 0xFF);

            var samplesMs = new double[Repetitions];
            for (int rep = 0; rep < Repetitions; rep++)
            {
                var assembler = new BinaryPacketAssembler();
                var packet = new SocketPacket(
                    type: SocketPacketType.BinaryEvent,
                    ns: "/",
                    ackId: null,
                    jsonPayload: "[\"data\",{\"_placeholder\":true,\"num\":0}]",
                    attachments: 1);

                var sw = Stopwatch.StartNew();
                assembler.Start(packet);
                bool complete = assembler.AddBinary(payload);
                sw.Stop();

                Assert.IsTrue(complete);
                assembler.Abort();
                samplesMs[rep] = sw.Elapsed.TotalMilliseconds;
            }

            var (minMs, medianMs, maxMs) = Stats(samplesMs);
            double mbPerSec = (byteCount / (1024.0 * 1024.0)) / (medianMs / 1000.0);
            Report("binary_assembly_throughput_" + label,
                ("mb_per_sec_median", mbPerSec.ToString("F1")),
                ("median_ms", medianMs.ToString("F4")),
                ("min_ms", minMs.ToString("F4")),
                ("max_ms", maxMs.ToString("F4")));

            Assert.Less(maxMs, 5000.0,
                $"{label} slowest of {Repetitions} reps took {maxMs:F2}ms — regression ceiling exceeded");
        }

        // --------------------------------------------------
        // ALLOCATIONS PER MESSAGE
        // --------------------------------------------------

        [Test]
        public void Benchmark_AllocationsPerMessage()
        {
            const int count = 1000;
            const int warmupCount = 10;
            var socket = BuildSocket();
            FullConnect(socket);

            int received = 0;
            socket.On("ping", (string _) => received++);

            // Pre-build messages outside the measured region — see the same
            // rationale in Benchmark_SustainedIncomingThroughput.
            var warmupMessages = new string[warmupCount];
            for (int i = 0; i < warmupCount; i++)
                warmupMessages[i] = $"42[\"ping\",{i}]";
            var messages = new string[count];
            for (int i = 0; i < count; i++)
                messages[i] = $"42[\"ping\",{i}]";

            // Warm up JIT / first-call allocations before measuring.
            for (int i = 0; i < warmupCount; i++)
                _currentStub.SimulateTextMessage(warmupMessages[i]);
            UnityMainThreadDispatcher.DrainQueue();

            // GC.GetAllocatedBytesForCurrentThread() reads back 0 under Unity's
            // Editor Mono runtime (unreliable there) — use the same
            // force-collect-then-GetTotalMemory delta StressTests' memory
            // footprint test already relies on instead. Deliberately no second
            // forced collect between "before" and "after" within a rep: that
            // would collect away the very transient garbage this benchmark
            // measures. Can still read low under an automatic incremental GC
            // pass mid-loop; the median across reps smooths that out somewhat.
            var samplesBytes = new double[Repetitions];
            for (int rep = 0; rep < Repetitions; rep++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                long before = GC.GetTotalMemory(false);

                for (int i = 0; i < count; i++)
                    _currentStub.SimulateTextMessage(messages[i]);
                UnityMainThreadDispatcher.DrainQueue();

                long after = GC.GetTotalMemory(false);
                samplesBytes[rep] = (after - before) / (double)count;
            }

            Assert.AreEqual(warmupCount + count * Repetitions, received);

            var (minBytes, medianBytes, maxBytes) = Stats(samplesBytes);
            Report("allocations_per_message",
                ("bytes_per_message_median", medianBytes.ToString("F0")),
                ("min_bytes", minBytes.ToString("F0")),
                ("max_bytes", maxBytes.ToString("F0")));
        }

        // --------------------------------------------------
        // RECONNECT CYCLE COST
        // --------------------------------------------------

        [Test]
        public void Benchmark_ReconnectCycleCost()
        {
            const int cycles = 50;
            var socket = BuildSocket();
            FullConnect(socket);

            var samplesMs = new double[Repetitions];
            for (int rep = 0; rep < Repetitions; rep++)
            {
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < cycles; i++)
                {
                    _currentStub.SimulateUnexpectedClose();
                    socket.AttemptReconnect();
                    _currentStub.SimulateEngineHandshake();
                    _currentStub.SimulateNamespaceConnect("/");
                }
                sw.Stop();

                // Drain after each rep so actions the reconnect cycles enqueued
                // don't linger in UnityMainThreadDispatcher's static queue and
                // get folded into a later rep's — or a later test's — own
                // timing/allocation window.
                UnityMainThreadDispatcher.DrainQueue();

                Assert.AreEqual(ConnectionState.Connected, socket.State);
                samplesMs[rep] = sw.Elapsed.TotalMilliseconds;
            }

            var (minMs, medianMs, maxMs) = Stats(samplesMs);
            double msPerCycleMedian = medianMs / cycles;
            Report("reconnect_cycle_cost",
                ("ms_per_cycle_median", msPerCycleMedian.ToString("F3")),
                ("min_total_ms", minMs.ToString("F3")),
                ("max_total_ms", maxMs.ToString("F3")));

            Assert.Less(maxMs, 5000.0,
                $"Slowest of {Repetitions} reps ({cycles} cycles each) took {maxMs:F2}ms — regression ceiling exceeded");
        }
    }
}
