namespace DynamoDB.Net.Serialization;

public class MemoryStreamComparer : IEqualityComparer<MemoryStream>
{
    public static MemoryStreamComparer Default { get; } = new MemoryStreamComparer();

    public bool Equals(MemoryStream? x, MemoryStream? y) =>
        (x == null && y == null) || 
        (x != null && y != null && BufferOf(x).SequenceEqual(BufferOf(y)));

    public int GetHashCode(MemoryStream obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        
        return BufferOf(obj).SequenceCombinedHashCode();
    }

    static ArraySegment<byte> BufferOf(MemoryStream stream) => 
        stream.TryGetBuffer(out var buffer) 
            ? buffer 
            : new ArraySegment<byte>(stream.ToArray());
}
