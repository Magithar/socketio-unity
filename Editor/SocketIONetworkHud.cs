#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Reflection;

namespace SocketIOUnity.Editor
{
    /// <summary>
    /// Editor-only Network HUD overlay in Scene View.
    /// Toggle via: SocketIO → Toggle Network HUD
    /// Zero build cost - completely stripped from builds.
    /// </summary>
    [InitializeOnLoad]
    internal static class SocketIONetworkHud
    {
        private const string PREF_KEY = "SocketIO_NetworkHUD_Enabled";
        private const string MENU_PATH = "SocketIO/Toggle Network HUD";

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PREF_KEY, false);
            set => EditorPrefs.SetBool(PREF_KEY, value);
        }

        static SocketIONetworkHud()
        {
            // Unsubscribe first to prevent duplicates after domain reload
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= OnEditorUpdate;
            
            // Subscribe
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            // Force repaint during Play Mode so HUD updates live
            if (Enabled && EditorApplication.isPlaying)
            {
                SceneView.RepaintAll();
            }
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (!Enabled)
                return;

            Handles.BeginGUI();

            var rect = new Rect(10, 10, 260, 150);
            GUI.Box(rect, GUIContent.none, GUI.skin.window);
            
            GUILayout.BeginArea(rect);
            GUILayout.Space(5);

            GUILayout.Label("  <b>Socket.IO Network</b>", new GUIStyle(EditorStyles.boldLabel)
            {
                richText = true
            });

#if SOCKETIO_PROFILER_COUNTERS
            GUILayout.Label($"  ⬆ Sent: {Debugging.SocketIOThroughputTracker.SentBytesPerSec:0} B/s");
            GUILayout.Label($"  ⬇ Recv: {Debugging.SocketIOThroughputTracker.ReceivedBytesPerSec:0} B/s");
#else
            GUILayout.Label("  ⬆ Sent: (counters disabled)");
            GUILayout.Label("  ⬇ Recv: (counters disabled)");
#endif

            Runtime.SocketIOClient client = null;

            if (Application.isPlaying)
            {
                // Use reflection to find any MonoBehaviour with a Socket property
                // This works with SocketIOManager from Samples without a hard dependency
                client = FindSocketIOClient();
            }

            if (client != null && client.IsConnected)
            {
                GUILayout.Label($"  RTT: {client.PingRttMs:0.0} ms");
                GUILayout.Label($"  Namespaces: {client.NamespaceCount}");
                GUILayout.Label($"  Pending ACKs: {client.PendingAckCount}");
                GUILayout.Label("  ● Connected", new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = Color.green }
                });
            }
            else
            {
                GUILayout.Label("  RTT: -- ms");
                GUILayout.Label("  Namespaces: --");
                GUILayout.Label("  Pending ACKs: --");
                
                string status = Application.isPlaying ? "○ Disconnected" : "○ Not Playing";
                GUILayout.Label($"  {status}", new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = Color.gray }
                });
            }

            GUILayout.EndArea();
            Handles.EndGUI();
        }

        [MenuItem(MENU_PATH)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            SceneView.RepaintAll();
            Debug.Log($"[SocketIO] Network HUD {(Enabled ? "enabled" : "disabled")}");
        }

        [MenuItem(MENU_PATH, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MENU_PATH, Enabled);
            return true;
        }

        /// <summary>
        /// Finds a SocketIOClient by searching all MonoBehaviours for a "Socket" property.
        /// This allows the HUD to work with SocketIOManager from Samples without a hard dependency.
        /// </summary>
        private static Runtime.SocketIOClient FindSocketIOClient()
        {
            var behaviours = Object.FindObjectsOfType<MonoBehaviour>();
            foreach (var behaviour in behaviours)
            {
                var prop = behaviour.GetType().GetProperty("Socket", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.PropertyType == typeof(Runtime.SocketIOClient))
                {
                    return prop.GetValue(behaviour) as Runtime.SocketIOClient;
                }
            }
            return null;
        }
    }
}
#endif



