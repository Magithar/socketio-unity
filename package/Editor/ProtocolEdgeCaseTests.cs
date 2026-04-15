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
            // Socket.IO type alone (e.g., "2" for EVENT) is valid - just no payload
            // Note: Engine.IO '4' prefix is stripped before reaching parser
            var result = SocketPacketParser.Parse("2");
            AssertNotNull(result, "Type-only packet '2' should parse (EVENT with no payload)");
            AssertEquals((int)result.Type, 2, "Type should be 2 (EVENT)");
        }

        private static void TestInvalidSocketIOType()
        {
            // Note: Engine.IO '4' prefix is stripped before reaching parser
            // Parser receives pure Socket.IO packets like "0", "2[...]", etc.

            // "9" = type 9 which is invalid (valid: 0-6)
            var result = SocketPacketParser.Parse("9");
            AssertNull(result, "Invalid type '9' should return null");

            // "X" = non-numeric type
            result = SocketPacketParser.Parse("X");
            AssertNull(result, "Non-numeric type 'X' should return null");

            // "7" = type 7 which is out of range
            result = SocketPacketParser.Parse("7");
            AssertNull(result, "Out-of-range type '7' should return null");
        }

        private static void TestValidSocketIOTypes()
        {
            // Test all valid Socket.IO types 0-6
            // Note: Engine.IO '4' prefix is stripped before reaching parser
            for (int i = 0; i <= 6; i++)
            {
                var result = SocketPacketParser.Parse($"{i}");
                AssertNotNull(result, $"Valid type '{i}' should parse");
                AssertEquals((int)result.Type, i, $"Type should be {i}");
            }
        }

        private static void TestHugeAckIdOverflow()
        {
            // ACK ID that would overflow int.Parse
            // "2" = EVENT, followed by huge number, then payload
            var result = SocketPacketParser.Parse("2999999999999999999[]");
            // Should not crash - either returns null or parses without ACK
            if (result != null)
                AssertNull(result.AckId, "Overflowing ACK ID should be null");
            else
                Pass("Overflowing ACK ID handled (returned null)");
        }

        private static void TestBinaryMissingSeparator()
        {
            // Binary event without proper separator: "51" instead of "51-"
            // "5" = BINARY_EVENT, "1" = attachment count, missing "-"
            var result = SocketPacketParser.Parse("51");
            // Parser should handle this gracefully (might fail on attachment parse)
            // Key: no crash
            Pass("Binary without separator did not crash");
        }

        private static void TestBinaryWithSeparator()
        {
            // Proper binary event: "51-[\"event\",{\"_placeholder\":true,\"num\":0}]"
            // "5" = BINARY_EVENT, "1" = 1 attachment, "-" = separator
            var result = SocketPacketParser.Parse("51-[\"event\",{\"_placeholder\":true,\"num\":0}]");
            AssertNotNull(result, "Valid binary packet should parse");
            AssertEquals(result.Attachments, 1, "Should have 1 attachment");
        }

        private static void TestNamespaceParking()
        {
            // Valid namespace: "0/admin," = CONNECT to /admin namespace
            var result = SocketPacketParser.Parse("0/admin,");
            AssertNotNull(result, "Namespace packet should parse");
            AssertEquals(result.Namespace, "/admin", "Namespace should be /admin");

            // Default namespace: "0" = CONNECT to root namespace
            result = SocketPacketParser.Parse("0");
            AssertNotNull(result, "Default namespace packet should parse");
            AssertEquals(result.Namespace, "/", "Namespace should be /");
        }

        private static void TestMalformedJson()
        {
            // Malformed JSON payload - parser should still work (JSON parsing happens later)
            // "2" = EVENT
            var result = SocketPacketParser.Parse("2[\"event\",{invalid}]");
            AssertNotNull(result, "Malformed JSON should still parse (validation is deferred)");
            AssertNotNull(result.JsonPayload, "Should have payload");
        }

        // ═══════════════════════════════════════════════════════════
        // P0.4 NAMESPACE DISCONNECT TESTS
        // ═══════════════════════════════════════════════════════════

        private static void TestDisconnectPacketParsing()
        {
            // Note: Engine.IO '4' prefix is stripped before reaching parser
            // "1" = Socket.IO DISCONNECT type

            // Root namespace disconnect: "1"
            var result = SocketPacketParser.Parse("1");
            AssertNotNull(result, "Root disconnect should parse");
            AssertEquals((int)result.Type, 1, "Type should be 1 (Disconnect)");
            AssertEquals(result.Namespace, "/", "Namespace should be /");

            // Custom namespace disconnect: "1/admin,"
            result = SocketPacketParser.Parse("1/admin,");
            AssertNotNull(result, "Namespace disconnect should parse");
            AssertEquals((int)result.Type, 1, "Type should be 1 (Disconnect)");
            AssertEquals(result.Namespace, "/admin", "Namespace should be /admin");

            // Disconnect without trailing comma (edge case)
            result = SocketPacketParser.Parse("1/chat");
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
