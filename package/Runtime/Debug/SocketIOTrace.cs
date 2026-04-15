using System.Runtime.CompilerServices;

namespace SocketIOUnity.Debugging
{
    /// <summary>
    /// Core tracing API. All protocol code calls these static methods.
    /// Uses AggressiveInlining for zero overhead when tracing is disabled.
    /// </summary>
    [System.Obsolete("Debugging APIs may change structure before v2.0.", false)]
    public static class SocketIOTrace
    {
        private static ITraceSink _sink = new UnityDebugTraceSink();

        /// <summary>
        /// Set a custom trace sink (file logger, network logger, UI overlay, etc.)
        /// </summary>
        public static void SetSink(ITraceSink sink)
        {
            _sink = sink ?? new UnityDebugTraceSink();
        }

        /// <summary>
        /// Log an error-level trace event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Error(TraceCategory category, string message)
        {
            if (!TraceConfig.IsEnabled(TraceLevel.Errors)) return;

            _sink.Emit(new TraceEvent(category, TraceLevel.Errors, message));
        }

        /// <summary>
        /// Log a protocol-level trace event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Protocol(TraceCategory category, string message)
        {
            if (!TraceConfig.IsEnabled(TraceLevel.Protocol)) return;

            _sink.Emit(new TraceEvent(category, TraceLevel.Protocol, message));
        }

        /// <summary>
        /// Log a verbose-level trace event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Verbose(TraceCategory category, string message)
        {
            if (!TraceConfig.IsEnabled(TraceLevel.Verbose)) return;

            _sink.Emit(new TraceEvent(category, TraceLevel.Verbose, message));
        }
    }
}
