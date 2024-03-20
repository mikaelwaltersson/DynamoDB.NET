using DynamoDB.Net.Model;

namespace DynamoDB.Net.Tests.UnitTests;

public class PrimaryKeyTests
{
    [Fact]
    public void CanCreatePrimaryKeyFromItem()
    {
        Assert.True(PrimaryKey<Item>.ForItem(new Item { P = "XYZ", S = 123 }) is { PartitionKey: "XYZ", SortKey: 123 });
    }

    [Fact]
    public void CanCreatePrimaryKeyFromTuple()
    {
        Assert.True(PrimaryKey<Item>.FromTuple(("XYZ", 123)) is { PartitionKey: "XYZ", SortKey: 123 });
    }

    [Fact]
    public void CanCreatePrimaryKeyFromTupleWhereValuesNeedsCast()
    {
        Assert.True(
            PrimaryKey<ItemWithFixedLength3Key>.FromTuple(("XYZ", 123)) is { PartitionKey: FixedLengthString3 p, SortKey: 123 } &&
            p == new FixedLengthString3('X', 'Y', 'Z'));

        Assert.True(PrimaryKey<Item>.FromTuple((new FixedLengthString3('X', 'Y', 'Z'), 123)) is { PartitionKey: "XYZ", SortKey: 123 });
    }

    [Fact]
    public void CreatePrimaryKeyFromTupleFailsIfKeyPartIsNull()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimaryKey<Item>.FromTuple((null, 123)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimaryKey<Item>.FromTuple(("XYZ", null)));
    }

    [Fact]
    public void CreatePrimaryKeyFromTupleFailsIfNonNullValueIsSpecifiedForNonExistantSortKey()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimaryKey<ItemWithoutSortKey>.FromTuple(("XYZ", 1)));
    }

    [Fact]
    public void ByteArrayKeyValuesAreComparedByValue()
    {
        var a = PrimaryKey<ItemWithByteArrayKey>.FromTuple((new byte[] { 1, 2, 3 }, null));
        var b = PrimaryKey<ItemWithByteArrayKey>.FromTuple((new byte[] { 1, 2, 3 }, null));

        Assert.True(a.Equals(b));
        Assert.True(a.GetHashCode() == b.GetHashCode());
    }

    [Fact]
    public void CanFormatKeyValues()
    {
        Assert.Equal("XYZ|123", PrimaryKey<Item>.FromTuple(("XYZ", 123)).ToString(keysSeparator: '|'));
    }

    [Fact]
    public void CanFormatKeyValuesThatNeedsEscaping()
    {
        Assert.Equal("X%7CY%7CZ|123", PrimaryKey<Item>.FromTuple(("X|Y|Z", 123)).ToString(keysSeparator: '|'));
    }

    [Fact]
    public void CanFormatByteArrayKeyValues()
    {
        Assert.Equal("AQID", PrimaryKey<ItemWithByteArrayKey>.FromTuple((new byte[] { 1, 2, 3 }, null)).ToString());
    }

    [Fact]
    public void CanParseKeyValues()
    {
        Assert.Equal(PrimaryKey<Item>.FromTuple(("XYZ", 123)), PrimaryKey<Item>.Parse("XYZ|123", keysSeparator: '|'));
    }

    [Fact]
    public void CanParseKeyValuesThatNeedsEscaping()
    {
        Assert.Equal(PrimaryKey<Item>.FromTuple(("X|Y|Z", 123)), PrimaryKey<Item>.Parse("X%7CY%7CZ|123", keysSeparator: '|'));
    }

    [Fact]
    public void CanParseByteArrayKeyValues()
    {
        Assert.Equal(PrimaryKey<ItemWithByteArrayKey>.FromTuple((new byte[] { 1, 2, 3 }, null)), PrimaryKey<ItemWithByteArrayKey>.Parse("AQID"));
    }


    [Table]
    class Item
    {
        [PartitionKey]
        public required string P { get; set; }

        [SortKey]
        public int S { get; set; }
    }

    [Table]
    class ItemWithFixedLength3Key
    {
        [PartitionKey]
        public required FixedLengthString3 P { get; set; }

        [SortKey]
        public int S { get; set; }
    }

    [Table]
    class ItemWithoutSortKey
    {
        [PartitionKey]
        public required string P { get; set; }
    }

    [Table]
    class ItemWithByteArrayKey
    {
        [PartitionKey]
        public required byte[] P { get; set; }
    }

    readonly struct FixedLengthString3(char a, char b, char c)
    {
        readonly char a = a, b = b, c = c;

        public static explicit operator FixedLengthString3(string value)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(value.Length, 3, nameof(value));
            return new FixedLengthString3(value[0], value[1], value[2]);
        }

        public static implicit operator string(FixedLengthString3 value) => value.ToString();

        public override string ToString() => new([a, b, c]);
    }
}
