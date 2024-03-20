using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace DynamoDB.Net.Serialization;

static class Activator
{
    static readonly ConcurrentDictionary<Type, bool> cachedIsConstructable = new();
    static readonly ConcurrentDictionary<Type, Func<object>> compiledCreateInstance = new();

    public static bool IsConstructable(Type type) =>
        cachedIsConstructable.GetOrAdd(
            type, 
            static type =>
                (type.GetConstructors().Any(constructor => constructor.GetParameters() is []) || type.IsValueType) &&
                type is { 
                    IsGenericMethodParameter: false, 
                    IsGenericTypeParameter: false,
                    IsGenericTypeDefinition: false, 
                    IsAbstract: false 
                });

    public static T CreateInstance<T>() =>
        (T)CreateInstance(typeof(T));
        
    public static object CreateInstance(Type type) => 
        compiledCreateInstance.GetOrAdd(
            type, 
            static type => 
                Expression
                    .Lambda<Func<object>>(Expression.Convert(Expression.New(type), typeof(object)))
                    .Compile())();
}
