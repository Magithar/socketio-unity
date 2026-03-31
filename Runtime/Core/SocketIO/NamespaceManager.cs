using System;
using System.Collections.Generic;
using SocketIOUnity.Debugging;

namespace SocketIOUnity.Runtime
{
    internal sealed class NamespaceManager
    {
        private readonly Dictionary<string, NamespaceSocket> _namespaces = new();
        private readonly SocketIOClient _root;
        private NamespaceSocket[] _allCached = Array.Empty<NamespaceSocket>();

        public NamespaceManager(SocketIOClient root)
        {
            _root = root;
        }

        public NamespaceSocket Get(string ns, object auth = null)
        {
            if (_namespaces.TryGetValue(ns, out var socket))
            {
                // 🔥 CRITICAL: Socket.IO forbids changing auth after connection
                if (auth != null)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[SocketIO] Auth ignored for existing namespace '{ns}'. " +
                        "Auth cannot be changed after namespace creation.");
                }
                return socket;
            }

            socket = new NamespaceSocket(ns, _root, auth);
            _namespaces[ns] = socket;
            RebuildCache();

#if SOCKETIO_PROFILER_COUNTERS && UNITY_2020_2_OR_NEWER
            SocketIOProfilerCounters.SetActiveNamespaces(_namespaces.Count);
#endif

            // 🔥 CRITICAL FIX:
            // If root namespace already connected,
            // immediately connect this namespace
            if (ns != "/" && _root.IsConnected)
            {
                socket.SendConnect();
            }

            return socket;
        }

        public void ResetAll()
        {
            foreach (var ns in _namespaces.Values)
                ns.Reset();
        }

        public bool TryGet(string ns, out NamespaceSocket socket)
        {
            return _namespaces.TryGetValue(ns, out socket);
        }

        public int Count => _namespaces.Count;

        /// <summary>
        /// Cached snapshot of all namespace sockets. Rebuilt only when a namespace is added.
        /// Safe to iterate even if a namespace is added during iteration (snapshot semantics).
        /// Zero allocation per frame.
        /// </summary>
        public NamespaceSocket[] All => _allCached;

        private void RebuildCache()
        {
            var arr = new NamespaceSocket[_namespaces.Count];
            _namespaces.Values.CopyTo(arr, 0);
            _allCached = arr;
        }
    }
}
