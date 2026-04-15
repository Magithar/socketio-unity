using System;
using UnityEngine;

namespace SocketIOUnity.Runtime
{
    /// <summary>
    /// Configuration for automatic reconnection behavior.
    /// Added in v1.1.0 to provide flexible reconnection strategies.
    /// </summary>
    [Serializable]
    public class ReconnectConfig
    {
        [Tooltip("Initial delay before first reconnect attempt (seconds)")]
        public float initialDelay = 1f;

        [Tooltip("Multiplier applied each attempt (e.g., 2 = exponential doubling)")]
        public float multiplier = 2f;

        [Tooltip("Maximum delay cap (seconds)")]
        public float maxDelay = 30f;

        [Tooltip("Maximum number of attempts (-1 = unlimited)")]
        public int maxAttempts = -1;

        [Tooltip("Enable automatic reconnection on disconnect")]
        public bool autoReconnect = true;

        [Tooltip("Random jitter percentage (0-1). Example: 0.1 = ±10% to prevent thundering herd")]
        [Range(0f, 0.5f)]
        public float jitterPercent = 0f;

        /// <summary>
        /// Default constructor with standard exponential backoff configuration.
        /// </summary>
        public ReconnectConfig() { }

        /// <summary>
        /// Copy constructor for defensive copying (prevents external mutation bugs).
        /// </summary>
        public ReconnectConfig(ReconnectConfig source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            initialDelay = source.initialDelay;
            multiplier = source.multiplier;
            maxDelay = source.maxDelay;
            maxAttempts = source.maxAttempts;
            autoReconnect = source.autoReconnect;
            jitterPercent = source.jitterPercent;
        }

        /// <summary>
        /// Aggressive reconnection strategy for local development.
        /// Fast retry with short delays.
        /// </summary>
        public static ReconnectConfig Aggressive() => new ReconnectConfig
        {
            initialDelay = 0.5f,
            multiplier = 1.5f,
            maxDelay = 10f,
            jitterPercent = 0.1f
        };

        /// <summary>
        /// Conservative reconnection strategy for production environments.
        /// Slower retry with longer delays to reduce server load.
        /// </summary>
        public static ReconnectConfig Conservative() => new ReconnectConfig
        {
            initialDelay = 2f,
            multiplier = 2.5f,
            maxDelay = 60f,
            jitterPercent = 0.15f
        };

        /// <summary>
        /// Default configuration matching v1.0.x behavior.
        /// </summary>
        public static ReconnectConfig Default() => new ReconnectConfig();
    }
}
