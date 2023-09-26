using System;
using System.Collections.Generic;

using DynamoDB.Net.Serialization;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DynamoDB.Net
{
    public class DynamoDBClientOptions
    {
        public string TableNamePrefix { get; set; } = string.Empty;
        
        public IDictionary<string, string> TableNameMappings { get; set; } = new Dictionary<string, string>();

        public bool DefaultConsistentRead { get; set; } = false;

        public Action<JsonSerializerSettings, IContractResolver> ConfigureSerializer { get; set; } = DefaultConfigureSerializer;

        public DynamoDBJsonWriterFlags JsonWriterFlags { get; set; }
        

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
}