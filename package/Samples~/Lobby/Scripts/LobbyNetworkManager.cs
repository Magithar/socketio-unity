using System;
using UnityEngine;
using SocketIOUnity.Runtime;
using SocketIOUnity.Transport;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Thin transport layer: manages the /lobby namespace socket connection
/// and translates raw socket events into LobbyStateStore calls.
///
/// Does NOT own any state or fire any events directly.
/// All state lives in LobbyStateStore; consumers subscribe there.
/// </summary>
public class LobbyNetworkManager : MonoBehaviour
{
    [Header("Server")]
    [SerializeField] private string serverUrl = "http://localhost:3001";

    [Header("State")]
    [SerializeField] private LobbyStateStore store;

    private SocketIOClient _root;
    private NamespaceSocket _lobby;
    private bool _destroyed;

    private void Start()
    {
        _root = new SocketIOClient(TransportFactoryHelper.CreateDefault());
        _root.ReconnectConfig = new ReconnectConfig { autoReconnect = false };

        _root.OnError += err =>
        {
            if (_destroyed) return;
            Debug.LogError($"❌ Lobby socket error: {err}");
            store.FireError(err);
        };

        SetupNamespace();           // create _lobby before injecting into store
        store.SetSocket(_root, _lobby);
        _root.Connect(serverUrl);
    }

    private void ConnectToLobby()
    {
        _root.Connect(serverUrl);
    }

    /// <summary>Socket reference exposed for consumers that need to read socket.State directly.</summary>
    public SocketIOClient Socket => _root;

    private void SetupNamespace()
    {
        _lobby = _root.Of("/lobby");

        _lobby.OnConnected += () =>
        {
            if (_destroyed) return;
            Debug.Log("✅ Connected to /lobby");
        };

        _lobby.OnDisconnected += () =>
        {
            if (_destroyed) return;
            Debug.LogWarning("❌ Disconnected from /lobby");
        };

        // Server emits player_identity before ACK and room_state to guarantee
        // the client knows its playerId before IsHost is evaluated.
        _lobby.On("player_identity", (string json) =>
        {
            if (_destroyed) return;
            try 
            {
                var obj = JObject.Parse(json);
                store.SetLocalPlayerId(obj.Value<string>("playerId"));
                store.SetSessionToken(obj.Value<string>("sessionToken"));
                Debug.Log($"🆔 Identity received: {store.LocalPlayerId}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Lobby] Failed to parse player_identity: {ex.Message}\nRaw JSON: {json}");
            }
        });

        _lobby.On("match_started", (string json) =>
        {
            if (_destroyed) return;
            var obj = JObject.Parse(json);
            string sceneName   = obj["sceneName"]?.ToString();
            string hostAddress = obj["hostAddress"]?.ToString();

            // Normalize: JS backends may serialize missing/undefined fields as "undefined" or "null".
            hostAddress = hostAddress?.Trim();
            if (string.IsNullOrWhiteSpace(hostAddress) || hostAddress == "undefined" || hostAddress == "null")
                hostAddress = null;

            Debug.Log($"🎮 Match started! scene={sceneName ?? "(none)"} hostAddress={hostAddress ?? "(none)"}");
            store.FireMatchStarted(sceneName, hostAddress);
        });

        _lobby.On("room_state", (string json) =>
        {
            if (_destroyed) return;
            try 
            {
                var state = JsonConvert.DeserializeObject<RoomState>(json);
                Debug.Log($"[Lobby] room_state parsed. ID={state?.roomId}, P-Count={state?.players?.Count}");
                store.ApplyRoomState(state);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Lobby] Failed to parse room_state: {ex.Message}\nRaw JSON: {json}");
            }
        });

