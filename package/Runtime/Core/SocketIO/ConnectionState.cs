namespace SocketIOUnity.Runtime
{
    /// <summary>
    /// Represents the current connection state of a <see cref="SocketIOClient"/>.
    /// Stable public API since v1.2.0.
    /// </summary>
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting
    }
}
