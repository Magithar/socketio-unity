namespace SocketIOUnity.Debugging
{
    /// <summary>
    /// Source category for trace events.
    /// </summary>
    public enum TraceCategory
    {
        /// <summary>Engine.IO layer (handshake, ping/pong)</summary>
        EngineIO,
        
        /// <summary>Socket.IO layer (events, namespaces)</summary>
        SocketIO,
        
        /// <summary>Transport layer (WebSocket send/receive)</summary>
        Transport,
        
        /// <summary>Binary attachments</summary>
        Binary,
        
        /// <summary>Reconnection logic</summary>
        Reconnect,
        
        /// <summary>Namespace operations</summary>
        Namespace,
        
        /// <summary>Acknowledgement tracking</summary>
        Ack
    }
}
