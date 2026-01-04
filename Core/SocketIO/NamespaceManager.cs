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

        public NamespaceSocket Get(string ns)
        {
            if (_namespaces.TryGetValue(ns, out var socket))
                return socket;

            socket = new NamespaceSocket(ns, _root);
            _namespaces[ns] = socket;

            // 🔥 CRITICAL FIX:
            // If root namespace already connected,
            // immediately connect this namespace
            if (ns != "/" && _root.IsConnected)
            {
                _root.ConnectNamespace(ns);
            }

            return socket;
        }

        public bool TryGet(string ns, out NamespaceSocket socket)
        {
            return _namespaces.TryGetValue(ns, out socket);
        }

        public IEnumerable<NamespaceSocket> All => _namespaces.Values;
    }
}
