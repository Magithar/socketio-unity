using System;

namespace SocketIOUnity.SocketProtocol
{
    public static class SocketPacketParser
    {
        public static SocketPacket Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                throw new ArgumentException("Empty Socket.IO packet");

            int i = 0;

            // --------------------------------------------------
            // Engine.IO framing (WebSocket)
            // --------------------------------------------------
            // '4' = Engine.IO message
            if (raw[i] == '4')
                i++;

            if (i >= raw.Length)
                throw new ArgumentException("Invalid Socket.IO packet");

            // --------------------------------------------------
            // Socket.IO packet type
            // --------------------------------------------------
            var type = (SocketPacketType)(raw[i] - '0');
            i++;

            // --------------------------------------------------
            // Binary attachments (for BinaryEvent/BinaryAck)
            // Format: "51-..." where 1 is attachment count
            // --------------------------------------------------
            int attachments = 0;
            if (type == SocketPacketType.BinaryEvent || type == SocketPacketType.BinaryAck)
            {
                int attachStart = i;
                while (i < raw.Length && raw[i] != '-')
                    i++;

                if (i > attachStart)
                    int.TryParse(raw.Substring(attachStart, i - attachStart), out attachments);

                if (i < raw.Length && raw[i] == '-')
                    i++; // skip '-'
            }

            // --------------------------------------------------
            // Namespace
            // --------------------------------------------------
            string ns = "/";

            if (i < raw.Length && raw[i] == '/')
            {
                int nsStart = i;
                while (i < raw.Length && raw[i] != ',')
                    i++;

                ns = raw.Substring(nsStart, i - nsStart);

                if (i < raw.Length && raw[i] == ',')
                    i++;
            }

            // --------------------------------------------------
            // ACK ID
            // --------------------------------------------------
            int? ackId = null;
            int ackStart = i;

            while (i < raw.Length && char.IsDigit(raw[i]))
                i++;

            if (i > ackStart)
                ackId = int.Parse(raw.Substring(ackStart, i - ackStart));

            // --------------------------------------------------
            // Payload
            // --------------------------------------------------
            string payload = i < raw.Length ? raw.Substring(i) : null;

            return new SocketPacket(type, ns, ackId, payload, attachments);
        }
    }
}
