using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net.Serialization
{
    static class AttributeValueExtensions
    {
        public static bool IsEmpty(this AttributeValue value) =>
            value.B == null &&
            !value.IsBOOLSet &&
            (value.BS == null || value.BS.Count == 0) &&
            !value.IsLSet &&
            !value.IsMSet &&
            value.N == null &&
            (value.NS == null || value.NS.Count == 0) &&
            !value.NULL &&
            value.S == null &&
            (value.SS == null || value.SS.Count == 0);


        public static AttributeValue EmptyAsNull(this AttributeValue value) =>
            value.IsEmpty()
            ? new AttributeValue { NULL = true }
            : value;

        public static AttributeValue EnsureIsMSet(this AttributeValue value) =>
            !value.IsMSet
            ? new AttributeValue { IsMSet = true } 
            : value;

    }
}