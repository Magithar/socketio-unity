using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SocketIOUnity.Debugging;
using SocketIOUnity.SocketProtocol;

namespace SocketIOUnity.Serialization
{
    /// <summary>
    /// Collects binary frames and reconstructs Socket.IO binary event payloads.
    /// Socket.IO sends binary data as placeholders in JSON, followed by raw binary frames.
    /// </summary>
    internal sealed class BinaryPacketAssembler
    {
        private SocketPacketType _type;
        private string _namespace;
        private int? _ackId;
        private int _expected;
        private JArray _json;
        private readonly List<byte[]> _buffers = new();

        /// <summary>
        /// Returns true if we are waiting for binary frames.
        /// </summary>
        public bool IsWaiting => _expected > 0;

        /// <summary>
        /// Start collecting binary frames for a binary packet.
        /// </summary>
        public void Start(SocketPacket packet)
        {
            _type = packet.Type;
            _namespace = packet.Namespace;
            _ackId = packet.AckId;
            _expected = packet.Attachments;

            try
            {
                _json = JArray.Parse(packet.JsonPayload);
            }
            catch (Exception ex)
            {
                SocketIOTrace.Error(TraceCategory.Binary, $"Failed to parse binary packet JSON payload: {ex.Message}");
                _json = new JArray(); // Use empty array as fallback
            }

            _buffers.Clear();
        }

        /// <summary>
        /// Add a binary frame. Returns true when all expected frames are received.
        /// </summary>
        public bool AddBinary(byte[] data)
        {
            using (SocketIOProfiler.Binary_Assembly.Auto())
            {
                _buffers.Add(data);
                return _buffers.Count == _expected;
            }
        }

        /// <summary>
        /// Build the final event with placeholders replaced by actual binary data.
        /// Returns the packet type, ACK ID (for BinaryAck), event name, args, and namespace.
        /// </summary>
        public (SocketPacketType type, int? ackId, string eventName, JArray args, string ns) Build()
        {
            ReplacePlaceholders(_json);

            var eventName = _json[0]?.ToString();
            var result = (_type, _ackId, eventName, _json, _namespace);

            Reset();
            return result;
        }

        /// <summary>
        /// Abort the current binary packet assembly (e.g., on disconnect or protocol error).
        /// </summary>
        public void Abort() => Reset();

        private void Reset()
        {
            _type = default;
            _ackId = null;
            _expected = 0;
            _json = null;
            _buffers.Clear();
        }

        /// <summary>
        /// Recursively finds and replaces {"_placeholder":true,"num":N} with actual byte[] data.
        /// </summary>
        private void ReplacePlaceholders(JToken token)
        {
            if (token is JObject obj &&
                obj.TryGetValue("_placeholder", out var ph) &&
                ph.Value<bool>())
            {
                int idx = obj["num"].Value<int>();
                if (idx >= 0 && idx < _buffers.Count)
                    token.Replace(new JValue(_buffers[idx]));
                return;
            }

            // Use pooled list to avoid allocation during enumeration
            var children = Runtime.ListPool<JToken>.Rent();
            foreach (var child in token.Children())
                children.Add(child);
                
            foreach (var child in children)
                ReplacePlaceholders(child);
                
            Runtime.ListPool<JToken>.Return(children);
        }
    }
}
