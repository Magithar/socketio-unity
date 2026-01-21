using System;

namespace SocketIOUnity.Runtime
{
    /// <summary>
    /// Pooled ACK callback entry to eliminate closure allocations.
    /// </summary>
    internal sealed class AckEntry
    {
        public int Id;
        public Action<string> Callback;
        public float ExpireAt;

        /// <summary>
        /// Reset state before returning to pool.
        /// </summary>
        public void Reset()
        {
            Id = 0;
            Callback = null;
            ExpireAt = 0;
        }
    }
}
