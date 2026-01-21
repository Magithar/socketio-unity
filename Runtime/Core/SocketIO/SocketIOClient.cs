using System;
using Newtonsoft.Json;
using SocketIOUnity.Debugging;
using SocketIOUnity.EngineProtocol;
using SocketIOUnity.Serialization;
using SocketIOUnity.SocketProtocol;
using SocketIOUnity.Transport;
using SocketIOUnity.UnityIntegration;

namespace SocketIOUnity.Runtime
{
    public sealed class SocketIOClient : ITickable
    {
        private readonly TransportFactory _transportFactory;
        private readonly ReconnectController _reconnect;

        private EngineIOClient _engine;
        private NamespaceManager _namespaces;
        private NamespaceSocket _defaultNamespace;
        private BinaryPacketAssembler _binaryAssembler;

        private string _lastUrl;
        private bool _intentionalDisconnect;

        public bool IsConnected => _engine != null && _engine.IsConnected;

        // Telemetry accessors for Editor HUD
        public int NamespaceCount => _namespaces.Count;
        public int PendingAckCount => _defaultNamespace?.PendingAckCount ?? 0;
        public float PingRttMs => _engine?.PingRttMs ?? 0f;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;

        // --------------------------------------------------
        // CONSTRUCTOR
        // --------------------------------------------------

        public SocketIOClient(TransportFactory transportFactory)
        {
            _transportFactory = transportFactory 
                ?? throw new ArgumentNullException(nameof(transportFactory));

            // 🔥 CRITICAL: Create ReconnectController ONCE (not on every reconnect)
            _reconnect = new ReconnectController(AttemptReconnect);

            CreateFreshEngine();

            UnityTickDriver.Register(this);
        }

        // --------------------------------------------------
        // ENGINE LIFECYCLE (🔥 THE FIX)
        // --------------------------------------------------

        private void CreateFreshEngine()
        {
            DestroyEngine();

            var transport = _transportFactory.Invoke();

            _engine = new EngineIOClient(transport);
            _engine.OnOpen += HandleEngineOpen;
            _engine.OnClose += HandleEngineClose;
            _engine.OnError += HandleEngineError;
            _engine.OnMessage += HandleEngineMessage;
            _engine.OnBinary += HandleEngineBinary;

            // 🔥 CRITICAL: Recreate ALL state on reconnect
            _namespaces = new NamespaceManager(this);
            _defaultNamespace = _namespaces.Get("/");
            _binaryAssembler = new BinaryPacketAssembler();
        }

        private void DestroyEngine()
        {
            _engine?.Disconnect();
            _engine = null;
        }

        private void AttemptReconnect()
        {
            CreateFreshEngine();
            _engine.Connect(_lastUrl);
        }

        // --------------------------------------------------
        // PUBLIC API
        // --------------------------------------------------

        public NamespaceSocket Of(string ns, object auth = null) => _namespaces.Get(ns, auth);

        public void Connect(string url)
        {
            SocketIOTrace.Protocol(TraceCategory.SocketIO, $"Connecting to {url}");
            _lastUrl = url;
            _intentionalDisconnect = false;
            _engine.Connect(url);
        }

        public void Disconnect()
        {
            SocketIOTrace.Protocol(TraceCategory.SocketIO, "Intentional disconnect");
            _intentionalDisconnect = true;
            _reconnect.Stop();
            DestroyEngine();
        }

        public void Shutdown()
        {
            _intentionalDisconnect = true;
            _reconnect.Stop();
            DestroyEngine();
            UnityTickDriver.Unregister(this);
        }

