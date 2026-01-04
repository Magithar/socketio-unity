using UnityEngine;

public class GameSocketTest : MonoBehaviour
{
    void Start()
    {
        var socket = SocketIOManager.Instance.Socket;

        socket.OnConnected += () =>
        {
            Debug.Log("🎮 Game connected");

            socket.Emit("getTime", null, res =>
            {
                Debug.Log("⏱ Server time: " + res);
            });
        };
    }
}
