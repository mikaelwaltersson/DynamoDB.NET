using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace DynamoDB.Net.Serialization;

public static class CastConvert
{
    static readonly ConcurrentDictionary<Tuple<Type, Type>, Func<object?, object?>> compiledCastTo = new();

    static Func<object?, object?> CompileCastTo(Tuple<Type, Type> args)
    {
        var fromType = args.Item1;
        var toType = args.Item2;

        var parameter = Expression.Parameter(typeof(object), "value");
        try
        {
            var compiledCast = 
                Expression.
                    Lambda<Func<object?, object?>>(
                        Enumerable.Aggregate(
                            [fromType, toType, typeof(object)], 
                            (Expression)parameter, 
                            Expression.Convert), 
                        parameter).
                    Compile();

            return compiledCast;
        }
        catch (InvalidOperationException ex)
        {
            return value => throw ex;
        }
    }


    public static object? CastTo<T>(this T? value, Type type) =>
        typeof(T) == type 
            ? value 
            : compiledCastTo.GetOrAdd(Tuple.Create(typeof(T), type), CompileCastTo)(value);

    public static object? CastTo(this object? value, Type type) =>
        value == null || value.GetType() == type 
            ? value 
            : compiledCastTo.GetOrAdd(Tuple.Create(value?.GetType() ?? typeof(object), type), CompileCastTo)(value);
}
