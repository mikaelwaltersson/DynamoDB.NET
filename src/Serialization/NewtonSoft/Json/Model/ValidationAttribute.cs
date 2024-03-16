using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json.Model
{
    public abstract class ValidationAttribute : PropertyDecoratorAttribute
    {
        public ValidationAttribute(SerializationTarget target = SerializationTarget.Both, bool skipSerializeCheck = false)
            : base(target)
        {
            SkipSerializeCheck = skipSerializeCheck;
        }

        public bool SkipSerializeCheck { get; }


        protected abstract bool IsValid(object target, string propertyName, object value, out string errorMessage);

        protected override void DecorateProperty(JsonProperty property, JsonContractResolver contractResolver)
        {
            var valueProvider = property.ValueProvider;

            property.ValueProvider = new ValidatingValueProvider(this, valueProvider, property.PropertyName);
            property.ShouldSerialize = target => AssertIsValid(target, property.PropertyName, valueProvider.GetValue(target), skipThrowOnError: SkipSerializeCheck);
        }

        bool AssertIsValid(object target, string propertyName, object value, bool skipThrowOnError = false)
        {
            if (!IsValid(target, propertyName, value, out var errorMessage))
            {
                if (skipThrowOnError)
                    return false;

                throw new JsonSerializationException(errorMessage);
            }

            return true;
        }

        class ValidatingValueProvider : IValueProvider
        {
            ValidationAttribute validateAttribute;
            IValueProvider valueProvider;
            string propertyName;

            public ValidatingValueProvider(ValidationAttribute attribute, IValueProvider valueProvider, string propertyName)
            {
                this.validateAttribute = attribute;
                this.valueProvider = valueProvider;
                this.propertyName = propertyName;
            }

            object IValueProvider.GetValue(object target) => valueProvider.GetValue(target);

            void IValueProvider.SetValue(object target, object value)
            {
                validateAttribute.AssertIsValid(target, propertyName, value);
                valueProvider.SetValue(target, value);
            }
        }
    }
}