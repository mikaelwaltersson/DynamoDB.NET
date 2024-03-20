using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text;
using Amazon.DynamoDBv2.Model;
using DynamoDB.Net.Model;
using DynamoDB.Net.Serialization;
using DynamoDB.Net.Serialization.Converters;

namespace DynamoDB.Net;

public static class PrimaryKey
{
    public const char DefaultKeysSeparator = ',';

    public static bool IsPrimaryKeyType(Type type, [NotNullWhen(true)] out Type? itemType)
    {
        ArgumentNullException.ThrowIfNull(type);

        var typeInfo = GenericTypeInfo.Get(type);

        itemType = typeInfo.PrimaryKeyItemType;
        return itemType != null;
    }

    public static PrimaryKey<T> ForItem<T>(T item) where T : class => PrimaryKey<T>.ForItem(item);
}

public readonly struct PrimaryKey<T> : IPrimaryKey, IEquatable<PrimaryKey<T>> where T : class
{
    public object? PartitionKey { get; }

    public object? SortKey { get; }

    public static PrimaryKey<T> FromTuple((object?, object?) keyTuple)
    {
        var (partitionKey, sortKey) = keyTuple;

        if (partitionKey == null || (sortKey == null) != (TableDescription.PropertyTypes<T>.SortKey == null))
            throw new ArgumentOutOfRangeException(nameof(keyTuple));
        
        partitionKey = CastConvert.CastTo(partitionKey, TableDescription.PropertyTypes<T>.PartitionKey);
        
        if (TableDescription.PropertyTypes<T>.SortKey != null)
            sortKey = CastConvert.CastTo(sortKey!, TableDescription.PropertyTypes<T>.SortKey);
        
        return new(partitionKey, sortKey);
    }

    public static PrimaryKey<T> ForItem(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new(
            TableDescription.PropertyAccessors<T>.GetPartitionKey(item),
            TableDescription.PropertyAccessors<T>.GetSortKey?.Invoke(item));
    }

    public static implicit operator PrimaryKey<T>((object?, object?) keyTuple) => FromTuple(keyTuple);

    public static bool operator ==(PrimaryKey<T> x, PrimaryKey<T> y) => x.Equals(y);

    public static bool operator !=(PrimaryKey<T> x, PrimaryKey<T> y) => !(x == y);

    public bool Equals(PrimaryKey<T> other) =>
        KeyEquals(this.PartitionKey, other.PartitionKey) &&
        KeyEquals(this.SortKey, other.SortKey);

    public override bool Equals(object? other) =>
        other is PrimaryKey<T> key && 
        Equals(key);

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

    public string ToString(IDynamoDBSerializer? serializer = null, char keysSeparator = PrimaryKey.DefaultKeysSeparator)
    {
        var s = new StringBuilder();

        serializer ??= DynamoDBSerializer.Default;

        ArgumentNullException.ThrowIfNull(nameof(serializer));

        s.Append(
            EscapeKeyValue(
                serializer.SerializeDynamoDBValue(this.PartitionKey, TableDescription.PropertyTypes<T>.PartitionKey),
                keysSeparator));

        if (TableDescription.PropertyTypes<T>.SortKey != null)
        {
            s.Append(keysSeparator);
            s.Append(
                EscapeKeyValue(
                    serializer.SerializeDynamoDBValue(this.SortKey, TableDescription.PropertyTypes<T>.SortKey),
                    keysSeparator));
        }

        return s.ToString();
    }

    public static PrimaryKey<T> Parse(string s, IDynamoDBSerializer? serializer = null, char keysSeparator = PrimaryKey.DefaultKeysSeparator)
    {
        var key = s.Split(keysSeparator);
        if (key.Length != (TableDescription.PropertyTypes<T>.SortKey != null ? 2 : 1))
            throw new FormatException(nameof(s));

        serializer ??= DynamoDBSerializer.Default;

        ArgumentNullException.ThrowIfNull(nameof(serializer));

        return
            new PrimaryKey<T>(
                DeserializeKeyValue(serializer, UnescapeKeyValue(key[0]), TableDescription.PropertyTypes<T>.PartitionKey),
                TableDescription.PropertyTypes<T>.SortKey != null 
                    ? DeserializeKeyValue(serializer, UnescapeKeyValue(key[1]), TableDescription.PropertyTypes<T>.SortKey)
                    : null);


    }

    PrimaryKey(object? partitionKey, object? sortKey)
    {
        PartitionKey = partitionKey;
        SortKey = sortKey;
    }

    static object? DeserializeKeyValue(IDynamoDBSerializer serializer, string value, Type type) =>
        serializer.DeserializeDynamoDBValue(
            type == typeof(byte[])
                ? new() { B = new(Convert.FromBase64String(value)) }
                : DynamoDBNumber.IsSupportedType(type)
                    ? new() { N = value }
                    : new() { S = value },
            type);

    static string UnescapeKeyValue(string s) => Uri.UnescapeDataString(s);

    static string EscapeKeyValue(string s, char keyValueSeparator)
    {
        var escaped = (StringBuilder?)null;
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

    static bool KeyEquals(object? x, object? y)
    {
        if (Equals(x, y))
            return true;

        if (x is byte[] xByteArray && y is byte[] yByteArray)
            return ByteArrayComparer.Default.Equals(xByteArray,  yByteArray);

        return false;
    }

    static int KeyHashCode(object obj)
    {
        if (obj is byte[] byteArray)
            return ByteArrayComparer.Default.GetHashCode(byteArray);

        return obj.GetHashCode();
    }

    static class CastConvert
    {
        static readonly ConcurrentDictionary<(Type, Type), Func<object?, object?>> compiledCastTo = new();

        static Func<object?, object?> CompileCastTo((Type, Type) types)
        {
            var (fromType, toType) = types;

            var parameter = Expression.Parameter(typeof(object), "value");

            var compiledCast = 
                Expression
                    .Lambda<Func<object?, object?>>(
                        Enumerable.Aggregate(
                            [fromType, toType, typeof(object)], 
                            (Expression)parameter, 
                            Expression.Convert), 
                        parameter)
                    .Compile();

            return compiledCast;
        }

        public static object? CastTo(object value, Type type) =>
            type.IsAssignableFrom(value.GetType()) 
                ? value
                : compiledCastTo.GetOrAdd((value.GetType(), type), CompileCastTo)(value);
    }
}
