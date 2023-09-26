using System;
using System.Collections.Generic;

using Amazon.DynamoDBv2.Model;

using DynamoDB.Net.Serialization;

using Newtonsoft.Json;

namespace DynamoDB.Net.Expressions
{
    public class ExpressionTranslationContext<T> where T : class
    {
        Dictionary<string, string> attributeNames;
        Dictionary<string, string> attributeNameAliases;
        Dictionary<string, AttributeValue> attributeValues;
        Dictionary<AttributeValue, string> attributeValueAliases;

        public ExpressionTranslationContext(JsonSerializer serializer, DynamoDBJsonWriterFlags jsonWriterFlags)
        {
            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            Serializer = serializer;
            JsonWriterFlags = jsonWriterFlags;
            TableDescription = serializer.GetTableDescription(typeof(T));
        }


        public JsonSerializer Serializer { get; }
        public DynamoDBJsonWriterFlags JsonWriterFlags { get; }
        public Model.TableDescription TableDescription { get; }

        public Dictionary<string, string> AttributeNames => attributeNames;
        public Dictionary<string, AttributeValue> AttributeValues => attributeValues;


        public string GetOrAddAttributeName(string name) => 
            GetOrAddWithAlias(name, ref attributeNameAliases, ref attributeNames, StringComparer.InvariantCulture, "#p");

        public string GetOrAddAttributeValue(AttributeValue value) => 
            GetOrAddWithAlias(value, ref attributeValueAliases, ref attributeValues, AttributeValueComparer.Default, ":v");

        public bool IsSerializedToEmpty(object value) =>
            value == null
            ? !JsonWriterFlags.HasFlag(DynamoDBJsonWriterFlags.PersistNullValues)
            : Serializer.SerializeDynamoDBValue(value, value?.GetType(), JsonWriterFlags).IsEmpty();     


        static string GetOrAddWithAlias<TValue>(
            TValue value, 
            ref Dictionary<TValue, string> valueToAlias, 
            ref Dictionary<string, TValue> aliasToValue, 
            IEqualityComparer<TValue> valueComparer,
            string prefix)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            LazyInitializeDictionary(ref valueToAlias, valueComparer);

            if (!valueToAlias.TryGetValue(value, out var alias))
            {
                alias = GetNextUnusedAlias(ref aliasToValue, prefix);
                valueToAlias.Add(value, alias);
                aliasToValue.Add(alias, value);
            }

            return alias;
        }

        static string GetNextUnusedAlias<TValue>(ref Dictionary<string, TValue> aliasToValue, string prefix)
        {
            LazyInitializeDictionary(ref aliasToValue, StringComparer.Ordinal);

            for (var i = aliasToValue.Count; ; i++)
            {
                var alias = $"{prefix}{i}";
                if (!aliasToValue.ContainsKey(alias))
                    return alias;
            }
        }

        static void LazyInitializeDictionary<TKey, TValue>(ref Dictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
        {
            if (dictionary == null)
                dictionary = new Dictionary<TKey, TValue>(comparer);
        }

        internal void Add(DynamoDBExpressions.RawExpression raw)
        {
            if (raw.names != null)
            {
                if (this.attributeNames == null)
                    this.attributeNames = new Dictionary<string, string>();

                foreach (var entry in raw.names)
                    this.attributeNames.Add(entry.Key, entry.Value);
            }

            if (raw.values != null)
            {
                if (this.attributeValues == null)
                    this.attributeValues = new Dictionary<string, AttributeValue>();

                foreach (var entry in raw.values)
                {
                    this.attributeValues.Add(
                        entry.Key,
                        entry.Value as AttributeValue ??
                        Serializer.SerializeDynamoDBValue(entry.Value, JsonWriterFlags).EmptyAsNull());
                }
            }
        }

        
    }
}