using DynamoDB.Net.Serialization;

namespace DynamoDB.Net.Tests.UnitTests.Serialization;

public class MemoryStreamComparerTests
{
    [Fact]
    public void CanCompareMemoryStreams()
    {
        var a = new MemoryStream([1, 2, 3]);
        var b = new MemoryStream([1, 2, 3, 4]);
        var c = new MemoryStream([1, 2, 2]);
        var d = new MemoryStream([1, 2, 3]);

        Assert.False(MemoryStreamComparer.Default.Equals(a, b));
        Assert.False(MemoryStreamComparer.Default.Equals(a, c));
        Assert.True(MemoryStreamComparer.Default.Equals(a, d));
        Assert.True(MemoryStreamComparer.Default.GetHashCode(a) == MemoryStreamComparer.Default.GetHashCode(d));
        Assert.False(MemoryStreamComparer.Default.Equals(a, null));
        Assert.False(MemoryStreamComparer.Default.Equals(null, a));
        Assert.True(MemoryStreamComparer.Default.Equals(null, null));
    }
}
