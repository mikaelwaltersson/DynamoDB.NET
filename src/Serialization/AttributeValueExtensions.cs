using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Serialization;

public static class AttributeValueExtensions
{
    public static bool IsEmpty(this AttributeValue value) =>
        value is 
        { 
            NULL: false, 
            IsBOOLSet: false, 
            S: null, 
            N: null, 
            B: null, 
            SS.Count: 0, 
            NS.Count: 0,
            BS.Count: 0,
            IsLSet: false,
            IsMSet: false 
        };

    public static bool IsEmptyOrNull(this AttributeValue value) =>
        value.NULL || value.IsEmpty();

    internal static AttributeValue EnsureIsMSet(this AttributeValue value) =>
        !value.IsMSet
        ? new AttributeValue { IsMSet = true } 
        : value;
}
