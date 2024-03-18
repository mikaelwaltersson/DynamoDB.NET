using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DynamoDB.Net.Expressions;

namespace DynamoDB.Net;

public interface IDynamoDBClient
{
    Task<T> GetAsync<T>(
        PrimaryKey<T> key, 
        bool? consistentRead = false,
        CancellationToken cancellationToken = default) where T : class;
    
    Task<T> TryGetAsync<T>(
        PrimaryKey<T> key, 
        bool? consistentRead = false,
        CancellationToken cancellationToken = default) where T : class;

    Task<T> PutAsync<T>(
        T item, 
        Expression<Func<T, bool>> condition = null, 
        CancellationToken cancellationToken = default) where T : class;
    
    Task<T> PutAsync<T>(
        T item, 
        Expression<Func<bool>> condition, 
        CancellationToken cancellationToken = default) where T : class =>
        PutAsync(
            item,
            condition?.ReplaceConstantWithParameter(item),
            cancellationToken);

    Task<T> UpdateAsync<T>(
        PrimaryKey<T> key, 
        Expression<Func<T, DynamoDBExpressions.UpdateAction>> update, 
        Expression<Func<T, bool>> condition = null, 
        object version = null,
        CancellationToken cancellationToken = default) where T : class;

    Task<T> UpdateAsync<T>(
        T item,
        Expression<Func<DynamoDBExpressions.UpdateAction>> update,
        Expression<Func<bool>> condition = null, 
        object version = null,
        CancellationToken cancellationToken = default) where T : class => 
        UpdateAsync(
            PrimaryKey.ForItem(item),
            update.ReplaceConstantWithParameter(item),
            condition?.ReplaceConstantWithParameter(item),
            version,
            cancellationToken);

    Task DeleteAsync<T>(
        PrimaryKey<T> key, 
        Expression<Func<T, bool>> condition = null, 
        object version = null,
        CancellationToken cancellationToken = default) where T : class;

    Task DeleteAsync<T>(
        T item,
        Expression<Func<bool>> condition = null, 
        object version = null,
        CancellationToken cancellationToken = default) where T : class => 
        DeleteAsync(
            PrimaryKey.ForItem(item),
            condition?.ReplaceConstantWithParameter(item),
            version,
            cancellationToken);

    Task<IDynamoDBPartialResult<T>> ScanAsync<T>(
        Expression<Func<T, bool>> filter = null, 
        PrimaryKey<T> exclusiveStartKey = default, 
        int? limit = null,
        bool? consistentRead = false,
        (string, string) index = default,
        CancellationToken cancellationToken = default) where T : class;

    Task<IDynamoDBPartialResult<T>> QueryAsync<T>(
        Expression<Func<T, bool>> keyCondition,
        Expression<Func<T, bool>> filter = null, 
        PrimaryKey<T> exclusiveStartKey = default, 
        bool? scanIndexForward = null,
        int? limit = null,
        bool? consistentRead = false,
        (string, string) index = default,
        CancellationToken cancellationToken = default) where T : class;

    IDynamoDBWriteTransaction BeginWriteTransaction();
}
