using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Amazon.Runtime;

using Microsoft.Extensions.Logging;

namespace DynamoDB.Net;

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

        public override string ToString()
        {
            try
            {
                return JsonSerializer.Serialize(obj, serializerOptions);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
