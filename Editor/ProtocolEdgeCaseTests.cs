using UnityEngine;
using UnityEditor;
using SocketIOUnity.SocketProtocol;
using SocketIOUnity.Debugging;

namespace SocketIOUnity.Editor
{
    /// <summary>
    /// Editor tests for protocol edge-cases.
    /// Run via menu: SocketIO → Run Protocol Edge Tests
    /// </summary>
    public static class ProtocolEdgeCaseTests
    {
        private static int _passed;
        private static int _failed;

        [MenuItem("SocketIO/Run Protocol Edge Tests")]
        public static void RunAllTests()
        {
            _passed = 0;
            _failed = 0;

            Debug.Log("═══════════════════════════════════════════════════════════");
            Debug.Log("🧪 P0.1 + P0.4 Protocol Edge-Case Tests");
            Debug.Log("═══════════════════════════════════════════════════════════");

            // Socket.IO Parser Tests (P0.1)
            TestEmptyPacket();
            TestNullPacket();
            TestTypeOnlyPacket();
            TestInvalidSocketIOType();
            TestValidSocketIOTypes();
            TestHugeAckIdOverflow();
            TestBinaryMissingSeparator();
            TestBinaryWithSeparator();
            TestNamespaceParking();
            TestMalformedJson();

            // Namespace Disconnect Tests (P0.4)
            TestDisconnectPacketParsing();

            // Summary
            Debug.Log("═══════════════════════════════════════════════════════════");
            Debug.Log($"📊 Results: {_passed} PASSED, {_failed} FAILED");
            Debug.Log("═══════════════════════════════════════════════════════════");

            if (_failed == 0)
                Debug.Log("✅ All protocol edge-case tests passed!");
            else
                Debug.LogError($"❌ {_failed} test(s) failed!");
        }

        // ═══════════════════════════════════════════════════════════
        // SOCKET.IO PARSER TESTS
        // ═══════════════════════════════════════════════════════════

        private static void TestEmptyPacket()
        {
            var result = SocketPacketParser.Parse("");
            AssertNull(result, "Empty packet should return null");
        }

        private static void TestNullPacket()
        {
            var result = SocketPacketParser.Parse(null);
            AssertNull(result, "Null packet should return null");
        }

        private static void TestTypeOnlyPacket()
        {
            // "4" alone (just Engine.IO frame, no Socket.IO type)
            var result = SocketPacketParser.Parse("4");
            AssertNull(result, "Type-only packet '4' should return null");
        }

        private static void TestInvalidSocketIOType()
        {
            // "49" = type 9 which is invalid (valid: 0-6)
            var result = SocketPacketParser.Parse("49");
            AssertNull(result, "Invalid type '49' should return null");

            // "4X" = non-numeric type
            result = SocketPacketParser.Parse("4X");
            AssertNull(result, "Non-numeric type '4X' should return null");

            // "47" = type 7 which is out of range
            result = SocketPacketParser.Parse("47");
            AssertNull(result, "Out-of-range type '47' should return null");
        }

        private static void TestValidSocketIOTypes()
        {
            // Test all valid Socket.IO types 0-6
            for (int i = 0; i <= 6; i++)
            {
                var result = SocketPacketParser.Parse($"4{i}");
                AssertNotNull(result, $"Valid type '4{i}' should parse");
                AssertEquals((int)result.Type, i, $"Type should be {i}");
            }
        }

        private static void TestHugeAckIdOverflow()
        {
            // ACK ID that would overflow int.Parse
            var result = SocketPacketParser.Parse("42999999999999999999[]");
            // Should not crash - either returns null or parses without ACK
            if (result != null)
                AssertNull(result.AckId, "Overflowing ACK ID should be null");
            else
                Pass("Overflowing ACK ID handled (returned null)");
        }

        private static void TestBinaryMissingSeparator()
        {
            // Binary event without proper separator: "451" instead of "451-"
            var result = SocketPacketParser.Parse("451");
            // Parser should handle this gracefully (might fail on attachment parse)
            // Key: no crash
            Pass("Binary without separator did not crash");
        }

        private static void TestBinaryWithSeparator()
        {
            // Proper binary event: "451-/ns,[\"event\",{\"_placeholder\":true,\"num\":0}]"
            var result = SocketPacketParser.Parse("451-[\"event\",{\"_placeholder\":true,\"num\":0}]");
            AssertNotNull(result, "Valid binary packet should parse");
            AssertEquals(result.Attachments, 1, "Should have 1 attachment");
        }

        private static void TestNamespaceParking()
        {
            // Valid namespace
            var result = SocketPacketParser.Parse("40/admin,");
            AssertNotNull(result, "Namespace packet should parse");
            AssertEquals(result.Namespace, "/admin", "Namespace should be /admin");

            // Default namespace
            result = SocketPacketParser.Parse("40");
            AssertNotNull(result, "Default namespace packet should parse");
            AssertEquals(result.Namespace, "/", "Namespace should be /");
        }

        private static void TestMalformedJson()
        {
            // Malformed JSON payload - parser should still work (JSON parsing happens later)
            var result = SocketPacketParser.Parse("42[\"event\",{invalid}]");
            AssertNotNull(result, "Malformed JSON should still parse (validation is deferred)");
            AssertNotNull(result.JsonPayload, "Should have payload");
        }

        // ═══════════════════════════════════════════════════════════
        // P0.4 NAMESPACE DISCONNECT TESTS
        // ═══════════════════════════════════════════════════════════

        private static void TestDisconnectPacketParsing()
        {
            // Root namespace disconnect: "41"
            var result = SocketPacketParser.Parse("41");
            AssertNotNull(result, "Root disconnect should parse");
            AssertEquals((int)result.Type, 1, "Type should be 1 (Disconnect)");
            AssertEquals(result.Namespace, "/", "Namespace should be /");

            // Custom namespace disconnect: "41/admin,"
            result = SocketPacketParser.Parse("41/admin,");
            AssertNotNull(result, "Namespace disconnect should parse");
            AssertEquals((int)result.Type, 1, "Type should be 1 (Disconnect)");
            AssertEquals(result.Namespace, "/admin", "Namespace should be /admin");

            // Disconnect without trailing comma (edge case)
            result = SocketPacketParser.Parse("41/chat");
            AssertNotNull(result, "Disconnect without comma should parse");
            AssertEquals(result.Namespace, "/chat", "Namespace should be /chat");
        }

        // ═══════════════════════════════════════════════════════════
        // TEST HELPERS
        // ═══════════════════════════════════════════════════════════

        private static void AssertNull(object value, string message)
        {
            if (value == null)
                Pass(message);
            else
                Fail(message, "null", value?.ToString() ?? "null");
        }

        private static void AssertNotNull(object value, string message)
        {
            if (value != null)
                Pass(message);
            else
                Fail(message, "not null", "null");
        }

        private static void AssertEquals<T>(T actual, T expected, string message)
        {
            if (Equals(actual, expected))
                Pass(message);
            else
                Fail(message, expected?.ToString(), actual?.ToString());
        }

        private static void Pass(string message)
        {
            _passed++;
            Debug.Log($"  ✓ {message}");
        }

        private static void Fail(string message, string expected, string actual)
        {
            _failed++;
            Debug.LogError($"  ✗ {message}\n    Expected: {expected}\n    Actual: {actual}");
        }
    }
}
