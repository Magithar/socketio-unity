using System;
using UnityEngine;
using NativeWebSocket;

namespace SocketIOUnity.Transport
{
    public class WebSocketTransport : ITransport
    {
        private WebSocket _ws;

        public event Action OnOpen;
        public event Action OnClose;
        public event Action<string> OnTextMessage;
#pragma warning disable CS0067 // Event is required by ITransport interface but not used in this implementation
        public event Action<byte[]> OnBinaryMessage;
#pragma warning restore CS0067
        public event Action<string> OnError;

        public async void Connect(string url)
        {
            Debug.Log("[WS] Connecting to: " + url);

            _ws = new WebSocket(url);

            _ws.OnOpen += () =>
            {
                Debug.Log("[WS] OPEN");
                OnOpen?.Invoke();
            };

            _ws.OnClose += code =>
            {
                Debug.Log("[WS] CLOSE: " + code);
                OnClose?.Invoke();
            };

            _ws.OnError += msg =>
            {
                Debug.LogError("[WS] ERROR: " + msg);
                OnError?.Invoke(msg);
            };

            _ws.OnMessage += data =>
            {
                var text = System.Text.Encoding.UTF8.GetString(data);
                Debug.Log("[WS] RX: " + text);
                OnTextMessage?.Invoke(text);
            };

            await _ws.Connect();
        }

        public async void SendText(string message)
        {
            await _ws.SendText(message);
        }

        public async void SendBinary(byte[] data)
        {
            await _ws.Send(data);
        }

        public async void Close()
        {
            await _ws.Close();
        }

        // ✅ REQUIRED FOR NativeWebSocket
        public void Dispatch()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            _ws?.DispatchMessageQueue();
#endif
        }
    }
}
