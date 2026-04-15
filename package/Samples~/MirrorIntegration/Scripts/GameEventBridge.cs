using System;
using Newtonsoft.Json;
using SocketIOUnity.Runtime;
using UnityEngine;

/// <summary>
/// Subscribes to the /game Socket.IO namespace during a match and routes
/// server-authoritative events to the correct Mirror-spawned objects.
///
/// Attach to a persistent manager in the game scene.
/// Wire lobbyNetworkManager via inspector — do NOT use a singleton.
///
/// Always cache handler references and call Off() in Cleanup()/OnDestroy().
/// Failing to unsubscribe causes callbacks to fire against destroyed MonoBehaviours.
/// </summary>
public class GameEventBridge : MonoBehaviour
{
    [SerializeField] private LobbyNetworkManager lobbyNetworkManager;

    private NamespaceSocket _gameNs;
    private bool _destroyed;

    // Cached references required for Off() — lambdas cannot be unsubscribed.
    private Action<string> _onScoreUpdate;
    private Action<string> _onPlayerKilled;

    /// <summary>
    /// Subscribe to /game namespace events. Call this from MirrorGameOrchestrator
    /// after match_started fires — the socket is guaranteed to be initialized by then.
    /// Do NOT call from Start(): LobbyNetworkManager.Start() may not have run yet.
    /// </summary>
    public void Subscribe()
    {
        if (lobbyNetworkManager == null)
        {
            Debug.LogError("[GameEventBridge] lobbyNetworkManager is not assigned.");
            return;
        }

        if (lobbyNetworkManager.Socket == null)
        {
            Debug.LogError("[GameEventBridge] Socket is not initialized — Subscribe() called too early.");
            return;
        }

        if (_gameNs != null) return; // already subscribed

        // Of() returns a NamespaceSocket on the existing WebSocket — no new connection.
        _gameNs = lobbyNetworkManager.Socket.Of("/game");

        _onScoreUpdate = json =>
        {
            if (_destroyed) return;
            try
            {
                var data = JsonConvert.DeserializeAnonymousType(json, new { playerId = "", score = 0 });
                Debug.Log($"[GameEventBridge] score_update: player={data.playerId} score={data.score}");
                // TODO: HUDManager.Instance.UpdateScore(data.playerId, data.score);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameEventBridge] Failed to parse score_update: {ex.Message}");
            }
        };

        _onPlayerKilled = json =>
        {
            if (_destroyed) return;
            try
            {
                var data   = JsonConvert.DeserializeAnonymousType(json, new { victimId = "" });
                var victim = GameIdentityRegistry.GetNetworkObject(data.victimId);
                if (victim == null)
                {
                    Debug.LogWarning($"[GameEventBridge] player_killed: no Mirror object for victimId={data.victimId}");
                    return;
                }
                Debug.Log($"[GameEventBridge] player_killed: victimId={data.victimId}");
                // TODO: victim.GetComponent<PlayerHealth>()?.Die();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameEventBridge] Failed to parse player_killed: {ex.Message}");
            }
        };

        _gameNs.On("score_update",  _onScoreUpdate);
        _gameNs.On("player_killed", _onPlayerKilled);
        Debug.Log("[GameEventBridge] Subscribed to /game namespace.");
    }

    /// <summary>
    /// Unsubscribe all /game handlers. Call before returning to lobby.
    /// Safe to call multiple times.
    /// </summary>
    public void Cleanup()
    {
        if (_gameNs == null) return;
        _gameNs.Off("score_update",  _onScoreUpdate);
        _gameNs.Off("player_killed", _onPlayerKilled);
        _gameNs = null;
    }

    private void OnDestroy()
    {
        _destroyed = true;
        Cleanup();
    }
}
