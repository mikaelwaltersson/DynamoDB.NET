namespace DynamoDB.Net.Model;

public abstract class IndexKeyAttribute : Attribute
{
    public const int MaxNumberOfLocalSecondaryIndexes = 5;

    public const int MaxNumberOfGlobalSecondaryIndexes = 20;

    protected void SetOrdinalForIndexType(IndexType indexType, int value, int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, maximum);

        if (IndexType != default)
            throw new InvalidOperationException("Multiple index types specified");

        IndexType = indexType;
        Ordinal = value;
    }

    public string? IndexName { get; init; }

    public IndexType IndexType { get; private set; }
    
    public int Ordinal { get; private set; }   
}
