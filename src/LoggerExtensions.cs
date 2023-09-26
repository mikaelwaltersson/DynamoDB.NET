using System;
using System.Threading;

using Amazon.DynamoDBv2;
using Amazon.Runtime;

using DynamoDB.Net.Serialization;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

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

        class ToJson
        {
            static Lazy<JsonSerializerSettings> settings =
                new Lazy<JsonSerializerSettings>(
                    () => new JsonSerializerSettings { ContractResolver = JsonContractResolver.Default }, 
                    LazyThreadSafetyMode.PublicationOnly);

            object obj;

            public ToJson(object obj)
            {
                this.obj = obj;
            }

            public override string ToString() => JsonConvert.SerializeObject(obj, Formatting.Indented, settings.Value);
        }
    }
}