        public void Dispose()
        {
            Shutdown();
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

        public void On(string eventName, Action<byte[]> handler)
        {
            _defaultNamespace.On(eventName, handler);
        }

        /// <summary>
        /// Unsubscribe a string event handler from the default namespace.
        /// </summary>
        public void Off(string eventName, Action<string> handler)
        {
            _defaultNamespace.Off(eventName, handler);
        }

        /// <summary>
        /// Unsubscribe a binary event handler from the default namespace.
        /// </summary>
        public void Off(string eventName, Action<byte[]> handler)
        {
            _defaultNamespace.Off(eventName, handler);
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

            SocketIOTrace.Verbose(TraceCategory.SocketIO, $"→ Event '{eventName}' ns={ns} ackId={ackId}");
            _engine.SendRaw("4" + packet);
        }

        internal void SendEnginePacket(string packet)
        {
            _engine.SendRaw("4" + packet);
        }

        internal void ConnectNamespace(string ns)
        {
            if (ns == "/" || !_engine.IsConnected)
                return;

            var namespaceSocket = _namespaces.Get(ns);
            namespaceSocket.SendConnect();
        }

        // --------------------------------------------------
        // ENGINE.IO CALLBACKS
        // --------------------------------------------------

        private void HandleEngineOpen()
        {
            SocketIOTrace.Protocol(TraceCategory.EngineIO, "Engine.IO connection opened");
            // Connect default namespace with auth support
            _defaultNamespace.SendConnect();
        }


        private void HandleEngineClose()
        {
            OnDisconnected?.Invoke();

            // Reset namespaces before disconnect handlers
            _namespaces.ResetAll();

            foreach (var ns in _namespaces.All)
                ns.HandleDisconnect();

            if (_intentionalDisconnect)
                return;

            // 🔥 SAFETY: Don't reconnect if already connected (race condition guard)
            if (IsConnected)
                return;

            if (!_reconnect.IsRunning)
                _reconnect.Start();
        }

        private void HandleEngineError(string error)
        {
            SocketIOTrace.Error(TraceCategory.SocketIO, $"Engine error: {error}");
            OnError?.Invoke(error);
        }

        private void HandleEngineMessage(string raw)
        {
            SocketPacket packet;

            try
            {
                packet = SocketPacketParser.Parse(raw);
                SocketIOTrace.Verbose(TraceCategory.SocketIO, $"← Packet type={packet.Type} ns={packet.Namespace}");
            }
            catch (Exception ex)
            {
                SocketIOTrace.Error(TraceCategory.SocketIO, $"Parse error: {ex.Message}");
                OnError?.Invoke($"Socket.IO parse error: {ex.Message}");
                return;
            }

            if (!_namespaces.TryGet(packet.Namespace, out var nsSocket))
                nsSocket = _defaultNamespace;

            switch (packet.Type)
            {
                case SocketPacketType.Connect:
                    SocketIOTrace.Protocol(TraceCategory.Namespace, $"Namespace connected: {packet.Namespace}");
                    nsSocket.HandleConnect();

                    if (packet.Namespace == "/")
                    {
                        _reconnect.Reset();
                        OnConnected?.Invoke();

                        foreach (var ns in _namespaces.All)
                            if (ns.Namespace != "/")
                                ns.SendConnect();
                    }
                    break;

                case SocketPacketType.ConnectError:
                    nsSocket.HandleConnectError(packet.JsonPayload);
                    break;

                case SocketPacketType.Event:
                    nsSocket.HandleEvent(packet.JsonPayload);
                    break;

                case SocketPacketType.Ack:
                    SocketIOTrace.Protocol(TraceCategory.Ack, $"ACK received id={packet.AckId}");
                    nsSocket.HandleAck(packet.AckId.Value, packet.JsonPayload);
                    break;

                case SocketPacketType.Disconnect:
                    nsSocket.HandleDisconnect();
                    break;

                case SocketPacketType.BinaryEvent:
                case SocketPacketType.BinaryAck:
                    SocketIOTrace.Verbose(TraceCategory.Binary, $"Binary packet started, attachments={packet.Attachments}");
                    // Abort any in-progress assembly (overlapping packet protection)
                    if (_binaryAssembler.IsWaiting)
                        _binaryAssembler.Abort();
                    _binaryAssembler.Start(packet);
                    // TODO: Route BinaryAck to ack handler (currently only BinaryEvent is dispatched)
                    break;
            }
        }

        private void HandleEngineBinary(byte[] data)
        {
            if (!_binaryAssembler.IsWaiting)
                return;

            SocketIOTrace.Verbose(TraceCategory.Binary, $"Binary attachment received: {data.Length} bytes");

            if (_binaryAssembler.AddBinary(data))
            {
                var (eventName, args, ns) = _binaryAssembler.Build();

                SocketIOTrace.Protocol(TraceCategory.Binary, $"Binary event complete: '{eventName}' with {args.Count} args");

                if (!_namespaces.TryGet(ns, out var nsSocket))
                    nsSocket = _defaultNamespace;

                nsSocket.HandleBinaryEvent(eventName, args);
            }
        }
    }
}
