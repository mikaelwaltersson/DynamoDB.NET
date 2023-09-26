using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;

namespace DynamoDB.Net.Serialization
{
    public static class CastConvert
    {
        static ConcurrentDictionary<Tuple<Type, Type>, Func<object, object>> compiledCastTo = 
            new ConcurrentDictionary<Tuple<Type, Type>, Func<object, object>>();

        static Func<object, object> CompileCastTo(Tuple<Type, Type> args)
        {
            var fromType = args.Item1;
            var toType = args.Item2;

            if (fromType == toType)
                return value => value;

            var parameter = Expression.Parameter(typeof(object), "value");
            try
            {
                var casts = new[] { fromType, toType, typeof(object) };
                var compiledCast = 
                    Expression.
                        Lambda<Func<object, object>>(casts.Aggregate((Expression)parameter, Expression.Convert), parameter).
                        Compile();

                return compiledCast;
            }
            catch (InvalidOperationException ex)
            {
                return value => NoCoercionOperatorIsDefined(ex);
            }
        }



        static object NoCoercionOperatorIsDefined(InvalidOperationException ex)
        {
            throw ex;
        }

        public static object CastTo<T>(this T value, Type objectType) => 
            compiledCastTo.GetOrAdd(Tuple.Create(typeof(T), objectType), CompileCastTo)(value);

        public static object CastTo(this object value, Type objectType) => 
            compiledCastTo.GetOrAdd(Tuple.Create(value?.GetType() ?? typeof(object), objectType), CompileCastTo)(value);
    }
}