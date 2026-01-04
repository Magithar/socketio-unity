using System;
using System.Text.RegularExpressions;

namespace SocketIOUnity.SocketProtocol
{
    public static class SocketPacketParser
    {
        private static readonly Regex NamespaceRegex =
            new Regex(@"^([0-6])(/[^,]*),?(.*)$");

        public static SocketPacket Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                throw new ArgumentException("Empty Socket.IO packet");

            var type = (SocketPacketType)(raw[0] - '0');
            var remainder = raw.Substring(1);

            string ns = "/";
            string payload = remainder;

            if (remainder.StartsWith("/"))
            {
                var match = NamespaceRegex.Match(raw);
                if (match.Success)
                {
                    ns = match.Groups[2].Value;
                    payload = match.Groups[3].Value;
                }
            }

            return new SocketPacket(type, ns, null, payload);
        }
    }
}

