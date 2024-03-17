using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using DynamoDB.Net.Serialization.Newtonsoft.Json.Converters;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using TableDescription = DynamoDB.Net.Model.TableDescription;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json;

public class JsonDynamoDbSerializer : IDynamoDBSerializer
{
    JsonSerializer serializer;

    static JsonDynamoDbSerializer()
    {
        PrimaryKey.DefaultSerializer = new JsonDynamoDbSerializer(Options.Create(new JsonDynamoDbSerializerOptions()));
    }

    public JsonDynamoDbSerializer(IOptions<JsonDynamoDbSerializerOptions> options)
    {
        this.serializer = JsonSerializer.Create(GetSerializerSettings(options));
    }


    public object DeserializeDynamoDBValue(AttributeValue value, Type objectType)
    {
        ArgumentNullException.ThrowIfNull(value);

        return serializer.Deserialize(new DynamoDBJsonReader(value), objectType);
    }

    public AttributeValue SerializeDynamoDBValue(object value, Type objectType, SerializeDynamoDBValueFlags flags = 0)
    {
        var attributeValue = new AttributeValue();

        serializer.Serialize(new DynamoDBJsonWriter(attributeValue, flags), value, objectType);

        return attributeValue;
    }

    public TableDescription GetTableDescription(Type type) =>
        ((JsonContractResolver)serializer.ContractResolver).GetTableDescription(type);

    public string GetSerializedPropertyName(MemberInfo member)
    {
        var memberContract = serializer.ContractResolver.ResolveContract(member.DeclaringType) as JsonObjectContract;
        var memberProperty = 
            GetPrimaryKeyProperty(member) ??
            memberContract?.Properties?.SingleOrDefault(property => property.UnderlyingName == member.Name);

        return memberProperty?.PropertyName;
    }

    public bool TryCreateDynamoDBSet(Type elementType, IEnumerable values, [NotNullWhen(true)] out object dynamoDBSet)
    {
        var setType = typeof(ISet<>).MakeGenericType(elementType);
        var converter = serializer.ContractResolver.ResolveContract(setType).Converter;
        if (converter is DynamoDBSetJsonConverter dynamoDBSetJsonConverter)
        {
            dynamoDBSet = dynamoDBSetJsonConverter.CreateSet(elementType, values);
            return true;
        }
        else 
        {
            dynamoDBSet = null;
            return false;
        }
    }

    static JsonSerializerSettings GetSerializerSettings(IOptions<JsonDynamoDbSerializerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var settings = new JsonSerializerSettings();

        options.Value.ConfigureSerializer(settings, JsonContractResolver.DefaultDynamoDB);

        return settings;
    }

    JsonProperty GetPrimaryKeyProperty(MemberInfo member)
    {
        var primaryKeyUnderlyingType = PrimaryKey.GetUnderlyingType(member.DeclaringType);
        
        if (primaryKeyUnderlyingType != null)
        {
            var tableDescription = GetTableDescription(primaryKeyUnderlyingType);
            switch (member.Name)
            {
                case nameof(PrimaryKey<object>.PartitionKey):
                    return JsonContractResolver.UnwrapJsonProperty(tableDescription.PartitionKeyProperty);

                case nameof(PrimaryKey<object>.SortKey):
                    return JsonContractResolver.UnwrapJsonProperty(tableDescription.SortKeyProperty);
            }
        }

        return null;
    }

}
