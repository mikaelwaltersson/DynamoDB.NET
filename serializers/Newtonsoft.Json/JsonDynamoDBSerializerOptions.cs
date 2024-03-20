using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json;

public class JsonDynamoDBSerializerOptions
{
    public Action<JsonSerializerSettings, IContractResolver> ConfigureSerializer { get; set; } = DefaultConfigureSerializer;
    
    public static void DefaultConfigureSerializer(JsonSerializerSettings settings) => DefaultConfigureSerializer(settings, JsonContractResolver.Default);

    public static void DefaultConfigureSerializer(JsonSerializerSettings settings, IContractResolver contractResolver)
    {
        settings.ContractResolver = contractResolver;
        settings.DateParseHandling = DateParseHandling.None;
        settings.NullValueHandling = NullValueHandling.Ignore;
        settings.DefaultValueHandling = DefaultValueHandling.Ignore;
        settings.MissingMemberHandling = MissingMemberHandling.Ignore;
        settings.TypeNameHandling = TypeNameHandling.Auto;
        settings.MetadataPropertyHandling = MetadataPropertyHandling.ReadAhead;
        settings.SerializationBinder = KnownTypesSerializationBinder.Default;
    }
}
