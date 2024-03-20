namespace DynamoDB.Net.Serialization;

public abstract class DynamoDBObjectTypeNameResolver
{
    public static readonly DynamoDBObjectTypeNameResolver Default = new DefaultResolver();

    public abstract string Attribute { get; }

    public abstract Type GetObjectType(string typeName);

    public abstract string GetTypeName(Type objectType);

    class DefaultResolver : DynamoDBObjectTypeNameResolver
    {
        public override string Attribute => "$type";

        public override Type GetObjectType(string typeName) =>
            Type.GetType(typeName, throwOnError: false) ?? throw new DynamoDBSerializationException($"Can not resolve type from '{typeName}'");

        public override string GetTypeName(Type objectType) => 
            objectType.AssemblyQualifiedName ?? throw new DynamoDBSerializationException($"Can not resolve type name for '{objectType}'");
    }
}
