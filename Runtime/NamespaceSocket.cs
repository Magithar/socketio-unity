using System;
using Newtonsoft.Json.Linq;

namespace SocketIOUnity.Runtime
{
    public sealed class NamespaceSocket : ITickable
    {
        private readonly string _namespace;
        private readonly SocketIOClient _root;
        private readonly EventRegistry _events = new();
        private readonly AckRegistry _acks = new();

        internal string Namespace => _namespace;
        internal bool IsConnected { get; private set; }

        public event Action OnConnected;

        internal NamespaceSocket(string ns, SocketIOClient root)
        {
            _namespace = ns;
            _root = root;
        }

        // ---------------- Public API ----------------

        public void On(string eventName, Action<string> handler)
        {
            _events.On(eventName, handler);
        }

        public void Emit(string eventName, object payload)
        {
            if (!IsConnected)
                return;

            _root.EmitInternal(_namespace, eventName, payload, null);
        }

        public void Emit(
            string eventName,
            object payload,
            Action<string> ack,
            int timeoutMs = 5000)
        {
            if (!IsConnected)
                return;

            var ackId = _acks.Register(
                ack,
                TimeSpan.FromMilliseconds(timeoutMs));

            _root.EmitInternal(_namespace, eventName, payload, ackId);
        }

        // ---------------- Internal ----------------

        internal void HandleConnect()
        {
            if (IsConnected)
                return;

            IsConnected = true;
            OnConnected?.Invoke();
        }

        internal void HandleDisconnect()
        {
            IsConnected = false;
            _acks.RemoveExpired(); // 🔥 avoid leaking pending ACKs
        }

        internal void HandleEvent(string payload)
        {
            if (string.IsNullOrEmpty(payload))
                return;

            JArray arr;
            try
            {
                arr = JArray.Parse(payload);
            }
            catch
            {
                // Ignore malformed payloads instead of crashing
                return;
            }

            if (arr.Count == 0)
                return;

            var eventName = arr[0]?.ToString();
            var data = arr.Count > 1 ? arr[1]?.ToString() : null;

            if (!string.IsNullOrEmpty(eventName))
                _events.Emit(eventName, data);
        }

        internal void HandleAck(int ackId, string payload)
        {
            _acks.Resolve(ackId, payload);
        }

        public void Tick()
        {
            _acks.RemoveExpired();
        }
    }
}
