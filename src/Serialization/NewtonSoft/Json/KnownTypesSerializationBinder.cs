using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Serialization;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json;

public class KnownTypesSerializationBinder : ISerializationBinder
{
    Dictionary<Type, string> typeToName = [];
    Dictionary<string, Type> nameToType = [];

    public static readonly KnownTypesSerializationBinder Default = new KnownTypesSerializationBinder();

    public NamingStrategy NamingStrategy { get; set; } = new CamelCaseNamingStrategy();


    public void Register(Assembly assembly, string namespacePrefix = null, bool keepRelativeNamespace = false, char? typeDelimiter = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var type in assembly.GetExportedTypes())
        {
            var typeInfo = type.GetTypeInfo();
            var typeNamespace = type.Namespace ?? string.Empty;

            if (!typeInfo.IsClass || typeInfo.IsAbstract || typeInfo.IsGenericType)
                continue;

            if (namespacePrefix != null &&
                !typeNamespace.Equals(namespacePrefix) &&
                !typeNamespace.StartsWith(namespacePrefix + Type.Delimiter))
                continue;

            var name =
                keepRelativeNamespace
                ? string.Join(
                    (typeDelimiter ?? Type.Delimiter).ToString(), 
                    type.FullName.
                        Substring(string.IsNullOrEmpty(namespacePrefix) ? 0 : namespacePrefix.Length + 1).
                        Split(Type.Delimiter).
                        Select(segment => NamingStrategy?.GetPropertyName(segment, false) ?? segment)) 
                : NamingStrategy?.GetPropertyName(type.Name, false) ?? type.Name;

            Register(type, name);
        }
    }

    public void Register(Type type, string name)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(name);

        typeToName.Add(type, name);
        nameToType.Add(name, type);
    }

    public void BindToName(Type type, out string assemblyName, out string typeName)
    {
        ArgumentNullException.ThrowIfNull(type);

        assemblyName = null;
        if (!typeToName.TryGetValue(type, out typeName))
            throw new InvalidOperationException($"Not serializing $type property for unregistered type {type.FullName}");
    }

    public Type BindToType(string assemblyName, string typeName)
    {
        ArgumentNullException.ThrowIfNull(typeName);

        if (!string.IsNullOrEmpty(assemblyName))
            throw new ArgumentOutOfRangeException(nameof(assemblyName));

        if (!nameToType.TryGetValue(typeName, out Type type))
            throw new InvalidOperationException($"Unknown value for $type: '{typeName}'");

        return type;
    }
}
