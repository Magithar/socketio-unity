namespace SocketIOUnity.EngineProtocol
{
    [System.Serializable]
    public class HandshakeInfo
    {
        public string sid;
        public int pingInterval;
        public int pingTimeout;
    }
}

