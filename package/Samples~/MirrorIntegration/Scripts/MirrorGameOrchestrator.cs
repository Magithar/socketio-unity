using Mirror;
using UnityEngine;

public enum ServerMode
{
    PeerToPeer,
    DedicatedKCP,
    DedicatedWebSocket,
}

/// <summary>
/// Controls the Lobby → Mirror game flow.
///
/// Listens to LobbyStateStore for match lifecycle events and starts/stops
/// Mirror networking at the correct moment. Socket.IO remains connected
/// throughout — GameEventBridge handles in-game backend events on /game.
///
/// Inspector wiring required:
///   store                  → LobbyStateStore
///   lobbyNetworkManager    → LobbyNetworkManager
///   mirrorNetworkManager   → Mirror NetworkManager
///   gameEventBridge        → GameEventBridge
///   lobbyLayer             → Root GameObject of lobby UI
///   gameLayer              → Root GameObject of game world
/// </summary>
public class MirrorGameOrchestrator : MonoBehaviour
{
    [Header("Socket.IO")]
    [SerializeField] private LobbyStateStore store;
    [SerializeField] private LobbyNetworkManager lobbyNetworkManager;

    [Header("Mirror")]
    [SerializeField] private NetworkManager mirrorNetworkManager;
    [SerializeField] private GameEventBridge gameEventBridge;

    [Header("Networking Mode")]
    [SerializeField]
    [Tooltip(
        "PeerToPeer: host calls StartHost(), others connect to hostAddress on the default transport port.\n" +
        "DedicatedKCP: all clients connect to hostAddress:kcpPort from match_started (native builds).\n" +
        "DedicatedWebSocket: all clients connect to hostAddress:wsPort from match_started (WebGL builds)."
    )]
    private ServerMode serverMode = ServerMode.PeerToPeer;

    [Header("Scene Layers")]
    [SerializeField] private GameObject lobbyLayer;
    [SerializeField] private GameObject gameLayer;

    private bool _inGame;

    private void Awake()
    {
        if (lobbyLayer != null) lobbyLayer.SetActive(true);
        if (gameLayer != null) gameLayer.SetActive(false);
    }

    private void OnEnable()
    {
        if (store == null) return;
        store.OnMatchStarted += HandleMatchStarted;
        store.OnDisconnected += HandleLobbyDisconnected;
    }

    private void OnDisable()
    {
        if (store == null) return;
        store.OnMatchStarted -= HandleMatchStarted;
        store.OnDisconnected -= HandleLobbyDisconnected;
    }

    // ---------------------------------------------------------------
    // Match lifecycle
    // ---------------------------------------------------------------

    private void HandleMatchStarted(string sceneName, string hostAddress, int kcpPort, int wsPort)
    {
        if (_inGame) return;
        _inGame = true;

        if (NetworkClient.isConnected || NetworkServer.active)
        {
            Debug.LogWarning("[MirrorOrchestrator] Mirror is already running — ignoring match_started.");
            _inGame = false;
            return;
        }

        lobbyLayer.SetActive(false);
        gameLayer.SetActive(true);

        gameEventBridge?.Subscribe();

        switch (serverMode)
        {
            case ServerMode.PeerToPeer:
                StartPeerToPeer(hostAddress);
                break;

            case ServerMode.DedicatedKCP:
                StartDedicatedClient(hostAddress, kcpPort, "KcpTransport", "Port");
                break;

            case ServerMode.DedicatedWebSocket:
                StartDedicatedClient(hostAddress, wsPort, "SimpleWebTransport", "clientPort");
                break;
        }
    }

    private void StartPeerToPeer(string hostAddress)
    {
        if (store.IsHost)
        {
            Debug.Log("[MirrorOrchestrator] PeerToPeer — starting as host.");
            mirrorNetworkManager.StartHost();
            return;
        }

        if (string.IsNullOrEmpty(hostAddress))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[MirrorOrchestrator] hostAddress is null — falling back to localhost (dev only).");
            hostAddress = "localhost";
#else
            Debug.LogError("[MirrorOrchestrator] hostAddress is null — cannot start client. Returning to lobby.");
            ReturnToLobby();
            return;
#endif
        }

        Debug.Log($"[MirrorOrchestrator] PeerToPeer — connecting to {hostAddress}");
        mirrorNetworkManager.networkAddress = hostAddress;
        mirrorNetworkManager.StartClient();
    }

    // Uses reflection to set the transport port so this sample compiles regardless
    // of which optional Mirror transport packages (kcp2k, SimpleWebTransport) are installed.
    private void StartDedicatedClient(string hostAddress, int port, string transportTypeName, string portFieldName)
    {
        if (string.IsNullOrEmpty(hostAddress))
        {
            Debug.LogError($"[MirrorOrchestrator] {serverMode} — hostAddress is null. Returning to lobby.");
            ReturnToLobby();
            return;
        }

        mirrorNetworkManager.networkAddress = hostAddress;

        if (port > 0)
        {
            var transport = mirrorNetworkManager.transport;
            if (transport != null && transport.GetType().Name == transportTypeName)
            {
                var field = transport.GetType().GetField(portFieldName);
                field?.SetValue(transport, (ushort)port);
            }
            else if (transport != null)
            {
                Debug.LogWarning($"[MirrorOrchestrator] {serverMode} — transport is not {transportTypeName}; using inspector port.");
            }
        }

        Debug.Log($"[MirrorOrchestrator] {serverMode} — connecting to {hostAddress}:{port}");
        mirrorNetworkManager.StartClient();
    }

    private void HandleLobbyDisconnected()
    {
        if (!_inGame) return;
        Debug.Log("[MirrorOrchestrator] Lobby disconnected during game — returning to lobby.");
        GameIdentityRegistry.Clear();
        ReturnToLobby();
    }

    // ---------------------------------------------------------------
    // Teardown — mandatory order (see MIRROR_INTEGRATION.md)
    // ---------------------------------------------------------------

    /// <summary>
    /// Graceful exit: stops Mirror, cleans Socket.IO /game handlers,
    /// and emits leave_room so the server skips its reconnect grace window.
    /// Callable from a Leave Game button.
    /// </summary>
    public void ReturnToLobby()
    {
        if (!_inGame) return;
        _inGame = false;

        // Step 1 — Mirror first (sends peer disconnect before socket closes).
        if (NetworkServer.active)
            mirrorNetworkManager.StopHost();
        else if (NetworkClient.isConnected)
            mirrorNetworkManager.StopClient();

        // Step 2 — Clean up /game namespace handlers.
        gameEventBridge?.Cleanup();

        // Step 3 — Clear netId ↔ playerId mappings.
        GameIdentityRegistry.Clear();

        // Step 4 — Intentional leave (server skips 10-second reconnect grace window).
        lobbyNetworkManager?.LeaveRoom();

        // Step 5 — Restore layers.
        gameLayer.SetActive(false);
        lobbyLayer.SetActive(true);

        Debug.Log("[MirrorOrchestrator] Returned to lobby.");

        // NOTE: socket.Shutdown() is intentionally omitted here.
        // LobbyNetworkManager.OnDestroy() handles it.
    }
}
