using System;

namespace SocketIOUnity.Transport
{
    public interface ITransport
    {
        void Connect(string url);
        void SendText(string message);
        void SendBinary(byte[] data);
        void Close();

        void Dispatch();

        event Action OnOpen;
        event Action OnClose;
        event Action<string> OnTextMessage;
        event Action<byte[]> OnBinaryMessage;
        event Action<string> OnError;
    }
}

