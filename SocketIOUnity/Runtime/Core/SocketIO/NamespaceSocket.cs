using System;
using Newtonsoft.Json.Linq;
using SocketIOUnity.Debugging;

namespace SocketIOUnity.Runtime
{
    public sealed class NamespaceSocket : ITickable
    {
        private readonly string _namespace;
        private readonly SocketIOClient _root;
        private readonly EventRegistry _events = new();
        private readonly AckRegistry _acks = new();
        private readonly object _authPayload;

        private bool _connected;

        internal string Namespace => _namespace;
        internal bool IsConnected => _connected;
        public int PendingAckCount => _acks.Count;

        public event Action OnConnected;

        internal NamespaceSocket(string ns, SocketIOClient root, object auth = null)
        {
            _namespace = ns;
            _root = root;
            _authPayload = auth;
        }

        // ---------------- Public API ----------------

        public void On(string eventName, Action<string> handler)
        {
            _events.On(eventName, handler);
        }

        public void On(string eventName, Action<byte[]> handler)
        {
            _events.On(eventName, handler);
        }

        /// <summary>
        /// Unsubscribe a string event handler from this namespace.
        /// </summary>
        public void Off(string eventName, Action<string> handler)
        {
            _events.Off(eventName, handler);
        }

        /// <summary>
        /// Unsubscribe a binary event handler from this namespace.
        /// </summary>
        public void Off(string eventName, Action<byte[]> handler)
        {
            _events.Off(eventName, handler);
        }

        public void Emit(string eventName, object payload)
        {
            if (!_connected)
                return;

            _root.EmitInternal(_namespace, eventName, payload, null);
        }

        public void Emit(
            string eventName,
            object payload,
            Action<string> ack,
            int timeoutMs = 5000)
        {
            if (!_connected)
                return;

            var ackId = _acks.Register(
                ack,
                TimeSpan.FromMilliseconds(timeoutMs));

            _root.EmitInternal(_namespace, eventName, payload, ackId);
        }

        // ---------------- Internal ----------------

        internal void SendConnect()
        {
            if (_connected)
                return;

            string packet = "0"; // CONNECT packet type

            if (_namespace != "/")
                packet += _namespace;

            if (_authPayload != null)
                packet += "," + Newtonsoft.Json.JsonConvert.SerializeObject(_authPayload);

            SocketIOTrace.Protocol(TraceCategory.Namespace, $"Connecting namespace '{_namespace}'");
            _root.SendEnginePacket(packet);
        }

        internal void HandleConnect()
        {
            if (_connected)
                return;

            _connected = true;
            OnConnected?.Invoke();
            _events.Emit("connect", null);
        }

        internal void HandleConnectError(string json)
        {
            SocketIOTrace.Error(TraceCategory.Namespace, $"Connect error on '{_namespace}': {json}");
            _connected = false;
            _events.Emit("connect_error", json);
        }

        internal void HandleDisconnect()
        {
            _connected = false;
            _acks.Clear(); // 🔥 purge all pending ACKs on disconnect
        }

        internal void Reset()
        {
            _connected = false;
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
            catch (Exception ex)
            {
                // Log malformed payloads for debugging
                SocketIOTrace.Error(TraceCategory.SocketIO, $"Malformed JSON payload on '{_namespace}': {ex.Message}");
                return;
            }

            if (arr.Count == 0)
                return;

            var eventName = arr[0]?.ToString();
            var data = arr.Count > 1 ? arr[1]?.ToString() : null;

            SocketIOTrace.Verbose(TraceCategory.SocketIO, $"Event '{eventName}' on '{_namespace}' data={data?.Length ?? 0} chars");

            if (!string.IsNullOrEmpty(eventName))
                _events.Emit(eventName, data);
        }

        internal void HandleAck(int ackId, string payload)
        {
            _acks.Resolve(ackId, payload);
        }

        internal void HandleBinaryEvent(string eventName, Newtonsoft.Json.Linq.JArray args)
        {
            if (string.IsNullOrEmpty(eventName))
                return;

            // Skip event name (args[0]), process remaining arguments
            if (args.Count > 1)
            {
                var arg = args[1];

                // If the argument is a byte[] (from JValue), dispatch to binary handlers
                if (arg is Newtonsoft.Json.Linq.JValue jval && jval.Value is byte[] bytes)
                {
                    _events.EmitBinary(eventName, bytes);
                }
                else
                {
                    // Fall back to string handler
                    _events.Emit(eventName, arg?.ToString());
                }
            }
        }

        public void Tick()
        {
            _acks.RemoveExpired();
        }
    }
}
