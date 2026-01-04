using System;
using Newtonsoft.Json;
using SocketIOUnity.Transport;

namespace SocketIOUnity.EngineProtocol
{
    public class EngineIOClient
    {
        private readonly ITransport _transport;
        private readonly HeartbeatController _heartbeat;

        private bool _isConnected;
        private HandshakeInfo _handshake;

        public bool IsConnected => _isConnected;
        public string SessionId => _handshake?.sid;

        public event Action OnOpen;
        public event Action OnClose;
        public event Action<string> OnError;
        public event Action<string> OnMessage;

        public EngineIOClient(ITransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _heartbeat = new HeartbeatController();

            BindTransportEvents();
            BindHeartbeatEvents();
        }

        // --------------------------------------------------
        // Public API
        // --------------------------------------------------

        public void Connect(string baseUrl)
        {
            if (_isConnected)
                return;

            var url = BuildEngineIOUrl(baseUrl);
            _transport.Connect(url);
        }

        public void Disconnect()
        {
            _transport.Close();
            Cleanup();
        }

        /// <summary>
        /// Engine.IO MESSAGE wrapper (4 + Socket.IO payload)
        /// </summary>
        public void Send(string socketIoPayload)
        {
            if (!_isConnected)
                return;

            _transport.SendText("4" + socketIoPayload);
        }

        /// <summary>
        /// Raw Engine.IO packet (40, 41, etc.)
        /// </summary>
        public void SendRaw(string raw)
        {
            if (!_isConnected)
                return;

            _transport.SendText(raw);
        }

        /// <summary>
        /// MUST be called every Unity frame
        /// </summary>
        public void Update()
        {
            _heartbeat.Update();
        }

        // --------------------------------------------------
        // Transport bindings
        // --------------------------------------------------

        private void BindTransportEvents()
        {
            _transport.OnOpen += HandleTransportOpen;
            _transport.OnClose += HandleTransportClose;
            _transport.OnTextMessage += HandleTextMessage;
            _transport.OnError += HandleTransportError;
        }

        private void BindHeartbeatEvents()
        {
            _heartbeat.OnTimeout += () =>
            {
                OnError?.Invoke("Engine.IO heartbeat timeout");
                Disconnect();
            };
        }

        // --------------------------------------------------
        // Transport handlers
        // --------------------------------------------------

        private void HandleTransportOpen()
        {
            // Waiting for Engine.IO OPEN packet
        }

        private void HandleTransportClose()
        {
            Cleanup();
            OnClose?.Invoke();
        }

        private void HandleTransportError(string error)
        {
            OnError?.Invoke(error);
        }

        private void HandleTextMessage(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return;

            var type = (EngineMessageType)(raw[0] - '0');
            var payload = raw.Length > 1 ? raw.Substring(1) : null;

            HandleEngineMessage(new EngineMessage(type, payload));
        }

        // --------------------------------------------------
        // Engine.IO message handling
        // --------------------------------------------------

        private void HandleEngineMessage(EngineMessage message)
        {
            switch (message.Type)
            {
                case EngineMessageType.Open:
                    HandleOpen(message.Payload);
                    break;

                case EngineMessageType.Ping:
                    // 🔥 REQUIRED: respond + reset timeout window
                    _transport.SendText("3");   // PONG
                    _heartbeat.Beat();          // reset heartbeat window
                    break;

                case EngineMessageType.Pong:
                    // Engine.IO v4: server may never send this
                    break;

                case EngineMessageType.Message:
                    OnMessage?.Invoke(message.Payload);
                    break;

                case EngineMessageType.Close:
                    Disconnect();
                    break;
            }
        }

        private void HandleOpen(string payload)
        {
            try
            {
                _handshake = JsonConvert.DeserializeObject<HandshakeInfo>(payload);

                // Use pingTimeout only (spec-correct)
                _heartbeat.Start(_handshake.pingTimeout);

                _isConnected = true;
                OnOpen?.Invoke();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Handshake parse failed: {ex.Message}");
                Disconnect();
            }
        }

        private void HandlePing()
        {
            // Respond with Engine.IO PONG
            _transport.SendText("3");
        }

        // --------------------------------------------------
        // Helpers
        // --------------------------------------------------

        private static string BuildEngineIOUrl(string baseUrl)
        {
            var uri = new Uri(baseUrl);

            string scheme = uri.Scheme switch
            {
                "http" => "ws",
                "https" => "wss",
                "ws" => "ws",
                "wss" => "wss",
                _ => throw new ArgumentException($"Unsupported protocol: {uri.Scheme}")
            };

            var portPart = uri.IsDefaultPort ? "" : $":{uri.Port}";
            var path = string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/"
                ? "/socket.io/"
                : uri.AbsolutePath;

            return $"{scheme}://{uri.Host}{portPart}{path}?EIO=4&transport=websocket";
        }

        private void Cleanup()
        {
            _isConnected = false;
            _heartbeat.Stop();
            _handshake = null;
        }
    }
}
