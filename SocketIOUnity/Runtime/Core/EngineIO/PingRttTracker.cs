using UnityEngine;

namespace SocketIOUnity.EngineProtocol
{
    /// <summary>
    /// Measures round-trip latency using Engine.IO PING timing.
    /// In Engine.IO v4, server sends PING, client responds with PONG.
    /// We measure the time from receiving PING to sending PONG (local processing)
    /// plus track when we expect the next PING to estimate connection health.
    /// </summary>
    internal sealed class PingRttTracker
    {
        float _lastPingReceiveTime;
        float _pingInterval;
        float _rttMs;
        bool _hasReceivedPing;

        /// <summary>Current estimated RTT in milliseconds</summary>
        public float RttMs => _rttMs;

        /// <summary>Call when PING is received from server, before sending PONG</summary>
        public void OnPingReceived()
        {
            float now = Time.realtimeSinceStartup;
            
            if (_hasReceivedPing && _pingInterval > 0)
            {
                // Measure how long since last ping (should be ~pingInterval)
                // Any deviation from expected interval indicates network jitter/latency
                float elapsed = (now - _lastPingReceiveTime) * 1000f;
                float expectedMs = _pingInterval;
                
                // RTT estimate = how much later than expected the ping arrived
                // If ping arrives exactly on time, RTT ~= 0
                // If ping is late, that latency is roughly RTT / 2
                float deviation = elapsed - expectedMs;
                if (deviation > 0)
                {
                    _rttMs = deviation; // Rough RTT estimate
                }
            }
            
            _lastPingReceiveTime = now;
            _hasReceivedPing = true;
        }

        /// <summary>Set expected ping interval from handshake (in ms)</summary>
        public void SetPingInterval(int intervalMs)
        {
            _pingInterval = intervalMs;
        }

        /// <summary>Reset when disconnected</summary>
        public void Reset()
        {
            _hasReceivedPing = false;
            _rttMs = 0;
        }
    }
}

