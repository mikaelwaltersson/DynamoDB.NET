using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using DynamoDB.Net.Expressions;
using DynamoDB.Net.Serialization;

using TableDescription = DynamoDB.Net.Model.TableDescription;

namespace DynamoDB.Net;

public class VersionChecker : IDynamoDBItemEventHandler
{
    public T OnItemDeserialized<T>(T item) where T : class => item;

    public Dictionary<string, AttributeValue> OnItemSerialized<T>(Dictionary<string, AttributeValue> item, ExpressionTranslationContext<T> translationContext) where T : class
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(translationContext);

        var versionProperty = TableDescription.Get(typeof(T)).VersionProperty;
        if (versionProperty != null)
        {
            var propertyName = translationContext.Serializer.GetSerializedPropertyName(versionProperty);

            var serializedVersion = item.GetValueOrDefault(propertyName);
            if (serializedVersion == null || serializedVersion.NULL || serializedVersion.IsEmpty())
                serializedVersion = GetDefaultSerializedVersionValue(versionProperty, translationContext.Serializer);

            item[propertyName] = GetNextVersion(serializedVersion, versionProperty, translationContext.Serializer);
        }

        return item;
    }

    public string OnItemUpdateTranslated<T>(string expression, object version, ExpressionTranslationContext<T> translationContext) where T : class
    {
        ArgumentNullException.ThrowIfNull(translationContext);

        var versionProperty = TableDescription.Get(typeof(T)).VersionProperty;
        if (versionProperty != null && !ReferenceEquals(version, DynamoDBExpressions.SkipVersionCheckAndUpdate))
        {
            var serializedVersion = translationContext.Serializer.SerializeDynamoDBValue(version);
            var isIncrementableVersion = GetDefaultSerializedVersionValue(versionProperty, translationContext.Serializer).N != null;

            var propertyName = translationContext.Serializer.GetSerializedPropertyName(versionProperty);
            var propertyAlias = translationContext.GetOrAddAttributeName(propertyName);
            if (serializedVersion.IsEmpty() && isIncrementableVersion)
            {
                var zeroAlias = translationContext.GetOrAddAttributeValue(new AttributeValue { N = "0" });
                var oneAlias = translationContext.GetOrAddAttributeValue(new AttributeValue { N = "1" });
                expression = ExpressionTranslator.AppendUpdate(expression, "SET", $"{propertyAlias} = if_not_exists({propertyAlias}, {zeroAlias}) + {oneAlias}");
            }
            else
            {
                var valueAlias = translationContext.GetOrAddAttributeValue(GetNextVersion(serializedVersion, versionProperty, translationContext.Serializer));
                expression = ExpressionTranslator.AppendUpdate(expression, "SET", $"{propertyAlias} = {valueAlias}");
            }
        }

        return expression;
    }
    
    public string OnItemConditionTranslated<T>(string expression, object version, ExpressionTranslationContext<T> translationContext) where T : class
    {
        ArgumentNullException.ThrowIfNull(translationContext);

        var versionProperty = TableDescription.Get(typeof(T)).VersionProperty;
        if (versionProperty != null && !ReferenceEquals(version, DynamoDBExpressions.SkipVersionCheckAndUpdate))
        {
            var serializedVersion = translationContext.Serializer.SerializeDynamoDBValue(version);
            var isEmptyInitialVersion = IsEmptyVersionValue(version, versionProperty);

            if (!serializedVersion.IsEmpty())
            {
                var propertyName = translationContext.Serializer.GetSerializedPropertyName(versionProperty);
                var propertyAlias = translationContext.GetOrAddAttributeName(propertyName);
                var valueAlias = translationContext.GetOrAddAttributeValue(serializedVersion);

                expression =
                    ExpressionTranslator.AppendCondition(
                        expression,
                        isEmptyInitialVersion
                            ? $"attribute_not_exists({propertyAlias})"
                            : $"{propertyAlias} = {valueAlias}",
                        "AND");
            }
        }

        return expression;
    }

    static AttributeValue GetNextVersion(AttributeValue serializedVersion, MemberInfo versionProperty, IDynamoDBSerializer serializer) 
    {
        var number = serializedVersion.N;
        if (number != null)
        {
            var invariantCulture = CultureInfo.InvariantCulture;
            long integer;
            number = long.TryParse(number, NumberStyles.Any, invariantCulture, out integer)
                ? (integer + 1).ToString(invariantCulture)
                : (double.Parse(number, NumberStyles.Any, invariantCulture) + 1.0).ToString(invariantCulture);
            return new AttributeValue { N = number };
        }

        var type = GetUnderlyingVersionType(versionProperty.GetPropertyType());
        object autogeneratedVersion;

        if (type == typeof(Guid))
            autogeneratedVersion = Guid.NewGuid();
        else if (type == typeof(DateTime))
            autogeneratedVersion = DateTime.UtcNow;
        else if (type == typeof(DateTimeOffset))
            autogeneratedVersion = DateTimeOffset.UtcNow;
        else
            throw new InvalidOperationException(
                $"Can not automatically get next version for property {versionProperty.DeclaringType.FullName}.{versionProperty.Name}");

        return serializer.SerializeDynamoDBValue(autogeneratedVersion);
    }



    static Type GetUnderlyingVersionType(Type type) =>
        Nullable.GetUnderlyingType(type) ?? type;
    
    static AttributeValue GetDefaultSerializedVersionValue(MemberInfo versionProperty, IDynamoDBSerializer serializer) => 
        serializer.SerializeDynamoDBValue(GetUnderlyingVersionType(versionProperty.GetPropertyType()).CreateInstance());

    static bool IsEmptyVersionValue(object version, MemberInfo versionProperty) =>
        Equals(version, GetUnderlyingVersionType(versionProperty.GetPropertyType()).CreateInstance());

}
