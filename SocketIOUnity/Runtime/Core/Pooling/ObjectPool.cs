using System;
using System.Collections.Generic;

namespace SocketIOUnity.Core
{
    /// <summary>
    /// Generic object pool to eliminate GC allocations.
    /// Thread-safe for Unity main thread (single-threaded).
    /// </summary>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Stack<T> _pool;
        private readonly Func<T> _factory;
        private readonly Action<T> _reset;

        /// <summary>
        /// Creates a new object pool.
        /// </summary>
        /// <param name="factory">Function to create new instances when pool is empty</param>
        /// <param name="reset">Optional callback to reset object state before returning to pool</param>
        /// <param name="initialCapacity">Number of objects to pre-allocate</param>
        public ObjectPool(Func<T> factory, Action<T> reset = null, int initialCapacity = 16)
        {
            _factory = factory;
            _reset = reset;
            _pool = new Stack<T>(initialCapacity);

            // Pre-allocate objects to avoid allocations during runtime
            for (int i = 0; i < initialCapacity; i++)
                _pool.Push(_factory());
        }

        /// <summary>
        /// Rent an object from the pool. Creates new instance if pool is empty.
        /// </summary>
        public T Rent()
        {
            return _pool.Count > 0 ? _pool.Pop() : _factory();
        }

        /// <summary>
        /// Return an object to the pool. Calls reset callback before adding to pool.
        /// </summary>
        public void Return(T item)
        {
            _reset?.Invoke(item);
            _pool.Push(item);
        }

        /// <summary>
        /// Clear all pooled objects. WARNING: Only call during shutdown, not reconnect.
        /// </summary>
        public void Clear()
        {
            _pool.Clear();
        }
    }
}
