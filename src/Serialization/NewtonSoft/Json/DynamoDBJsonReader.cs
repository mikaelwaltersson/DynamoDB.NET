using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Amazon.DynamoDBv2.Model;
using Newtonsoft.Json;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json;
 
public class DynamoDBJsonReader : JsonReader
{
    IEnumerator<Token> tokenEnumerator;

    public DynamoDBJsonReader(AttributeValue target)
    {
        if (target == null)
            throw new NullReferenceException(nameof(target));

        tokenEnumerator = GetTokens(target).GetEnumerator();
    }

    public override bool Read()
    {
        if (!tokenEnumerator.MoveNext())
            return false;

        SetToken(tokenEnumerator.Current);
        return true;
    }

    void SetToken(Token token) => base.SetToken(token.Type, token.Value);


    static IEnumerable<Token> GetTokens(AttributeValue target)
    {
        if (target.B != null)
        {
            yield return Token.WithValue(JsonToken.Bytes, target.B.ToArray());
            yield break;
        }

        if (target.IsBOOLSet)
        {
            yield return Token.WithValue(JsonToken.Boolean, target.BOOL);
            yield break;
        }

        if (target.BS != null && target.BS.Count > 0)
        {
            yield return JsonToken.StartArray;
            foreach (var element in target.BS)
                yield return Token.WithValue(JsonToken.Bytes, element.ToArray());
            yield return JsonToken.EndArray;
            yield break;
        }

        if (target.IsLSet)
        {
            yield return JsonToken.StartArray;
            foreach (var token in target.L.SelectMany(GetTokens))
                yield return token;
            yield return JsonToken.EndArray;
            yield break;
        }

        if (target.IsMSet)
        {
            yield return JsonToken.StartObject;
            foreach (var entry in target.M)
            {
                yield return Token.WithValue(JsonToken.PropertyName, entry.Key);
                foreach (var token in GetTokens(entry.Value))
                    yield return token;
            }
            yield return JsonToken.EndObject;
            yield break;
        }

        if (target.N != null)
        {
            yield return Token.WithIntegerOrFloat(target.N);
            yield break;
        }

        if (target.NS != null && target.NS.Count > 0)
        {
            yield return JsonToken.StartArray;
            foreach (var element in target.NS)
                yield return Token.WithIntegerOrFloat(element);
            yield return JsonToken.EndArray;
            yield break;
        }

        if (target.NULL)
        {
            yield return JsonToken.Null;
            yield break;
        }

        if (target.S != null)
        {
            yield return Token.WithValue(JsonToken.String, target.S);
            yield break;
        }

        if (target.SS != null && target.SS.Count > 0)
        {
            yield return JsonToken.StartArray;
            foreach (var element in target.SS)
                yield return Token.WithValue(JsonToken.String, element);
            yield return JsonToken.EndArray;
            yield break;
        }

        throw new InvalidOperationException("Invalid / Empty Attribute Value");
    }

    struct Token
    {
        public JsonToken Type;

        public object Value;

        public static Token WithValue(JsonToken type, object value) => new() { Type = type, Value = value };
        
        public static Token WithIntegerOrFloat(string number) =>        
            long.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out long integer)
                ? WithValue(JsonToken.Integer, integer)
                : WithValue(JsonToken.Float, decimal.Parse(number, NumberStyles.Any, CultureInfo.InvariantCulture));

        public static implicit operator Token(JsonToken type) => new() { Type = type };
    }
}
