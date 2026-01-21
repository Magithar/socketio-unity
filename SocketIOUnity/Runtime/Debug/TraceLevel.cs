namespace SocketIOUnity.Debugging
{
    /// <summary>
    /// Tracing verbosity level. Higher values include all lower levels.
    /// </summary>
    public enum TraceLevel
    {
        /// <summary>No tracing output</summary>
        None = 0,
        
        /// <summary>Only errors</summary>
        Errors = 1,
        
        /// <summary>Protocol-level messages (packets, reconnects, ACKs)</summary>
        Protocol = 2,
        
        /// <summary>Full detail including binary payloads</summary>
        Verbose = 3
    }
}
