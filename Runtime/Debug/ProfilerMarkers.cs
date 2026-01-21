#if SOCKETIO_PROFILER
using Unity.Profiling;
#endif

namespace SocketIOUnity.Debugging
{
    /// <summary>
    /// Zero-cost profiler markers for SocketIOUnity.
    /// Enable via scripting define: SOCKETIO_PROFILER
    /// When disabled, all code compiles to nothing.
    /// </summary>
    internal static class SocketIOProfiler
    {
#if SOCKETIO_PROFILER
        public static readonly ProfilerMarker EngineIO_PacketParse =
            new ProfilerMarker("SocketIO.EngineIO.Parse");

        public static readonly ProfilerMarker SocketIO_EventDispatch =
            new ProfilerMarker("SocketIO.Event.Dispatch");

        public static readonly ProfilerMarker Binary_Assembly =
            new ProfilerMarker("SocketIO.Binary.Assemble");

        public static readonly ProfilerMarker Ack_Resolve =
            new ProfilerMarker("SocketIO.Ack.Resolve");

        public static readonly ProfilerMarker Reconnect_Loop =
            new ProfilerMarker("SocketIO.Reconnect.Tick");
#else
        // Zero-cost stubs when profiler is disabled
        public static readonly DummyMarker EngineIO_PacketParse = default;
        public static readonly DummyMarker SocketIO_EventDispatch = default;
        public static readonly DummyMarker Binary_Assembly = default;
        public static readonly DummyMarker Ack_Resolve = default;
        public static readonly DummyMarker Reconnect_Loop = default;
#endif
    }

#if !SOCKETIO_PROFILER
    /// <summary>
    /// Zero-cost dummy marker that compiles to nothing.
    /// </summary>
    internal readonly struct DummyMarker
    {
        public DummyScope Auto() => default;
    }

    /// <summary>
    /// Zero-cost dummy scope that compiles to nothing.
    /// </summary>
    internal readonly struct DummyScope : System.IDisposable
    {
        public void Dispose() { }
    }
#endif
}
