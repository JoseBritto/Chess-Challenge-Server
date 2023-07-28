using System.Text;

namespace Chess_Challenge_Server;


public interface ISerializableMessage
{
    public byte[] SerializeAsArray()
    {
        using var stream = new MemoryStream();
        SerializeIntoStream(stream);
        return stream.ToArray();
    }
    
    public void SerializeIntoStream(Stream stream);

    public void DeserializeFromArray(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        
        ReadFromStream(stream);
    }
    
    public void ReadFromStream(Stream stream);
}

public struct ServerHelloMsg : ISerializableMessage
{
    public string ProtocolVersion;
    public string ServerVersion;
    public string SessionId;
    
    public void SerializeIntoStream(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(ProtocolVersion);
        writer.Write(ServerVersion);
        writer.Write(SessionId);
    }
    
    public void ReadFromStream(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8,  true);
        
        ProtocolVersion = reader.ReadString();
        ServerVersion = reader.ReadString();
        SessionId = reader.ReadString();
    }
}

public struct ClientHelloMsg : ISerializableMessage
{
    public string ProtocolVersion;
    public string ClientVersion; 
            
    // RoomId: No weird characters (maybe)
    public string RoomId; // Create or connect to this room  
    
    
    public void SerializeIntoStream(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(ProtocolVersion);
        writer.Write(ClientVersion);
        writer.Write(RoomId);
    }
    
    public void ReadFromStream(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8,  true);
        
        ProtocolVersion = reader.ReadString();
        ClientVersion = reader.ReadString();
        RoomId = reader.ReadString();
    }
    
}


// Sent from server to client
public struct RoomInfo : ISerializableMessage
{
    public string RoomId;
    public bool StartsOffAsWhite; // True if this client will be on the white side for the first match
    
    public void SerializeIntoStream(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(RoomId);
        writer.Write(StartsOffAsWhite);
    }
    
    public void ReadFromStream(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8,  true);
        
        RoomId = reader.ReadString();
        StartsOffAsWhite = reader.ReadBoolean();

    }
}

// Sent by client to server after hello
public struct ClientPreferences : ISerializableMessage
{
    public int PreferredTimeForEachPlayer; // The final say is by the server but each client can request (in seconds)
    public bool PreferWhite;
    public void SerializeIntoStream(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(PreferredTimeForEachPlayer);
        writer.Write(PreferWhite);
    }

    public void ReadFromStream(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8,  true);
        
        PreferredTimeForEachPlayer = reader.ReadInt32();
        PreferWhite = reader.ReadBoolean();

    }
}

// Sent by server to client after receiving preferences
public struct GameSettings : ISerializableMessage
{
    public int TimeForEachPlayer; // in seconds
    public bool IsWhite;
    public void SerializeIntoStream(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(TimeForEachPlayer);
        writer.Write(IsWhite);
    }

    public void ReadFromStream(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8,  true);
        
        TimeForEachPlayer = reader.ReadInt32();
        IsWhite = reader.ReadBoolean();
    }
}



//Signal from client to server to say we are ready
public struct IsReady : ISerializableMessage
{
    public bool isReady;
    public void SerializeIntoStream(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(isReady);
    }

    public void ReadFromStream(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8,  true);
        isReady = reader.ReadBoolean();
    }
}

//From server to clients
public struct GameStart : ISerializableMessage
{
    public long Timestamp; // Use this timestamp to calculate client move timings


    public void SerializeIntoStream(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(Timestamp);
    }

    public void ReadFromStream(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8,  true);
        Timestamp = reader.ReadInt64();
    }
}


public struct MoveMessage : ISerializableMessage
{
    public bool LastMove; // True if last move in a game
    public string MoveName;
    public long Clock;

    public void SerializeIntoStream(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(LastMove);
        writer.Write(MoveName);
        writer.Write(Clock);
    }

    public void ReadFromStream(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8,  true);
        
        LastMove = reader.ReadBoolean();
        MoveName = reader.ReadString();
        Clock = reader.ReadInt64();
    }
}

public struct NewGameRequest : ISerializableMessage
{
    public bool IsWhite;
    public void SerializeIntoStream(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(IsWhite);
    }

    public void ReadFromStream(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8,  true);

        IsWhite = reader.ReadBoolean();
    }
}