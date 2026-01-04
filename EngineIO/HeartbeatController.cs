using System;

namespace SocketIOUnity.EngineProtocol
{
    public class HeartbeatController
    {
        private int _timeoutMs;
        private DateTime _lastBeatTime;
        private bool _active;

        public event Action OnTimeout;

        /// <summary>
        /// Starts the heartbeat timeout window.
        /// </summary>
        public void Start(int pingTimeoutMs)
        {
            _timeoutMs = pingTimeoutMs;
            _lastBeatTime = DateTime.UtcNow;
            _active = true;
        }

        /// <summary>
        /// Call this when a PING is received.
        /// </summary>
        public void Beat()
        {
            _lastBeatTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Backward-compatible alias.
        /// </summary>
        public void Reset()
        {
            Beat();
        }

        /// <summary>
        /// Stops heartbeat monitoring.
        /// </summary>
        public void Stop()
        {
            _active = false;
        }

        /// <summary>
        /// Must be called regularly from Unity main thread.
        /// </summary>
        public void Update()
        {
            if (!_active)
                return;

            var elapsedMs = (DateTime.UtcNow - _lastBeatTime).TotalMilliseconds;

            if (elapsedMs >= _timeoutMs)
            {
                _active = false;
                OnTimeout?.Invoke();
            }
        }
    }
}
