using System.Collections.Generic;
using Mirror;

/// <summary>
/// Static lookup table bridging Mirror netId to Socket.IO playerId.
///
/// Populated by PlayerIdentityBridge on each player spawn.
/// Consumed by GameEventBridge to target the correct Mirror object
/// when the backend emits player-specific events (e.g. player_killed).
///
/// Call Clear() on ReturnToLobby() and on store.OnDisconnected.
/// </summary>
public static class GameIdentityRegistry
{
    private static readonly Dictionary<uint, string> _netToPlayer = new();
    private static readonly Dictionary<string, uint> _playerToNet = new();

    public static void Register(uint netId, string playerId)
    {
        _netToPlayer[netId]    = playerId;
        _playerToNet[playerId] = netId;
    }

    /// <summary>
    /// Returns the NetworkIdentity for the given Socket.IO playerId, or null if not registered.
    /// Checks NetworkServer.spawned (host/server) first, then NetworkClient.spawned (client).
    /// </summary>
    public static NetworkIdentity GetNetworkObject(string playerId)
    {
        if (!_playerToNet.TryGetValue(playerId, out uint netId)) return null;

        if (NetworkServer.spawned.TryGetValue(netId, out var serverIdentity))
            return serverIdentity;

        if (NetworkClient.spawned.TryGetValue(netId, out var clientIdentity))
            return clientIdentity;

        return null;
    }

    public static void Clear()
    {
        _netToPlayer.Clear();
        _playerToNet.Clear();
    }
}
