using System.Collections.Generic;
using SocketIOUnity.Core;

namespace SocketIOUnity.Runtime
{
    /// <summary>
    /// Static pool for temporary List&lt;T&gt; instances to eliminate hidden GC allocations.
    /// Usage: var list = ListPool&lt;int&gt;.Rent(); ... ListPool&lt;int&gt;.Return(list);
    /// </summary>
    internal static class ListPool<T>
    {
        private static readonly ObjectPool<List<T>> _pool =
            new ObjectPool<List<T>>(
                factory: () => new List<T>(8),
                reset: l => l.Clear(),
                initialCapacity: 16
            );

        /// <summary>
        /// Rent a list from the pool.
        /// </summary>
        public static List<T> Rent() => _pool.Rent();

        /// <summary>
        /// Return a list to the pool. List is automatically cleared.
        /// </summary>
        public static void Return(List<T> list) => _pool.Return(list);
    }
}
