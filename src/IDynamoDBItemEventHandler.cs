using Amazon.DynamoDBv2.Model;
using DynamoDB.Net.Expressions;

namespace DynamoDB.Net;

public interface IDynamoDBItemEventHandler
{
    T OnItemDeserialized<T>(T item) where T : class;
    
    Dictionary<string, AttributeValue> OnItemSerialized<T>(Dictionary<string, AttributeValue> item, ExpressionTranslationContext translationContext) where T : class;
    
    string OnItemUpdateTranslated<T>(string expression, object? version, ExpressionTranslationContext translationContext) where T : class;
    
    string? OnItemConditionTranslated<T>(string? expression, object? version, ExpressionTranslationContext translationContext) where T : class;
}
