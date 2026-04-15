using Mirror;
using TMPro;
using UnityEngine;

/// <summary>
/// Attach to Mirror's player prefab.
///
/// Responsibilities:
///   1. Registers the netId ↔ socketPlayerId mapping in GameIdentityRegistry so
///      backend events (e.g. player_killed) can locate the correct Mirror object.
///   2. Syncs the player's display name to all clients and updates the name label.
///
/// Uses FindObjectOfType because Mirror-spawned prefabs cannot hold inspector
/// references to scene objects.
/// </summary>
public class PlayerIdentityBridge : NetworkBehaviour
{
    [SyncVar] public string socketPlayerId;

    [SyncVar(hook = nameof(OnDisplayNameChanged))]
    private string _displayName;

    [SerializeField] private TMP_Text nameLabel;

    public override void OnStartLocalPlayer()
    {
        var store = FindObjectOfType<LobbyStateStore>();
        if (store == null)
        {
            Debug.LogError("[PlayerIdentityBridge] LobbyStateStore not found in scene.");
            return;
        }

        if (string.IsNullOrEmpty(store.LocalPlayerId))
        {
            Debug.LogWarning("[PlayerIdentityBridge] LocalPlayerId is empty — identity not registered.");
            return;
        }

        CmdRegisterIdentity(store.LocalPlayerId);

        // Resolve display name from lobby room; fall back to playerId.
        string displayName = store.LocalPlayerId;
        if (store.CurrentRoom?.players != null)
        {
            foreach (var p in store.CurrentRoom.players)
            {
                if (p.id == store.LocalPlayerId && !string.IsNullOrEmpty(p.name))
                {
                    displayName = p.name;
                    break;
                }
            }
        }
        CmdSetDisplayName(displayName);
    }

    // Called on all clients when _displayName changes, and on late-joining clients.
    private void OnDisplayNameChanged(string _, string newName)
    {
        if (nameLabel != null)
            nameLabel.text = newName;
    }

    [Command]
    private void CmdRegisterIdentity(string playerId)
    {
        socketPlayerId = playerId;
        GameIdentityRegistry.Register(netId, playerId);
        Debug.Log($"[PlayerIdentityBridge] Registered netId={netId} → playerId={playerId}");
    }

    [Command]
    private void CmdSetDisplayName(string displayName)
    {
        _displayName = displayName;
        // Mirror SyncVar hooks don't fire on the server/host — call manually.
        OnDisplayNameChanged(string.Empty, displayName);
    }
}
