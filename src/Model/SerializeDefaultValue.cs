using DynamoDB.Net.Serialization;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DynamoDB.Net.Model
{
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
}