namespace DynamoDB.Net.Model;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
public sealed class SortKeyAttribute : IndexKeyAttribute
{
    public int LocalSecondaryIndex
    {
        get => IndexType == IndexType.LocalSecondaryIndex ? Ordinal : -1;
        init => SetOrdinalForIndexType(IndexType.LocalSecondaryIndex, value, MaxNumberOfLocalSecondaryIndexes);
    }

    public int GlobalSecondaryIndex
    {
        get => IndexType == IndexType.GlobalSecondaryIndex ? Ordinal : -1;
        init => SetOrdinalForIndexType(IndexType.GlobalSecondaryIndex, value, MaxNumberOfGlobalSecondaryIndexes);
    }  
}
