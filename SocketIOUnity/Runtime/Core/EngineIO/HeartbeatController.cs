using System;

namespace SocketIOUnity.EngineProtocol
{
    /// <summary>
    /// Engine.IO v4 heartbeat manager.
    /// Timeout = pingInterval + pingTimeout
    /// </summary>
    public sealed class HeartbeatController
    {
        private int _pingIntervalMs;
        private int _pingTimeoutMs;

        private DateTime _lastPingTime;
        private bool _active;

        public event Action OnTimeout;

        /// <summary>
        /// Start heartbeat tracking using Engine.IO handshake values.
        /// </summary>
        public void Start(int pingIntervalMs, int pingTimeoutMs)
        {
            _pingIntervalMs = pingIntervalMs;
            _pingTimeoutMs = pingTimeoutMs;

            _lastPingTime = DateTime.UtcNow;
            _active = true;
        }

        /// <summary>
        /// Must be called when a PING frame is received from server.
        /// </summary>
        public void OnPing()
        {
            _lastPingTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Stop heartbeat tracking.
        /// </summary>
        public void Stop()
        {
            _active = false;
        }

        /// <summary>
        /// Called every frame from UnityTickDriver.
        /// </summary>
        public void Tick()
        {
            if (!_active)
                return;

            var now = DateTime.UtcNow;
            var timeoutAt = _lastPingTime
                .AddMilliseconds(_pingIntervalMs + _pingTimeoutMs);

            if (now > timeoutAt)
            {
                _active = false;
                OnTimeout?.Invoke();
            }
        }
    }
}
