using UnityEngine;
using System;
using System.Runtime.InteropServices;

namespace SocketIOUnity.Transport
{
    /// <summary>
    /// Unity MonoBehaviour bridge for WebGL JavaScript WebSocket callbacks.
    /// This must exist in the scene and uses Unity's SendMessage system.
    /// </summary>
    public sealed class WebGLSocketBridge : MonoBehaviour
    {
        public static WebGLSocketBridge Instance { get; private set; }

        public Action OnOpen;
        public Action OnClose;
        public Action<string> OnText;
        public Action<byte[]> OnBinary;
        public Action OnError;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Called from JavaScript when WebSocket opens.
        /// </summary>
        public void JSOnOpen(string _) => OnOpen?.Invoke();

        /// <summary>
        /// Called from JavaScript when WebSocket closes.
        /// </summary>
        public void JSOnClose(string _) => OnClose?.Invoke();

        /// <summary>
        /// Called from JavaScript when WebSocket errors.
        /// </summary>
        public void JSOnError(string _) => OnError?.Invoke();

        /// <summary>
        /// Called from JavaScript when text message received.
        /// </summary>
        public void JSOnText(string msg)
            => OnText?.Invoke(msg);

        /// <summary>
        /// Called from JavaScript when binary message received.
        /// Payload format: "ptr,length"
        /// </summary>
        public void JSOnBinary(string payload)
        {
            var parts = payload.Split(',');
            int ptr = int.Parse(parts[0]);
            int len = int.Parse(parts[1]);

            byte[] data = new byte[len];
            Marshal.Copy((IntPtr)ptr, data, 0, len);
            OnBinary?.Invoke(data);
        }

    }
}
