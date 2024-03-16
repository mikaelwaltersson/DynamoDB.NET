using System;
using System.Linq.Expressions;
using DynamoDB.Net.Serialization;

namespace DynamoDB.Net.Model;

public static class TypePropertyExtensions
{
    public static Func<T, object> CompileGetter<T>(this ITypeContractProperty property)
    {
        var parameter = Expression.Parameter(typeof(T), "item");
        var body =  Expression.Convert(Expression.PropertyOrField(parameter, property.UnderlyingName), typeof(object));

        return Expression.Lambda<Func<T, object>>(body, parameter).Compile();
    }
}
