using System;
using System.Collections.Generic;

namespace DynamoDB.Net.Serialization;

public interface ITypeContractProperty
{
    string PropertyName { get; }

    Type PropertyType { get; }

    string UnderlyingName  { get; }
    
    Type DeclaringType { get; }

    bool HasAttribute<T>() where T : Attribute;
    
    IEnumerable<T> GetAttributes<T>() where T : Attribute;
}
