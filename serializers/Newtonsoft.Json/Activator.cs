using System;

namespace DynamoDB.Net.Serialization;

static class Activator
{
    // TODO: Make faster with compiled lambdas

    public static object CreateInstance(this Type type) => System.Activator.CreateInstance(type)!;
    
    public static object CreateInstance(this Type type, Type[] argTypes, object[] argValues) => System.Activator.CreateInstance(type, argValues)!;

    public static object CreateInstance<T>(this Type type, T arg) => type.CreateInstance([typeof(T)], [arg!]);
}
