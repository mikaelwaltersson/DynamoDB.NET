using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json.Model;

public class SerializeDefaultValueAttribute : PropertyDecoratorAttribute
{
    public SerializeDefaultValueAttribute(SerializationTarget target = SerializationTarget.Both) 
        : base(target)
    {
    }

    protected override void DecorateProperty(JsonProperty property, JsonContractResolver contractResolver)
    {
        property.DefaultValueHandling = DefaultValueHandling.Include;
    }
}
