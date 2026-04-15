using UnityEngine;

/// <summary>
/// D-02: Controls the Lobby → PlayerSync flow in DemoScene.
///
/// Responsibilities:
///   - Show lobby layer on start, hide PlayerSync layer
///   - On match start: hide lobby layer, activate PlayerSync layer (Start() fires → connects)
///   - On lobby disconnect while in game: call StopGame(), return to lobby UI
///   - No business logic — pure flow control
///
/// Inspector wiring required:
///   store            → LobbyStateStore on the LobbyManager GameObject
///   lobbyLayer       → Root GameObject containing all lobby UI
///   playerSyncLayer  → Root GameObject containing PlayerNetworkSync and game world
///   playerNetworkSync → PlayerNetworkSync component in the playerSyncLayer
/// </summary>
public class GameOrchestrator : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private LobbyStateStore store;

    [Header("Scene Layers")]
    [Tooltip("Root GameObject containing all lobby UI — active at start, hidden on match start.")]
    [SerializeField] private GameObject lobbyLayer;
    [Tooltip("Root GameObject containing PlayerNetworkSync and the game world — inactive at start.")]
    [SerializeField] private GameObject playerSyncLayer;

    [Header("PlayerSync")]
    [SerializeField] private PlayerNetworkSync playerNetworkSync;

    private bool _inGame;

    private void Awake()
    {
        // Start with lobby visible, game world hidden
        if (lobbyLayer != null)    lobbyLayer.SetActive(true);
        if (playerSyncLayer != null) playerSyncLayer.SetActive(false);
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

    private void HandleMatchStarted(string sceneName, string hostAddress)
    {
        if (_inGame) return;
        _inGame = true;

        Debug.Log("[GameOrchestrator] Match started — activating PlayerSync layer");
        lobbyLayer.SetActive(false);
        // Activating playerSyncLayer triggers PlayerNetworkSync.Start() on first activation,
        // which creates the socket and connects to the PlayerSync server.
        playerSyncLayer.SetActive(true);
    }

    private void HandleLobbyDisconnected()
    {
        if (!_inGame) return;
        Debug.Log("[GameOrchestrator] Lobby disconnected during game — returning to lobby");
        ReturnToLobby();
    }

    /// <summary>
    /// Returns to the lobby UI and stops PlayerSync.
    /// Callable from a Leave Game button in the game world UI.
    /// </summary>
    public void ReturnToLobby()
    {
        if (!_inGame) return;
        _inGame = false;

        if (playerNetworkSync != null)
            playerNetworkSync.StopGame();

        playerSyncLayer.SetActive(false);
        lobbyLayer.SetActive(true);
        Debug.Log("[GameOrchestrator] Returned to lobby");
    }
}
