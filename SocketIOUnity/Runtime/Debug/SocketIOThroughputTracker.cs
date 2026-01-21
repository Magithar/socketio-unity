#if SOCKETIO_PROFILER_COUNTERS
using UnityEngine;

namespace SocketIOUnity.Debugging
{
    /// <summary>
    /// Rolling bytes/sec average tracker.
    /// Provides smooth throughput numbers instead of spiky raw counters.
    /// Enable via scripting define: SOCKETIO_PROFILER_COUNTERS
    /// </summary>
    public static class SocketIOThroughputTracker
    {
        const float WindowSeconds = 1f;

        static long _bytesSent;
        static long _bytesReceived;

        static float _sentRate;
        static float _receivedRate;

        static float _lastUpdateTime;

        /// <summary>Smooth sent bytes per second</summary>
        public static float SentBytesPerSec => _sentRate;

        /// <summary>Smooth received bytes per second</summary>
        public static float ReceivedBytesPerSec => _receivedRate;

        /// <summary>Call when bytes are sent</summary>
        public static void AddSent(int bytes)
        {
            _bytesSent += bytes;
        }

        /// <summary>Call when bytes are received</summary>
        public static void AddReceived(int bytes)
        {
            _bytesReceived += bytes;
        }

        /// <summary>
        /// Called every frame from UnityTickDriver.
        /// Calculates average rate when window expires.
        /// </summary>
        public static void Tick()
        {
            float now = Time.unscaledTime;
            float dt = now - _lastUpdateTime;

            if (dt < WindowSeconds)
                return;

            _sentRate = _bytesSent / dt;
            _receivedRate = _bytesReceived / dt;

            _bytesSent = 0;
            _bytesReceived = 0;
            _lastUpdateTime = now;
        }
    }
}
#endif
