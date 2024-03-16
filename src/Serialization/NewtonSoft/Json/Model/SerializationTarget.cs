using System;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json.Model
{
    [Flags]
    public enum SerializationTarget
    {
        Json = 0x01,
        DynamoDB = 0x02,
        
        Both = Json | DynamoDB
    }
}