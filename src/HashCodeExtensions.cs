namespace DynamoDB.Net;

static class EnumerableExtensions
{
    public static int SequenceCombinedHashCode<T>(this IEnumerable<T> source) =>
        SequenceCombinedHashCode(source, EqualityComparer<T>.Default);

    public static int SequenceCombinedHashCode<T>(this IEnumerable<T> source, IEqualityComparer<T> comparer)
    {
        var hashCode = new HashCode();

        foreach (var element in source)
            hashCode.Add(element, comparer);

        return hashCode.ToHashCode();
    }

    public static int SequenceCombinedHashCode(this ReadOnlySpan<byte> source)
    {
        var hashCode = new HashCode();

        hashCode.AddBytes(source);

        return hashCode.ToHashCode();
    }

    public static int SequenceCombinedHashCode(this byte[] source) =>
        ((ReadOnlySpan<byte>)source).SequenceCombinedHashCode();

    public static int SequenceCombinedHashCode(this ArraySegment<byte> source) =>
        ((ReadOnlySpan<byte>)source).SequenceCombinedHashCode();
}
