using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace SocketIOUnity.UnityIntegration
{
    /// <summary>
    /// Ensures all Socket.IO callbacks execute on Unity's main thread.
    /// Auto-initializes at runtime. Thread-safe and lock-free.
    /// </summary>
    public sealed class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private static readonly ConcurrentQueue<Action> _queue = new();

        public static bool IsInitialized => _instance != null;

        /// <summary>
        /// Enqueue an action to run on Unity main thread.
        /// Thread-safe. Can be called from any thread.
        /// </summary>
        public static void Enqueue(Action action)
        {
            if (action == null)
                return;

            _queue.Enqueue(action);
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
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
    }
}
