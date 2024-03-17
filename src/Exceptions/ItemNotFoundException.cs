using System;

namespace DynamoDB.Net.Exceptions;

public class ItemNotFoundException(string message) : Exception(message)
{
}

public class ItemNotFoundException<T>(PrimaryKey<T> key, string tableName) 
    : Exception($"Item with key {key} not found in table {tableName}") where T : class
{
}
