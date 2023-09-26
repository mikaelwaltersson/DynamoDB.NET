using System;

namespace DynamoDB.Net.Exceptions
{
    public class ItemNotFoundException : Exception
    {
        public ItemNotFoundException(string message)
            : base(message)
        {
        }
    }

    public class ItemNotFoundException<T> : Exception where T : class
    {
        public ItemNotFoundException(PrimaryKey<T> key, string tableName)
            : base($"Item with key {key} not found in table {tableName}")
        {
        }
    }
}