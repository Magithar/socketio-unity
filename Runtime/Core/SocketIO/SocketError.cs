namespace SocketIOUnity.Runtime
{
    /// <summary>
    /// Categories of errors that can occur in SocketIOUnity.
    /// Stable public API since v1.3.0.
    /// </summary>
    public enum ErrorType
    {
        /// <summary>WebSocket connection or send failure.</summary>
        Transport,

        /// <summary>Server-rejected connection (connect_error), typically authentication.</summary>
        Auth,

        /// <summary>Heartbeat timeout — server stopped responding to pings.</summary>
        Timeout,

        /// <summary>Malformed or unparseable packet data.</summary>
        Protocol
    }

    /// <summary>
    /// Typed error value passed to <see cref="SocketIOClient.OnError"/>.
    /// Replaces the raw string from v1.2 and earlier.
    /// Stable public API since v1.3.0.
    /// </summary>
    public readonly struct SocketError
    {
        /// <summary>Category of the error.</summary>
        public ErrorType Type { get; }

        /// <summary>Human-readable error description.</summary>
        public string Message { get; }

        public SocketError(ErrorType type, string message)
        {
            Type = type;
            Message = message ?? string.Empty;
        }

        public override string ToString() => $"[{Type}] {Message}";
    }
}
