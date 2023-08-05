namespace Chess_Challenge_Server;

public interface ISerializableMessage
{
    public byte RefCode { get; }
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