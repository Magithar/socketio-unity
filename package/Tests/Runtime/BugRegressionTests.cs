using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SocketIOUnity.Serialization;
using SocketIOUnity.SocketProtocol;
using SocketIOUnity.Runtime;

namespace SocketIOUnity.Tests
{
    /// <summary>
    /// Regression tests for v1.0.1 bug fixes
    /// </summary>
    public class BugRegressionTests
    {
        /// <summary>
        /// Bug #1: BinaryPacketAssembler should handle malformed JSON gracefully
        /// Previously: JArray.Parse() could throw without try-catch
        /// Fixed: Added try-catch with fallback to empty array
        /// </summary>
        [Test]
        public void BinaryPacketAssembler_HandlesInvalidJson_DoesNotThrow()
        {
            // Arrange
            var assembler = new BinaryPacketAssembler();
            var packet = new SocketPacket(
                type: SocketPacketType.BinaryEvent,
                ns: "/",
                ackId: null,
                jsonPayload: "this is not valid JSON {{{",
                attachments: 1
            );

            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() =>
            {
                assembler.Start(packet);
            });
        }

        /// <summary>
        /// Bug #1 cont: BinaryPacketAssembler should handle empty JSON
        /// </summary>
        [Test]
        public void BinaryPacketAssembler_HandlesEmptyJson_DoesNotThrow()
        {
            // Arrange
            var assembler = new BinaryPacketAssembler();
            var packet = new SocketPacket(
                type: SocketPacketType.BinaryEvent,
                ns: "/",
                ackId: null,
                jsonPayload: "",
                attachments: 1
            );

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                assembler.Start(packet);
            });
        }

        /// <summary>
        /// Bug #1 cont: BinaryPacketAssembler should handle null JSON
        /// </summary>
        [Test]
        public void BinaryPacketAssembler_HandlesNullJson_DoesNotThrow()
        {
            // Arrange
            var assembler = new BinaryPacketAssembler();
            var packet = new SocketPacket(
                type: SocketPacketType.BinaryEvent,
                ns: "/",
                ackId: null,
                jsonPayload: null,
                attachments: 1
            );

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                assembler.Start(packet);
            });
        }

        /// <summary>
        /// Bug #4: AckRegistry should handle integer overflow gracefully
        /// Previously: _nextId would overflow to negative after 2 billion increments
        /// Fixed: Wraps around to 1 (skips 0 and negatives)
        /// </summary>
        [Test]
        public void AckRegistry_HandlesIntegerOverflow_WrapsToOne()
        {
            // Arrange
            var registry = new AckRegistry();
            Action<string> callback = (payload) => { /* callback placeholder */ };

            // Use reflection to set _nextId to int.MaxValue - 1
            var field = typeof(AckRegistry).GetField("_nextId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "Could not find _nextId field via reflection");

            field.SetValue(registry, int.MaxValue - 1);

            // Act - Register three times to trigger overflow
            var id1 = registry.Register(callback, TimeSpan.FromSeconds(5));  // Should be int.MaxValue
            var id2 = registry.Register(callback, TimeSpan.FromSeconds(5));  // Would overflow to negative
            var id3 = registry.Register(callback, TimeSpan.FromSeconds(5));  // Next ID

            // Assert - IDs should be positive and wrapped
            Assert.Greater(id1, 0, "ID1 should be positive");
            Assert.AreEqual(int.MaxValue, id1, "ID1 should be int.MaxValue");
            Assert.Greater(id2, 0, "ID2 should be positive (wrapped to 1)");
            Assert.AreEqual(1, id2, "ID2 should wrap to 1");
            Assert.Greater(id3, 0, "ID3 should be positive");
            Assert.AreEqual(2, id3, "ID3 should be 2");
        }

        /// <summary>
        /// Bug #4 cont: Verify ACK registry continues to work after overflow
        /// </summary>
        [Test]
        public void AckRegistry_AfterOverflow_ContinuesWorking()
        {
            // Arrange
            var registry = new AckRegistry();
            Action<string> callback = (payload) => { /* callback placeholder */ };

            // Force overflow
            var field = typeof(AckRegistry).GetField("_nextId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(registry, int.MaxValue - 1);

            // Act - Register after overflow point
            registry.Register(callback, TimeSpan.FromSeconds(5));
            var wrappedId = registry.Register(callback, TimeSpan.FromSeconds(5));  // Should be 1

            // Resolve the wrapped ID
            var resolved = registry.Resolve(wrappedId, "test payload");

            // Assert
            Assert.IsTrue(resolved, "Should successfully resolve wrapped ID");
            Assert.AreEqual(1, wrappedId, "Wrapped ID should be 1");

            // Note: Callback is queued to UnityMainThreadDispatcher and will execute on next Update()
            // In a real scenario, you'd need to wait for next frame or use UnityTest with yield
        }

        /// <summary>
        /// Bug #2 & #3 are internal implementation fixes with no direct test surface:
        /// - Bug #2: WebSocketTransport event nullification removed (tested via integration)
        /// - Bug #3: Static dictionary cleanup on domain reload (tested via Unity lifecycle)
        ///
        /// These are validated through:
        /// 1. Manual reconnection testing (Bug #2)
        /// 2. Editor play mode enter/exit cycles (Bug #3)
        /// </summary>
    }
}
