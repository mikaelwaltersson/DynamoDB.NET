using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using DynamoDB.Net.Serialization.Newtonsoft.Json.Converters;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json;

public class JsonDynamoDBSerializer : IDynamoDBSerializer
{
    JsonSerializer serializer;
    ConcurrentDictionary<(Type, string), string> cachedSerializedPropertyNames = [];

    static JsonDynamoDBSerializer()
    {
        PrimaryKey.DefaultSerializer = new JsonDynamoDBSerializer(Options.Create(new JsonDynamoDBSerializerOptions()));
    }

    public JsonDynamoDBSerializer(IOptions<JsonDynamoDBSerializerOptions> options)
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

    public string GetSerializedPropertyName(MemberInfo property) =>
        cachedSerializedPropertyNames.GetOrAdd((property.DeclaringType, property.Name), _ => serializer.ContractResolver.GetJsonProperty(property).PropertyName);

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

    static JsonSerializerSettings GetSerializerSettings(IOptions<JsonDynamoDBSerializerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var settings = new JsonSerializerSettings();

        options.Value.ConfigureSerializer(settings, JsonContractResolver.DefaultDynamoDB);

        return settings;
    }



}
