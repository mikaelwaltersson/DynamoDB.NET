using System.Collections;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json.Model
{
    public class RequiredAttribute : ValidationAttribute
    {
        public RequiredAttribute(SerializationTarget target = SerializationTarget.Both, bool skipSerializeCheck = false)
            : base(target, skipSerializeCheck)
        {
        }

        override protected void DecorateProperty(JsonProperty property, JsonContractResolver contractResolver)
        {
            property.Required = Required.Always;
            property.DefaultValueHandling = DefaultValueHandling.Include;

            base.DecorateProperty(property, contractResolver);
        }        

        override protected bool IsValid(object target, string propertyName, object value, out string errorMessage)
        {
            if (value == null ||
                value.Equals(null) ||
                (value is string && ((string)value).Length == 0) ||
                (value is ICollection && ((ICollection)value).Count == 0))
            {
                errorMessage = $"Required property '{propertyName}' expects a value but got null or empty.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}