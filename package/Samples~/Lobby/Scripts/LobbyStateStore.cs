using System;
using System.Collections.Generic;
using UnityEngine;
using SocketIOUnity.Runtime;

/// <summary>
/// Owns all authoritative lobby state and fires semantic events to consumers
/// (UI, Audio, Analytics, Chat, etc.).
///
/// LobbyNetworkManager feeds raw data in via the Apply/Set/Fire methods.
/// Everything else subscribes to events here — no coupling to the socket layer.
/// </summary>
public class LobbyStateStore : MonoBehaviour
{
    // ---- Public state ----
    public RoomState CurrentRoom { get; private set; }
    public string LocalPlayerId { get; private set; }
    /// <summary>Secret token issued by the server at join time. Required to reconnect.</summary>
    public string SessionToken { get; private set; }
    /// <summary>Derived from socket.State — no shadow bool.</summary>
    public bool IsConnected => _socket?.State == ConnectionState.Connected;
    public bool IsHost => CurrentRoom != null && CurrentRoom.hostId == LocalPlayerId;

    private SocketIOClient _socket;
    private bool _destroyed;
    private int _lastRoomVersion;

    // ---- Events ----
    public event Action OnConnected;
    public event Action OnDisconnected;
    /// <summary>Full authoritative room snapshot. Always reflects server state.</summary>
    public event Action<RoomState> OnRoomStateChanged;
    /// <summary>Fired for every new player detected in a room_state diff.</summary>
    public event Action<LobbyPlayer> OnPlayerJoined;
    /// <summary>Fired for every player absent from the latest room_state diff.</summary>
    public event Action<string> OnPlayerLeft;   // playerId
    /// <summary>Fired when the server explicitly removes a player with a reason.</summary>
    public event Action<string, string, string> OnPlayerRemoved; // playerId, name, reason
    public event Action<SocketIOUnity.Runtime.SocketError> OnError;
    /// <summary>
    /// Fired when the backend confirms a match has started.
    /// sceneName: the scene to load (may be null).
    /// hostAddress: the Mirror server address to connect to (null for non-Mirror flows — always null-check before use).
    /// </summary>
    public event Action<string, string, int, int> OnMatchStarted; // sceneName, hostAddress, kcpPort, wsPort

    // =========================================================
    // Write API — called only by LobbyNetworkManager
    // =========================================================

    /// <summary>
    /// Inject socket references so this store can derive state instead of caching it.
    /// Call once in LobbyNetworkManager.Start() after creating the root socket and lobby namespace.
    /// OnConnected fires from lobbyNamespace.OnConnected (namespace-level, not root-level).
    /// OnDisconnected fires from socket.OnStateChanged → Disconnected (root-level).
    /// </summary>
    public void SetSocket(SocketIOClient socket, NamespaceSocket lobbyNamespace)
    {
        _socket = socket;
        socket.OnStateChanged += state =>
        {
            if (_destroyed) return;
            if (state == ConnectionState.Disconnected)
            {
                Reset(); // server session gone — stale room/player state is invalid
                OnDisconnected?.Invoke();
            }
        };
        lobbyNamespace.OnConnected += () =>
        {
            if (_destroyed) return;
            OnConnected?.Invoke();
        };
    }

    public void SetLocalPlayerId(string id)
    {
        LocalPlayerId = id;
        // Re-fire room state so consumers re-evaluate IsHost.
        // ACK and room_state can arrive in any order on the same frame,
        // so always re-fire if we have a room — even if just set.
        if (CurrentRoom != null)
            OnRoomStateChanged?.Invoke(CurrentRoom);
    }

    public void SetSessionToken(string token) => SessionToken = token;

    public void ApplyRoomState(RoomState newState)
    {
        if (newState == null) return;
        if (newState.version > 0 && newState.version <= _lastRoomVersion)
        {
            Debug.Log($"[LobbyStore] Ignoring duplicate room_state v{newState.version} (last={_lastRoomVersion})");
            return;
        }
        _lastRoomVersion = newState.version;
        DiffAndFirePlayerEvents(CurrentRoom, newState);
        CurrentRoom = newState;
        OnRoomStateChanged?.Invoke(CurrentRoom);
    }

    public void FirePlayerRemoved(string playerId, string name, string reason) =>
        OnPlayerRemoved?.Invoke(playerId, name, reason);

    public void FireError(SocketIOUnity.Runtime.SocketError error) => OnError?.Invoke(error);

    public void FireMatchStarted(string sceneName, string hostAddress, int kcpPort = 0, int wsPort = 0) =>
        OnMatchStarted?.Invoke(sceneName, hostAddress, kcpPort, wsPort);

    /// <summary>Clear local state on leave or disconnect.</summary>
    public void Reset()
    {
        CurrentRoom = null;
        LocalPlayerId = null;
        SessionToken = null;
        _lastRoomVersion = 0;
    }

    private void OnDestroy() => _destroyed = true;

    // =========================================================
    // Private: player list diffing
    // =========================================================

    private void DiffAndFirePlayerEvents(RoomState old, RoomState next)
    {
        if (next == null) return;

        var nextPlayers = next.players ?? new List<LobbyPlayer>();

        if (old?.players == null)
        {
            // Initial snapshot — every player is "joining"
            foreach (var p in nextPlayers)
                OnPlayerJoined?.Invoke(p);
            return;
        }

        var oldIds = new HashSet<string>();
        foreach (var p in old.players) oldIds.Add(p.id);

        var nextIds = new HashSet<string>();
        foreach (var p in nextPlayers) nextIds.Add(p.id);

        foreach (var p in nextPlayers)
            if (!oldIds.Contains(p.id)) OnPlayerJoined?.Invoke(p);

        foreach (var p in old.players)
            if (!nextIds.Contains(p.id)) OnPlayerLeft?.Invoke(p.id);
    }
}
