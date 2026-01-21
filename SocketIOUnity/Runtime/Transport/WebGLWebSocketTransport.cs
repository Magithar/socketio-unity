#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SocketIOUnity.Transport
{
    /// <summary>
    /// WebGL WebSocket transport implementation.
    /// Uses JavaScript bridge (.jslib) for actual WebSocket management.
    /// Only compiles in WebGL builds (not in Unity Editor).
    /// </summary>
    public sealed class WebGLWebSocketTransport : ITransport
    {
        private readonly string _id = Guid.NewGuid().ToString();

        public event Action OnOpen;
        public event Action OnClose;
        public event Action<string> OnTextMessage;
        public event Action<byte[]> OnBinaryMessage;
        public event Action<string> OnError;

        [DllImport("__Internal")]
        private static extern void SocketIO_WebSocket_Create(string id, string url);

        [DllImport("__Internal")]
        private static extern void SocketIO_WebSocket_SendText(string id, string msg);

        [DllImport("__Internal")]
        private static extern void SocketIO_WebSocket_SendBinary(string id, IntPtr data, int len);

        [DllImport("__Internal")]
        private static extern void SocketIO_WebSocket_Close(string id);

        public void Connect(string url)
        {
            var bridge = EnsureBridge();
            bridge.OnOpen += () => OnOpen?.Invoke();
            bridge.OnClose += () => OnClose?.Invoke();
            bridge.OnText += msg => OnTextMessage?.Invoke(msg);
            bridge.OnBinary += data => OnBinaryMessage?.Invoke(data);
            bridge.OnError += () => OnError?.Invoke("WebGL socket error");

            SocketIO_WebSocket_Create(_id, url);
        }

        public void SendText(string message)
            => SocketIO_WebSocket_SendText(_id, message);

        public void SendBinary(byte[] data)
        {
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            SocketIO_WebSocket_SendBinary(_id, handle.AddrOfPinnedObject(), data.Length);
            handle.Free();
        }

        public void Close()
            => SocketIO_WebSocket_Close(_id);

        /// <summary>
        /// WebGL uses browser event loop, no manual dispatch needed.
        /// </summary>
        public void Dispatch()
        {
            // No-op for WebGL - browser handles message dispatch
        }

        private static WebGLSocketBridge EnsureBridge()
        {
            if (WebGLSocketBridge.Instance != null)
                return WebGLSocketBridge.Instance;

            var go = new GameObject("WebGLSocketBridge");
            return go.AddComponent<WebGLSocketBridge>();
        }
    }
}
#endif
