using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DynamoDB.Net.Serialization;

static class ReflectionExtensions
{
    static readonly ConcurrentDictionary<Type, IReadOnlyList<MemberInfo>> cachedSerializablePropertiesAndFields = [];

    public static IReadOnlyList<MemberInfo> GetSerializablePropertiesAndFields(this Type type) =>
        cachedSerializablePropertiesAndFields.GetOrAdd(
            type,
            static type =>
                type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(property =>
                        property is FieldInfo or PropertyInfo &&
                        !property.GetCustomAttributes<CompilerGeneratedAttribute>().Any())
                    .OrderBy(property => property.MetadataToken)
                    .ToList()
                    .AsReadOnly());

    public static Type GetPropertyType(this MemberInfo property) =>
        property switch
        {
            PropertyInfo propertyInfo => propertyInfo.PropertyType,

            FieldInfo fieldInfo => fieldInfo.FieldType, 

            _ => throw new DynamoDBSerializationException($"Member is neither a property or a field: {property}")
        };

    public static (Type DeclaringType, string Name) AsSimplePropertyReference(this MemberInfo property) =>
        (property.DeclaringType ?? throw new ArgumentOutOfRangeException(nameof(property)), property.Name);

    public static bool HasCustomAttribute<T>(this MemberInfo property) where T : Attribute =>
        property.GetCustomAttributes<T>().Any();

    public static bool HasCustomAttribute<T>(this MemberInfo property, Func<T, bool> predicate) where T : Attribute =>
        property.GetCustomAttributes<T>().Any(predicate);

    public static Func<TTarget, TValue> CompilePropertyGetter<TTarget, TValue>(this MemberInfo property) =>
        property switch 
        {
            PropertyInfo { GetMethod: not null } propertyInfo =>
                CompilePropertyGetter<TTarget, TValue>(
                    targetType: property.DeclaringType ?? typeof(TTarget),
                    memberAccess: target => Expression.Property(target, propertyInfo)),

            FieldInfo fieldInfo =>
                CompilePropertyGetter<TTarget, TValue>(
                    targetType: property.DeclaringType ?? typeof(TTarget),
                    memberAccess: target => Expression.Field(target, fieldInfo)),
                
            _ => 
                target => 
                    throw new DynamoDBSerializationException($"Not a readable property or field: '{(property.DeclaringType ?? typeof(TTarget)).FullName}.{property.Name}'")
        };

    public static Action<TTarget, TValue> CompilePropertySetter<TTarget, TValue>(this MemberInfo property) =>
        property switch 
        {
            PropertyInfo { SetMethod: not null } propertyInfo =>
                CompilePropertySetter<TTarget, TValue>(
                    targetType: property.DeclaringType ?? typeof(TTarget),
                    memberType: propertyInfo.PropertyType,
                    memberAccess: target => Expression.Property(target, propertyInfo)),

            FieldInfo { IsInitOnly: false } fieldInfo =>
                CompilePropertySetter<TTarget, TValue>(
                    targetType: property.DeclaringType ?? typeof(TTarget),
                    memberType: fieldInfo.FieldType,
                    memberAccess: target => Expression.Field(target, fieldInfo)),
                
            _ => 
                (target, value) => 
                    throw new DynamoDBSerializationException($"Not a writable property or field: '{(property.DeclaringType ?? typeof(TTarget)).FullName}.{property.Name}'")
        };
 
    public static object? Invoke(this Expression expression) =>
        ExpressionInvoker.Get(expression.Type).Invoke(expression);

    public static bool IsDefaultValueTypeValue(this Type type, object? value) =>
        type.IsValueType && 
        Nullable.GetUnderlyingType(type) == null && 
        Activator.CreateInstance(type).Equals(value);

    public static Type UnwrapNullableType(this Type type) => Nullable.GetUnderlyingType(type) ?? type;

    static Func<TTarget, TValue> CompilePropertyGetter<TTarget, TValue>(Type targetType, Func<Expression, Expression> memberAccess)
    {
        var target = Expression.Parameter(typeof(TTarget), "target");
        var body = Expression.Convert(memberAccess(Expression.Convert(target, targetType)), typeof(TValue));
        
        return Expression.Lambda<Func<TTarget, TValue>>(body, target).Compile();
    }

    static Action<TTarget, TValue> CompilePropertySetter<TTarget, TValue>(Type targetType, Type memberType, Func<Expression, Expression> memberAccess)
    {
        var target = Expression.Parameter(typeof(TTarget), "target");
        var value = Expression.Parameter(typeof(TValue), "value");
        var body = 
            Expression.Assign(
                memberAccess(Expression.Convert(target, targetType)),
                Expression.Convert(value, memberType));

        return Expression.Lambda<Action<TTarget, TValue>>(body, target, value).Compile();
    }

    abstract class ExpressionInvoker
    {
        public abstract object? Invoke(Expression expression);
        
        static readonly ConcurrentDictionary<Type, ExpressionInvoker> cachedInvokers = [];

        public static ExpressionInvoker Get(Type type) => 
            cachedInvokers.GetOrAdd(type, static type => (ExpressionInvoker)
                Activator.CreateInstance(typeof(ExpressionInvoker<>).MakeGenericType(type)));
    }

    class ExpressionInvoker<T> : ExpressionInvoker
    {
        public override object? Invoke(Expression expression) => 
            Expression.Lambda<Func<T>>(expression).Compile()();
    }
}
