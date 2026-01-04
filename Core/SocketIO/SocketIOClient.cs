using System;
using Newtonsoft.Json;
using SocketIOUnity.EngineProtocol;
using SocketIOUnity.SocketProtocol;
using SocketIOUnity.Transport;
using SocketIOUnity.UnityIntegration;

namespace SocketIOUnity.Runtime
{
    public sealed class SocketIOClient : ITickable
    {
        private EngineIOClient _engine;
        private readonly NamespaceManager _namespaces;
        private readonly NamespaceSocket _defaultNamespace;
        private readonly ReconnectController _reconnect;
        private readonly Func<ITransport> _transportFactory;

        private string _lastUrl;
        private bool _intentionalDisconnect;

        public bool IsConnected => _engine != null && _engine.IsConnected;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;

        // --------------------------------------------------
        // CONSTRUCTOR
        // --------------------------------------------------

        public SocketIOClient(Func<ITransport> transportFactory)
        {
            _transportFactory = transportFactory;

            CreateEngine();

            _namespaces = new NamespaceManager(this);
            _defaultNamespace = _namespaces.Get("/");

            _reconnect = new ReconnectController(() =>
            {
                if (!string.IsNullOrEmpty(_lastUrl))
                {
                    RecreateAndReconnect();
                }
            });

            UnityTickDriver.Register(this);
        }

        // --------------------------------------------------
        // ENGINE LIFECYCLE (CRITICAL)
        // --------------------------------------------------

        private void CreateEngine()
        {
            var transport = _transportFactory();

            _engine = new EngineIOClient(transport);
            _engine.OnOpen += HandleEngineOpen;
            _engine.OnClose += HandleEngineClose;
            _engine.OnError += HandleEngineError;
            _engine.OnMessage += HandleEngineMessage;
        }

        private void RecreateAndReconnect()
        {
            _engine?.Disconnect();
            CreateEngine();
            _engine.Connect(_lastUrl);
        }

        // --------------------------------------------------
        // PUBLIC API
        // --------------------------------------------------

        public NamespaceSocket Of(string ns) => _namespaces.Get(ns);

        public void Connect(string url)
        {
            _lastUrl = url;
            _intentionalDisconnect = false;
            _engine.Connect(url);
        }

        public void Disconnect()
        {
            _intentionalDisconnect = true;
            _reconnect.Stop();
            _engine?.Disconnect();
        }

        public void Dispose()
        {
            Disconnect();
        }

        public void Tick()
        {
            foreach (var ns in _namespaces.All)
                ns.Tick();

            _reconnect.Tick();
        }

        // --------------------------------------------------
        // DEFAULT NAMESPACE HELPERS
        // --------------------------------------------------

        public void Emit(string eventName, object payload)
        {
            _defaultNamespace.Emit(eventName, payload);
        }

        public void Emit(string eventName, object payload, Action<string> ack, int timeoutMs = 5000)
        {
            _defaultNamespace.Emit(eventName, payload, ack, timeoutMs);
        }

        public void On(string eventName, Action<string> handler)
        {
            _defaultNamespace.On(eventName, handler);
        }

        // --------------------------------------------------
        // INTERNAL — SOCKET.IO PACKETS
        // --------------------------------------------------

        internal void EmitInternal(string ns, string eventName, object payload, int? ackId)
        {
            var json = JsonConvert.SerializeObject(new object[] { eventName, payload });

            var packet =
                ((int)SocketPacketType.Event) +
                (ns != "/" ? ns + "," : "") +
                (ackId.HasValue ? ackId.Value.ToString() : "") +
                json;

            _engine.SendRaw("4" + packet);
        }

        internal void ConnectNamespace(string ns)
        {
            if (ns == "/" || !_engine.IsConnected)
                return;

            _engine.SendRaw("40" + ns);
        }

        // --------------------------------------------------
        // ENGINE.IO CALLBACKS
        // --------------------------------------------------

        private void HandleEngineOpen()
        {
            // Connect default namespace ONLY
            _engine.SendRaw("40");
        }

        private void HandleEngineClose()
        {
            OnDisconnected?.Invoke();

            foreach (var ns in _namespaces.All)
                ns.HandleDisconnect();

            if (_intentionalDisconnect)
                return;

            if (!_reconnect.IsRunning)
                _reconnect.Start();
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

            if (!_namespaces.TryGet(packet.Namespace, out var nsSocket))
                nsSocket = _defaultNamespace;

            switch (packet.Type)
            {
                case SocketPacketType.Connect:
                    nsSocket.HandleConnect();

                    if (packet.Namespace == "/")
                    {
                        _reconnect.Reset();
                        OnConnected?.Invoke();

                        foreach (var ns in _namespaces.All)
                            if (ns.Namespace != "/")
                                ConnectNamespace(ns.Namespace);
                    }
                    break;

                case SocketPacketType.Event:
                    nsSocket.HandleEvent(packet.JsonPayload);
                    break;

                case SocketPacketType.Ack:
                    nsSocket.HandleAck(packet.AckId.Value, packet.JsonPayload);
                    break;

                case SocketPacketType.Disconnect:
                    nsSocket.HandleDisconnect();
                    break;

                case SocketPacketType.Error:
                    OnError?.Invoke(packet.JsonPayload);
                    break;
            }
        }
    }
}
