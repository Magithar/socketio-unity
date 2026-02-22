using System;
using SocketIOUnity.Debugging;
using UnityEngine;

namespace SocketIOUnity.Runtime
{
    internal sealed class ReconnectController
    {
        private readonly Action _reconnectAction;

        // Store as private copy - prevent external mutation
        private ReconnectConfig _config = new ReconnectConfig();

        /// <summary>
        /// Get or set reconnection configuration.
        /// Setting creates a defensive copy to prevent external mutation.
        /// </summary>
        public ReconnectConfig Config
        {
            get => _config; // Return reference (v1.x compatibility)
            set => _config = new ReconnectConfig(value ?? new ReconnectConfig()); // Defensive copy on set
        }

        private int _attempt;
        private float _nextAttemptTime;
        private bool _enabled;

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

                // Check if we've exceeded max attempts
                if (_config.maxAttempts > 0 && _attempt >= _config.maxAttempts)
                {
                    SocketIOTrace.Protocol(TraceCategory.Reconnect, $"Max reconnect attempts ({_config.maxAttempts}) reached");
                    Stop();
                    return;
                }

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
            // Calculate exponential backoff with configurable parameters
            float baseDelay = _config.initialDelay * Mathf.Pow(_config.multiplier, _attempt);
            float delay = Mathf.Min(baseDelay, _config.maxDelay);

            // Apply jitter if configured (prevents thundering herd problem)
            if (_config.jitterPercent > 0f)
            {
                float jitterAmount = delay * _config.jitterPercent;
                delay += UnityEngine.Random.Range(-jitterAmount, jitterAmount);

                // Ensure non-negative delay with reasonable minimum
                delay = Mathf.Max(delay, 0.1f);
            }

            _nextAttemptTime = Time.time + delay;

            SocketIOTrace.Protocol(TraceCategory.Reconnect, $"Next reconnect in {delay:0.2}s (attempt {_attempt + 1})");
        }
    }
}
