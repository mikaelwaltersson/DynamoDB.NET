using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace DynamoDB.Net
{
    public interface IDynamoDBWriteTransaction
    {
        void Put<T>(
            T item, 
            Expression<Func<T, bool>> condition = null) where T : class;
        
        void Update<T>(
            PrimaryKey<T> key, 
            Expression<Func<T, DynamoDBExpressions.UpdateAction>> update, 
            Expression<Func<T, bool>> condition = null, 
            object version = null) where T : class;

        void Delete<T>(
            PrimaryKey<T> key, 
            Expression<Func<T, bool>> condition = null, 
            object version = null) where T : class;

        void ConditionCheck<T>(
            PrimaryKey<T> key, 
            Expression<Func<T, bool>> condition, 
            object version = null) where T : class;

        Task CommitAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}