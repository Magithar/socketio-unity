using System;
using NativeWebSocket;
using SocketIOUnity.Debugging;
using SocketIOUnity.Runtime;
using SocketIOUnity.Transport;
using UnityEngine;

namespace SocketIOUnity.Transport
{
    internal sealed class WebSocketTransport : ITransport
    {
        private WebSocket _ws;
        private bool _eventsBound;

        public WebSocketTransport()
        {
        }

        public event Action OnOpen;
        public event Action OnClose;
        public event Action<string> OnTextMessage;
        public event Action<byte[]> OnBinaryMessage;
        public event Action<SocketError> OnError;

        public async void Connect(string url)
        {
            try
            {
                SocketIOTrace.Protocol(TraceCategory.Transport, $"WebSocket connecting to {url}");
                _ws = new WebSocket(url);
                _eventsBound = false; // reset so new _ws instance gets its events bound

                // Bind events to this WebSocket instance
                if (!_eventsBound)
                {
                    _eventsBound = true;

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
                        OnError?.Invoke(new SocketError(ErrorType.Transport, msg));
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
                }

                await _ws.Connect();
            }
            catch (Exception ex)
            {
                SocketIOTrace.Error(TraceCategory.Transport, $"WebSocket connect error: {ex.Message}");
                OnError?.Invoke(new SocketError(ErrorType.Transport, $"WebSocket connect failed: {ex.Message}"));
            }
        }

        public async void SendText(string message)
        {
            try
            {
#if SOCKETIO_PROFILER_COUNTERS && UNITY_2020_2_OR_NEWER
                SocketIOProfilerCounters.AddBytesSent(message.Length);
                SocketIOThroughputTracker.AddSent(message.Length);
#endif
                SocketIOTrace.Verbose(TraceCategory.Transport, $"→ TEXT {message.Length} chars");
                await _ws.SendText(message);
            }
            catch (Exception ex)
            {
                SocketIOTrace.Error(TraceCategory.Transport, $"WebSocket send text error: {ex.Message}");
                OnError?.Invoke(new SocketError(ErrorType.Transport, $"WebSocket send failed: {ex.Message}"));
            }
        }

        public async void SendBinary(byte[] data)
        {
            try
            {
#if SOCKETIO_PROFILER_COUNTERS && UNITY_2020_2_OR_NEWER
                SocketIOProfilerCounters.AddBytesSent(data.Length);
                SocketIOThroughputTracker.AddSent(data.Length);
#endif
                SocketIOTrace.Verbose(TraceCategory.Transport, $"→ BINARY {data.Length} bytes");
                await _ws.Send(data);
            }
            catch (Exception ex)
            {
                SocketIOTrace.Error(TraceCategory.Transport, $"WebSocket send binary error: {ex.Message}");
                OnError?.Invoke(new SocketError(ErrorType.Transport, $"WebSocket send failed: {ex.Message}"));
            }
        }

        public async void Close()
        {
            if (_ws == null)
                return;

            try
            {
                // Reset event binding flag for potential reconnect with new WebSocket
                _eventsBound = false;

                // Do NOT nullify events - this would break reconnection and lose subscribers
                // The WebSocket instance will be disposed and won't fire further events

                await _ws.Close();
            }
            catch (Exception ex)
            {
                SocketIOTrace.Error(TraceCategory.Transport, $"WebSocket close error: {ex.Message}");
            }
        }

        public void Dispatch()
        {
#if !UNITY_WEBGL
            _ws?.DispatchMessageQueue();
#endif
        }
    }
}
