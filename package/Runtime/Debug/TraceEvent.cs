using System;

namespace SocketIOUnity.Debugging
{
    /// <summary>
    /// Immutable structured log entry for tracing.
    /// Uses readonly struct to avoid heap allocation.
    /// </summary>
    public readonly struct TraceEvent
    {
        /// <summary>Source category of the trace event</summary>
        public readonly TraceCategory Category;
        
        /// <summary>Human-readable message</summary>
        public readonly string Message;
        
        /// <summary>Severity/verbosity level</summary>
        public readonly TraceLevel Level;
        
        /// <summary>UTC timestamp when the event was created</summary>
        public readonly DateTime Timestamp;

        public TraceEvent(
            TraceCategory category,
            TraceLevel level,
            string message)
        {
            Category = category;
            Level = level;
            Message = message;
            Timestamp = DateTime.UtcNow;
        }
    }
}
