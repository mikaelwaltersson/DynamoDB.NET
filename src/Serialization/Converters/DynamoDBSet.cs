
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using DynamoDB.Net.Model;

namespace DynamoDB.Net.Serialization.Converters;

static class DynamoDBSet
{
    static readonly ConcurrentDictionary<Type, ISetFactory> setFactories = new();
    
    static ISetFactory CreateSetFactory(Type type)
    {
        if (!IsSupportedType(type, out var elementType))
            throw new DynamoDBSerializationException($"Set type not supported: {type}");

        if (type == typeof(ISet<>).MakeGenericType(elementType) || type == typeof(SortedSet<>).MakeGenericType(elementType))
            return (ISetFactory)Activator.CreateInstance(typeof(SortedSetFactory<>).MakeGenericType(elementType));

        return (ISetFactory)Activator.CreateInstance(typeof(GenericSetFactory<,>).MakeGenericType(type, elementType));
    }

    public static bool IsSupportedType(Type type, [NotNullWhen(true)] out Type? elementType) =>
        IsSupportedType(type, GenericTypeInfo.Get(type), out elementType);

    internal static bool IsSupportedType(Type type, GenericTypeInfo typeInfo, [NotNullWhen(true)] out Type? elementType)
    {
        if (typeInfo.SetElementType != null && (type == typeof(ISet<>).MakeGenericType(typeInfo.SetElementType) || Activator.IsConstructable(type)))
        {
            elementType = typeInfo.SetElementType;
            return true;
        }
        else
        {
            elementType = null;
            return false;
        }
    }

    public static object CreateSet<T>(ICollection<T> values, Type setType, Func<T, Type, object> convertFromValue) =>
        setFactories.GetOrAdd(setType, CreateSetFactory).CreateSet(values, convertFromValue);

    public static object CreateSet(Array array) =>
        ((ISetFromArrayFactory)setFactories.GetOrAdd(typeof(SortedSet<>).MakeGenericType(array.GetType().GetElementType()!), CreateSetFactory)).CreateSet(array);

    interface ISetFactory
    {
        public object CreateSet<T>(ICollection<T> values, Func<T, Type, object> convertFromValue);
    }

    interface ISetFromArrayFactory
    {
        public object CreateSet(Array array);
    }

    class SortedSetFactory<TElement> : ISetFactory, ISetFromArrayFactory where TElement : notnull
    {
        public static IComparer<TElement?> Comparer { get; } = 
            typeof(TElement) == typeof(byte[]) 
                ? (IComparer<TElement?>)ByteArrayComparer.Default 
                : Comparer<TElement?>.Default;

        public object CreateSet<T>(ICollection<T> values, Func<T, Type, object> convertFromValue) =>
            new SortedSet<TElement>(values.Select(value => (TElement)convertFromValue(value, typeof(TElement))), Comparer);

        public object CreateSet(Array array) =>
            new SortedSet<TElement>(array.Cast<TElement>());
    }

    class GenericSetFactory<TSet, TElement> : ISetFactory where TSet : ISet<TElement>, new() where TElement : notnull
    {
        public object CreateSet<T>(ICollection<T> values, Func<T, Type, object> convertFromValue)
        {
            var set = new TSet();

            foreach (var value in values)
                set.Add((TElement)convertFromValue(value, typeof(TElement)));

            return set;
        }
    }
}
