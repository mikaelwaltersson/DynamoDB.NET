using System;
using System.Text;
using Amazon.DynamoDBv2.Model;
using DynamoDB.Net.Model;
using DynamoDB.Net.Serialization;

using TableDescription = DynamoDB.Net.Model.TableDescription;

namespace DynamoDB.Net;

public static class PrimaryKey
{
    public const char DefaultKeysSeparator = ',';

    public static IDynamoDBSerializer DefaultSerializer { get; set; }

    public static Type GetUnderlyingType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return
            type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(PrimaryKey<>)
                ? type.GenericTypeArguments[0]
                : null;
    }

    public static PrimaryKey<T> ForItem<T>(T item) where T : class => PrimaryKey<T>.ForItem(item);
}

public readonly struct PrimaryKey<T> : IPrimaryKey, IEquatable<PrimaryKey<T>> where T : class
{
    public object PartitionKey { get; }

    public object SortKey { get; }

    public static PrimaryKey<T> FromTuple((object, object) keyTuple)
    {
        var (partitionKey, sortKey) = keyTuple;

        partitionKey = partitionKey?.CastTo(TableDescription.KeyTypes<T>.PartitionKey);
        
        if (partitionKey == null || partitionKey.Equals(null))
            throw new ArgumentOutOfRangeException(nameof(keyTuple));

        if (HasSortKey)
        {
            sortKey = sortKey?.CastTo(TableDescription.KeyTypes<T>.SortKey);
            if (sortKey == null || sortKey.Equals(null))
                throw new ArgumentOutOfRangeException(nameof(keyTuple));
        }
        else 
        {
            if (!(sortKey == null || sortKey.Equals(null)))
                throw new ArgumentOutOfRangeException(nameof(keyTuple));

            sortKey = null;
        }

        return new(partitionKey, sortKey);
    }

    public static PrimaryKey<T> ForItem(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new(
            TableDescription.PropertyAccessors<T>.GetPartitionKey(item),
            TableDescription.PropertyAccessors<T>.GetSortKey?.Invoke(item)
        );
    }

    public static implicit operator PrimaryKey<T>((object, object) keyTuple) => FromTuple(keyTuple);

    public static bool operator ==(PrimaryKey<T> x, PrimaryKey<T> y) => x.Equals(y);

    public static bool operator !=(PrimaryKey<T> x, PrimaryKey<T> y) => !(x == y);

    public bool Equals(PrimaryKey<T> other) =>
        KeyEquals(this.PartitionKey, other.PartitionKey) &&
        KeyEquals(this.SortKey, other.SortKey);

    public override bool Equals(object other) =>
        other is T item
            ? Equals(PrimaryKey.ForItem(item)) // TODO: remove PrimaryKey<T> == T comparision
            : other is PrimaryKey<T> key
                ? Equals(key)
                : other == null && PartitionKey == null && SortKey == null;

    public override int GetHashCode()
    {
        var hashCode = 0;
        
        if (PartitionKey != null)
            hashCode = KeyHashCode(PartitionKey);
        
        if (SortKey != null)
            hashCode = (((hashCode << 5) + hashCode) ^ KeyHashCode(SortKey));

        return hashCode;
    }

    public override string ToString() => ToString(null);

    public string ToString(IDynamoDBSerializer serializer = null, char keysSeparator = PrimaryKey.DefaultKeysSeparator)
    {
        var s = new StringBuilder();

        serializer ??= PrimaryKey.DefaultSerializer;

        ArgumentNullException.ThrowIfNull(nameof(serializer));

        s.Append(
            EscapeKeyValue(
                serializer.SerializeDynamoDBValue(this.PartitionKey, TableDescription.KeyTypes<T>.PartitionKey),
                keysSeparator));

        if (HasSortKey)
        {
            s.Append(keysSeparator);
            s.Append(
                EscapeKeyValue(
                    serializer.SerializeDynamoDBValue(this.SortKey, TableDescription.KeyTypes<T>.SortKey),
                    keysSeparator));
        }

        return s.ToString();
    }

    public static PrimaryKey<T> Parse(string s, IDynamoDBSerializer serializer = null, char keysSeparator = PrimaryKey.DefaultKeysSeparator)
    {
        var key = s.Split(keysSeparator);
        if (key.Length != (HasSortKey ? 2 : 1))
            throw new FormatException(nameof(s));

        serializer ??= PrimaryKey.DefaultSerializer;

        ArgumentNullException.ThrowIfNull(nameof(serializer));

        return
            new PrimaryKey<T>(
                serializer.DeserializeDynamoDBValue(
                    new AttributeValue { S = UnescapeKeyValue(key[0]) }, 
                    TableDescription.KeyTypes<T>.PartitionKey),
                HasSortKey 
                ? serializer.DeserializeDynamoDBValue(
                    new AttributeValue { S = UnescapeKeyValue(key[1]) }, 
                    TableDescription.KeyTypes<T>.SortKey)
                : null);
    }

    PrimaryKey(object partitionKey, object sortKey)
    {
        PartitionKey = partitionKey;
        SortKey = sortKey;
    }

    static string UnescapeKeyValue(string s) => Uri.UnescapeDataString(s);

    static string EscapeKeyValue(string s, char keyValueSeparator)
    {
        var escaped = (StringBuilder)null;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '%' || c == keyValueSeparator)
            {
                escaped ??= new StringBuilder(s, 0, i, s.Length + 1);
                escaped.Append($"%{((int)c):X2}");
            }
            else
                escaped?.Append(c);
        }
        return escaped?.ToString() ?? s;
    }

    static string EscapeKeyValue(AttributeValue value, char keyValueSeparator) =>
        EscapeKeyValue(value.B?.ToBase64String() ?? value.N ?? value.S ?? string.Empty, keyValueSeparator);

    static bool HasSortKey => TableDescription.PropertyAccessors<T>.GetSortKey != null;

    static bool KeyEquals(object a, object b)
    {
        if (Equals(a, b))
            return true;

        if (a is byte[] byteArray)
            return ByteArrayComparer.Default.Equals(byteArray,  b as byte[]);

        return false;
    }

    static int KeyHashCode(object obj)
    {
        if (obj is byte[] byteArray)
            return ByteArrayComparer.Default.GetHashCode(byteArray);

        return obj.GetHashCode();
    }
}
