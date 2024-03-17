using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json.Converters;

public class DynamoDBSetJsonConverter : JsonConverter
{
    public DynamoDBSetJsonConverter(JsonConverter itemConverter, IComparer comparer = null)
    {
        ArgumentNullException.ThrowIfNull(itemConverter);

        this.ItemConverter = itemConverter;
        this.Comparer = comparer;
    }

    public DynamoDBSetJsonConverter(Type elementType, Func<string, object> itemParser = null, IComparer comparer = null)
    {
        ArgumentNullException.ThrowIfNull(elementType);

        this.ElementType = elementType;
        this.ItemParser = itemParser;
        this.Comparer = comparer;
    }

    public Type ElementType { get; }
    public JsonConverter ItemConverter { get; }
    public Func<string, object> ItemParser { get; }
    public IComparer Comparer { get; }

    public override bool CanConvert(Type objectType) => 
        ItemConverter != null
            ? GetElementTypeForSetWithItemConverter(objectType) != null
            : typeof(ISet<>).MakeGenericType(ElementType).IsAssignableFrom(objectType);

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var tokenType = reader.TokenType;

        if (tokenType == JsonToken.Null)
            return null;

        reader.ExpectTokenType(JsonToken.StartArray);

        var elementType = this.ElementType ?? GetElementTypeForSetWithItemConverter(objectType);
        var array = (IList)typeof(List<>).MakeGenericType(elementType).CreateInstance();

        while (reader.Read() && reader.TokenType != JsonToken.EndArray)
        {
            var element =
                ItemConverter != null
                    ? ItemConverter.ReadJson(reader, elementType, null, serializer)
                    : reader.Value;

            if (element == null || element.Equals(null))
                continue;

            element =
                ItemParser != null
                    ? ItemParser((string)element)
                    : element.CastTo(elementType);

            array.Add(element);
        }

        return CreateSet(elementType, array);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (IsNullOrEmptySet(value))
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartArray();

        foreach (var element in (IEnumerable)value)
        {
            if (element == null || element.Equals(null))
                continue;

            if (ItemConverter != null)
                ItemConverter.WriteJson(writer, element, serializer);
            else
                writer.WriteValue(element);
        }

        if (writer is DynamoDBJsonWriter)
            ((DynamoDBJsonWriter)writer).WriteEndArray(isSet: true);
        else
            writer.WriteEndArray();
    }

    public virtual object CreateSet(Type elementType, IEnumerable elements) =>
        typeof(SortedSet<>).
            MakeGenericType(elementType).
            CreateInstance(
                [ 
                    typeof(IEnumerable<>).MakeGenericType(elementType), 
                    typeof(IComparer<>).MakeGenericType(elementType) 
                ],
                [
                    elements, 
                    Comparer
                ]);


    static bool IsNullOrEmptySet(object value) =>
        (value == null || !((IEnumerable)value).GetEnumerator().MoveNext());

    Type GetElementTypeForSetWithItemConverter(Type objectType) =>
        objectType.GetInterfaces().Append(objectType).
            Where(type => type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(ISet<>)).
            Select(type => type.GenericTypeArguments[0]).
            FirstOrDefault(ItemConverter.CanConvert);

}
