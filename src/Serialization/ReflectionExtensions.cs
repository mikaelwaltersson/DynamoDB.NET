using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DynamoDB.Net.Serialization;

public static class ReflectionExtensions
{
    static readonly ConcurrentDictionary<Type, IEnumerable<MemberInfo>> cachedSerializablePropertiesAndFields = [];

    public static IEnumerable<MemberInfo> GetSerializablePropertiesAndFields(this Type type) =>
        cachedSerializablePropertiesAndFields.GetOrAdd(
            type,
            static type => 
                (type.BaseType?.GetSerializablePropertiesAndFields() ?? []).Concat(
            from 
                property in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            where 
                property is FieldInfo or PropertyInfo &&
                !property.GetCustomAttributes<CompilerGeneratedAttribute>().Any()
            orderby
                property.MetadataToken
            select
                property));

    public static Type GetPropertyType(this MemberInfo property) =>
        property switch
        {
            PropertyInfo propertyInfo => propertyInfo.PropertyType,

            FieldInfo fieldInfo => fieldInfo.FieldType, 

            _ => throw new InvalidOperationException($"Member is neither a property or a field: {property}")
        };

    public static bool HasCustomAttribute<T>(this MemberInfo property) where T : Attribute =>
        property.GetCustomAttributes<T>().Any();

    public static Func<TTarget, TValue> CompilePropertyGetter<TTarget, TValue>(this MemberInfo property)
    {
        var target = Expression.Parameter(typeof(TTarget), "target");
        var body = 
            Expression.Convert(
                Expression.PropertyOrField(
                    Expression.Convert(target, property.DeclaringType), 
                    property.Name), 
                typeof(TValue));

        return Expression.Lambda<Func<TTarget, TValue>>(body, target).Compile();
    }

    public static Func<TTarget, TValue, TValue> CompilePropertySetter<TTarget, TValue>(this MemberInfo property)
    {
        var target = Expression.Parameter(typeof(TTarget), "target");
        var value = Expression.Parameter(typeof(TValue), "value");
        var body = 
            Expression.Convert(
                Expression.Assign(
                    Expression.PropertyOrField(
                        Expression.Convert(target, property.DeclaringType),
                        property.Name),
                    Expression.Convert(value, property.GetPropertyType())),
                typeof(TValue));

        return Expression.Lambda<Func<TTarget, TValue, TValue>>(body, target, value).Compile();
    }
}
