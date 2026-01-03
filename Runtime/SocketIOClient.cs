using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocketIOUnity.EngineProtocol;
using SocketIOUnity.SocketProtocol;
using SocketIOUnity.Transport;

namespace SocketIOUnity.Runtime
{
    public class SocketIOClient
    {
        private readonly EngineIOClient _engine;
        private readonly EventRegistry _events = new();

        public bool IsConnected => _engine.IsConnected;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;

        public SocketIOClient(ITransport transport)
        {
            _engine = new EngineIOClient(transport);

            _engine.OnOpen += HandleEngineOpen;
            _engine.OnClose += HandleEngineClose;
            _engine.OnError += HandleEngineError;
            _engine.OnMessage += HandleEngineMessage;
        }

        // --------------------------------------------------
        // Public API
        // --------------------------------------------------

        public void Connect(string url)
        {
            _engine.Connect(url);
        }

        public void Disconnect()
        {
            _engine.Disconnect();
        }

        public void On(string eventName, Action<string> handler)
        {
            _events.On(eventName, handler);
        }

        public void Emit(string eventName, object payload)
        {
            if (!IsConnected)
                return;

            var json = JsonConvert.SerializeObject(new object[]
            {
                eventName,
                payload
            });

            // Socket.IO EVENT = 2
            _engine.Send("2" + json);
        }

        // --------------------------------------------------
        // Engine.IO handlers
        // --------------------------------------------------

        private void HandleEngineOpen()
        {
            // 🔥 Correct Socket.IO CONNECT framing
            _engine.SendRaw("40");
        }

        private void HandleEngineClose()
        {
            OnDisconnected?.Invoke();
        }

        private void HandleEngineError(string error)
        {
            OnError?.Invoke(error);
        }

        private void HandleEngineMessage(string raw)
        {
            SocketPacket packet;

            try
            {
                packet = SocketPacketParser.Parse(raw);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Socket.IO parse error: {ex.Message}");
                return;
            }

            switch (packet.Type)
            {
                case SocketPacketType.Connect:
                    OnConnected?.Invoke();
                    break;

                case SocketPacketType.Event:
                    HandleEventPacket(packet);
                    break;

                case SocketPacketType.Disconnect:
                    OnDisconnected?.Invoke();
                    break;

                case SocketPacketType.Error:
                    OnError?.Invoke(packet.JsonPayload);
                    break;
            }
        }

        // --------------------------------------------------
        // Socket.IO helpers
        // --------------------------------------------------

        private void HandleEventPacket(SocketPacket packet)
        {
            if (string.IsNullOrEmpty(packet.JsonPayload))
                return;

            try
            {
                var arr = JArray.Parse(packet.JsonPayload);
                if (arr.Count == 0)
                    return;

                var eventName = arr[0]?.ToString();
                var payload = arr.Count > 1 ? arr[1]?.ToString() : null;

                if (!string.IsNullOrEmpty(eventName))
                {
                    _events.Emit(eventName, payload);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Event decode failed: {ex.Message}");
            }
        }
    }
}
