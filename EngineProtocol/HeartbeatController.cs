using System;
using System.Timers;

namespace SocketIOUnity.EngineProtocol
{
    public class HeartbeatController
    {
        private Timer _timeoutTimer;
        private int _timeoutMs;

        public event Action OnTimeout;

        public void Start(int pingTimeoutMs)
        {
            _timeoutMs = pingTimeoutMs;

            _timeoutTimer?.Stop();
            _timeoutTimer = new Timer(_timeoutMs);
            _timeoutTimer.Elapsed += HandleTimeout;
            _timeoutTimer.AutoReset = false;
            _timeoutTimer.Start();
        }

        public void Reset()
        {
            _timeoutTimer?.Stop();
            _timeoutTimer?.Start();
        }

        public void Stop()
        {
            _timeoutTimer?.Stop();
            _timeoutTimer = null;
        }

        private void HandleTimeout(object sender, ElapsedEventArgs e)
        {
            OnTimeout?.Invoke();
        }
    }
}

