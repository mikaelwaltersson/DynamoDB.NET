using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Amazon.DynamoDBv2.Model;
using Newtonsoft.Json;

namespace DynamoDB.Net.Serialization.Newtonsoft.Json;

public class DynamoDBJsonWriter : JsonWriter
{
    AttributeValue result;
    Stack<StackEntry> stack = [];
    SerializeDynamoDBValueTarget target;
    NullValueHandling nullValueHandling;

    public DynamoDBJsonWriter(SerializeDynamoDBValueTarget target, NullValueHandling nullValueHandling)
    {
        this.result = new AttributeValue();
        this.stack.Push(new StackEntry { Value = result });
        this.target = target;
        this.nullValueHandling = nullValueHandling;
    }

    public AttributeValue Result => result;


    AttributeValue NextValue() =>
        stack.Peek().BracketType switch
        {
            BracketType.Object => throw new InvalidOperationException(),

            BracketType.Array => NextArrayElement(),
            
            _ => (AttributeValue)stack.Pop().Value,
        };

    AttributeValue NextArrayElement()
    {
        var element = new AttributeValue();
        CurrentArray().Add(element);
        return element;
    }

    List<KeyValuePair<string, AttributeValue>> CurrentObject(bool pop = false)
    {
        var entry = pop ? stack.Pop() : stack.Peek();

        if (entry.BracketType != BracketType.Object)
            throw new InvalidOperationException();

        return (List<KeyValuePair<string, AttributeValue>>)entry.Value;
    }

    List<AttributeValue> CurrentArray(bool pop = false)
    {
        var entry = pop ? stack.Pop() : stack.Peek();

        if (entry.BracketType != BracketType.Array)
            throw new InvalidOperationException();

        return (List<AttributeValue>)entry.Value;
    }

    bool ShouldPersistNullOrEmpty => stack.Count == 0 && this.target == SerializeDynamoDBValueTarget.ExpressionConstant; 

    bool CurrentIsArray => stack.Count > 0 && stack.Peek().BracketType == BracketType.Array;

    static bool IsEmpty(AttributeValue value) => value.IsEmpty();

    static bool IsEmpty(KeyValuePair<string, AttributeValue> entry) => entry.Value.IsEmpty();

    public override void WriteStartObject()
    {
        base.WriteStartObject();
        stack.Push(
            new StackEntry 
            {
                Value = new List<KeyValuePair<string, AttributeValue>>(), 
                BracketType = BracketType.Object 
            });
    }

    public override void WritePropertyName(string name)
    {
        base.WritePropertyName(name);

        var value = new AttributeValue();
        CurrentObject().Add(new KeyValuePair<string, AttributeValue>(name, value));
        stack.Push(new StackEntry { Value = value });
    }

    public override void WriteEndObject()
    {
        base.WriteEndObject();

        var currentObject = CurrentObject(pop: true);

        currentObject.RemoveAll(IsEmpty);

        var nextValue = NextValue();

        if (currentObject.Count == 0 && !ShouldPersistNullOrEmpty)
            return;
        
        nextValue.M = currentObject.ToDictionary(entry => entry.Key, entry => entry.Value);
        nextValue.IsMSet = true;
    }

    public override void WriteStartArray()
    {
        base.WriteStartArray();
        stack.Push(
            new StackEntry 
            {
                Value = new List<AttributeValue>(), 
                BracketType = BracketType.Array
            });
    }

    public override void WriteEndArray() => WriteEndArray(false);

    public virtual void WriteEndArray(bool isSet)
    {
        base.WriteEndArray();

        var currentArray = CurrentArray(pop: true);
        
        currentArray.RemoveAll(IsEmpty);

        var nextValue = NextValue();
        
        if (currentArray.Count == 0 && !ShouldPersistNullOrEmpty)
            return;

        if (isSet && !CurrentIsArray)
        {
            if (currentArray.Count == 0)
            {
                WriteNull(nextValue);
                return;
            }

            if (currentArray.TrueForAll(element => element.B != null))
            {
                nextValue.BS = currentArray.Select(element => element.B).ToList();
                return;
            }
            
            if (currentArray.TrueForAll(element => element.N != null))
            {
                nextValue.NS = currentArray.Select(element => element.N).ToList();
                return;
            }

            if (currentArray.TrueForAll(element => element.S != null))
            {
                nextValue.SS = currentArray.Select(element => element.S).ToList();
                return;
            }

            throw new InvalidOperationException("Can not persist values of different types in set");
        }

        nextValue.L = currentArray;
        nextValue.IsLSet = true;
    }


    public override void WriteNull()
    {
        base.WriteNull();
        WriteNull(NextValue());
    }

    public override void WriteUndefined() => WriteNull();

    public override void WriteRaw(string json)
    {
        throw new NotSupportedException("WriteRaw not supported");
    }

    public override void WriteValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            WriteNull();
            return;
        }

        base.WriteValue(value);
        NextValue().S = value;
    }


    public override void WriteValue(int value) => WriteValue((long)value);
    public override void WriteValue(uint value) => WriteValue((long)value);

    public override void WriteValue(long value)
    {
        base.WriteValue(value);
        NextValue().N = value.ToString();
    }

    public override void WriteValue(ulong value) => WriteValue((long)value);

    public override void WriteValue(float value) => WriteValue((double)value);

    public override void WriteValue(double value)
    {
        base.WriteValue(value);
        NextValue().N = value.ToString(CultureInfo.InvariantCulture);
    }

    public override void WriteValue(bool value)
    {
        base.WriteValue(value);
        NextValue().BOOL = value;
    }

    public override void WriteValue(short value) => WriteValue((long)value);
    
    public override void WriteValue(ushort value) => WriteValue((long)value);

    public override void WriteValue(char value) => WriteValue(new string(value, 1));

    public override void WriteValue(byte value) => WriteValue((long)value);

    public override void WriteValue(sbyte value) => WriteValue((long)value);

    public override void WriteValue(decimal value)
    {
        base.WriteValue(value);
        NextValue().N = value.ToString(CultureInfo.InvariantCulture);
    }


    public override void WriteValue(DateTime value) => WriteValue(value.ToString("O"));

    public override void WriteValue(byte[] value)
    {
        if (value == null || value.Length == 0)
        {
            WriteNull();
            return;
        }

        base.WriteValue(value);
        NextValue().B = new MemoryStream(value);
    }

    public override void WriteValue(DateTimeOffset value) => WriteValue(value.ToString("O"));

    public override void WriteValue(Guid value) => WriteValue(value.ToString("D"));

    public override void WriteValue(TimeSpan value) => WriteValue(value.ToString("c"));

    public override void WriteValue(Uri value)=> WriteValue(value?.OriginalString);


    public override void Flush()
    {
    }

    void WriteNull(AttributeValue nextValue)
    {
        if (ShouldPersistNullOrEmpty || nullValueHandling == NullValueHandling.Include)
            nextValue.NULL = true;
    }

    enum BracketType 
    {
        None,
        Object,
        Array
    }

    struct StackEntry
    {
        public object Value;
        public BracketType BracketType;
    }
}
