using UnityEngine;
using SocketIOUnity.Runtime;
using SocketIOUnity.Transport;

public class TestTick : MonoBehaviour
{
    private SocketIOClient _socket;

    void Start()
    {
        var transport = new WebSocketTransport();
        _socket = new SocketIOClient(TransportFactoryHelper.CreateDefault());


    }

    void OnDestroy()
    {
        _socket?.Dispose();
    }
}
