using System.Net.Sockets;

namespace Chess_Challenge_Server;

public class Player
{
    public string SessionId;
    public NetworkStream Stream;
    public TcpClient Client;
    
    public void SendMessage(ISerializableMessage message) => message.SerializeIntoStream(Stream);

    public T ReadMessage<T>() where T : ISerializableMessage, new()
    {
        var ret = new T();
            
        ret.ReadFromStream(Stream);
        return ret;
    }
}