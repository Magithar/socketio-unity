using System;
using System.Collections.Generic;
using SocketIOUnity.Core;
using SocketIOUnity.UnityIntegration;
using UnityEngine;

namespace SocketIOUnity.Runtime
{
    internal class AckRegistry
    {
        private readonly Dictionary<int, AckEntry> _pending = new();
        private readonly ObjectPool<AckEntry> _pool;
        private int _nextId = 0;

        public AckRegistry()
        {
            _pool = new ObjectPool<AckEntry>(
                factory: () => new AckEntry(),
                reset: e => e.Reset(),
                initialCapacity: 32
            );
        }

        public int Register(Action<string> callback, TimeSpan timeout)
        {
            var id = ++_nextId;
            var entry = _pool.Rent();
            
            entry.Id = id;
            entry.Callback = callback;
            entry.ExpireAt = Time.time + (float)timeout.TotalSeconds;
            
            _pending[id] = entry;
            return id;
        }

        public bool Resolve(int id, string payload)
        {
            if (!_pending.TryGetValue(id, out var entry))
                return false;

            _pending.Remove(id);
            
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                entry.Callback?.Invoke(payload);
            });
            
            _pool.Return(entry);
            return true;
        }

        public void RemoveExpired()
        {
            if (_pending.Count == 0)
                return;

            float now = Time.time;
            var expired = ListPool<int>.Rent();

            foreach (var kv in _pending)
            {
                if (kv.Value.ExpireAt <= now)
                    expired.Add(kv.Key);
            }

            foreach (var id in expired)
            {
                var entry = _pending[id];
                _pending.Remove(id);
                _pool.Return(entry);
            }

            ListPool<int>.Return(expired);
        }

        public void Clear()
        {
            foreach (var entry in _pending.Values)
                _pool.Return(entry);
                
            _pending.Clear();
        }
    }
}

