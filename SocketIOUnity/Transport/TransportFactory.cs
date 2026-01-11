namespace SocketIOUnity.Transport
{
    /// <summary>
    /// Factory delegate for creating fresh transports.
    /// CRITICAL: Must return a NEW instance on every call.
    /// This enables clean reconnect resets.
    /// </summary>
    public delegate ITransport TransportFactory();

    /// <summary>
    /// Helper class for creating platform-specific transport factories.
    /// </summary>
    public static class TransportFactoryHelper
    {
        /// <summary>
        /// Creates a platform-appropriate transport factory.
        /// - WebGL builds: Uses WebGLWebSocketTransport (.jslib bridge)
        /// - All other platforms: Uses WebSocketTransport (WebSocketSharp)
        /// </summary>
        public static TransportFactory CreateDefault()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return () => new WebGLWebSocketTransport();
#else
            return () => new WebSocketTransport();
#endif
        }
    }
}
