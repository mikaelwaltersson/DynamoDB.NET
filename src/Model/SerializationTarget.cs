using System;

namespace DynamoDB.Net.Model
{
    [Flags]
    public enum SerializationTarget
    {
        Json = 0x01,
        DynamoDB = 0x02,
        
        Both = Json | DynamoDB
    }
}