using System.Globalization;
using System.Numerics;
using Amazon.DynamoDBv2.Model;
using DynamoDB.Net.Expressions;
using DynamoDB.Net.Serialization;

namespace DynamoDB.Net;

public class VersionChecker : IDynamoDBItemEventHandler
{
    public static readonly object Skip = new();

    public T OnItemDeserialized<T>(T item) where T : class => item;

    public Dictionary<string, AttributeValue> OnItemSerialized<T>(Dictionary<string, AttributeValue> item, ExpressionTranslationContext translationContext) where T : class
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(translationContext);

        var versionProperty = TableDescription.Properties<T>.Version;
        var versionPropertyType = TableDescription.PropertyTypes<T>.Version;
        
        if ((versionProperty, versionPropertyType) is (not null, not null))
        {
            var propertyName = translationContext.Serializer.GetPropertyAttributeInfo(versionProperty.Value).AttributeName;

            if (!item.TryGetValue(propertyName, out var serializedVersion) || serializedVersion.IsEmptyOrNull())
                serializedVersion = GetInitialVersion(versionPropertyType, translationContext.Serializer);

            item[propertyName] = GetNextVersion(serializedVersion, versionPropertyType, translationContext.Serializer);
        }

        return item;
    }

    public string OnItemUpdateTranslated<T>(string expression, object? version, ExpressionTranslationContext translationContext) where T : class
    {
        ArgumentNullException.ThrowIfNull(translationContext);

        var versionProperty = TableDescription.Properties<T>.Version;
        var versionPropertyType = TableDescription.PropertyTypes<T>.Version;
        
        if ((versionProperty, versionPropertyType) is (not null, not null) && !ReferenceEquals(version, Skip))
        {
            var serializedVersion = translationContext.Serializer.SerializeDynamoDBValue(version);
            var isIncrementableVersion = GetInitialVersion(versionPropertyType, translationContext.Serializer).N != null;

            var propertyName = translationContext.Serializer.GetPropertyAttributeInfo(versionProperty.Value).AttributeName;
            var propertyAlias = translationContext.GetOrAddAttributeName(propertyName);
            if (serializedVersion.IsEmptyOrNull() && isIncrementableVersion)
            {
                var zeroAlias = translationContext.GetOrAddAttributeValue(new AttributeValue { N = "0" });
                var oneAlias = translationContext.GetOrAddAttributeValue(new AttributeValue { N = "1" });
                expression = ExpressionTranslator.AppendUpdate(expression, "SET", $"{propertyAlias} = if_not_exists({propertyAlias}, {zeroAlias}) + {oneAlias}");
            }
            else
            {
                var valueAlias = translationContext.GetOrAddAttributeValue(GetNextVersion(serializedVersion, versionPropertyType, translationContext.Serializer));
                expression = ExpressionTranslator.AppendUpdate(expression, "SET", $"{propertyAlias} = {valueAlias}");
            }
        }

        return expression;
    }
    
    public string? OnItemConditionTranslated<T>(string? expression, object? version, ExpressionTranslationContext translationContext) where T : class
    {
        ArgumentNullException.ThrowIfNull(translationContext);

        var versionProperty = TableDescription.Properties<T>.Version;
        var versionPropertyType = TableDescription.PropertyTypes<T>.Version;
        
        if ((versionProperty, versionPropertyType) is (not null, not null) && !ReferenceEquals(version, Skip))
        {
            var serializedVersion = translationContext.Serializer.SerializeDynamoDBValue(version);
            if (!serializedVersion.IsEmptyOrNull())
            {
                var propertyAlias = 
                    translationContext.GetOrAddAttributeName(
                        translationContext.Serializer
                            .GetPropertyAttributeInfo(versionProperty.Value)
                            .AttributeName);

                expression =
                    ExpressionTranslator.AppendCondition(
                        expression,
                        AttributeValueComparer.Default.Equals(serializedVersion, GetInitialVersion(versionPropertyType, translationContext.Serializer))
                            ? $"attribute_not_exists({propertyAlias})"
                            : $"{propertyAlias} = {translationContext.GetOrAddAttributeValue(serializedVersion)}",
                        "AND");
            }
        }

        return expression;
    }

    static AttributeValue GetNextVersion(AttributeValue serializedVersion, Type versionPropertyType, IDynamoDBSerializer serializer) 
    {
        var number = serializedVersion.N;
        if (number != null)
        {
            var formatProvider = CultureInfo.InvariantCulture;
            
            number = long.TryParse(number, formatProvider, out var longValue)
                ? (longValue + 1).ToString(formatProvider)
                : decimal.TryParse(number, formatProvider, out var decimalValue)
                    ? (decimalValue + 1).ToString(formatProvider)
                    : (BigInteger.Parse(number, formatProvider) + 1).ToString(formatProvider);
            
            return new() { N = number };
        }

        var type = versionPropertyType.UnwrapNullableType();
        object autoGeneratedVersion;

        if (type == typeof(Guid))
            autoGeneratedVersion = Guid.NewGuid();
        else if (type == typeof(DateTime))
            autoGeneratedVersion = DateTime.UtcNow;
        else if (type == typeof(DateTimeOffset))
            autoGeneratedVersion = DateTimeOffset.UtcNow;
        else
            throw new InvalidOperationException($"Can not automatically get next version for type '{versionPropertyType}'");

        return serializer.SerializeDynamoDBValue(autoGeneratedVersion);
    }

    static AttributeValue GetInitialVersion(Type versionPropertyType, IDynamoDBSerializer serializer) => 
        serializer.SerializeDynamoDBValue(Serialization.Activator.CreateInstance(versionPropertyType.UnwrapNullableType()), versionPropertyType);
}
