using DynamoDB.Net.Model;

namespace DynamoDB.Net.Tests.UnitTests.Model;

public class ByteArraySetTests
{
    [Fact]
    public void CanCompareByteArraySets()
    {
        var a = new ByteArraySet([[1, 2, 3], [4, 5], [6]]);
        var b = new ByteArraySet([[1, 2], [3, 4, 5], [6]]);
        var c = new ByteArraySet([[1, 2, 3], [4, 5], [6]]);

        Assert.False(a.Equals(b));
        Assert.True(a.Equals(c));
        Assert.True(a.Equals((object)c));
        Assert.True(a.GetHashCode() == c.GetHashCode());
        Assert.False(a.Equals(new byte[] {1, 2, 3, 4, 5, 6 }));
        Assert.False(a.Equals(null));
    }
}
