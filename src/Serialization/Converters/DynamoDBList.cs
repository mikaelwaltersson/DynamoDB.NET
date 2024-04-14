
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Serialization.Converters;

static class DynamoDBList
{
    static readonly ConcurrentDictionary<Type, IListFactory> listFactories = new();
    
    static IListFactory CreateListFactory(Type type)
    {
        if (!IsSupportedType(type, out var elementType))
            throw new DynamoDBSerializationException($"List type not supported: {type}");

        if (type == typeof(IList<>).MakeGenericType(elementType) || type == typeof(List<>).MakeGenericType(elementType))
            return (IListFactory)Activator.CreateInstance(typeof(ListFactory<>).MakeGenericType(elementType));

        return (IListFactory)Activator.CreateInstance(typeof(GenericListFactory<,>).MakeGenericType(type, elementType));
    }

    public static bool IsSupportedType(Type type, [NotNullWhen(true)] out Type? elementType) =>
        IsSupportedType(type, GenericTypeInfo.Get(type), out elementType);

    internal static bool IsSupportedType(Type type, GenericTypeInfo typeInfo, [NotNullWhen(true)] out Type? elementType)
    {
        if (typeInfo.ListElementType != null && (type == typeof(IList<>).MakeGenericType(typeInfo.ListElementType) || Activator.IsConstructable(type)))
        {
            elementType = typeInfo.ListElementType;
            return true;
        }
        else
        {
            elementType = null;
            return false;
        }
    }

    public static object CreateList(List<AttributeValue> elements, Type listType, IDynamoDBSerializer serializer) =>
        listFactories.GetOrAdd(listType, CreateListFactory).CreateList(elements, serializer);

    public static object CreateList(Array array) =>
        ((IListFromArrayFactory)listFactories.GetOrAdd(typeof(List<>).MakeGenericType(array.GetType().GetElementType()!), CreateListFactory)).CreateList(array);


    interface IListFactory
    {
        public object CreateList(List<AttributeValue> elements, IDynamoDBSerializer serializer);
    }

    interface IListFromArrayFactory
    { 
        public object CreateList(Array array);
    }

    class ListFactory<TElement> : IListFactory, IListFromArrayFactory
    {
        public object CreateList(List<AttributeValue> elements, IDynamoDBSerializer serializer) =>
            new List<TElement>(elements.Select((value, i) => serializer.DeserializeDynamoDBValue<TElement>(value, pathElement: i.ToString())).Cast<TElement>());

        public object CreateList(Array array) =>
            new List<TElement>(array.Cast<TElement>());
    }

    class GenericListFactory<TList, TElement> : IListFactory where TList : IList<TElement>, new()
    {
        public object CreateList(List<AttributeValue> elements, IDynamoDBSerializer serializer)
        {
            var list = new TList();

            for (var i = 0; i < elements.Count; i++)
                list.Add(serializer.DeserializeDynamoDBValue<TElement>(elements[i], pathElement: i.ToString())!);

            return list;
        }
    }
}
