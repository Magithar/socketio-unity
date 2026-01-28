using System;
using SocketIOUnity.Debugging;

namespace SocketIOUnity.SocketProtocol
{
    internal static class SocketPacketParser
    {
        /// <summary>
        /// Parse a raw Socket.IO packet string into a SocketPacket.
        /// Returns null if the packet is malformed (defensive parsing).
        /// </summary>
        public static SocketPacket Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                SocketIOTrace.Error(TraceCategory.SocketIO, "Empty Socket.IO packet received");
                return null;
            }

            int i = 0;

            // Note: Engine.IO framing ('4' prefix) is already stripped by
            // EngineIOClient before reaching this parser.
            // The raw string here is the Socket.IO packet payload only.

            // --------------------------------------------------
            // Socket.IO packet type (validate 0-6 range)
            // --------------------------------------------------
            if (!char.IsDigit(raw[i]))
            {
                SocketIOTrace.Error(TraceCategory.SocketIO, $"Invalid Socket.IO packet type: '{raw[i]}' in '{raw}'");
                return null;
            }
            int typeInt = raw[i] - '0';
            if (typeInt < 0 || typeInt > 6)
            {
                SocketIOTrace.Error(TraceCategory.SocketIO, $"Socket.IO packet type out of range: {typeInt} in '{raw}'");
                return null;
            }
            var type = (SocketPacketType)typeInt;
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
                {
                    if (!int.TryParse(raw.Substring(attachStart, i - attachStart), out attachments))
                    {
                        SocketIOTrace.Error(TraceCategory.SocketIO, $"Invalid binary attachment count in '{raw}'");
                        return null;
                    }
                }

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
            {
                if (int.TryParse(raw.Substring(ackStart, i - ackStart), out int parsedAckId))
                {
                    ackId = parsedAckId;
                }
                else
                {
                    SocketIOTrace.Error(TraceCategory.SocketIO, $"ACK ID overflow or invalid: '{raw.Substring(ackStart, i - ackStart)}'");
                    // Continue without ACK ID rather than crashing
                }
            }

            // --------------------------------------------------
            // Payload
            // --------------------------------------------------
            string payload = i < raw.Length ? raw.Substring(i) : null;

            return new SocketPacket(type, ns, ackId, payload, attachments);
        }
    }
}
