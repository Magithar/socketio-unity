namespace SocketIOUnity.EngineProtocol
{
    public enum EngineMessageType
    {
        Open = 0,
        Close = 1,
        Ping = 2,
        Pong = 3,
        Message = 4
    }

    public struct EngineMessage
    {
        public EngineMessageType Type;
        public string Payload;

        public EngineMessage(EngineMessageType type, string payload = null)
        {
            Type = type;
            Payload = payload;
        }
    }
}

