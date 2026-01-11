using UnityEngine;
using SocketIOUnity.Runtime;

public class AdminNamespaceTest : MonoBehaviour
{
    void Start()
    {
        var socket = SocketIOManager.Instance.Socket;

        // Request namespace with authentication
        var admin = socket.Of("/admin", new { token = "test-secret" });

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
