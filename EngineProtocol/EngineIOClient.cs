using System;
using UnityEngine;
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

        // Engine.IO lifecycle events
        public event Action OnOpen;
        public event Action OnClose;
        public event Action<string> OnError;

        // Raw MESSAGE packets (Socket.IO layer will consume later)
        public event Action<string> OnMessage;

        public EngineIOClient(ITransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _heartbeat = new HeartbeatController();

            BindTransportEvents();
            BindHeartbeatEvents();
        }

        // --------------------------------------------------------------------
        // Public API
        // --------------------------------------------------------------------

        public void Connect(string baseUrl)
        {
            if (_isConnected)
                return;

            var url = BuildEngineIOUrl(baseUrl);
            _transport.Connect(url);
        }

        public void Disconnect()
        {
            if (!_isConnected)
                return;

            _transport.Close();
        }

        public void Send(string payload)
        {
            if (!_isConnected || string.IsNullOrEmpty(payload))
                return;

            // Engine.IO MESSAGE packet type = 4
            _transport.SendText(((int)EngineMessageType.Message) + payload);
        }

        // ✅ REQUIRED for NativeWebSocket
        public void Dispatch()
        {
            _transport.Dispatch();
        }

        // --------------------------------------------------------------------
        // Transport bindings
        // --------------------------------------------------------------------

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

        // --------------------------------------------------------------------
        // Transport event handlers
        // --------------------------------------------------------------------

        private void HandleTransportOpen()
        {
            // Waiting for Engine.IO OPEN packet (type 0)
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

            if (!char.IsDigit(raw[0]))
            {
                OnError?.Invoke($"Invalid Engine.IO packet: {raw}");
                return;
            }

            var type = (EngineMessageType)(raw[0] - '0');
            var payload = raw.Length > 1 ? raw.Substring(1) : null;

            HandleEngineMessage(new EngineMessage(type, payload));
        }

        // --------------------------------------------------------------------
        // Engine.IO message handling
        // --------------------------------------------------------------------

        private void HandleEngineMessage(EngineMessage message)
        {
            switch (message.Type)
            {
                case EngineMessageType.Open:
                    HandleOpen(message.Payload);
                    break;

                case EngineMessageType.Ping:
                    HandlePing();
                    break;

                case EngineMessageType.Pong:
                    _heartbeat.Reset();
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
                _handshake = JsonUtility.FromJson<HandshakeInfo>(payload);

                if (_handshake == null)
                    throw new Exception("Handshake JSON parsed as null");

                _heartbeat.Start(_handshake.pingTimeout);

                _isConnected = true;

                // 🔥 IMPORTANT: CONNECT TO SOCKET.IO DEFAULT NAMESPACE
                // This sends the Socket.IO "CONNECT" packet
                _transport.SendText("40");

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
            // Respond with PONG (type 3)
            _transport.SendText(((int)EngineMessageType.Pong).ToString());
            _heartbeat.Reset();
        }

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------

        private static string BuildEngineIOUrl(string baseUrl)
        {
            var separator = baseUrl.Contains("?") ? "&" : "?";
            return $"{baseUrl}{separator}EIO=4&transport=websocket";
        }

        private void Cleanup()
        {
            _isConnected = false;
            _heartbeat.Stop();
            _handshake = null;
        }
    }
}