        _lobby.On("player_removed", json =>
        {
            if (_destroyed) return;
            var obj       = JObject.Parse(json);
            string pid    = obj["playerId"]?.ToString();
            string name   = obj["name"]?.ToString();
            string reason = obj["reason"]?.ToString();
            Debug.Log($"[Lobby] player_removed: {name} ({pid}) reason={reason}");
            store.FirePlayerRemoved(pid, name, reason);
        });
    }

    // =========================================================
    // Emit API — called by LobbyUIController (and future systems)
    // =========================================================

    public void CreateRoom(string playerName)
    {
        if (!store.IsConnected) { Debug.LogWarning("Cannot create room: not connected"); return; }

        _lobby.Emit("create_room", new { name = playerName }, ack =>
        {
            var result = ParseAck(ack);
            if (result != null && result.Value<bool>("ok"))
            {
                store.SetLocalPlayerId(result.Value<string>("playerId"));
                store.SetSessionToken(result.Value<string>("sessionToken"));
                Debug.Log($"🏠 Room created: {result.Value<string>("roomId")} (me: {store.LocalPlayerId})");
            }
            else
            {
                Debug.LogWarning($"create_room failed: {ack}");
            }
        });
    }

    public void JoinRoom(string roomId, string playerName)
    {
        if (!store.IsConnected) { Debug.LogWarning("Cannot join room: not connected"); return; }

        _lobby.Emit("join_room", new { roomId = roomId.ToUpper(), name = playerName }, ack =>
        {
            var result = ParseAck(ack);
            if (result != null && result.Value<bool>("ok"))
            {
                store.SetLocalPlayerId(result.Value<string>("playerId"));
                store.SetSessionToken(result.Value<string>("sessionToken"));
                Debug.Log($"🚪 Joined room: {result.Value<string>("roomId")} (me: {store.LocalPlayerId})");
            }
            else
            {
                string error = result?.Value<string>("error") ?? ack;
                Debug.LogWarning($"join_room failed: {error}");
                store.FireError(new SocketError(ErrorType.Auth, error));
            }
        });
    }

    public void LeaveRoom()
    {
        if (!store.IsConnected) return;

        _lobby.Emit("leave_room", new { }, ack =>
        {
            store.Reset();
            Debug.Log("🚶 Left room");
        });
    }

    public void SetReady(bool ready)
    {
        if (!store.IsConnected) return;
        _lobby.Emit("player_ready", new { ready });
    }

    public void StartMatch(string sceneName = null, string hostAddress = null)
    {
        if (!store.IsConnected || !store.IsHost) return;
        _lobby.Emit("start_match", new { sceneName, hostAddress });
    }

    /// <summary>
    /// Restore a previous session using the credentials issued at join time.
    /// The sessionToken prevents playerId spoofing on reconnect.
    /// </summary>
    public void ReconnectSession(string playerId, string roomId, string sessionToken)
    {
        if (!store.IsConnected) { Debug.LogWarning("Cannot restore session: not connected"); return; }

        _lobby.Emit("reconnect_player", new { playerId, roomId, sessionToken }, ack =>
        {
            var result = ParseAck(ack);
            if (result != null && result.Value<bool>("ok"))
            {
                store.SetLocalPlayerId(result.Value<string>("playerId"));
                Debug.Log($"♻️ Session restored: room={result.Value<string>("roomId")} player={result.Value<string>("playerId")}");
            }
            else
            {
                string error = result?.Value<string>("error") ?? ack;
                Debug.LogWarning($"reconnect_player failed: {error}");
                store.FireError(new SocketError(ErrorType.Auth, error));
            }
        });
    }

    public void Reconnect()
    {
        if (store.IsConnected) return;
        Debug.Log("🔄 Reconnecting to lobby...");
        ConnectToLobby();
    }

    private void OnDestroy()
    {
        _destroyed = true;
        _root?.Shutdown();
    }

    private static JObject ParseAck(string ack)
    {
        try
        {
            // Socket.IO wraps ACK data in a JSON array: ["{...}"]
            // Try parsing as array first, extracting the first element.
            var token = Newtonsoft.Json.Linq.JToken.Parse(ack);
            if (token is Newtonsoft.Json.Linq.JArray arr && arr.Count > 0)
            {
                var first = arr[0];
                if (first.Type == Newtonsoft.Json.Linq.JTokenType.String)
                    return JObject.Parse(first.ToString());
                if (first.Type == Newtonsoft.Json.Linq.JTokenType.Object)
                    return (JObject)first;
            }
            return token as JObject ?? JObject.Parse(ack);
        }
        catch { return null; }
    }
}
