using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Serialization;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json
{
    public class KnownTypesSerializationBinder : ISerializationBinder
    {
        Dictionary<Type, string> typeToName = new Dictionary<Type, string>();
        Dictionary<string, Type> nameToType = new Dictionary<string, Type>();

        public static readonly KnownTypesSerializationBinder Default = new KnownTypesSerializationBinder();

        public NamingStrategy NamingStrategy { get; set; } = new CamelCaseNamingStrategy();


        public void Register(Assembly assembly, string namespacePrefix = null, bool keepRelativeNamespace = false, char? typeDelimiter = null)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

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
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            typeToName.Add(type, name);
            nameToType.Add(name, type);
        }

        public void BindToName(Type type, out string assemblyName, out string typeName)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            assemblyName = null;
            if (!typeToName.TryGetValue(type, out typeName))
                throw new InvalidOperationException($"Not serializing $type property for unregistered type {type.FullName}");
        }

        public Type BindToType(string assemblyName, string typeName)
        {
            if (typeName == null)
                throw new ArgumentNullException(nameof(typeName));

            if (!string.IsNullOrEmpty(assemblyName))
                throw new ArgumentOutOfRangeException(nameof(assemblyName));

            Type type;
            if (!nameToType.TryGetValue(typeName, out type))
                throw new InvalidOperationException($"Unknown value for $type: '{typeName}'");

            return type;
        }
    }
}