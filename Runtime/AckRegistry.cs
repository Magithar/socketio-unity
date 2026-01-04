using System;
using System.Collections.Generic;

namespace SocketIOUnity.Runtime
{
    internal class AckRegistry
    {
        private readonly Dictionary<int, AckEntry> _pending = new();
        private int _nextId = 0;

        public int Register(Action<string> callback, TimeSpan timeout)
        {
            var id = ++_nextId;
            _pending[id] = new AckEntry(id, callback, timeout);
            return id;
        }

        public bool Resolve(int id, string payload)
        {
            if (!_pending.TryGetValue(id, out var entry))
                return false;

            _pending.Remove(id);
            entry.Callback?.Invoke(payload);
            return true;
        }

        public void RemoveExpired()
        {
            var expired = new List<int>();

            foreach (var kv in _pending)
            {
                if (kv.Value.IsExpired)
                    expired.Add(kv.Key);
            }

            foreach (var id in expired)
            {
                _pending.Remove(id);
            }
        }
    }
}
