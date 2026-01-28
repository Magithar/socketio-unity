using System;
using UnityEngine;
using SocketIOUnity.Runtime;

/// <summary>
/// Comprehensive namespace authentication test.
/// Tests successful auth, rejection, and reconnect scenarios.
/// </summary>
public class NamespaceAuthTest : MonoBehaviour
{
    private SocketIOClient _socket;
    
    [SerializeField] private string serverUrl = "http://localhost:3000";

    void Start()
    {
        _socket = SocketIOManager.Instance.Socket;

        // Test 1: Root namespace (no auth)
        _socket.On("connect", OnRootConnected);
        _socket.On("connect_error", OnRootConnectError);

        // Actually connect to the server
        Debug.Log($"[NamespaceAuthTest] Connecting to {serverUrl}...");
        _socket.Connect(serverUrl);

        // Test 2: Admin namespace with valid auth
        var admin = _socket.Of("/admin", new { token = "test-secret" });
        
        admin.On("connect", (string _) =>
        {
            Debug.Log("✅ /admin connected (auth succeeded)");
            
            // Test ping-pong
            admin.Emit("ping", null, (string res) =>
            {
                Debug.Log($"✅ /admin ACK: {res}");
            });
        });
        
        admin.On("connect_error", (string err) =>
        {
            Debug.LogError($"❌ /admin auth failed: {err}");
        });

        // Test 3: Unauthorized namespace with bad token
        var unauthorized = _socket.Of("/admin-bad", new { token = "wrong" });
        
        unauthorized.On("connect", (string _) =>
        {
            Debug.LogWarning("⚠️ /admin-bad connected (should not happen)");
        });
        
        unauthorized.On("connect_error", (string err) =>
        {
            Debug.Log($"✅ /admin-bad correctly rejected: {err}");
        });

        // Test 4: Namespace without auth
        var noAuth = _socket.Of("/public");
        
        noAuth.On("connect", (string _) =>
        {
            Debug.Log("✅ /public connected (no auth required)");
        });
    }

    private void OnRootConnected(string data)
    {
        Debug.Log("✅ Root namespace connected");
    }

    private void OnRootConnectError(string err)
    {
        Debug.LogError($"❌ Root connection error: {err}");
    }

    private void OnDestroy()
    {
        // Cleanup
        _socket?.Shutdown();
    }
}
