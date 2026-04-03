using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Scripting;

/// <summary>
/// LOB-25: Authoritative lobby room snapshot received from the server via room_state.
/// LOB-27: Deserialized directly from JSON using Newtonsoft.Json field mapping.
/// </summary>
[Serializable]
[Preserve]
public class RoomState
{
    [Preserve] [JsonProperty("roomId")]  public string roomId;
    [Preserve] [JsonProperty("hostId")]  public string hostId;
    [Preserve] [JsonProperty("version")] public int version;
    [Preserve] [JsonProperty("players")] public List<LobbyPlayer> players;
}
