using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace SocketIOUnity.Serialization
{
    /// <summary>
    /// Pooled binary packet builder to eliminate allocations during binary emit.
    /// </summary>
    internal sealed class BinaryPacketBuilder
    {
        public readonly List<byte[]> Buffers = new();
        public readonly JArray JsonArgs = new();

        /// <summary>
        /// Reset state before returning to pool.
        /// </summary>
        public void Reset()
        {
            Buffers.Clear();
            JsonArgs.Clear();
        }
    }
}
