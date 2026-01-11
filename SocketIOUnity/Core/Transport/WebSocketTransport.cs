using System;
using NativeWebSocket;
using SocketIOUnity.Transport;

namespace SocketIOUnity.Transport
{
    public sealed class WebSocketTransport : ITransport
    {
        private WebSocket _ws;

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
            _ws.OnMessage += data => OnTextMessage?.Invoke(System.Text.Encoding.UTF8.GetString(data));

            await _ws.Connect();
        }

        public async void SendText(string message) => await _ws.SendText(message);
        public async void SendBinary(byte[] data) => await _ws.Send(data);
        public async void Close() => await _ws.Close();

        public void Dispatch()
        {
#if !UNITY_WEBGL
            _ws?.DispatchMessageQueue();
#endif
        }
    }
}
