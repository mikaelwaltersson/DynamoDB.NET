using Newtonsoft.Json.Serialization;

using DynamoDB.Net.Serialization;

namespace DynamoDB.Net.Model
{
    public class NotSerializedAttribute : PropertyDecoratorAttribute
    {
        public NotSerializedAttribute(SerializationTarget target = SerializationTarget.Both)
            : base(target)
        {
        }
        
        protected override void DecorateProperty(JsonProperty property, JsonContractResolver contractResolver)
        {
            property.Ignored = true;
        }
    }
}