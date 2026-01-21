using System;
using SocketIOUnity.Debugging;
using UnityEngine;

namespace SocketIOUnity.Runtime
{
    internal sealed class ReconnectController
    {
        private readonly Action _reconnectAction;

        private int _attempt;
        private float _nextAttemptTime;
        private bool _enabled;

        private const float MaxDelay = 30f;

        public bool IsRunning => _enabled;

        public ReconnectController(Action reconnectAction)
        {
            _reconnectAction = reconnectAction;
        }

        /// <summary>
        /// Start reconnect attempts (idempotent)
        /// </summary>
        public void Start()
        {
            if (_enabled)
                return; // 🔥 CRITICAL FIX — prevent restart loop

            _enabled = true;
            _attempt = 0;
            ScheduleNext();
        }

        /// <summary>
        /// Stop reconnect attempts
        /// </summary>
        public void Stop()
        {
            _enabled = false;
        }

        /// <summary>
        /// Reset after successful connection
        /// </summary>
        public void Reset()
        {
            _enabled = false;
            _attempt = 0;
        }

        public void Tick()
        {
            using (SocketIOProfiler.Reconnect_Loop.Auto())
            {
                if (!_enabled)
                    return;

                if (Time.time >= _nextAttemptTime)
                {
                    SocketIOTrace.Protocol(TraceCategory.Reconnect, $"Reconnect attempt {_attempt + 1} firing now");
                    _attempt++;
                    _reconnectAction.Invoke();
                    ScheduleNext();
                }
            }
        }

        private void ScheduleNext()
        {
            float delay = Mathf.Min(Mathf.Pow(2, _attempt), MaxDelay);
            _nextAttemptTime = Time.time + delay;

            SocketIOTrace.Protocol(TraceCategory.Reconnect, $"Next reconnect in {delay:0.0}s (attempt {_attempt + 1})");
        }
    }
}
