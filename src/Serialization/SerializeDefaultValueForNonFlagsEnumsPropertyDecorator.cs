using System;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DynamoDB.Net.Serialization
{
    public class SerializeDefaultValueForNonFlagsEnumsPropertyDecorator : IPropertyDecorator
    {
        public void Decorate(JsonProperty property, JsonContractResolver contractResolver)
        {
            if (property.PropertyType.IsEnum && !property.PropertyType.GetCustomAttributes(typeof (FlagsAttribute), false).Any())
                property.DefaultValueHandling = DefaultValueHandling.Include;
        }
    }
}