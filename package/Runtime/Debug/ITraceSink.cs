namespace SocketIOUnity.Debugging
{
    /// <summary>
    /// Interface for trace output destinations.
    /// Implement this to redirect trace output to custom targets
    /// (file, network, UI overlay, etc.)
    /// </summary>
    public interface ITraceSink
    {
        /// <summary>
        /// Emit a trace event to the output destination.
        /// </summary>
        /// <param name="evt">The trace event to emit</param>
        void Emit(in TraceEvent evt);
    }
}
