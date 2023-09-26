using Newtonsoft.Json.Serialization;

namespace DynamoDB.Net.Serialization
{
    public interface IPropertyDecorator
    {
        void Decorate(JsonProperty property, JsonContractResolver contractResolver);
    }
}