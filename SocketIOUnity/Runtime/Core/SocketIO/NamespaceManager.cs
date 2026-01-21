using System.Collections.Generic;

namespace SocketIOUnity.Runtime
{
    internal sealed class NamespaceManager
    {
        private readonly Dictionary<string, NamespaceSocket> _namespaces = new();
        private readonly SocketIOClient _root;

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

        public IEnumerable<NamespaceSocket> All => _namespaces.Values;
    }
}
