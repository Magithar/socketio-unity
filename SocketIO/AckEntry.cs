using System;

namespace SocketIOUnity.Runtime
{
    internal class AckEntry
    {
        public int Id { get; }
        public Action<string> Callback { get; }
        public DateTime Expiry { get; }

        public AckEntry(int id, Action<string> callback, TimeSpan timeout)
        {
            Id = id;
            Callback = callback;
            Expiry = DateTime.UtcNow.Add(timeout);
        }

        public bool IsExpired => DateTime.UtcNow >= Expiry;
    }
}
