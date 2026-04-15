using System;
using System.Collections.Generic;
using SocketIOUnity.Core;
using SocketIOUnity.Debugging;
using SocketIOUnity.UnityIntegration;
using UnityEngine;

namespace SocketIOUnity.Runtime
{
    internal class AckRegistry
    {
        private readonly Dictionary<int, AckEntry> _pending = new();
        private readonly ObjectPool<AckEntry> _pool;
        private int _nextId = 0;

        public int Count => _pending.Count;

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
            // Handle integer overflow by wrapping around to 1 (skip 0 and negatives)
            _nextId++;
            if (_nextId <= 0)
                _nextId = 1;

            var id = _nextId;
            var entry = _pool.Rent();

            entry.Id = id;
            entry.Callback = callback;
            entry.ExpireAt = Time.time + (float)timeout.TotalSeconds;

            _pending[id] = entry;

#if SOCKETIO_PROFILER_COUNTERS && UNITY_2020_2_OR_NEWER
            SocketIOProfilerCounters.SetPendingAcks(_pending.Count);
#endif
            
            SocketIOTrace.Verbose(TraceCategory.Ack, $"ACK registered id={id} timeout={timeout.TotalSeconds}s");
            return id;
        }

        public bool Resolve(int id, string payload)
        {
            using (SocketIOProfiler.Ack_Resolve.Auto())
            {
                if (!_pending.TryGetValue(id, out var entry))
                {
                    SocketIOTrace.Verbose(TraceCategory.Ack, $"ACK id={id} not found (expired or already resolved)");
                    return false;
                }

                _pending.Remove(id);
                
                SocketIOTrace.Protocol(TraceCategory.Ack, $"ACK resolved id={id}");
                
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    entry.Callback?.Invoke(payload);
                });

#if SOCKETIO_PROFILER_COUNTERS && UNITY_2020_2_OR_NEWER
                SocketIOProfilerCounters.SetPendingAcks(_pending.Count);
#endif
                
                _pool.Return(entry);
                return true;
            }
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
                
                SocketIOTrace.Verbose(TraceCategory.Ack, $"ACK expired id={id}");
            }

#if SOCKETIO_PROFILER_COUNTERS && UNITY_2020_2_OR_NEWER
            if (expired.Count > 0)
                SocketIOProfilerCounters.SetPendingAcks(_pending.Count);
#endif

            ListPool<int>.Return(expired);
        }

        public void Clear()
        {
            foreach (var entry in _pending.Values)
                _pool.Return(entry);
                
            _pending.Clear();

#if SOCKETIO_PROFILER_COUNTERS && UNITY_2020_2_OR_NEWER
            SocketIOProfilerCounters.SetPendingAcks(0);
#endif
        }
    }
}

