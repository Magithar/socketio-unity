using UnityEngine;
using SocketIOUnity.Debugging;

namespace SocketIOUnity.Samples
{
    /// <summary>
    /// Demonstrates how to configure the packet tracing system.
    /// 
    /// This script ONLY handles trace configuration.
    /// Connection management should be done by SocketIOManager.
    /// 
    /// Usage: Attach to any GameObject. Use the runtime UI to toggle trace levels.
    /// </summary>
    public class TraceDemo : MonoBehaviour
    {
        [Header("Trace Settings")]
        [SerializeField] private TraceLevel traceLevel = TraceLevel.Protocol;

        private void Awake()
        {
            // 🔥 Enable tracing BEFORE SocketIOManager connects
            TraceConfig.Level = traceLevel;
            
            Debug.Log($"[TraceDemo] Tracing enabled at level: {traceLevel}");
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 180));
            
            GUILayout.Label($"Trace Level: {TraceConfig.Level}");
            
            if (GUILayout.Button("None (Disabled)"))
                TraceConfig.Level = TraceLevel.None;
                
            if (GUILayout.Button("Errors Only"))
                TraceConfig.Level = TraceLevel.Errors;
                
            if (GUILayout.Button("Protocol"))
                TraceConfig.Level = TraceLevel.Protocol;
                
            if (GUILayout.Button("Verbose"))
                TraceConfig.Level = TraceLevel.Verbose;
            
            GUILayout.EndArea();
        }
    }
}
