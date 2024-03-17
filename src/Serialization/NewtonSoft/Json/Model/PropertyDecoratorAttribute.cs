using System;
using Newtonsoft.Json.Serialization;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json.Model;

[AttributeUsage(AttributeTargets.Property)]
public abstract class PropertyDecoratorAttribute : Attribute, IPropertyDecorator
{
    SerializationTarget target;

    public PropertyDecoratorAttribute(SerializationTarget target = SerializationTarget.Both)
    {
        this.target = target;
    }
    
    void IPropertyDecorator.Decorate(JsonProperty property, JsonContractResolver contractResolver)
    {
        if (target.HasFlag(contractResolver.SerializationTarget))
            DecorateProperty(property, contractResolver);
    }

    protected abstract void DecorateProperty(JsonProperty property, JsonContractResolver contractResolver);
}
