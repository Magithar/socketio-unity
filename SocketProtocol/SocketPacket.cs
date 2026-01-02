namespace SocketIOUnity.SocketProtocol
{
    public class SocketPacket
    {
        public SocketPacketType Type;
        public string Namespace;
        public int? AckId;
        public string JsonPayload;

        public SocketPacket(
            SocketPacketType type,
            string ns = "/",
            int? ackId = null,
            string jsonPayload = null)
        {
            Type = type;
            Namespace = ns;
            AckId = ackId;
            JsonPayload = jsonPayload;
        }
    }
}

