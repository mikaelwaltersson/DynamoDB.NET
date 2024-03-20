using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Amazon.Runtime;

using Microsoft.Extensions.Logging;

namespace DynamoDB.Net;

static partial class LoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "DynamoDB request: {Request}")]
    public static partial void InvokeBegin(this ILogger<DynamoDBClient> logger, FormatAsJson<AmazonDynamoDBRequest> request);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "DynamoDB response: {Response}")]
    public static partial void InvokeSuccess(this ILogger<DynamoDBClient> logger, FormatAsJson<AmazonWebServiceResponse> response);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Error invoking DynamoDB operation: {ErrorCode}")]
    public static partial void InvokeFailed(this ILogger<DynamoDBClient> logger, Exception ex, string errorCode);

    [ExcludeFromCodeCoverage]
    internal class FormatAsJson<T> where T : notnull
    {
        readonly T obj;

        FormatAsJson(T obj) => this.obj = obj;

        static readonly JsonSerializerOptions serializerOptions;

        static FormatAsJson()
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

        public static implicit operator FormatAsJson<T>(T obj) => new(obj);

        public override string ToString()
        {
            try
            {
                return $"({obj.GetType().Name}) {JsonSerializer.Serialize(obj, obj.GetType(), serializerOptions)}";
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
