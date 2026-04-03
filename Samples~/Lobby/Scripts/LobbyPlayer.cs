using System;
using Newtonsoft.Json;
using UnityEngine.Scripting;

/// <summary>
/// LOB-26: Per-player data model inside a RoomState.
/// LOB-27: Deserialized from the players array in room_state JSON.
/// </summary>
[Serializable]
[Preserve]
public class LobbyPlayer
{
    [Preserve] [JsonProperty("id")]     public string id;
    [Preserve] [JsonProperty("name")]   public string name;
    [Preserve] [JsonProperty("ready")]  public bool ready;
    /// <summary>"connected" or "disconnected" (grace period active).</summary>
    [Preserve] [JsonProperty("status")] public string status;
}
