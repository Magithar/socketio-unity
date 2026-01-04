using System;
using System.Collections.Generic;

namespace SocketIOUnity.Runtime
{
    internal class EventRegistry
    {
        private readonly Dictionary<string, List<Action<string>>> _handlers = new();

        public void On(string eventName, Action<string> handler)
        {
            if (!_handlers.TryGetValue(eventName, out var list))
            {
                list = new List<Action<string>>();
                _handlers[eventName] = list;
            }

            list.Add(handler);
        }

        public void Emit(string eventName, string payload)
        {
            if (!_handlers.TryGetValue(eventName, out var list))
                return;

            foreach (var handler in list)
            {
                handler.Invoke(payload);
            }
        }
    }
}
