using UnityEngine;
using SocketIOUnity.Runtime;
using SocketIOUnity.Transport;

public sealed class SocketIOManager : MonoBehaviour
{
    public static SocketIOManager Instance { get; private set; }

    public SocketIOClient Socket { get; private set; }

    private SocketIODiagnosticsOverlay _overlay;

    /// <summary>
    /// Toggle the runtime diagnostics overlay on/off.
    /// Creates the overlay on first use (adds it as a child "DiagnosticsPanel" GameObject).
    /// </summary>
    public bool ShowDiagnostics
    {
        get => _overlay != null && _overlay.gameObject.activeSelf;
        set
        {
            if (value && _overlay == null)
            {
                var go = new GameObject("DiagnosticsPanel");
                go.transform.SetParent(transform);
                _overlay = go.AddComponent<SocketIODiagnosticsOverlay>();
            }
            if (_overlay != null)
                _overlay.gameObject.SetActive(value);
        }
    }

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

        // Note: Connection is initiated by scene scripts (e.g., BasicChatUI.Start())
        // Do NOT call Socket.Connect() here to avoid double-connect race condition
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Socket?.Shutdown();
            Instance = null;
        }
    }

    private void OnApplicationQuit()
    {
        Socket?.Shutdown();
    }
}
