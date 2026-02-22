using System.Collections.Generic;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject remotePlayerPrefab;

    private Dictionary<string, GameObject> remotePlayers =
        new Dictionary<string, GameObject>();

    public void SpawnRemotePlayer(string id)
    {
        if (remotePlayers.ContainsKey(id)) return;

        GameObject go = Instantiate(remotePlayerPrefab);
        go.name = $"Remote_{id}";

        remotePlayers[id] = go;
    }

    public void UpdateRemotePlayer(string id, Vector3 position)
    {
        if (!remotePlayers.TryGetValue(id, out var player))
            return;

        // Use interpolation component if available, otherwise set position directly
        var movement = player.GetComponent<RemotePlayerMovement>();
        if (movement != null)
        {
            movement.SetTargetPosition(position);
        }
        else
        {
            player.transform.position = position;
        }
    }

    public void RemoveRemotePlayer(string id)
    {
        if (!remotePlayers.TryGetValue(id, out var player))
            return;

        Destroy(player);
        remotePlayers.Remove(id);
    }

    public void RemoveAllRemotePlayers()
    {
        foreach (var player in remotePlayers.Values)
        {
            if (player != null)
                Destroy(player);
        }

        remotePlayers.Clear();
    }
}
