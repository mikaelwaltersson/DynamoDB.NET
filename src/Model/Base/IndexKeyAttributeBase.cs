using System;

namespace DynamoDB.Net.Model.Base;

public abstract class IndexKeyAttributeBase : Attribute
{
    public const int MaxNumberOfLocalSecondaryIndexes = 5;
    public const int MaxNumberOfGlobalSecondaryIndexes = 20;

    protected int GetOrdinalForType(IndexType type)
    {
        if (type != Type)
            throw new InvalidOperationException($"Key is not type {type}");

        return Ordinal;
    }

    protected void SetTypeAndOrdinal(IndexType type, int ordinal, int maximum)
    {
        if (ordinal < 0 && ordinal >= maximum)
            throw new ArgumentOutOfRangeException(nameof(ordinal), $"{Type} ordinal must be between 0 and {maximum - 1}");

        if (Type != default)
            throw new InvalidOperationException("Multiple index types specified");

        Type = type;
        Ordinal = ordinal;
    }

    public string Name { get; set; }
    public IndexType Type { get; private set; }
    public int Ordinal { get; private set; }   
}
