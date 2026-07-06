using NUnit.Framework;
using SocketIOUnity.Debugging;
using SocketIOUnity.SocketProtocol;

namespace SocketIOUnity.Tests
{
    /// <summary>
    /// NUnit conversion of the protocol edge-case suite that previously lived in
    /// Editor/ProtocolEdgeCaseTests.cs as a [MenuItem] ("SocketIO → Run Protocol
    /// Edge Tests") and therefore never ran in CI. These now run in EditMode CI.
    ///
    /// Coverage of <see cref="SocketPacketParser.Parse"/> defensive behavior:
    ///   - Empty / null input returns null (no throw)
    ///   - Packet type range validation (0-6 valid, else null)
    ///   - ACK ID overflow is dropped, not thrown
    ///   - Binary attachment framing (with / without '-' separator)
    ///   - Namespace parsing (root and named, with / without trailing comma)
    ///   - Deferred JSON validation (malformed payload still parses)
    ///   - Disconnect packet parsing across namespaces
    ///
    /// Note: Engine.IO '4' framing is stripped by EngineIOClient before the parser
    /// sees the string, so these inputs are pure Socket.IO packet payloads.
    /// </summary>
    public class ProtocolParserTests
    {
        // The parser calls SocketIOTrace.Error on malformed input. Tracing is off by
        // default (TraceLevel.None), but this is a shared EditMode assembly — force it
        // off so a sibling test cannot leave tracing enabled and turn these graceful
        // null-returns into unexpected Debug.LogError test failures.
        [SetUp]
        public void DisableTracing()
        {
#pragma warning disable CS0618 // Debugging APIs are [Obsolete] but intentional here
            TraceConfig.Level = TraceLevel.None;
#pragma warning restore CS0618
        }

        // --------------------------------------------------
        // Empty / null input
        // --------------------------------------------------

        [Test]
        public void EmptyPacket_ReturnsNull()
        {
            Assert.IsNull(SocketPacketParser.Parse(""));
        }

        [Test]
        public void NullPacket_ReturnsNull()
        {
            Assert.IsNull(SocketPacketParser.Parse(null));
        }

        // --------------------------------------------------
        // Packet type validation
        // --------------------------------------------------

        [Test]
        public void TypeOnlyPacket_ParsesAsEventWithNoPayload()
        {
            var p = SocketPacketParser.Parse("2");
            Assert.IsNotNull(p, "Type-only packet '2' should parse (EVENT with no payload)");
            Assert.AreEqual(SocketPacketType.Event, p.Type);
        }

        [TestCase("9", TestName = "InvalidType_OutOfRangeHigh_9_ReturnsNull")]
        [TestCase("7", TestName = "InvalidType_OutOfRange_7_ReturnsNull")]
        [TestCase("X", TestName = "InvalidType_NonNumeric_X_ReturnsNull")]
        public void InvalidType_ReturnsNull(string raw)
        {
            Assert.IsNull(SocketPacketParser.Parse(raw), $"Invalid type '{raw}' should return null");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void ValidType_Parses(int typeInt)
        {
            var p = SocketPacketParser.Parse(typeInt.ToString());
            Assert.IsNotNull(p, $"Valid type '{typeInt}' should parse");
            Assert.AreEqual(typeInt, (int)p.Type, $"Type should be {typeInt}");
        }

        // --------------------------------------------------
        // ACK ID overflow
        // --------------------------------------------------

        [Test]
        public void HugeAckId_DoesNotCrash_AndDropsAckId()
        {
            // "2" = EVENT, followed by an 18-digit number that overflows Int32, then payload.
            var p = SocketPacketParser.Parse("2999999999999999999[]");
            // Parser must continue without an ACK ID rather than throwing.
            if (p != null)
                Assert.IsNull(p.AckId, "Overflowing ACK ID should be dropped (null)");
        }

        // --------------------------------------------------
        // Binary attachment framing
        // --------------------------------------------------

        [Test]
        public void BinaryEvent_MissingSeparator_DoesNotCrash()
        {
            // "5" = BINARY_EVENT, "1" = attachment count, missing "-" separator.
            var p = SocketPacketParser.Parse("51");
            Assert.IsNotNull(p, "Binary packet without '-' separator should be handled gracefully");
        }

        [Test]
        public void BinaryEvent_WithSeparator_ParsesAttachmentCount()
        {
            var p = SocketPacketParser.Parse("51-[\"event\",{\"_placeholder\":true,\"num\":0}]");
            Assert.IsNotNull(p, "Valid binary packet should parse");
            Assert.AreEqual(SocketPacketType.BinaryEvent, p.Type);
            Assert.AreEqual(1, p.Attachments, "Should report 1 attachment");
        }

        // --------------------------------------------------
        // Namespace parsing
        // --------------------------------------------------

        [TestCase("0/admin,", "/admin", TestName = "Namespace_Named_ParsesAdmin")]
        [TestCase("0", "/", TestName = "Namespace_Root_DefaultsToSlash")]
        public void ConnectPacket_ParsesNamespace(string raw, string expectedNs)
        {
            var p = SocketPacketParser.Parse(raw);
            Assert.IsNotNull(p, $"Packet '{raw}' should parse");
            Assert.AreEqual(SocketPacketType.Connect, p.Type);
            Assert.AreEqual(expectedNs, p.Namespace);
        }

        // --------------------------------------------------
        // Deferred JSON validation
        // --------------------------------------------------

        [Test]
        public void MalformedJsonPayload_StillParses_ValidationIsDeferred()
        {
            // "2" = EVENT. The parser extracts the payload verbatim; JSON validation
            // happens later, so a malformed body must not fail packet parsing.
            var p = SocketPacketParser.Parse("2[\"event\",{invalid}]");
            Assert.IsNotNull(p, "Malformed JSON should still parse (validation is deferred)");
            Assert.IsNotNull(p.JsonPayload, "Payload should be captured");
        }

        // --------------------------------------------------
        // Disconnect packet parsing (P0.4)
        // --------------------------------------------------

        [TestCase("1", "/", TestName = "Disconnect_Root_DefaultsToSlash")]
        [TestCase("1/admin,", "/admin", TestName = "Disconnect_NamedNamespace_WithComma")]
        [TestCase("1/chat", "/chat", TestName = "Disconnect_NamedNamespace_WithoutComma")]
        public void DisconnectPacket_ParsesNamespace(string raw, string expectedNs)
        {
            var p = SocketPacketParser.Parse(raw);
            Assert.IsNotNull(p, $"Disconnect packet '{raw}' should parse");
            Assert.AreEqual(SocketPacketType.Disconnect, p.Type, "Type should be Disconnect (1)");
            Assert.AreEqual(expectedNs, p.Namespace);
        }
    }
}
