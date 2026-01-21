using System;
using NativeWebSocket;
using SocketIOUnity.Debugging;
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
            SocketIOTrace.Protocol(TraceCategory.Transport, $"WebSocket connecting to {url}");
            _ws = new WebSocket(url);

            _ws.OnOpen += () =>
            {
                SocketIOTrace.Protocol(TraceCategory.Transport, "WebSocket opened");
                OnOpen?.Invoke();
            };
            _ws.OnClose += _ =>
            {
                SocketIOTrace.Protocol(TraceCategory.Transport, "WebSocket closed");
                OnClose?.Invoke();
            };
            _ws.OnError += msg =>
            {
                SocketIOTrace.Error(TraceCategory.Transport, $"WebSocket error: {msg}");
                OnError?.Invoke(msg);
            };
            _ws.OnMessage += data =>
            {
#if SOCKETIO_PROFILER_COUNTERS && UNITY_2020_2_OR_NEWER
                SocketIOProfilerCounters.AddBytesReceived(data.Length);
                SocketIOProfilerCounters.PacketReceived();
                SocketIOThroughputTracker.AddReceived(data.Length);
#endif
                // NativeWebSocket doesn't preserve text vs binary frame type.
                // We use a heuristic: Engine.IO text packets start with ASCII '0'-'6' (0x30-0x36).
                // Binary attachments from Socket.IO are raw binary and won't start with these.
                
                bool isTextPacket = data.Length > 0 && data[0] >= 0x30 && data[0] <= 0x36;
                
                if (isTextPacket)
                {
                    var text = System.Text.Encoding.UTF8.GetString(data);
                    SocketIOTrace.Verbose(TraceCategory.Transport, $"← TEXT {data.Length} bytes");
                    OnTextMessage?.Invoke(text);
                }
                else
                {
                    SocketIOTrace.Verbose(TraceCategory.Transport, $"← BINARY {data.Length} bytes");
                    OnBinaryMessage?.Invoke(data);
                }
            };

            await _ws.Connect();
        }

        public async void SendText(string message)
        {
#if SOCKETIO_PROFILER_COUNTERS && UNITY_2020_2_OR_NEWER
            SocketIOProfilerCounters.AddBytesSent(message.Length);
            SocketIOThroughputTracker.AddSent(message.Length);
#endif
            SocketIOTrace.Verbose(TraceCategory.Transport, $"→ TEXT {message.Length} chars");
            await _ws.SendText(message);
        }

        public async void SendBinary(byte[] data)
        {
#if SOCKETIO_PROFILER_COUNTERS && UNITY_2020_2_OR_NEWER
            SocketIOProfilerCounters.AddBytesSent(data.Length);
            SocketIOThroughputTracker.AddSent(data.Length);
#endif
            SocketIOTrace.Verbose(TraceCategory.Transport, $"→ BINARY {data.Length} bytes");
            await _ws.Send(data);
        }

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
