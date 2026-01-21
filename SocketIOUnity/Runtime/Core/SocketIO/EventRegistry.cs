using System;
using System.Collections.Generic;
using SocketIOUnity.Debugging;
using SocketIOUnity.UnityIntegration;

namespace SocketIOUnity.Runtime
{
    internal class EventRegistry
    {
        private readonly Dictionary<string, List<Action<string>>> _handlers = new();
        private readonly Dictionary<string, List<Action<byte[]>>> _binaryHandlers = new();

        public void On(string eventName, Action<string> handler)
        {
            if (!_handlers.TryGetValue(eventName, out var list))
            {
                list = new List<Action<string>>();
                _handlers[eventName] = list;
            }

            list.Add(handler);
        }

        public void On(string eventName, Action<byte[]> handler)
        {
            if (!_binaryHandlers.TryGetValue(eventName, out var list))
            {
                list = new List<Action<byte[]>>();
                _binaryHandlers[eventName] = list;
            }

            list.Add(handler);
        }

        public void Emit(string eventName, string payload)
        {
            using (SocketIOProfiler.SocketIO_EventDispatch.Auto())
            {
                if (!_handlers.TryGetValue(eventName, out var list))
                    return;

                foreach (var handler in list)
                {
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        handler.Invoke(payload);
                    });
                }
            }
        }

        public void EmitBinary(string eventName, byte[] data)
        {
            using (SocketIOProfiler.SocketIO_EventDispatch.Auto())
            {
                if (!_binaryHandlers.TryGetValue(eventName, out var list))
                    return;

                foreach (var handler in list)
                {
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        handler.Invoke(data);
                    });
                }
            }
        }

        /// <summary>
        /// Unsubscribe a string event handler.
        /// </summary>
        public void Off(string eventName, Action<string> handler)
        {
            if (_handlers.TryGetValue(eventName, out var list))
                list.Remove(handler);
        }

        /// <summary>
        /// Unsubscribe a binary event handler.
        /// </summary>
        public void Off(string eventName, Action<byte[]> handler)
        {
            if (_binaryHandlers.TryGetValue(eventName, out var list))
                list.Remove(handler);
        }
    }
}
