namespace SocketIOUnity.Debugging
{
    /// <summary>
    /// Global configuration for packet tracing.
    /// Set Level at runtime to enable/disable tracing.
    /// </summary>
    [System.Obsolete("Debugging APIs may change structure before v2.0.", false)]
    public static class TraceConfig
    {
        /// <summary>
        /// Current trace level. Set to None to disable all tracing.
        /// Can be changed at runtime.
        /// </summary>
        public static TraceLevel Level { get; set; } = TraceLevel.None;

        /// <summary>
        /// Check if a given trace level is enabled.
        /// </summary>
        public static bool IsEnabled(TraceLevel level)
        {
            return Level >= level && Level != TraceLevel.None;
        }
    }
}
