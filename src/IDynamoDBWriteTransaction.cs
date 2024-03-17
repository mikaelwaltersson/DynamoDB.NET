using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DynamoDB.Net.Expressions;

namespace DynamoDB.Net;

public interface IDynamoDBWriteTransaction
{
    void Put<T>(
        T item, 
        Expression<Func<T, bool>> condition = null) where T : class;
    
    void Put<T>(
        T item, 
        Expression<Func<bool>> condition) where T : class =>
        Put(
            item, 
            condition?.ReplaceConstantWithParameter(item));
            
    void Update<T>(
        PrimaryKey<T> key, 
        Expression<Func<T, DynamoDBExpressions.UpdateAction>> update, 
        Expression<Func<T, bool>> condition = null, 
        object version = null) where T : class;

    void Update<T>(
        T item,
        Expression<Func<DynamoDBExpressions.UpdateAction>> update,
        Expression<Func<bool>> condition = null, 
        object version = null) where T : class => 
        Update(
            new PrimaryKey<T>(item),
            update.ReplaceConstantWithParameter(item),
            condition?.ReplaceConstantWithParameter(item),
            version);

    void Delete<T>(
        PrimaryKey<T> key, 
        Expression<Func<T, bool>> condition = null, 
        object version = null) where T : class;

    void Delete<T>(
        T item,
        Expression<Func<bool>> condition = null, 
        object version = null) where T : class => 
        Delete(
            new PrimaryKey<T>(item),
            condition?.ReplaceConstantWithParameter(item),
            version);

    void ConditionCheck<T>(
        PrimaryKey<T> key, 
        Expression<Func<T, bool>> condition, 
        object version = null) where T : class;

    void ConditionCheck<T>(
        T item, 
        Expression<Func<bool>> condition, 
        object version = null) where T : class =>
        ConditionCheck(
            new PrimaryKey<T>(item),
            condition.ReplaceConstantWithParameter(item),
            version);

    Task CommitAsync(CancellationToken cancellationToken = default);
}
