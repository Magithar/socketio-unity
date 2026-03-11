using System;
using Newtonsoft.Json;

/// <summary>
/// LOB-26: Per-player data model inside a RoomState.
/// LOB-27: Deserialized from the players array in room_state JSON.
/// </summary>
[Serializable]
public class LobbyPlayer
{
    [JsonProperty("id")]     public string id;
    [JsonProperty("name")]   public string name;
    [JsonProperty("ready")]  public bool ready;
    /// <summary>"connected" or "disconnected" (grace period active).</summary>
    [JsonProperty("status")] public string status;
}
