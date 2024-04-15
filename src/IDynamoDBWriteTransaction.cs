using System.Linq.Expressions;
using DynamoDB.Net.Expressions;

namespace DynamoDB.Net;

public interface IDynamoDBWriteTransaction
{
    IDynamoDBWriteTransaction Put<T>(
        T item, 
        Expression<Func<T, bool>>? condition = null) where T : class;
    
    IDynamoDBWriteTransaction Put<T>(
        T item, 
        Expression<Func<bool>> condition) where T : class =>
        Put(
            item, 
            condition?.ReplaceConstantWithParameter(item));
            
    IDynamoDBWriteTransaction Update<T>(
        PrimaryKey<T> key, 
        Expression<Func<T, DynamoDBExpressions.UpdateAction>> update, 
        Expression<Func<T, bool>>? condition = null, 
        object? version = null) where T : class;

    IDynamoDBWriteTransaction Update<T>(
        T item,
        Expression<Func<DynamoDBExpressions.UpdateAction>> update,
        Expression<Func<bool>>? condition = null, 
        object? version = null) where T : class => 
        Update(
            PrimaryKey.ForItem(item),
            update.ReplaceConstantWithParameter(item),
            condition?.ReplaceConstantWithParameter(item),
            version);

    IDynamoDBWriteTransaction Delete<T>(
        PrimaryKey<T> key, 
        Expression<Func<T, bool>>? condition = null, 
        object? version = null) where T : class;

    IDynamoDBWriteTransaction Delete<T>(
        T item,
        Expression<Func<bool>>? condition = null, 
        object? version = null) where T : class => 
        Delete(
            PrimaryKey.ForItem(item),
            condition?.ReplaceConstantWithParameter(item),
            version);

    IDynamoDBWriteTransaction ConditionCheck<T>(
        PrimaryKey<T> key, 
        Expression<Func<T, bool>> condition, 
        object? version = null) where T : class;

    IDynamoDBWriteTransaction ConditionCheck<T>(
        T item, 
        Expression<Func<bool>> condition, 
        object? version = null) where T : class =>
        ConditionCheck(
            PrimaryKey.ForItem(item),
            condition.ReplaceConstantWithParameter(item),
            version);

    Task CommitAsync(CancellationToken cancellationToken = default);
}
