namespace DynamoDB.Net.Serialization;

public abstract class NameTransform
{
    public static readonly NameTransform Default = new Transform(name => name);

    public static readonly NameTransform CamelCase = new Transform(NameTransformStringExtensions.ToCamelCase);

    public static readonly NameTransform SnakeCase = new Transform(NameTransformStringExtensions.ToSnakeCase);

    public static readonly NameTransform HyphenCase = new Transform(NameTransformStringExtensions.ToHyphenCase);

    public abstract string TransformName(string name);

    class Transform(Func<string, string> transform) : NameTransform
    {
        public override string TransformName(string name) => transform(name);
    }
}
