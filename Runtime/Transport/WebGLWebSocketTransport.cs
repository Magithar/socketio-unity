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
    internal sealed class WebGLWebSocketTransport : ITransport
    {
        private readonly string _id = Guid.NewGuid().ToString();
        private bool _registered;

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

            // Register this transport's handlers with the bridge
            if (!_registered)
            {
                _registered = true;
                bridge.Register(
                    _id,
                    () => OnOpen?.Invoke(),
                    () => OnClose?.Invoke(),
                    msg => OnTextMessage?.Invoke(msg),
                    data => OnBinaryMessage?.Invoke(data),
                    () => OnError?.Invoke("WebGL socket error")
                );
            }

            SocketIO_WebSocket_Create(_id, url);
        }

        public void SendText(string message)
            => SocketIO_WebSocket_SendText(_id, message);

        public void SendBinary(byte[] data)
        {
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                SocketIO_WebSocket_SendBinary(_id, handle.AddrOfPinnedObject(), data.Length);
            }
            finally
            {
                // Ensure handle is always freed, even if SendBinary throws
                handle.Free();
            }
        }

        public void Close()
        {
            // Unregister handlers to prevent stale callbacks
            if (_registered)
            {
                _registered = false;
                WebGLSocketBridge.Instance?.Unregister(_id);
            }
            SocketIO_WebSocket_Close(_id);
        }

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
