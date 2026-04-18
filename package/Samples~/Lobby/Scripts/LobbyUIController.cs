using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// LOB-28: Manages all lobby UI updates in response to LobbyStateStore events.
/// Subscribes to the store for state/events; uses networkManager only for emit actions.
/// Wire all [SerializeField] references in the Inspector, or use LobbySceneBuilder.
/// </summary>
public class LobbyUIController : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WebGL_CopyToClipboard(string text);
    [DllImport("__Internal")]
    private static extern void WebGL_InitPasteListener(string gameObjectName);
#endif

    [Header("Network")]
    [SerializeField] private LobbyNetworkManager networkManager;
    [SerializeField] private LobbyStateStore store;
    /// <summary>Scene to load when match starts. Leave empty to skip scene load.</summary>
    [SerializeField] private string matchSceneName = "GameScene";
    [Tooltip("Override for the Mirror host address. Leave empty to auto-detect this machine's LAN IPv4.")]
    [SerializeField] private string hostAddressOverride = "";

    // ---- Lobby selection panel ----
    [Header("Lobby Selection Panel")]
    [SerializeField] private GameObject lobbySelectionPanel;
    /// <summary>LOB-33: Player name input — persisted to PlayerPrefs.</summary>
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private TMP_InputField joinRoomCodeInput;
    [SerializeField] private Button joinRoomButton;

    // ---- Room panel ----
    [Header("Room Panel")]
    [SerializeField] private GameObject roomPanel;
    /// <summary>LOB-29: Active room code label.</summary>
    [SerializeField] private TextMeshProUGUI roomCodeText;
    [SerializeField] private Button leaveRoomButton;
    /// <summary>LOB-37: Copies current room code to clipboard.</summary>
    [SerializeField] private Button copyRoomCodeButton;

    // ---- Ready / Start ----
    [Header("Ready / Start")]
    [SerializeField] private Button readyButton;
    [SerializeField] private TextMeshProUGUI readyButtonLabel;
    /// <summary>LOB-16 / LOB-32: Host-only start match button.</summary>
    [SerializeField] private Button startMatchButton;

    // ---- Player list ----
    [Header("Player List")]
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerRowPrefab;

    // ---- LOB-38: Connection status indicator ----
    [Header("Connection Status")]
    /// <summary>LOB-38: Always-visible status text (Connected / Connecting… / Disconnected).</summary>
    [SerializeField] private TextMeshProUGUI connectionStatusText;

    // ---- LOB-39: Reconnect overlay ----
    [Header("Reconnect")]
    /// <summary>LOB-39: Panel shown when the connection is lost.</summary>
    [SerializeField] private GameObject reconnectPanel;
    [SerializeField] private Button reconnectButton;

    // ---- Runtime state ----
    private const string PREF_PLAYER_NAME    = "Lobby_PlayerName";
    private const string PREF_LAST_ROOM_ID   = "Lobby_LastRoomId";
    private const string PREF_PLAYER_ID      = "Lobby_PlayerId";
    private const string PREF_SESSION_TOKEN  = "Lobby_SessionToken";
    private const float  REJOIN_TIMEOUT_SEC = 5f;
    private bool _localReady;
    private string _currentRoomId;
    private bool _hadRoomBeforeDisconnect;
    private bool _rejoinPending;
    private bool _joinInFlight;
    private Coroutine _rejoinTimeoutCoroutine;
    private readonly Dictionary<string, GameObject> _playerRows = new();

    // =========================================================
    // Lifecycle
    // =========================================================

    private void Awake()
    {
        createRoomButton.onClick.RemoveAllListeners();
        createRoomButton.onClick.AddListener(OnCreateRoomClicked);
        joinRoomButton.onClick.RemoveAllListeners();
        joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
        leaveRoomButton.onClick.RemoveAllListeners();
        leaveRoomButton.onClick.AddListener(OnLeaveRoomClicked);
        readyButton.onClick.RemoveAllListeners();
        readyButton.onClick.AddListener(OnReadyClicked);
        startMatchButton.onClick.RemoveAllListeners();
        startMatchButton.onClick.AddListener(OnStartMatchClicked);

        if (copyRoomCodeButton != null)
        {
            copyRoomCodeButton.onClick.RemoveAllListeners();
            copyRoomCodeButton.onClick.AddListener(OnCopyRoomCodeClicked);
        }

        if (reconnectButton != null)
        {
            reconnectButton.onClick.RemoveAllListeners();
            reconnectButton.onClick.AddListener(OnReconnectClicked);
        }

        // LOB-33: restore saved player name
        if (playerNameInput != null)
        {
            playerNameInput.text = PlayerPrefs.GetString(PREF_PLAYER_NAME, string.Empty);
            playerNameInput.onEndEdit.AddListener(name => PlayerPrefs.SetString(PREF_PLAYER_NAME, name));
        }

        // Connection status is driven by store.OnConnected/OnDisconnected, which
        // derive from socket.State via LobbyStateStore.SetSocket — no manual init needed.
        ShowLobbySelection();

#if UNITY_WEBGL && !UNITY_EDITOR
        WebGL_InitPasteListener(gameObject.name);
#endif
    }

    /// <summary>Called from browser paste event via jslib SendMessage.</summary>
    public void OnBrowserPaste(string text)
    {
        if (joinRoomCodeInput != null && joinRoomCodeInput.isFocused)
            joinRoomCodeInput.text = text.Trim();
        else if (playerNameInput != null && playerNameInput.isFocused)
            playerNameInput.text = text.Trim();
    }

    private void OnEnable()
    {
        store.OnConnected        += HandleConnected;
        store.OnDisconnected     += HandleDisconnected;
        store.OnRoomStateChanged += HandleRoomStateChanged;
        store.OnPlayerJoined     += HandlePlayerJoined;
        store.OnPlayerLeft       += HandlePlayerLeft;
        store.OnPlayerRemoved    += HandlePlayerRemoved;
        store.OnError            += HandleError;
        store.OnMatchStarted     += HandleMatchStarted;
    }

    private void OnDisable()
    {
        store.OnConnected        -= HandleConnected;
        store.OnDisconnected     -= HandleDisconnected;
        store.OnRoomStateChanged -= HandleRoomStateChanged;
        store.OnPlayerJoined     -= HandlePlayerJoined;
        store.OnPlayerLeft       -= HandlePlayerLeft;
        store.OnPlayerRemoved    -= HandlePlayerRemoved;
        store.OnError            -= HandleError;
        store.OnMatchStarted     -= HandleMatchStarted;
    }

    /// <summary>
    /// Continuously sync Start Match button visibility with store.IsHost.
    /// Needed because the ACK (which sets LocalPlayerId) and room_state
    /// can arrive in any order across frames.
    /// </summary>
    private void Update()
    {
        if (startMatchButton != null && roomPanel.activeSelf)
            startMatchButton.gameObject.SetActive(store.IsHost);
    }

    // =========================================================
    // Network event handlers
    // =========================================================

    private void HandleConnected()
    {
        // LOB-38
        SetStatus("Connected", Color.green);
        SetLobbyButtonsInteractable(true);

        // Guard: skip if a rejoin is already in flight (network layer may fire OnConnected twice)
        if (_rejoinPending) return;

        // Auto-restore session if we were in a room before the disconnect
        if (_hadRoomBeforeDisconnect)
        {
            _hadRoomBeforeDisconnect = false;
            string savedPlayerId    = PlayerPrefs.GetString(PREF_PLAYER_ID, string.Empty);
            string savedRoom        = PlayerPrefs.GetString(PREF_LAST_ROOM_ID, string.Empty);
            string savedToken       = PlayerPrefs.GetString(PREF_SESSION_TOKEN, string.Empty);
            if (!string.IsNullOrEmpty(savedPlayerId) && !string.IsNullOrEmpty(savedRoom) && !string.IsNullOrEmpty(savedToken))
            {
                // Session restore: server keeps the player slot during grace period
                SetStatus("Rejoining...", Color.yellow);
                _rejoinPending = true;
                _rejoinTimeoutCoroutine = StartCoroutine(RejoinTimeout());
                networkManager.ReconnectSession(savedPlayerId, savedRoom, savedToken);
                return; // overlay hidden by HandleRoomStateChanged or RejoinTimeout
            }
            // Fallback: no saved playerId (first run after upgrade) — rejoin as new player
            string savedName = PlayerPrefs.GetString(PREF_PLAYER_NAME, string.Empty);
            if (!string.IsNullOrEmpty(savedRoom) && !string.IsNullOrEmpty(savedName))
            {
                SetStatus("Rejoining...", Color.yellow);
                _rejoinPending = true;
                _rejoinTimeoutCoroutine = StartCoroutine(RejoinTimeout());
                networkManager.JoinRoom(savedRoom, savedName);
                return;
            }
        }

        // LOB-39: no auto-rejoin — hide reconnect overlay
        if (reconnectPanel != null) reconnectPanel.SetActive(false);
    }

    private void HandleDisconnected()
    {
        // LOB-38
        SetStatus("Disconnected", Color.red);
        SetLobbyButtonsInteractable(false);

        // Cancel any in-flight rejoin — a second disconnect makes it invalid
        CancelRejoinTimeout("disconnect");
        _joinInFlight = false;

        // Remember if we were in a room — used to trigger auto-rejoin on reconnect
        _hadRoomBeforeDisconnect = !string.IsNullOrEmpty(_currentRoomId);

        // LOB-39: show reconnect overlay — keep room UI visible underneath
        if (reconnectPanel != null) reconnectPanel.SetActive(true);
    }

    private void HandleRoomStateChanged(RoomState state)
    {
        _currentRoomId = state.roomId;
        PlayerPrefs.SetString(PREF_LAST_ROOM_ID, state.roomId);
        if (!string.IsNullOrEmpty(store.LocalPlayerId))
            PlayerPrefs.SetString(PREF_PLAYER_ID, store.LocalPlayerId);
        if (!string.IsNullOrEmpty(store.SessionToken))
            PlayerPrefs.SetString(PREF_SESSION_TOKEN, store.SessionToken);

        // Cancel rejoin timeout — room_state confirms success
        if (_rejoinPending)
        {
            CancelRejoinTimeout("room_state");
            if (reconnectPanel != null) reconnectPanel.SetActive(false);
        }
        _joinInFlight = false;

        // Auto-show room panel on successful auto-rejoin
        if (!roomPanel.activeSelf) ShowRoomPanel();

        // LOB-29
        if (roomCodeText != null)
            roomCodeText.text = $"Room: {state.roomId}";

        // LOB-30 / 31
        RefreshPlayerRows(state);

        // LOB-32
        if (startMatchButton != null)
            startMatchButton.gameObject.SetActive(store.IsHost);

        RefreshReadyButtonLabel();
    }

    private void HandlePlayerJoined(LobbyPlayer player)
    {
        if (_playerRows.ContainsKey(player.id)) return;
        var row = Instantiate(playerRowPrefab, playerListContent);
        _playerRows[player.id] = row;
        // hostId may not be updated yet; HandleRoomStateChanged will correct it
        UpdatePlayerRow(row, player, store.CurrentRoom?.hostId);
        Debug.Log($"[Lobby] Player joined: {player.name} ({player.id})");
    }

    private void HandlePlayerLeft(string playerId)
    {
        if (_playerRows.TryGetValue(playerId, out var row))
        {
            Destroy(row);
            _playerRows.Remove(playerId);
        }
        Debug.Log($"[Lobby] Player left: {playerId}");
    }

    private void HandlePlayerRemoved(string playerId, string playerName, string reason)
    {
        string message = reason == "reconnect_timeout"
            ? $"{playerName} was removed (reconnect timed out)"
            : $"{playerName} left the room";
        Debug.Log($"[Lobby] {message}");
        StartCoroutine(FlashStatusMessage(message, Color.yellow));
    }

    private void HandleError(SocketIOUnity.Runtime.SocketError error)
    {
        _joinInFlight = false;
        Debug.LogWarning($"[Lobby] Error: {error}");

        // If a rejoin was in progress, fail fast instead of waiting for the 5s timeout
        if (_rejoinPending)
        {
            CancelRejoinTimeout("error");
            PlayerPrefs.DeleteKey(PREF_LAST_ROOM_ID);
            ClearPlayerList();
            _currentRoomId = null;
            ShowLobbySelection();
            if (reconnectPanel != null) reconnectPanel.SetActive(false);
            SetStatus("Previous room no longer available", Color.yellow);
            return;
        }

        SetStatus($"Error: {error}", Color.red);
    }

    // =========================================================
    // Player list rendering (LOB-30 / 31)
    // =========================================================

    // Source-of-truth reconciliation: removes stale rows, creates missing rows,
    // updates all existing rows. Events (HandlePlayerJoined/Left) are fast-path
    // only; this ensures correctness regardless of event delivery edge cases.
    private void RefreshPlayerRows(RoomState state)
    {
        // Remove rows not present in the authoritative state
        var stateIds = new HashSet<string>();
        foreach (var p in state.players) stateIds.Add(p.id);

        var toRemove = new List<string>();
        foreach (var id in _playerRows.Keys)
            if (!stateIds.Contains(id)) toRemove.Add(id);
        foreach (var id in toRemove)
        {
            Destroy(_playerRows[id]);
            _playerRows.Remove(id);
        }

        // Upsert all players from state
        foreach (var player in state.players)
        {
            if (!_playerRows.TryGetValue(player.id, out var row))
            {
                row = Instantiate(playerRowPrefab, playerListContent);
                _playerRows[player.id] = row;
            }
            UpdatePlayerRow(row, player, state.hostId);
        }
    }

    private static readonly Color RowBgColor = new Color(0.18f, 0.22f, 0.30f, 1f);

    private void UpdatePlayerRow(GameObject row, LobbyPlayer player, string hostId)
    {
        var bg = row.GetComponent<Image>();
        if (bg != null) bg.color = RowBgColor;

        bool disconnected = player.status == "disconnected";

        var nameText = row.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null)
        {
            string baseName = player.id == hostId ? $"{player.name} [Host]" : player.name;
            nameText.text = disconnected ? $"{baseName} (Reconnecting...)" : baseName;
            nameText.color = disconnected ? Color.gray : Color.white;
        }

        var readyIcon = row.transform.Find("ReadyIcon")?.GetComponent<Image>();
        if (readyIcon != null)
            readyIcon.color = disconnected ? Color.yellow : (player.ready ? Color.green : Color.gray);
    }

    // =========================================================
    // Button handlers
    // =========================================================

    private void OnCreateRoomClicked()
    {
        if (_joinInFlight) return;
        string name = PlayerName();
        if (string.IsNullOrEmpty(name)) return;
        _joinInFlight = true;
        networkManager.CreateRoom(name);
        ShowRoomPanel();
    }

    private void OnJoinRoomClicked()
    {
        if (_joinInFlight) return;
        string code = joinRoomCodeInput.text.Trim();
        string name = PlayerName();
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name)) return;
        _joinInFlight = true;
        networkManager.JoinRoom(code, name);
        ShowRoomPanel();
    }

    private void OnLeaveRoomClicked()
    {
        networkManager.LeaveRoom();
        PlayerPrefs.DeleteKey(PREF_LAST_ROOM_ID);
        PlayerPrefs.DeleteKey(PREF_PLAYER_ID);
        PlayerPrefs.DeleteKey(PREF_SESSION_TOKEN);
        _hadRoomBeforeDisconnect = false;
        _joinInFlight = false;
        store.Reset(); // clear room state + version counter so next room_state v1 isn't ignored
        ClearPlayerList();
        _currentRoomId = null;
        ShowLobbySelection();
    }

    private void OnReadyClicked()
    {
        _localReady = !_localReady;
        networkManager.SetReady(_localReady);
        RefreshReadyButtonLabel();
    }

    private void OnStartMatchClicked()
    {
        if (!store.IsHost) return;
        string addr = !string.IsNullOrWhiteSpace(hostAddressOverride)
            ? hostAddressOverride.Trim()
            : GetLocalHostAddress();
        Debug.Log($"[Lobby] Starting match with hostAddress={addr}");
        networkManager.StartMatch(matchSceneName, addr);
    }

    /// <summary>
    /// Returns the address WebGL clients should dial to reach this host's Mirror server.
    /// Same-machine testing uses "localhost"; LAN testing needs the first non-loopback IPv4.
    /// </summary>
    private static string GetLocalHostAddress()
    {
        try
        {
            foreach (var ip in System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()))
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                    !System.Net.IPAddress.IsLoopback(ip))
                    return ip.ToString();
            }
        }
        catch { /* fall through to localhost */ }
        return "localhost";
    }

    private void HandleMatchStarted(string sceneName, string hostAddress, int kcpPort, int wsPort)
    {
        Debug.Log($"[Lobby] Match started → loading scene: {sceneName}");
        if (!string.IsNullOrEmpty(sceneName))
            SceneManager.LoadScene(sceneName);
    }

    /// <summary>LOB-37: Copy active room code to system clipboard.</summary>
    private void OnCopyRoomCodeClicked()
    {
        if (string.IsNullOrEmpty(_currentRoomId)) return;
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGL_CopyToClipboard(_currentRoomId);
#else
        GUIUtility.systemCopyBuffer = _currentRoomId;
#endif
        Debug.Log($"[Lobby] Room code copied: {_currentRoomId}");

        // Brief label feedback
        if (copyRoomCodeButton != null)
        {
            var lbl = copyRoomCodeButton.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) StartCoroutine(FlashCopyLabel(lbl));
        }
    }

    /// <summary>LOB-39: Manual reconnect triggered by the player.</summary>
    private void OnReconnectClicked()
    {
        SetStatus("Reconnecting...", Color.yellow);
        if (reconnectButton != null) reconnectButton.interactable = false;
        networkManager.Reconnect();
    }

    // =========================================================
    // Helpers
    // =========================================================

    private void ShowLobbySelection()
    {
        lobbySelectionPanel.SetActive(true);
        roomPanel.SetActive(false);
        _localReady = false;
    }

    private void ShowRoomPanel()
    {
        lobbySelectionPanel.SetActive(false);
        roomPanel.SetActive(true);
        if (startMatchButton != null) startMatchButton.gameObject.SetActive(false);
        RefreshReadyButtonLabel();
    }

    private void RefreshReadyButtonLabel()
    {
        if (readyButtonLabel != null)
            readyButtonLabel.text = _localReady ? "Not Ready" : "Ready";
    }

    private void ClearPlayerList()
    {
        foreach (var row in _playerRows.Values)
            Destroy(row);
        _playerRows.Clear();
    }

    /// <summary>LOB-38: Update the persistent status indicator.</summary>
    private void SetStatus(string message, Color color)
    {
        if (connectionStatusText == null) return;
        connectionStatusText.text  = message;
        connectionStatusText.color = color;
    }

    private void SetLobbyButtonsInteractable(bool interactable)
    {
        if (createRoomButton != null) createRoomButton.interactable = interactable;
        if (joinRoomButton   != null) joinRoomButton.interactable   = interactable;
        if (reconnectButton  != null) reconnectButton.interactable  = true; // always clickable
    }

    private string PlayerName()
    {
        string n = playerNameInput != null ? playerNameInput.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(n))
            Debug.LogWarning("[Lobby] Player name is required.");
        return n;
    }

    private void CancelRejoinTimeout(string reason = null)
    {
        if (_rejoinTimeoutCoroutine != null)
        {
            StopCoroutine(_rejoinTimeoutCoroutine);
            _rejoinTimeoutCoroutine = null;
            var reasonPart = string.IsNullOrEmpty(reason) ? "" : $" ({reason})";
            Debug.Log($"[Lobby] Rejoin timeout cancelled{reasonPart} room={_currentRoomId}");
        }
        _rejoinPending = false;
    }

    private System.Collections.IEnumerator RejoinTimeout()
    {
        yield return new WaitForSeconds(REJOIN_TIMEOUT_SEC);
        if (!_rejoinPending) yield break;

        // Can't use CancelRejoinTimeout() here — we are the coroutine
        _rejoinPending = false;
        _rejoinTimeoutCoroutine = null;

        // Room is gone — clear saved ID so it won't be retried
        PlayerPrefs.DeleteKey(PREF_LAST_ROOM_ID);

        // Fall back to lobby selection
        ClearPlayerList();
        _currentRoomId = null;
        ShowLobbySelection();
        if (reconnectPanel != null) reconnectPanel.SetActive(false);

        Debug.LogWarning("[Lobby] Auto-rejoin timed out — room no longer available.");
        SetStatus("Room no longer available", Color.yellow);
    }

    private System.Collections.IEnumerator FlashCopyLabel(TextMeshProUGUI lbl)
    {
        string original = lbl.text;
        lbl.text = "Copied!";
        yield return new WaitForSeconds(1.5f);
        lbl.text = original;
    }

    /// <summary>Briefly shows a message in the status bar then restores the previous text.</summary>
    private System.Collections.IEnumerator FlashStatusMessage(string message, Color color)
    {
        if (connectionStatusText == null) yield break;
        string prevText  = connectionStatusText.text;
        Color  prevColor = connectionStatusText.color;
        SetStatus(message, color);
        yield return new WaitForSeconds(3f);
        // Only restore if the status hasn't changed to something more important
        if (connectionStatusText.text == message)
            SetStatus(prevText, prevColor);
    }
}
