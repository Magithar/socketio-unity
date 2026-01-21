using System;
using NativeWebSocket;
using SocketIOUnity.Transport;
using UnityEngine;

namespace SocketIOUnity.Transport
{
    public sealed class WebSocketTransport : ITransport
    {
        private WebSocket _ws;

        public WebSocketTransport()
        {
        }

        public event Action OnOpen;
        public event Action OnClose;
        public event Action<string> OnTextMessage;
        public event Action<byte[]> OnBinaryMessage;
        public event Action<string> OnError;

        public async void Connect(string url)
        {
            _ws = new WebSocket(url);

            _ws.OnOpen += () => OnOpen?.Invoke();
            _ws.OnClose += _ => OnClose?.Invoke();
            _ws.OnError += msg => OnError?.Invoke(msg);
            _ws.OnMessage += data =>
            {
                // NativeWebSocket doesn't preserve text vs binary frame type.
                // We use a heuristic: Engine.IO text packets start with ASCII '0'-'6' (0x30-0x36).
                // Binary attachments from Socket.IO are raw binary and won't start with these.
                
                bool isTextPacket = data.Length > 0 && data[0] >= 0x30 && data[0] <= 0x36;
                
                if (isTextPacket)
                    OnTextMessage?.Invoke(System.Text.Encoding.UTF8.GetString(data));
                else
                    OnBinaryMessage?.Invoke(data);
            };

            await _ws.Connect();
        }

        public async void SendText(string message) => await _ws.SendText(message);
        public async void SendBinary(byte[] data) => await _ws.Send(data);

        public async void Close()
        {
            if (_ws == null)
                return;

            // Note: We can't unsubscribe from WebSocket events directly here,
            // but we can null our own transport's events so callbacks become no-ops
            OnOpen = null;
            OnClose = null;
            OnError = null;
            OnTextMessage = null;
            OnBinaryMessage = null;

            await _ws.Close();
        }

        public void Dispatch()
        {
#if !UNITY_WEBGL
            _ws?.DispatchMessageQueue();
#endif
        }
    }
}
