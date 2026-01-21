using SocketIOUnity.Core;

namespace SocketIOUnity.Serialization
{
    /// <summary>
    /// Static pool for BinaryPacketBuilder instances.
    /// </summary>
    internal static class BinaryPacketBuilderPool
    {
        private static readonly ObjectPool<BinaryPacketBuilder> _pool =
            new ObjectPool<BinaryPacketBuilder>(
                factory: () => new BinaryPacketBuilder(),
                reset: b => b.Reset(),
                initialCapacity: 8
            );

        /// <summary>
        /// Rent a builder from the pool.
        /// </summary>
        public static BinaryPacketBuilder Rent() => _pool.Rent();

        /// <summary>
        /// Return a builder to the pool. Lists are automatically cleared.
        /// </summary>
        public static void Return(BinaryPacketBuilder builder) => _pool.Return(builder);
    }
}
