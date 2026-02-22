using NUnit.Framework;
using SocketIOUnity.Runtime;
using SocketIOUnity.Transport;
using UnityEngine;

namespace SocketIOUnity.Tests
{
    /// <summary>
    /// Tests for ReconnectConfig defensive copying behavior (v1.1.0)
    /// Verifies that external mutation does not affect the socket's internal config.
    /// </summary>
    public class ReconnectConfigTests
    {
        [Test]
        public void DefensiveCopy_OnSet_PreventsExternalMutation()
        {
            // Arrange
            var socket = new SocketIOClient(TransportFactoryHelper.CreateDefault());
            var config = new ReconnectConfig
            {
                maxDelay = 10f,
                initialDelay = 1f
            };

            // Act - Set the config
            socket.ReconnectConfig = config;

            // Mutate the external reference AFTER setting
            config.maxDelay = 999f;
            config.initialDelay = 999f;

            // Assert - Socket's config should NOT be affected by external mutation
            var socketConfig = socket.ReconnectConfig;
            Assert.AreEqual(10f, socketConfig.maxDelay, "maxDelay should not be affected by external mutation");
            Assert.AreEqual(1f, socketConfig.initialDelay, "initialDelay should not be affected by external mutation");

            Debug.Log("✅ Defensive copying on SET works - external mutation prevented");
        }

        [Test]
        public void CopyConstructor_CreatesIndependentCopy()
        {
            // Arrange
            var original = new ReconnectConfig
            {
                initialDelay = 2f,
                multiplier = 3f,
                maxDelay = 60f,
                maxAttempts = 10,
                autoReconnect = false,
                jitterPercent = 0.2f
            };

            // Act
            var copy = new ReconnectConfig(original);

            // Mutate original
            original.initialDelay = 999f;
            original.multiplier = 999f;

            // Assert
            Assert.AreEqual(2f, copy.initialDelay, "Copy should be independent");
            Assert.AreEqual(3f, copy.multiplier, "Copy should be independent");
            Assert.AreEqual(60f, copy.maxDelay);
            Assert.AreEqual(10, copy.maxAttempts);
            Assert.AreEqual(false, copy.autoReconnect);
            Assert.AreEqual(0.2f, copy.jitterPercent, 0.001f);

            Debug.Log("✅ Copy constructor creates independent copy");
        }

        [Test]
        public void FactoryMethods_CreateCorrectConfigs()
        {
            // Arrange & Act
            var aggressive = ReconnectConfig.Aggressive();
            var conservative = ReconnectConfig.Conservative();
            var defaultConfig = ReconnectConfig.Default();

            // Assert - Aggressive should have faster reconnection
            Assert.Less(aggressive.maxDelay, conservative.maxDelay, "Aggressive should have shorter max delay");
            Assert.Less(aggressive.initialDelay, conservative.initialDelay, "Aggressive should have shorter initial delay");

            // Default should match standard values
            Assert.AreEqual(1f, defaultConfig.initialDelay);
            Assert.AreEqual(2f, defaultConfig.multiplier);
            Assert.AreEqual(30f, defaultConfig.maxDelay);
            Assert.AreEqual(-1, defaultConfig.maxAttempts);
            Assert.AreEqual(true, defaultConfig.autoReconnect);
            Assert.AreEqual(0f, defaultConfig.jitterPercent);

            Debug.Log("✅ Factory methods create correct configurations");
        }

        [Test]
        public void DefaultConfig_MatchesV1_0Behavior()
        {
            // Arrange & Act
            var config = new ReconnectConfig();

            // Assert - Should match v1.0.x hardcoded behavior
            // Formula: initialDelay * multiplier^attempt, capped at maxDelay
            // Attempt 0: 1 * 2^0 = 1s
            // Attempt 1: 1 * 2^1 = 2s
            // Attempt 2: 1 * 2^2 = 4s
            // Attempt 3: 1 * 2^3 = 8s
            // Attempt 4: 1 * 2^4 = 16s
            // Attempt 5: 1 * 2^5 = 32s → capped at 30s

            Assert.AreEqual(1f, config.initialDelay, "Default initial delay should be 1s");
            Assert.AreEqual(2f, config.multiplier, "Default multiplier should be 2 (exponential doubling)");
            Assert.AreEqual(30f, config.maxDelay, "Default max delay should be 30s");
            Assert.AreEqual(-1, config.maxAttempts, "Default max attempts should be unlimited");
            Assert.AreEqual(0f, config.jitterPercent, "Default jitter should be 0 (disabled)");

            Debug.Log("✅ Default config matches v1.0.x behavior (backward compatible)");
        }

        [Test]
        public void PropertyMutation_StillWorks_InV1_X()
        {
            // This test verifies v1.x backward compatibility
            // In v1.x, direct property mutation is still supported (getter returns reference)
            // This will be removed in v2.0

            // Arrange
            var socket = new SocketIOClient(TransportFactoryHelper.CreateDefault());

            // Act - Direct property mutation (v1.x legacy pattern)
            socket.ReconnectConfig.maxDelay = 999f;

            // Assert - Should work in v1.x (getter returns reference)
            Assert.AreEqual(999f, socket.ReconnectConfig.maxDelay,
                "v1.x should support direct property mutation for backward compatibility");

            Debug.Log("⚠️ Direct property mutation works in v1.x (will be removed in v2.0)");
        }
    }
}
