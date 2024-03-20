using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Serialization;

public class AttributeValueComparer : IEqualityComparer<AttributeValue>
{
    public static AttributeValueComparer Default { get; } = new AttributeValueComparer();
    
    public bool Equals(AttributeValue? x, AttributeValue? y) =>
        x switch
        {
            null => y == null,

            { NULL: true } => y is { NULL: true },

            { IsBOOLSet: true } => y is { IsBOOLSet: true } && x.BOOL == y.BOOL,

            { S: not null } => y is { S: not null } && x.S == y.S,

            { N: not null } => y is { N: not null } && x.N == y.N,

            { B: not null } => y is { B: not null } && MemoryStreamComparer.Default.Equals(x.B, y.B),

            { SS.Count: > 0 } => y is { SS.Count: > 0 } && x.SS.SequenceEqual(y.SS),

            { NS.Count: > 0 } => y is { NS.Count: > 0 } && x.NS.SequenceEqual(y.NS),

            { BS.Count: > 0 } => y is { BS.Count: > 0 } && x.BS.SequenceEqual(y.BS, MemoryStreamComparer.Default),

            { IsLSet: true } => y is { IsLSet: true } && x.L.SequenceEqual(y.L, this),

            { IsMSet: true } => 
                y is { IsMSet: true } && 
                x.M.Count == y.M.Count && 
                x.M.All(xEntry => y.M.TryGetValue(xEntry.Key, out var yValue) && Equals(xEntry.Value, yValue)),

            _ => y != null && y.IsEmpty()
        };

    public int GetHashCode(AttributeValue obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        return obj switch 
        {
            { NULL: true } => 0,

            { IsBOOLSet: true } => obj.BOOL.GetHashCode(),

            { S: not null } => obj.S.GetHashCode(),

            { N: not null } => obj.N.GetHashCode(),

            { B: not null } => MemoryStreamComparer.Default.GetHashCode(obj.B),

            { SS.Count: > 0 } => obj.SS.SequenceCombinedHashCode(),

            { NS.Count: > 0 } => obj.NS.SequenceCombinedHashCode(),

            { BS.Count: > 0 } => obj.BS.SequenceCombinedHashCode(MemoryStreamComparer.Default),

            { IsLSet: true } => obj.L.SequenceCombinedHashCode(this),

            { IsMSet: true } => HashCode.Combine(obj.M.Keys.SequenceCombinedHashCode(), obj.M.Values.SequenceCombinedHashCode(this)),

            _ => -1
        };
    }
}
