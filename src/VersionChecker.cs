using System;
using System.Collections.Generic;
using System.Globalization;

using Amazon.DynamoDBv2.Model;

using DynamoDB.Net.Expressions;
using DynamoDB.Net.Model;
using DynamoDB.Net.Serialization;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DynamoDB.Net
{
    public class VersionChecker : IDynamoDBItemEventHandler
    {
        public T OnItemDeserialized<T>(T item) where T : class => item;

        public Dictionary<string, AttributeValue> OnItemSerialized<T>(Dictionary<string, AttributeValue> item, ExpressionTranslationContext<T> translationContext) where T : class
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (translationContext == null)
                throw new ArgumentNullException(nameof(translationContext));

            var versionProperty = translationContext.TableDescription.VersionProperty;
            if (versionProperty != null)
            {
                var serializedVersion = item.GetValueOrDefault(versionProperty.PropertyName);
                if (serializedVersion == null || serializedVersion.NULL || serializedVersion.IsEmpty())
                    serializedVersion = GetDefaultSerializedVersionValue(versionProperty, translationContext.Serializer);

                item[versionProperty.PropertyName] = GetNextVersion(serializedVersion, versionProperty, translationContext.Serializer);
            }

            return item;
        }

        public string OnItemUpdateTranslated<T>(string expression, object version, ExpressionTranslationContext<T> translationContext) where T : class
        {
            if (translationContext == null)
                throw new ArgumentNullException(nameof(translationContext));

            var versionProperty = translationContext.TableDescription.VersionProperty;
            if (versionProperty != null && !Object.ReferenceEquals(version, DynamoDBExpressions.SkipVersionCheckAndUpdate))
            {
                var serializedVersion = translationContext.Serializer.SerializeDynamoDBValue(version);
                var isIncrementableVersion = GetDefaultSerializedVersionValue(versionProperty, translationContext.Serializer).N != null;

                var propertyAlias = translationContext.GetOrAddAttributeName(versionProperty.PropertyName);
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
            if (translationContext == null)
                throw new ArgumentNullException(nameof(translationContext));
            
            var versionProperty = translationContext.TableDescription.VersionProperty;
            if (versionProperty != null && !Object.ReferenceEquals(version, DynamoDBExpressions.SkipVersionCheckAndUpdate))
            {
                var serializedVersion = translationContext.Serializer.SerializeDynamoDBValue(version);
                var isEmptyInitialVersion = IsEmptyVersionValue(version, versionProperty);

                if (!serializedVersion.IsEmpty())
                {
                    var propertyAlias = translationContext.GetOrAddAttributeName(versionProperty.PropertyName);
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

        static AttributeValue GetNextVersion(AttributeValue serializedVersion, JsonProperty versionProperty, JsonSerializer serializer) 
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

            var type = GetUnderlyingVersionType(versionProperty.PropertyType);
            object autogeneratedVersion;

            if (type == typeof(Guid))
                autogeneratedVersion = Guid.NewGuid();
            else if (type == typeof(DateTime))
                autogeneratedVersion = DateTime.UtcNow;
            else if (type == typeof(DateTimeOffset))
                autogeneratedVersion = DateTimeOffset.UtcNow;
            else
                throw new InvalidOperationException(
                    $"Can not automatically get next version for property {versionProperty.DeclaringType.FullName}.{versionProperty.UnderlyingName}");

            return serializer.SerializeDynamoDBValue(autogeneratedVersion);
        }



        static Type GetUnderlyingVersionType(Type type) =>
            Nullable.GetUnderlyingType(type) ?? type;
        
        static AttributeValue GetDefaultSerializedVersionValue(JsonProperty versionProperty, JsonSerializer serializer) => 
            serializer.SerializeDynamoDBValue(GetUnderlyingVersionType(versionProperty.PropertyType).CreateInstance());

        static bool IsEmptyVersionValue(object version, JsonProperty versionProperty) =>
            Equals(version, GetUnderlyingVersionType(versionProperty.PropertyType).CreateInstance());

    }

}