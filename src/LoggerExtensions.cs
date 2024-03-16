using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

using Microsoft.Extensions.Logging;

namespace DynamoDB.Net
{
    static class LoggerExtensions
    {
        public static void InvokeBegin(this ILogger logger, AmazonDynamoDBRequest request) => invokeBegin(logger, new ToJson(request), null);
        public static void InvokeSuccess(this ILogger logger, AmazonWebServiceResponse response) => invokeSuccess(logger, new ToJson(response), null);
        public static void InvokeFailed(this ILogger logger, AmazonDynamoDBException error) => invokeFailed(logger, error.ErrorCode, null);

        static readonly Action<ILogger, object, Exception> invokeBegin;
        static readonly Action<ILogger, object, Exception> invokeSuccess;
        static readonly Action<ILogger, string, Exception> invokeFailed;

        static LoggerExtensions()
        {
            var eventId = 1;

            invokeBegin =
                LoggerMessage.Define<object>(
                    eventId: eventId++,
                    logLevel: LogLevel.Debug,
                    formatString: "DynamoDB request:\n{Request}");

            invokeSuccess =
                LoggerMessage.Define<object>(
                    eventId: eventId++,
                    logLevel: LogLevel.Debug,
                    formatString: "DynamoDB response:\n{Response}");

            invokeFailed =
                LoggerMessage.Define<string>(
                    eventId: eventId++,
                    logLevel: LogLevel.Error,
                    formatString: "Error invoking DynamoDB operation: {ErrorCode}");
        }

        class ToJson(object obj)
        {
            static readonly JsonSerializerOptions serializerOptions;

            static ToJson()
            { 
                serializerOptions = new()
                { 
                    DefaultIgnoreCondition = 
                        JsonIgnoreCondition.WhenWritingNull | 
                        JsonIgnoreCondition.WhenWritingDefault,

                    WriteIndented = true,

                    Converters = { new AttributeValueJsonConverter() }
                };
                
                serializerOptions.MakeReadOnly(populateMissingResolver: true);
            }

            public override string ToString() => 
                JsonSerializer.Serialize(obj, serializerOptions);
        }
    }

    class AttributeValueJsonConverter : JsonConverter<AttributeValue>
    {
        [ExcludeFromCodeCoverage]
        public override AttributeValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => 
            throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, AttributeValue value, JsonSerializerOptions options)
        {
            if (value.NULL)
            {
                writer.WriteStartObject();
                writer.WriteBoolean("NULL", true);
                writer.WriteEndObject();
            }
            else if (value.IsBOOLSet)
            {
                writer.WriteStartObject();
                writer.WriteBoolean("BOOL", value.BOOL);
                writer.WriteEndObject();
            }
            else if (value.S is not null)
            {
                writer.WriteStartObject();
                writer.WriteString("S", value.S);
                writer.WriteEndObject();
            }
            else if (value.N is not null)
            {
                writer.WriteStartObject();
                writer.WriteNumber("N", decimal.Parse(value.N));
                writer.WriteEndObject();
            }
            else if (value.B is not null)
            {
                writer.WriteStartObject();
                writer.WriteBase64String("B", value.B.ToArray());
                writer.WriteEndObject();
            }
            else if (value.SS is { Count: > 0 })
            {
                writer.WriteStartObject();
                writer.WritePropertyName("SS");
                writer.WriteStartArray();
                foreach (var entry in value.SS)
                    writer.WriteStringValue(entry);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            else if (value.NS is { Count: > 0 })
            {
                writer.WriteStartObject();
                writer.WritePropertyName("NS");
                writer.WriteStartArray();
                foreach (var entry in value.NS)
                    writer.WriteNumberValue(decimal.Parse(entry));
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            else if (value.BS is { Count: > 0 })
            {
                writer.WriteStartObject();
                writer.WritePropertyName("BS");
                writer.WriteStartArray();
                foreach (var entry in value.BS)
                    writer.WriteBase64StringValue(entry.ToArray());
                writer.WriteEndArray();
                writer.WriteEndObject();
            }      
            else if (value.IsLSet)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("L");
                writer.WriteStartArray();
                foreach (var entry in value.L)
                    JsonSerializer.Serialize(writer, entry, options);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            else if (value.IsMSet)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("M");
                writer.WriteStartObject();
                foreach (var entry in value.M)
                {
                    writer.WritePropertyName(entry.Key);
                    JsonSerializer.Serialize(writer, entry.Value, options);
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
        }
    }
}