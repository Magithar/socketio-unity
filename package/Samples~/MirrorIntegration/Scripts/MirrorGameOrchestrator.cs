using Mirror;
using UnityEngine;

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

    private void HandleMatchStarted(string sceneName, string hostAddress)
    {
        // Guard 1: duplicate event protection (same pattern as GameOrchestrator).
        if (_inGame) return;
        _inGame = true;

        // Guard 2: Mirror state check — catches mismatches even if _inGame is bypassed.
        if (NetworkClient.isConnected || NetworkServer.active)
        {
            Debug.LogWarning("[MirrorOrchestrator] Mirror is already running — ignoring match_started.");
            _inGame = false;
            return;
        }

        lobbyLayer.SetActive(false);
        gameLayer.SetActive(true);

        // Subscribe to /game namespace now — socket is guaranteed initialized.
        gameEventBridge?.Subscribe();

        if (store.IsHost)
        {
            Debug.Log("[MirrorOrchestrator] Starting as host.");
            mirrorNetworkManager.StartHost();
        }
        else
        {
            // Normalize hostAddress — fall back to localhost in editor/dev builds only.
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

            Debug.Log($"[MirrorOrchestrator] Starting as client → {hostAddress}");
            mirrorNetworkManager.networkAddress = hostAddress;
            mirrorNetworkManager.StartClient();
        }
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
        // If returning to a lobby scene that reuses the socket, do not call Shutdown().
    }
}
