using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SocketIOUnity.Transport
{
    /// <summary>
    /// Unity MonoBehaviour bridge for WebGL JavaScript WebSocket callbacks.
    /// Routes events to specific transports by socket ID.
    /// </summary>
    public sealed class WebGLSocketBridge : MonoBehaviour
    {
        public static WebGLSocketBridge Instance { get; private set; }

        // Per-socket event handlers keyed by socket ID
        private readonly Dictionary<string, Action> _openHandlers = new Dictionary<string, Action>();
        private readonly Dictionary<string, Action> _closeHandlers = new Dictionary<string, Action>();
        private readonly Dictionary<string, Action<string>> _textHandlers = new Dictionary<string, Action<string>>();
        private readonly Dictionary<string, Action<byte[]>> _binaryHandlers = new Dictionary<string, Action<byte[]>>();
        private readonly Dictionary<string, Action> _errorHandlers = new Dictionary<string, Action>();

        // Track current socket for text/binary messages (JS sends id separately)
        private string _lastActiveSocketId;

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
        /// Register handlers for a specific socket ID.
        /// </summary>
        public void Register(string socketId, Action onOpen, Action onClose, Action<string> onText, Action<byte[]> onBinary, Action onError)
        {
            _openHandlers[socketId] = onOpen;
            _closeHandlers[socketId] = onClose;
            _textHandlers[socketId] = onText;
            _binaryHandlers[socketId] = onBinary;
            _errorHandlers[socketId] = onError;
        }

        /// <summary>
        /// Unregister handlers for a specific socket ID.
        /// </summary>
        public void Unregister(string socketId)
        {
            _openHandlers.Remove(socketId);
            _closeHandlers.Remove(socketId);
            _textHandlers.Remove(socketId);
            _binaryHandlers.Remove(socketId);
            _errorHandlers.Remove(socketId);
        }

        /// <summary>
        /// Called from JavaScript when WebSocket opens.
        /// </summary>
        public void JSOnOpen(string socketId)
        {
            _lastActiveSocketId = socketId;
            if (_openHandlers.TryGetValue(socketId, out var handler))
                handler?.Invoke();
        }

        /// <summary>
        /// Called from JavaScript when WebSocket closes.
        /// </summary>
        public void JSOnClose(string socketId)
        {
            if (_closeHandlers.TryGetValue(socketId, out var handler))
                handler?.Invoke();
        }

        /// <summary>
        /// Called from JavaScript when WebSocket errors.
        /// </summary>
        public void JSOnError(string socketId)
        {
            if (_errorHandlers.TryGetValue(socketId, out var handler))
                handler?.Invoke();
        }

        /// <summary>
        /// Called from JavaScript when a text message is received.
        ///
        /// Framing contract (see SocketIOWebGL.jslib): the payload is always
        /// "&lt;socketId&gt;:&lt;message&gt;", where socketId is a GUID. Because a GUID never
        /// contains ':', the FIRST ':' is unambiguously the separator — the message body may
        /// contain any number of colons (JSON, URLs, timestamps) without affecting routing.
        ///
        /// This replaces an earlier "first colon, index &lt; 40" heuristic with a silent
        /// last-active-socket fallback, which scanned the message body and could mis-split or
        /// silently drop payloads.
        /// </summary>
        public void JSOnText(string payload)
        {
            if (string.IsNullOrEmpty(payload))
                return;

            int sep = payload.IndexOf(':');
            if (sep <= 0)
            {
                Debug.LogWarning("[WebGLSocketBridge] JSOnText payload missing socket-id prefix — dropped");
                return;
            }

            string socketId = payload.Substring(0, sep);
            string message = payload.Substring(sep + 1);

            if (_textHandlers.TryGetValue(socketId, out var handler))
                handler?.Invoke(message);
            else
                Debug.LogWarning($"[WebGLSocketBridge] JSOnText for unknown socket id '{socketId}' — dropped");
        }

        /// <summary>
        /// Called from JavaScript when binary message received.
        /// Payload format: "socketId,ptr,length" or "ptr,length"
        /// </summary>
        public void JSOnBinary(string payload)
        {
            try
            {
                if (string.IsNullOrEmpty(payload))
                {
                    Debug.LogError("[WebGLSocketBridge] JSOnBinary received empty payload");
                    return;
                }

                var parts = payload.Split(',');
                string socketId;
                int ptrIndex, lenIndex;

                if (parts.Length >= 3)
                {
                    // New format: "socketId,ptr,length"
                    socketId = parts[0];
                    ptrIndex = 1;
                    lenIndex = 2;
                }
                else if (parts.Length >= 2)
                {
                    // Old format: "ptr,length" - use last active socket
                    socketId = _lastActiveSocketId;
                    ptrIndex = 0;
                    lenIndex = 1;
                }
                else
                {
                    Debug.LogError($"[WebGLSocketBridge] JSOnBinary malformed payload: {payload}");
                    return;
                }

                if (!int.TryParse(parts[ptrIndex], out int ptr) || !int.TryParse(parts[lenIndex], out int len))
                {
                    Debug.LogError($"[WebGLSocketBridge] JSOnBinary failed to parse ptr/len: {payload}");
                    return;
                }

                if (len < 0 || ptr == 0)
                {
                    Debug.LogError($"[WebGLSocketBridge] JSOnBinary invalid ptr={ptr} or len={len}");
                    return;
                }

                byte[] data = new byte[len];
                Marshal.Copy((IntPtr)ptr, data, 0, len);

                if (!string.IsNullOrEmpty(socketId) && _binaryHandlers.TryGetValue(socketId, out var handler))
                    handler?.Invoke(data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WebGLSocketBridge] JSOnBinary exception: {ex.Message}");
            }
        }
    }
}
