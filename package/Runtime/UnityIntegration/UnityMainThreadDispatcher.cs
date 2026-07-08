using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace SocketIOUnity.UnityIntegration
{
    /// <summary>
    /// Ensures all Socket.IO callbacks execute on Unity's main thread.
    /// Auto-initializes at runtime. Thread-safe and lock-free.
    /// </summary>
    public sealed class UnityMainThreadDispatcher : MonoBehaviour
    {
        private const int DefaultMaxQueueLength = 10_000;

        private static UnityMainThreadDispatcher _instance;
        private static readonly ConcurrentQueue<Action> _queue = new();
        private static int _queueLength;              // Interlocked; ConcurrentQueue.Count is not O(1) on Mono
        private static long _droppedActionCount;
        private static long _lastWarnedDropCount;

        public static bool IsInitialized => _instance != null;

        /// <summary>
        /// Maximum number of pending actions before <see cref="Enqueue"/> starts
        /// dropping new ones (drop-newest). Protects against a flooding server or a
        /// stalled main thread growing the queue without limit. Default 10000;
        /// set to 0 or negative for the pre-v1.7 unbounded behavior.
        /// Reset to the default on domain reload / play-mode re-entry.
        /// </summary>
        public static int MaxQueueLength { get; set; } = DefaultMaxQueueLength;

        /// <summary>
        /// Total actions dropped by the <see cref="MaxQueueLength"/> cap since
        /// startup (or the last domain reload). A non-zero value means the queue
        /// overflowed — the server is flooding or the main thread stalled too long.
        /// </summary>
        public static long DroppedActionCount => Interlocked.Read(ref _droppedActionCount);

        /// <summary>
        /// Enqueue an action to run on Unity main thread.
        /// Thread-safe. Can be called from any thread. When the queue is at
        /// <see cref="MaxQueueLength"/>, the action is dropped and counted in
        /// <see cref="DroppedActionCount"/> instead of growing the queue.
        /// </summary>
        public static void Enqueue(Action action)
        {
            if (action == null)
                return;

            int max = MaxQueueLength;
            if (max > 0 && Volatile.Read(ref _queueLength) >= max)
            {
                Interlocked.Increment(ref _droppedActionCount);
                return;
            }

            Interlocked.Increment(ref _queueLength);
            _queue.Enqueue(action);
        }

        /// <summary>
        /// Synchronously drain all pending actions. Used by EditMode tests
        /// where no Update loop is running.
        /// </summary>
        internal static void DrainQueue()
        {
            while (_queue.TryDequeue(out var action))
            {
                Interlocked.Decrement(ref _queueLength);
                action.Invoke();
            }
        }

        /// <summary>
        /// Called on domain reload to reset static state.
        /// Prevents stale references when Play → Stop → Play.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            while (_queue.TryDequeue(out _)) { } // Clear stale actions
            _queueLength = 0;
            _droppedActionCount = 0;
            _lastWarnedDropCount = 0;
            MaxQueueLength = DefaultMaxQueueLength;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_instance != null)
                return;

            var go = new GameObject("[SocketIO] MainThreadDispatcher");
            DontDestroyOnLoad(go);

            _instance = go.AddComponent<UnityMainThreadDispatcher>();
        }

        private void Update()
        {
            // Drain the queue every frame on the main thread
            while (_queue.TryDequeue(out var action))
            {
                Interlocked.Decrement(ref _queueLength);
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            // Warn about overflow drops at most once per drain cycle, never per drop.
            long dropped = Interlocked.Read(ref _droppedActionCount);
            if (dropped > _lastWarnedDropCount)
            {
                Debug.LogWarning(
                    $"[SocketIO] Main-thread dispatch queue overflowed MaxQueueLength={MaxQueueLength}; " +
                    $"{dropped} action(s) dropped in total. The server is sending faster than the game is processing.");
                _lastWarnedDropCount = dropped;
            }
        }
    }
}
