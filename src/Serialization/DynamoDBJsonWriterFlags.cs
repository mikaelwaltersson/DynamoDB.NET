using System;

namespace DynamoDB.Net.Serialization
{
    [Flags]
    public enum DynamoDBJsonWriterFlags
    {
        PersistEmptyObjects = 1 << 0,
        PersistEmptyArrays = 1 << 2,
        PersistNullValues = 1 << 3,

        PersistAll = 
            PersistEmptyObjects | 
            PersistEmptyArrays | 
            PersistNullValues
    }
}