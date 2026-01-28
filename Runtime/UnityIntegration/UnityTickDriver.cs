using System.Collections.Generic;
using UnityEngine;
using SocketIOUnity.Runtime;
#if SOCKETIO_PROFILER_COUNTERS
using SocketIOUnity.Debugging;
#endif

namespace SocketIOUnity.UnityIntegration
{
    internal sealed class UnityTickDriver : MonoBehaviour
    {
        private static UnityTickDriver _instance;
        private readonly List<ITickable> _tickables = new();

        public static void Register(ITickable tickable)
        {
            EnsureInstance();
            if (!_instance._tickables.Contains(tickable))
                _instance._tickables.Add(tickable);
        }

        public static void Unregister(ITickable tickable)
        {
            if (_instance == null) return;
            _instance._tickables.Remove(tickable);
        }

        /// <summary>
        /// Called on domain reload to reset static state.
        /// Prevents stale references when Play → Stop → Play.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;

            var go = new GameObject("[SocketIOUnity Tick Driver]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UnityTickDriver>();
        }

        private void Update()
        {
#if SOCKETIO_PROFILER_COUNTERS
            SocketIOThroughputTracker.Tick();
#endif
            for (int i = _tickables.Count - 1; i >= 0; i--)
                _tickables[i].Tick();
        }

#if UNITY_EDITOR
        private void OnApplicationQuit()
        {
            _tickables.Clear();
        }
#endif
    }
}

