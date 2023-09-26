using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DynamoDB.Net.Serialization
{
    static class JsonPropertyExtensions
    {
        static readonly ISet<Type> primitiveTypes =
            new HashSet<Type>
            {
                typeof(char),
                typeof(char?),
                typeof(bool),
                typeof(bool?),
                typeof(sbyte),
                typeof(sbyte?),
                typeof(short),
                typeof(short?),
                typeof(ushort),
                typeof(ushort?),
                typeof(int),
                typeof(int?),
                typeof(byte),
                typeof(byte?),
                typeof(uint),
                typeof(uint?),
                typeof(long),
                typeof(long?),
                typeof(ulong),
                typeof(ulong?),
                typeof(float),
                typeof(float?),
                typeof(double),
                typeof(double?),
                typeof(DateTime),
                typeof(DateTime?),
                typeof(DateTimeOffset),
                typeof(DateTimeOffset?),
                typeof(decimal),
                typeof(decimal?),
                typeof(Guid),
                typeof(Guid?),
                typeof(TimeSpan),
                typeof(TimeSpan?),
                typeof(Uri),
                typeof(string),
                typeof(byte[])
            };

        public static IEnumerable<T> GetAttributes<T>(this JsonProperty property) where T : Attribute =>
            property.AttributeProvider.GetAttributes(typeof(T), true).Cast<T>();

        public static bool HasAttribute<T>(this JsonProperty property) where T : Attribute =>
            property.GetAttributes<T>().Any();

        public static bool IsPrimitivePropertyType(this JsonProperty property) =>
            primitiveTypes.Contains(property.PropertyType) ||
            property.PropertyType.GetTypeInfo().IsEnum;

        public static void WriteProperty(this JsonProperty property, JsonWriter writer, object value, JsonSerializer serializer, bool skipWriteName = false)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            if (!skipWriteName)
                writer.WritePropertyName(property.PropertyName);

            var converter = property.Converter ?? serializer.ContractResolver.ResolveContract(property.PropertyType).Converter;
            if (converter != null)
            {
                converter.WriteJson(writer, value, serializer);
                return;
            }

            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            if (property.IsPrimitivePropertyType())
            {
                writer.WriteValue(value);
                return;
            }

            serializer.Serialize(writer, value);
        }

        public static Func<T, object> CompileGetter<T>(this JsonProperty property)
        {
            var parameter = Expression.Parameter(typeof(T), "item");
            var body =  Expression.Convert(Expression.PropertyOrField(parameter, property.UnderlyingName), typeof(object));

            return Expression.Lambda<Func<T, object>>(body, parameter).Compile();
        }

        public static JsonProperty Clone(this JsonProperty property, Required? required)
        {
            // TODO: speed up by not using reflection

            var clonedProperty = new JsonProperty();

            foreach (var p in typeof(JsonProperty).GetProperties(BindingFlags.Public|BindingFlags.Instance))
            {
                if (p.CanWrite && p.CanRead)
                    p.SetValue(clonedProperty, p.GetValue(property));
            }

            clonedProperty.Required = required ?? property.Required;

            return clonedProperty;
        }
    }
}