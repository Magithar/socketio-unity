using System.Reflection;
using NUnit.Framework;
using SocketIOUnity.UnityIntegration;

namespace SocketIOUnity.Tests
{
    /// <summary>
    /// Coverage for the bounded main-thread dispatch queue (v1.7.0):
    ///   - Below MaxQueueLength every enqueued action executes, nothing is dropped
    ///   - Past the cap, exactly MaxQueueLength actions execute and the overflow
    ///     is counted in DroppedActionCount (drop-newest, no throw)
    ///   - MaxQueueLength &lt;= 0 restores the pre-v1.7 unbounded behavior
    ///   - ResetStatics clears the queue, the drop counter, and restores the
    ///     default cap (Play → Stop → Play must not leak state)
    /// </summary>
    public class DispatcherTests
    {
        private static readonly MethodInfo ResetStatics =
            typeof(UnityMainThreadDispatcher).GetMethod(
                "ResetStatics", BindingFlags.NonPublic | BindingFlags.Static);

        [SetUp]
        public void SetUp() => ResetStatics.Invoke(null, null);

        [TearDown]
        public void TearDown() => ResetStatics.Invoke(null, null);

        [Test]
        public void Enqueue_BelowCap_AllActionsExecute_NothingDropped()
        {
            int executed = 0;
            for (int i = 0; i < 500; i++)
                UnityMainThreadDispatcher.Enqueue(() => executed++);

            UnityMainThreadDispatcher.DrainQueue();

            Assert.AreEqual(500, executed);
            Assert.AreEqual(0, UnityMainThreadDispatcher.DroppedActionCount);
        }

        [Test]
        public void Enqueue_PastCap_DropsNewestAndCounts()
        {
            UnityMainThreadDispatcher.MaxQueueLength = 100;

            int executed = 0;
            for (int i = 0; i < 150; i++)
                UnityMainThreadDispatcher.Enqueue(() => executed++);

            UnityMainThreadDispatcher.DrainQueue();

            Assert.AreEqual(100, executed, "exactly MaxQueueLength actions should run");
            Assert.AreEqual(50, UnityMainThreadDispatcher.DroppedActionCount);
        }

        [Test]
        public void Enqueue_AfterDrain_AcceptsAgain()
        {
            UnityMainThreadDispatcher.MaxQueueLength = 100;

            for (int i = 0; i < 150; i++)
                UnityMainThreadDispatcher.Enqueue(() => { });
            UnityMainThreadDispatcher.DrainQueue();

            // Queue emptied — capacity must be available again.
            int executed = 0;
            for (int i = 0; i < 100; i++)
                UnityMainThreadDispatcher.Enqueue(() => executed++);
            UnityMainThreadDispatcher.DrainQueue();

            Assert.AreEqual(100, executed);
        }

        [Test]
        public void Enqueue_NonPositiveCap_IsUnbounded()
        {
            UnityMainThreadDispatcher.MaxQueueLength = 0;

            int executed = 0;
            for (int i = 0; i < 20_000; i++) // 2x the default cap
                UnityMainThreadDispatcher.Enqueue(() => executed++);

            UnityMainThreadDispatcher.DrainQueue();

            Assert.AreEqual(20_000, executed);
            Assert.AreEqual(0, UnityMainThreadDispatcher.DroppedActionCount);
        }

        [Test]
        public void ResetStatics_ClearsDropCountAndRestoresDefaultCap()
        {
            UnityMainThreadDispatcher.MaxQueueLength = 10;
            for (int i = 0; i < 20; i++)
                UnityMainThreadDispatcher.Enqueue(() => { });
            Assert.Greater(UnityMainThreadDispatcher.DroppedActionCount, 0);

            ResetStatics.Invoke(null, null);

            Assert.AreEqual(0, UnityMainThreadDispatcher.DroppedActionCount);
            Assert.AreEqual(10_000, UnityMainThreadDispatcher.MaxQueueLength);

            // Queue was cleared too: a fresh action still runs.
            int executed = 0;
            UnityMainThreadDispatcher.Enqueue(() => executed++);
            UnityMainThreadDispatcher.DrainQueue();
            Assert.AreEqual(1, executed);
        }
    }
}
