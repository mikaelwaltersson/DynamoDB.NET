namespace DynamoDB.Net.Serialization;

static class Activator
{
    // TODO: Make faster with compiled lambdas

    public static object CreateInstance(this Type type) => System.Activator.CreateInstance(type)!;
}
