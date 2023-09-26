using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Amazon.DynamoDBv2.Model;

using DynamoDB.Net.Expressions;

namespace DynamoDB.Net
{
    public class DynamoDBItemEventHandlers : IDynamoDBItemEventHandler, IEnumerable<IDynamoDBItemEventHandler>
    {
        List<IDynamoDBItemEventHandler> handlerList;

        public DynamoDBItemEventHandlers(IEnumerable<IDynamoDBItemEventHandler> itemEventHandlers)
        {
            if (itemEventHandlers == null)
                throw new ArgumentNullException(nameof(itemEventHandlers));

            this.handlerList = itemEventHandlers.ToList();
        }



        T IDynamoDBItemEventHandler.OnItemDeserialized<T>(T item) => 
            this.Aggregate(item, (value, handler) => handler.OnItemDeserialized(value));

        Dictionary<string, AttributeValue> IDynamoDBItemEventHandler.OnItemSerialized<T>(Dictionary<string, AttributeValue> item, ExpressionTranslationContext<T> translationContext) =>
            this.Aggregate(item, (value, handler) => handler.OnItemSerialized(value, translationContext));

        string IDynamoDBItemEventHandler.OnItemUpdateTranslated<T>(string expression, object version, ExpressionTranslationContext<T> translationContext) =>
            this.Aggregate(expression, (value, handler) => handler.OnItemUpdateTranslated(value, version, translationContext));

        string IDynamoDBItemEventHandler.OnItemConditionTranslated<T>(string expression, object version, ExpressionTranslationContext<T> translationContext) =>
            this.Aggregate(expression, (value, handler) => handler.OnItemConditionTranslated(value, version, translationContext));


        IEnumerator<IDynamoDBItemEventHandler> IEnumerable<IDynamoDBItemEventHandler>.GetEnumerator() => 
            (handlerList ?? Enumerable.Empty<IDynamoDBItemEventHandler>()).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => 
            ((IEnumerable<IDynamoDBItemEventHandler>)this).GetEnumerator();
    }

}