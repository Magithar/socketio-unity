using System;
using System.Collections.Generic;
using UnityEngine;

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
    public bool IsConnected { get; private set; }
    public bool IsHost => CurrentRoom != null && CurrentRoom.hostId == LocalPlayerId;

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
    public event Action<string> OnError;
    public event Action<string> OnMatchStarted; // sceneName (may be null)

    // =========================================================
    // Write API — called only by LobbyNetworkManager
    // =========================================================

    public void SetConnected(bool connected)
    {
        IsConnected = connected;
        if (connected)
        {
            OnConnected?.Invoke();
        }
        else
        {
            Reset(); // server session gone — stale room/player state is invalid
            OnDisconnected?.Invoke();
        }
    }

    public void SetLocalPlayerId(string id) => LocalPlayerId = id;

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

    public void FireError(string error) => OnError?.Invoke(error);

    public void FireMatchStarted(string sceneName) => OnMatchStarted?.Invoke(sceneName);

    /// <summary>Clear local state on leave or disconnect.</summary>
    public void Reset()
    {
        CurrentRoom = null;
        LocalPlayerId = null;
        SessionToken = null;
        _lastRoomVersion = 0;
    }

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
