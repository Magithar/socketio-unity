using UnityEngine;

namespace SocketIOUnity.Debugging
{
    /// <summary>
    /// Default trace sink that outputs to Unity's Debug.Log console.
    /// </summary>
    internal sealed class UnityDebugTraceSink : ITraceSink
    {
        public void Emit(in TraceEvent evt)
        {
            var prefix = $"[SocketIO:{evt.Category}]";

            switch (evt.Level)
            {
                case TraceLevel.Errors:
                    Debug.LogError($"{prefix} {evt.Message}");
                    break;

                case TraceLevel.Protocol:
                case TraceLevel.Verbose:
                    Debug.Log($"{prefix} {evt.Message}");
                    break;
            }
        }
    }
}
