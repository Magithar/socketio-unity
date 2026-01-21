using UnityEngine;
using SocketIOUnity.Debugging;

/// <summary>
/// Temporary test script to verify Profiler Counters are working.
/// Attach to any GameObject and enter Play Mode.
/// Delete after verification.
/// </summary>
public class ProfilerCounterTest : MonoBehaviour
{
#if SOCKETIO_PROFILER_COUNTERS && UNITY_2020_2_OR_NEWER
    void Start()
    {
        // Force sample all counters so they appear in Profiler Module Editor
        SocketIOProfilerCounters.AddBytesSent(0);
        SocketIOProfilerCounters.AddBytesReceived(0);
        SocketIOProfilerCounters.PacketReceived();
        SocketIOProfilerCounters.SetActiveNamespaces(0);
        SocketIOProfilerCounters.SetPendingAcks(0);
        
        Debug.Log("[ProfilerCounterTest] Counters initialized. Search 'SocketIO' in Profiler Module Editor.");
    }
    
    void Update()
    {
        // Continuously sample to keep counters visible
        SocketIOProfilerCounters.AddBytesSent(Random.Range(10, 100));
        SocketIOProfilerCounters.AddBytesReceived(Random.Range(10, 100));
        SocketIOProfilerCounters.PacketReceived();
    }
#else
    void Start()
    {
        Debug.LogWarning("[ProfilerCounterTest] SOCKETIO_PROFILER_COUNTERS is not defined or Unity version is below 2020.2.");
    }
#endif
}
