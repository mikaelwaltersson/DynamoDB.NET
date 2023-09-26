using System;
using System.Text;

using DynamoDB.Net.Model;
using DynamoDB.Net.Serialization;

using Newtonsoft.Json;

using AttributeValue = Amazon.DynamoDBv2.Model.AttributeValue;

namespace DynamoDB.Net
{
    public static class PrimaryKey
    {
        internal static readonly JsonSerializer DefaultSerializer;
        
        static PrimaryKey()
        {
            var serializerSettings = new JsonSerializerSettings();

            DynamoDBClientOptions.DefaultConfigureSerializer(serializerSettings, JsonContractResolver.DefaultDynamoDB);
            DefaultSerializer = JsonSerializer.Create(serializerSettings);
        }

        public static Type GetUnderlyingType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            return
                type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(PrimaryKey<>)
                    ? type.GenericTypeArguments[0]
                    : null;
        }
    }

    public struct PrimaryKey<T> : IEquatable<PrimaryKey<T>> where T : class
    {
        public PrimaryKey(T item)
            : this()
        {
            if (item != null)
            {
                this.PartitionKey = TableDescription.PropertyAccessors<T>.GetPartitionKey(item);
                this.SortKey = TableDescription.PropertyAccessors<T>.GetSortKey?.Invoke(item);
            }
        }

        public PrimaryKey(object partitionKey, object sortKey)
        {
            partitionKey = partitionKey?.CastTo(TableDescription.KeyTypes<T>.PartitionKey);
            if (partitionKey == null || partitionKey.Equals(null))
                throw new ArgumentNullException(nameof(partitionKey));

            this.PartitionKey = partitionKey;

            if (HasSortKey)
            {
                sortKey = sortKey?.CastTo(TableDescription.KeyTypes<T>.SortKey);
                if (sortKey == null ||sortKey.Equals(null))
                    throw new ArgumentNullException(nameof(sortKey));

                this.SortKey = sortKey;
            }
            else 
            {
                 if (sortKey != null)
                    throw new ArgumentOutOfRangeException(nameof(sortKey));

                this.SortKey = null;
            }
        }


        public object PartitionKey { get; }

        public object SortKey { get; }


        public static implicit operator PrimaryKey<T>(T value) => new PrimaryKey<T>(value);

        public static bool operator ==(PrimaryKey<T> x, PrimaryKey<T> y) => x.Equals(y);

        public static bool operator !=(PrimaryKey<T> x, PrimaryKey<T> y) => !(x == y);

        public bool Equals(PrimaryKey<T> other) =>
            KeyEquals(this.PartitionKey, other.PartitionKey) &&
            KeyEquals(this.SortKey, other.SortKey);
 
        public override bool Equals(object other) => (
            (other is T || other == null)
                ? Equals(new PrimaryKey<T>((T)other))
                : (other is PrimaryKey<T> && Equals((PrimaryKey<T>)other)));

        public override int GetHashCode()
        {
            var hashCode = KeyHashCode(PartitionKey);
            
            if (SortKey != null)
                hashCode = (((hashCode << 5) + hashCode) ^ KeyHashCode(SortKey));

            return hashCode;
        }

        public override string ToString() => this.ToString(null);

        public string ToString(JsonSerializer keyValueSerializer = null, char keyValueSeparator = ',')
        {
            var s = new StringBuilder();

            if (keyValueSerializer == null)
                keyValueSerializer = PrimaryKey.DefaultSerializer;

            s.Append(
                EscapeKeyValue(
                    keyValueSerializer.SerializeDynamoDBValue(this.PartitionKey, TableDescription.KeyTypes<T>.PartitionKey),
                    keyValueSeparator));

            if (HasSortKey)
            {
                s.Append(keyValueSeparator);
                s.Append(
                    EscapeKeyValue(
                        keyValueSerializer.SerializeDynamoDBValue(this.SortKey, TableDescription.KeyTypes<T>.SortKey),
                        keyValueSeparator));
            }

            return s.ToString();
        }

        public static PrimaryKey<T> Parse(string s, JsonSerializer keyValueSerializer = null, char keyValueSeparator = ',')
        {
            var key = s.Split(keyValueSeparator);
            if (key.Length != (HasSortKey ? 2 : 1))
                throw new FormatException(nameof(s));

            if (keyValueSerializer == null)
                keyValueSerializer = PrimaryKey.DefaultSerializer;

            return
                new PrimaryKey<T>(
                    keyValueSerializer.DeserializeDynamoDBValue(
                        new AttributeValue { S = UnescapeKeyValue(key[0]) }, 
                        TableDescription.KeyTypes<T>.PartitionKey),
                    HasSortKey 
                    ? keyValueSerializer.DeserializeDynamoDBValue(
                        new AttributeValue { S = UnescapeKeyValue(key[1]) }, 
                        TableDescription.KeyTypes<T>.SortKey)
                    : null);
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
                    if (escaped == null)
                        escaped = new StringBuilder(s, 0, i, s.Length + 1);
                    escaped.Append($"%{((int)c):X2}");
                }
                else if (escaped != null)
                    escaped.Append(c);
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

            if (a is byte[])
                return ByteArrayComparer.Default.Equals((byte[])a,  b as byte[]);

            return false;
        }

        static int KeyHashCode(object obj)
        {
            if (obj is byte[])
                return ByteArrayComparer.Default.GetHashCode((byte[])obj);

            return obj.GetHashCode();
        }
    }
}