using System;

namespace DynamoDB.Net.Serialization;

public interface ITypeContractResolver
{
    ITypeContract ResolveContract(Type type);
}
