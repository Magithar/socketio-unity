using SocketIOUnity.EngineProtocol;
using SocketIOUnity.SocketProtocol;
using SocketIOUnity.Transport;
using System;

namespace SocketIOUnity.Runtime
{
    public class SocketIOClient
    {
        private readonly EngineIOClient _engine;

        public event Action<string> OnConnect;
        public event Action<string> OnDisconnect;
        public event Action<string, string> OnEvent;

        public SocketIOClient(ITransport transport)
        {
            _engine = new EngineIOClient(transport);
            _engine.OnMessage += HandleEngineMessage;
        }

        public void Connect(string url)
        {
            _engine.Connect(url);
        }
        
        public void Dispatch()
        {
            _engine.Dispatch();
        }

        private void HandleEngineMessage(string raw)
        {
            var packet = SocketPacketParser.Parse(raw);

            switch (packet.Type)
            {
                case SocketPacketType.Connect:
                    OnConnect?.Invoke(packet.Namespace);
                    break;

                case SocketPacketType.Event:
                    OnEvent?.Invoke(packet.Namespace, packet.JsonPayload);
                    break;

                case SocketPacketType.Disconnect:
                    OnDisconnect?.Invoke(packet.Namespace);
                    break;
            }
        }
    }
}
