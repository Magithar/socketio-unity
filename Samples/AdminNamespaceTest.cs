using UnityEngine;
using SocketIOUnity.Runtime;

public class AdminNamespaceTest : MonoBehaviour
{
    void Start()
    {
        var socket = SocketIOManager.Instance.Socket;

        // Request namespace AFTER root is connected
        var admin = socket.Of("/admin");

        admin.OnConnected += () =>
        {
            Debug.Log("🔐 /admin connected");

            admin.Emit("ping", null, res =>
            {
                Debug.Log("🔐 /admin ACK: " + res);
            });
        };
    }
}
