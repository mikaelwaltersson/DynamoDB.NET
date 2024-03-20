namespace DynamoDB.Net.Model;

public class ByteArraySet : SortedSet<byte[]>, IEquatable<ByteArraySet>
{
    public ByteArraySet() : base(ByteArrayComparer.Default)
    {
    }

    public ByteArraySet(IEnumerable<byte[]> collection) : base(collection, ByteArrayComparer.Default)
    {
    }

    public override bool Equals(object? obj) => 
        obj is ByteArraySet other && Equals(other); 

    public bool Equals(ByteArraySet? other) =>
        other != null && this.SequenceEqual(other, ByteArrayComparer.Default);

    public override int GetHashCode() =>
        this.SequenceCombinedHashCode(ByteArrayComparer.Default);
}
