using System.Collections.Concurrent;
using DynamoDB.Net.Serialization.Converters;

namespace DynamoDB.Net.Serialization;

class GenericTypeInfo
{
    GenericTypeInfo(Type type)
    {
        if (ImplementsGenericTypeDefinition(type, typeof(IDictionary<,>), out var dictionaryArguments))
            DictionaryTypes = (dictionaryArguments[0], dictionaryArguments[1]);

        if (ImplementsGenericTypeDefinition(type, typeof(IList<>), out var listArguments))
            ListElementType = listArguments[0];

        if (ImplementsGenericTypeDefinition(type, typeof(ISet<>), out var setArguments))
            SetElementType = setArguments[0];

        if (ImplementsGenericTypeDefinition(type, typeof(PrimaryKey<>), out var primaryKeyArguments))
            PrimaryKeyItemType = primaryKeyArguments[0];

        if (ImplementsGenericTypeDefinition(type, typeof(IParsable<>), out var parsableArguments) && 
            type == parsableArguments[0] &&
            typeof(IFormattable).IsAssignableFrom(type))
            ParsableTypeConverter = (ParsableTypeConverter)Activator.CreateInstance(typeof(ParsableTypeConverter<>).MakeGenericType(type));
    }

    public bool IsDictionaryType => DictionaryTypes != null;

    public bool IsListType => ListElementType != null;

    public bool IsSetType => SetElementType != null;

    public bool IsPrimaryKeyType => PrimaryKeyItemType != null;

    public bool IsParsableType => ParsableTypeConverter != null;
        
    public (Type Key, Type Value)? DictionaryTypes { get; }

    public Type? ListElementType { get; }

    public Type? SetElementType { get; }

    public Type? PrimaryKeyItemType { get; }

    public ParsableTypeConverter? ParsableTypeConverter { get; set; }

    public static GenericTypeInfo Get(Type type) => cachedTypeInfo.GetOrAdd(type, static type => new(type));

    static readonly ConcurrentDictionary<Type, GenericTypeInfo> cachedTypeInfo = [];

    static bool ImplementsGenericTypeDefinition(Type type, Type genericTypeDefinition, out Type[] genericTypeArguments)
    {
        var genericType = 
            type.GetInterfaces()
                .Prepend(type)
                .FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == genericTypeDefinition);

        if (genericType != null)
        {
            genericTypeArguments = genericType.GetGenericArguments();
            return true;
        }
        else 
        {
            genericTypeArguments = [];
            return false;
        }
    }
}