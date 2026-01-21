using System;
using Newtonsoft.Json;
using SocketIOUnity.Debugging;
using SocketIOUnity.Transport;
using SocketIOUnity.Runtime;
using SocketIOUnity.UnityIntegration;

namespace SocketIOUnity.EngineProtocol
{
    public sealed class EngineIOClient : ITickable, IDisposable
    {
        private readonly ITransport _transport;
        private readonly HeartbeatController _heartbeat;
        private readonly PingRttTracker _rtt = new PingRttTracker();

        private bool _isConnected;
        private HandshakeInfo _handshake;

        public bool IsConnected => _isConnected;
        public string SessionId => _handshake?.sid;
        public float PingRttMs => _rtt.RttMs;

        public event Action OnOpen;
        public event Action OnClose;
        public event Action<string> OnError;
        public event Action<string> OnMessage;
        public event Action<byte[]> OnBinary;

        public EngineIOClient(ITransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _heartbeat = new HeartbeatController();

            BindTransportEvents();
            BindHeartbeatEvents();

            UnityTickDriver.Register(this);
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
        /// Send RAW Engine.IO packet (caller handles framing)
        /// </summary>
        public void SendRaw(string raw)
        {
            if (!_isConnected)
                return;

            _transport.SendText(raw);
        }

        /// <summary>
        /// Called every frame by UnityTickDriver
        /// </summary>
        public void Tick()
        {
            _transport.Dispatch();
            _heartbeat.Tick();
        }

        // --------------------------------------------------
        // Transport bindings
        // --------------------------------------------------

        private void BindTransportEvents()
        {
            _transport.OnOpen += HandleTransportOpen;
            _transport.OnClose += HandleTransportClose;
            _transport.OnTextMessage += HandleTextMessage;
            _transport.OnBinaryMessage += HandleBinaryMessage;
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
            // Waiting for Engine.IO OPEN packet (type 0)
        }

        private void HandleTransportClose()
        {
            SocketIOTrace.Protocol(TraceCategory.EngineIO, "Transport closed");
            Cleanup();
            OnClose?.Invoke();
        }

        private void HandleTransportError(string error)
        {
            SocketIOTrace.Error(TraceCategory.EngineIO, $"Transport error: {error}");
            OnError?.Invoke(error);
        }

        private void HandleBinaryMessage(byte[] data)
        {
            SocketIOTrace.Verbose(TraceCategory.Binary, $"← BINARY {data.Length} bytes");
            OnBinary?.Invoke(data);
        }

        private void HandleTextMessage(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return;

            using (SocketIOProfiler.EngineIO_PacketParse.Auto())
            {
#if SOCKETIO_PROFILER_COUNTERS && UNITY_2020_2_OR_NEWER
                SocketIOProfilerCounters.PacketReceived();
#endif
                var type = (EngineMessageType)(raw[0] - '0');
                var payload = raw.Length > 1 ? raw.Substring(1) : null;

                HandleEngineMessage(new EngineMessage(type, payload));
            }
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
                    SocketIOTrace.Verbose(TraceCategory.EngineIO, "PING received");
                    _rtt.OnPingReceived();      // track ping timing for RTT estimate
                    // 🔥 REQUIRED BY ENGINE.IO v4 SPEC
                    _transport.SendText("3");   // PONG
                    SocketIOTrace.Verbose(TraceCategory.EngineIO, "PONG sent");
                    _heartbeat.OnPing();        // reset heartbeat window
                    break;

                case EngineMessageType.Pong:
                    SocketIOTrace.Verbose(TraceCategory.EngineIO, "PONG received");
                    // Note: In Engine.IO v4, server doesn't send PONG to clients
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

                SocketIOTrace.Protocol(
                    TraceCategory.EngineIO,
                    $"Handshake received (sid={_handshake.sid}, pingInterval={_handshake.pingInterval}ms, pingTimeout={_handshake.pingTimeout}ms)"
                );

                // ✅ Engine.IO v4 heartbeat config
                _heartbeat.Start(
                    _handshake.pingInterval,
                    _handshake.pingTimeout
                );

                // Set RTT tracker's expected interval
                _rtt.SetPingInterval(_handshake.pingInterval);

                _isConnected = true;
                OnOpen?.Invoke();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Handshake parse failed: {ex.Message}");
                Disconnect();
            }
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
            UnityTickDriver.Unregister(this);
            _isConnected = false;
            _heartbeat.Stop();
            _handshake = null;
        }

        /// <summary>
        /// Dispose and clean up all resources.
        /// </summary>
        public void Dispose()
        {
            Disconnect();
        }
    }
}
