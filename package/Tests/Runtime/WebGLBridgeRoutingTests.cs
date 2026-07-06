using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SocketIOUnity.Transport;

namespace SocketIOUnity.Tests
{
    /// <summary>
    /// Routing tests for WebGLSocketBridge.JSOnText (security audit finding #2).
    ///
    /// JSOnText is a plain public method, so its socket-id routing can be exercised in
    /// PlayMode without a browser — even though the JS↔C# marshalling itself still needs a
    /// manual WebGL build to verify. These tests pin the framing contract:
    /// "&lt;socketId&gt;:&lt;message&gt;", split at the first colon, message body colon-safe.
    /// </summary>
    public class WebGLBridgeRoutingTests
    {
        private WebGLSocketBridge _bridge;

        private static readonly Action Noop = () => { };
        private static readonly Action<byte[]> NoopBytes = _ => { };

        [OneTimeSetUp]
        public void CreateBridge()
        {
            var go = new GameObject("WebGLSocketBridge_Test");
            _bridge = go.AddComponent<WebGLSocketBridge>();
        }

        [OneTimeTearDown]
        public void DestroyBridge()
        {
            if (_bridge != null)
                UnityEngine.Object.DestroyImmediate(_bridge.gameObject);

            // Reset the static singleton so this test's bridge cannot leak into other tests.
            var backing = typeof(WebGLSocketBridge).GetField("<Instance>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Static);
            backing?.SetValue(null, null);
        }

        private void RegisterText(string socketId, Action<string> onText)
            => _bridge.Register(socketId, Noop, Noop, onText, NoopBytes, Noop);

        [Test]
        public void JSOnText_RoutesMessageToMatchingSocket()
        {
            string id = Guid.NewGuid().ToString();
            string received = null;
            RegisterText(id, msg => received = msg);

            _bridge.JSOnText($"{id}:hello world");

            Assert.AreEqual("hello world", received);
        }

        [Test]
        public void JSOnText_MessageContainingColons_DeliveredIntact()
        {
            // The regression: a JSON/URL payload with early colons must not be mis-split.
            string id = Guid.NewGuid().ToString();
            string received = null;
            RegisterText(id, msg => received = msg);

            const string json = "{\"status\":\"ok\",\"url\":\"https://example.com:8080/path\",\"t\":1}";
            _bridge.JSOnText($"{id}:{json}");

            Assert.AreEqual(json, received, "Message body colons must be preserved, not treated as a separator");
        }

        [Test]
        public void JSOnText_MultipleSockets_EachReceivesOnlyItsOwnMessage()
        {
            string idA = Guid.NewGuid().ToString();
            string idB = Guid.NewGuid().ToString();
            string a = null, b = null;
            RegisterText(idA, msg => a = msg);
            RegisterText(idB, msg => b = msg);

            _bridge.JSOnText($"{idA}:for-A");
            _bridge.JSOnText($"{idB}:for-B");

            Assert.AreEqual("for-A", a);
            Assert.AreEqual("for-B", b);
        }

        [Test]
        public void JSOnText_UnknownSocketId_DoesNotInvokeAnyHandler()
        {
            string known = Guid.NewGuid().ToString();
            bool invoked = false;
            RegisterText(known, _ => invoked = true);

            // A different (unregistered) id — routing should drop it, not misdeliver.
            _bridge.JSOnText($"{Guid.NewGuid()}:orphan");

            Assert.IsFalse(invoked, "Message for an unknown socket id must not reach another socket's handler");
        }
    }
}
