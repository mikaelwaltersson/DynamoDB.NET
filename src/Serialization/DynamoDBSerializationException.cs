using System.Text.Json.Nodes;

namespace DynamoDB.Net.Serialization;

/// <summary>
/// The exception that is thrown when serialization or deserialization of DynamoDB values fails.
/// </summary>
/// <param name="message">The message that describes the error.</param>
public class DynamoDBSerializationException(string message) : Exception(message)
{
    public override string Message => 
        this.Path.Length > 0
            ? string.Concat(base.Message, " (", this.Path, ")") 
            : base.Message;

    /// <summary>
    /// The path to the DynamoDB document node where the deserialization failed.
    /// </summary>
    public string Path { get; private set; } = string.Empty;

    internal void PrependPath(string path)
    {
        var prefix = "$";
        var suffix = this.Path.TrimStart('$');

        this.Path =
            path.All(char.IsDigit)
                ? string.Concat(prefix, "[", path ,"]", suffix)
                : char.IsLetter(path.FirstOrDefault()) && path.All(char.IsLetterOrDigit)
                    ? string.Concat(prefix, ".", path, suffix)
                    : string.Concat(prefix, $"[{JsonValue.Create(path).ToJsonString()}]",suffix);
    }
}
