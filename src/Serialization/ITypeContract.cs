using System;
using System.Collections.Generic;

namespace DynamoDB.Net.Serialization;

public interface ITypeContract
{
    Type UnderlyingType { get; }

    IEnumerable<ITypeContractProperty> Properties { get; }
}
