// ProfilerCounter<T> requires Unity 2020.2+
#if SOCKETIO_PROFILER_COUNTERS && UNITY_2020_2_OR_NEWER
using System.Runtime.CompilerServices;
using Unity.Profiling;

namespace SocketIOUnity.Debugging
{
    /// <summary>
    /// Unity Profiler Counters for SocketIOUnity.
    /// Enable via scripting define: SOCKETIO_PROFILER_COUNTERS
    /// When disabled, all code compiles to nothing.
    /// 
    /// View in: Profiler → Counters → Network / Scripts
    /// </summary>
    [System.Obsolete("Profiler APIs may change before v2.0.", false)]
    internal static class SocketIOProfilerCounters
    {
        // -------------------- TRANSPORT (Network category) --------------------

        static readonly ProfilerCounter<long> BytesSent =
            new ProfilerCounter<long>(
                ProfilerCategory.Network,
                "SocketIO.Bytes Sent",
                ProfilerMarkerDataUnit.Bytes
            );

        static readonly ProfilerCounter<long> BytesReceived =
            new ProfilerCounter<long>(
                ProfilerCategory.Network,
                "SocketIO.Bytes Received",
                ProfilerMarkerDataUnit.Bytes
            );

        static readonly ProfilerCounter<int> PacketsPerSecond =
            new ProfilerCounter<int>(
                ProfilerCategory.Network,
                "SocketIO.Packets/sec",
                ProfilerMarkerDataUnit.Count
            );

        // -------------------- SOCKET.IO (Scripts category) --------------------

        // Use ProfilerCounterValue for gauges (persistent state)
        static readonly ProfilerCounterValue<int> ActiveNamespaces =
            new ProfilerCounterValue<int>(
                ProfilerCategory.Scripts,
                "SocketIO.Active Namespaces",
                ProfilerMarkerDataUnit.Count,
                ProfilerCounterOptions.None 
            );

        static readonly ProfilerCounterValue<int> PendingAcks =
            new ProfilerCounterValue<int>(
                ProfilerCategory.Scripts,
                "SocketIO.Pending ACKs",
                ProfilerMarkerDataUnit.Count,
                ProfilerCounterOptions.None
            );

        // -------------------- PUBLIC API --------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddBytesSent(int bytes)
        {
            BytesSent.Sample(bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddBytesReceived(int bytes)
        {
            BytesReceived.Sample(bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PacketReceived()
        {
            PacketsPerSecond.Sample(1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetActiveNamespaces(int count)
        {
            ActiveNamespaces.Value = count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPendingAcks(int count)
        {
            PendingAcks.Value = count;
        }
    }
}
#endif
