using Newtonsoft.Json.Serialization;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json;

public interface IPropertyDecorator
{
    void Decorate(JsonProperty property, JsonContractResolver contractResolver);
}
