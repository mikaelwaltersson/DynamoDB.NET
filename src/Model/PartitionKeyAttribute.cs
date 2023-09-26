using System;

namespace DynamoDB.Net.Model
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public sealed class PartitionKeyAttribute : Base.IndexKeyAttributeBase
    {
        public int GlobalSecondaryIndex
        {
            get { return GetOrdinalForType(IndexType.GlobalSecondaryIndex); }
            set { SetTypeAndOrdinal(IndexType.GlobalSecondaryIndex, value, MaxNumberOfGlobalSecondaryIndexes); }
        }  
    }
}