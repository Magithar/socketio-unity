namespace SocketIOUnity.EngineProtocol
{
    [System.Serializable]
    internal class HandshakeInfo
    {
        public string sid;
        public int pingInterval;
        public int pingTimeout;
    }
}

