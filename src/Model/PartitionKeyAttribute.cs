using System;

namespace DynamoDB.Net.Model;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class PartitionKeyAttribute : IndexKeyAttribute
{
    public int GlobalSecondaryIndex
    {
        get => IndexType == IndexType.GlobalSecondaryIndex ? Ordinal : -1;
        init => SetOrdinalForIndexType(IndexType.GlobalSecondaryIndex, value, MaxNumberOfGlobalSecondaryIndexes);
    }  
}
