using UnityEngine;
using SocketIOUnity.Runtime;
using SocketIOUnity.Transport;

namespace SocketIOUnity.Samples
{
    /// <summary>
    /// WebGL test controller with namespace support.
    /// Fixed version that properly handles namespace connection.
    /// </summary>
    public class WebGLTestController : MonoBehaviour
    {
        [Header("Server Configuration")]
        [SerializeField] private string serverUrl = "http://localhost:3000";
        [SerializeField] private bool useWebglNamespace = true;
        
        private SocketIOClient _socket;
        private NamespaceSocket _webglNs;
        private string _status = "Disconnected";
        private bool _isConnecting;
        
        private void Start()
        {
            #if UNITY_WEBGL && !UNITY_EDITOR
            if (WebGLSocketBridge.Instance == null)
            {
                var go = new GameObject("WebGLSocketBridge");
                go.AddComponent<WebGLSocketBridge>();
                DontDestroyOnLoad(go);
            }
            #endif
        }
        
        public void Connect()
        {
            if (_isConnecting || (_socket != null && _socket.IsConnected))
            {
                Debug.Log("[WebGLTest] Already connected or connecting");
                return;
            }
            
            _isConnecting = true;
            
            if (_socket != null)
            {
                _socket.Dispose();
                _socket = null;
                _webglNs = null;
            }
            
            _socket = new SocketIOClient(TransportFactoryHelper.CreateDefault());
            
            _socket.OnConnected += HandleConnected;
            _socket.OnDisconnected += HandleDisconnected;
            _socket.OnError += HandleError;
            
            // Setup root namespace events (always works)
            _socket.On("hello", (string data) =>
            {
                Debug.Log($"[WebGLTest] 👋 Hello from server: {data}");
            });
            
            _socket.On("pong-test", (string data) =>
            {
                Debug.Log($"[WebGLTest] 🏓 Pong: {data}");
            });
            
            _status = "Connecting...";
            Debug.Log($"[WebGLTest] Connecting to {serverUrl}...");
            _socket.Connect(serverUrl);
        }
        
        private void HandleConnected()
        {
            _isConnecting = false;
            
            if (useWebglNamespace)
            {
                Debug.Log("[WebGLTest] Root connected, now joining /webgl namespace...");
                
                // Get namespace AFTER root is connected
                _webglNs = _socket.Of("/webgl");
                
                _webglNs.OnConnected += () =>
                {
                    _status = "✅ Connected (/webgl)";
                    Debug.Log("[WebGLTest] /webgl namespace connected!");
                };
                
                _webglNs.On("welcome", (string data) =>
                {
                    Debug.Log($"[WebGLTest] 👋 Welcome from /webgl: {data}");
                });
                
                _webglNs.On("pong", (string data) =>
                {
                    Debug.Log($"[WebGLTest] 🏓 Pong from /webgl: {data}");
                });
                
                _webglNs.On("message", (string data) =>
                {
                    Debug.Log($"[WebGLTest] 📨 Message from /webgl: {data}");
                });
            }
            else
            {
                _status = "✅ Connected (root)";
                Debug.Log("[WebGLTest] Connected to root namespace!");
            }
        }
        
        private void HandleDisconnected()
        {
            _status = "❌ Disconnected";
            _isConnecting = false;
            Debug.Log("[WebGLTest] Disconnected");
        }
        
        private void HandleError(string error)
        {
            _status = $"⚠️ Error: {error}";
            _isConnecting = false;
            Debug.LogError($"[WebGLTest] Error: {error}");
        }
        
        public void Disconnect()
        {
            if (_socket != null)
            {
                _socket.Shutdown();
                _socket.Dispose();
                _socket = null;
            }
            _webglNs = null;
            _isConnecting = false;
            _status = "Disconnected";
        }
        
        public void SendPing()
        {
            if (_socket == null || !_socket.IsConnected)
            {
                Debug.LogWarning("[WebGLTest] Not connected!");
                return;
            }
            
            if (useWebglNamespace && _webglNs != null)
            {
                _webglNs.Emit("ping", System.DateTime.UtcNow.ToString("o"));
                Debug.Log("[WebGLTest] 🏓 Ping sent to /webgl");
            }
            else
            {
                _socket.Emit("ping-test", "Hello from WebGL!");
                Debug.Log("[WebGLTest] 🏓 Ping sent to root");
            }
        }
        
        public void SendMessage()
        {
            if (_socket == null || !_socket.IsConnected)
            {
                Debug.LogWarning("[WebGLTest] Not connected!");
                return;
            }
            
            if (useWebglNamespace && _webglNs != null)
            {
                _webglNs.Emit("message", "Hello from Unity WebGL!");
                Debug.Log("[WebGLTest] 📤 Message sent to /webgl");
            }
        }
        
        private void OnDestroy()
        {
            Disconnect();
        }
        
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 200, 320, 200));
            
            GUILayout.Label($"Server: {serverUrl}");
            GUILayout.Label($"Namespace: {(useWebglNamespace ? "/webgl" : "/ (root)")}");
            GUILayout.Label($"Status: {_status}");
            GUILayout.Space(10);
            
            if (GUILayout.Button("Connect"))
                Connect();
            
            if (GUILayout.Button("Disconnect"))
                Disconnect();
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Send Ping"))
                SendPing();
            
            if (GUILayout.Button("Send Message"))
                SendMessage();
            
            GUILayout.EndArea();
        }
    }
}
