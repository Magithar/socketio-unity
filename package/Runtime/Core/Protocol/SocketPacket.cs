namespace SocketIOUnity.SocketProtocol
{
    internal class SocketPacket
    {
        public SocketPacketType Type;
        public string Namespace;
        public int? AckId;
        public string JsonPayload;
        public int Attachments;

        public SocketPacket(
            SocketPacketType type,
            string ns = "/",
            int? ackId = null,
            string jsonPayload = null,
            int attachments = 0)
        {
            Type = type;
            Namespace = ns;
            AckId = ackId;
            JsonPayload = jsonPayload;
            Attachments = attachments;
        }
    }
}

