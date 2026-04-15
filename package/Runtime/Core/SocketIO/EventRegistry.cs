using System;
using System.Collections.Generic;
using SocketIOUnity.Debugging;
using SocketIOUnity.UnityIntegration;
using UnityEngine;

namespace SocketIOUnity.Runtime
{
    internal class EventRegistry
    {
        private readonly Dictionary<string, List<Action<string>>> _handlers = new();
        private readonly Dictionary<string, List<Action<byte[]>>> _binaryHandlers = new();

        /// <summary>
        /// Subscribe a string event handler. Duplicate handlers are ignored (no double-registration).
        /// </summary>
        public void On(string eventName, Action<string> handler)
        {
            if (!_handlers.TryGetValue(eventName, out var list))
            {
                list = new List<Action<string>>();
                _handlers[eventName] = list;
            }

            // Prevent duplicate handler registration
            if (!list.Contains(handler))
                list.Add(handler);
        }

        /// <summary>
        /// Subscribe a binary event handler. Duplicate handlers are ignored (no double-registration).
        /// </summary>
        public void On(string eventName, Action<byte[]> handler)
        {
            if (!_binaryHandlers.TryGetValue(eventName, out var list))
            {
                list = new List<Action<byte[]>>();
                _binaryHandlers[eventName] = list;
            }

            // Prevent duplicate handler registration
            if (!list.Contains(handler))
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
                    // Capture handler in closure to avoid modified closure issues
                    var capturedHandler = handler;
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        try
                        {
                            capturedHandler.Invoke(payload);
                        }
                        catch (Exception ex)
                        {
                            // Log exception but don't prevent other handlers from firing
                            Debug.LogException(ex);
                            SocketIOTrace.Error(TraceCategory.SocketIO,
                                $"Exception in event handler for '{eventName}': {ex.Message}");
                        }
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
                    // Capture handler in closure to avoid modified closure issues
                    var capturedHandler = handler;
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        try
                        {
                            capturedHandler.Invoke(data);
                        }
                        catch (Exception ex)
                        {
                            // Log exception but don't prevent other handlers from firing
                            Debug.LogException(ex);
                            SocketIOTrace.Error(TraceCategory.SocketIO,
                                $"Exception in binary event handler for '{eventName}': {ex.Message}");
                        }
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
