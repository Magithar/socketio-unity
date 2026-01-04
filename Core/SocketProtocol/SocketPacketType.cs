namespace SocketIOUnity.SocketProtocol
{
    public enum SocketPacketType
    {
        Connect = 0,
        Disconnect = 1,
        Event = 2,
        Ack = 3,
        Error = 4,
        BinaryEvent = 5,
        BinaryAck = 6
    }
}

