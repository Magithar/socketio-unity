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

        // ✅ CRITICAL: Pass transport FACTORY, not instance
        // Platform-specific: WebGL uses .jslib bridge, others use WebSocketSharp
        Socket = new SocketIOClient(TransportFactoryHelper.CreateDefault());

        Socket.Connect(Url);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Socket?.Shutdown();
        }
    }

    private void OnApplicationQuit()
    {
        Socket?.Shutdown();
    }
}
