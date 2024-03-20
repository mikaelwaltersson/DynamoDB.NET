namespace DynamoDB.Net;

public abstract class ItemNotFoundException(string message) : Exception(message)
{
    public abstract string TableName { get; }

    public abstract IPrimaryKey Key { get; }
}

public class ItemNotFoundException<T>(PrimaryKey<T> key, string tableName) 
    : ItemNotFoundException($"Item with key {key} not found in table {tableName}") where T : class
{
    public override string TableName => tableName;

    public override IPrimaryKey Key => key;
}
