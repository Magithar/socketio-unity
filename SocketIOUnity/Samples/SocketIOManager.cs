using UnityEngine;
using SocketIOUnity.Runtime;
using SocketIOUnity.Transport;

public sealed class SocketIOManager : MonoBehaviour
{
    public static SocketIOManager Instance { get; private set; }

    public SocketIOClient Socket { get; private set; }

    private const string Url = "ws://localhost:3000";

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // ✅ CRITICAL FIX: pass transport FACTORY, not instance
        Socket = new SocketIOClient(() => new WebSocketTransport());
        Socket.Connect(Url);
    }

    private void OnDestroy()
    {
        Socket?.Disconnect();
    }
}
