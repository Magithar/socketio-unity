using System;
using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// LOB-25: Authoritative lobby room snapshot received from the server via room_state.
/// LOB-27: Deserialized directly from JSON using Newtonsoft.Json field mapping.
/// </summary>
[Serializable]
public class RoomState
{
    [JsonProperty("roomId")]  public string roomId;
    [JsonProperty("hostId")]  public string hostId;
    [JsonProperty("version")] public int version;
    [JsonProperty("players")] public List<LobbyPlayer> players;
}
