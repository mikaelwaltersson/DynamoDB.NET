using System.Text.Json.Nodes;

namespace DynamoDB.Net.Serialization;

public class DynamoDBSerializationException(string message) : Exception(message)
{
    public override string Message => 
        this.Path.Length > 0
            ? string.Concat(base.Message, " (", this.Path, ")") 
            : base.Message;

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
