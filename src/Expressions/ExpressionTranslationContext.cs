using Amazon.DynamoDBv2.Model;
using DynamoDB.Net.Serialization;

namespace DynamoDB.Net.Expressions;

public class ExpressionTranslationContext
{
    Dictionary<string, string>? attributeNames;
    Dictionary<string, string>? attributeNameAliases;
    Dictionary<string, AttributeValue>? attributeValues;
    Dictionary<AttributeValue, string>? attributeValueAliases;

    public ExpressionTranslationContext(IDynamoDBSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);

        Serializer = serializer;
    }


    public IDynamoDBSerializer Serializer { get; }

    public Dictionary<string, string>? AttributeNames => attributeNames;
    
    public Dictionary<string, AttributeValue>? AttributeValues => attributeValues;

    internal Stack<DynamoDBExpressions.ArrayConstantKind> ArrayConstantKind { get; } = new([DynamoDBExpressions.ArrayConstantKind.Unspecified]); 

    public string GetOrAddAttributeName(string name) => 
        GetOrAddWithAlias(name, ref attributeNameAliases, ref attributeNames, StringComparer.InvariantCulture, "#p");

    public string GetOrAddAttributeValue(AttributeValue value) => 
        GetOrAddWithAlias(value, ref attributeValueAliases, ref attributeValues, AttributeValueComparer.Default, ":v");

    static string GetOrAddWithAlias<TValue>(
        TValue value, 
        ref Dictionary<TValue, string>? valueToAlias, 
        ref Dictionary<string, TValue>? aliasToValue, 
        IEqualityComparer<TValue> valueComparer,
        string prefix) where TValue : class
    {
        ArgumentNullException.ThrowIfNull(value);

        valueToAlias ??= new(valueComparer);

        if (!valueToAlias.TryGetValue(value, out var alias))
        {
            alias = GetNextUnusedAlias(ref aliasToValue, prefix);
            valueToAlias.Add(value, alias);
            aliasToValue!.Add(alias, value);
        }

        return alias;
    }

    static string GetNextUnusedAlias<TValue>(ref Dictionary<string, TValue>? aliasToValue, string prefix)
    {
        aliasToValue ??= new(StringComparer.Ordinal);

        for (var i = aliasToValue.Count; ; i++)
        {
            var alias = $"{prefix}{i}";
            if (!aliasToValue.ContainsKey(alias))
                return alias;
        }
    }

    internal void Add(DynamoDBExpressions.RawExpression raw)
    {
        if (raw.names != null)
        {
            this.attributeNames ??= [];

            foreach (var entry in raw.names)
                this.attributeNames.Add(entry.Key, entry.Value);
        }

        if (raw.values != null)
        {
            this.attributeValues ??= [];

            foreach (var entry in raw.values)
            {
                this.attributeValues.Add(
                    entry.Key,
                    entry.Value as AttributeValue ??
                    Serializer.SerializeDynamoDBValue(entry.Value));
            }
        }
    }
}
