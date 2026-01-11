using UnityEngine;
using SocketIOUnity.Runtime;
using SocketIOUnity.Transport;

/// <summary>
/// Test script to verify binary event handling.
/// Attach this to a GameObject and enter Play mode.
/// Run test-binary-server.js before testing.
/// </summary>
public class BinaryEventTest : MonoBehaviour
{
    private SocketIOClient _socket;

    void Start()
    {
        _socket = new SocketIOClient(TransportFactoryHelper.CreateDefault());


        _socket.OnConnected += () => Debug.Log("✅ Connected to server!");
        _socket.OnDisconnected += () => Debug.Log("❌ Disconnected from server");
        _socket.OnError += error => Debug.LogError($"⚠️ Error: {error}");

        // Listen for binary events using byte[] handlers
        _socket.On("file", (byte[] data) =>
        {
            Debug.Log($"📥 Received 'file' binary event: {data.Length} bytes");
        });

        _socket.On("multi", (byte[] data) =>
        {
            Debug.Log($"📥 Received 'multi' binary event: {data.Length} bytes");
        });

        _socket.Connect("http://localhost:3000");
    }

    void OnDestroy()
    {
        _socket?.Dispose();
    }
}
