using Newtonsoft.Json.Serialization;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json.Model
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