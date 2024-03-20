using DynamoDB.Net.Model;

namespace DynamoDB.Net.Tests.UnitTests.Model;

public class ByteArrayComparerTests
{
    [Fact]
    public void CanSortByteArrays()
    {
        var byteArrays = new List<byte[]>([[4, 5], [1, 2, 3], [1, 2], [5], [4, 5, 6], []]);
        
        byteArrays.Sort(ByteArrayComparer.Default);
        
        Assert.Equal([[], [1, 2], [1, 2, 3], [4, 5], [4, 5, 6], [5]], byteArrays);
    }
}
