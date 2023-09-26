using System;

namespace DynamoDB.Net.Model
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public sealed class SortKeyAttribute : Base.IndexKeyAttributeBase
    {
        public int LocalSecondaryIndex
        {
            get { return GetOrdinalForType(IndexType.LocalSecondaryIndex); }
            set { SetTypeAndOrdinal(IndexType.LocalSecondaryIndex, value, MaxNumberOfLocalSecondaryIndexes); }
        }

        public int GlobalSecondaryIndex
        {
            get { return GetOrdinalForType(IndexType.GlobalSecondaryIndex); }
            set { SetTypeAndOrdinal(IndexType.GlobalSecondaryIndex, value, MaxNumberOfGlobalSecondaryIndexes); }
        }  
    }
}