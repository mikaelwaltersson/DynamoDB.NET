using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace DynamoDB.Net;

[ExcludeFromCodeCoverage]
public static class DynamoDBExpressions
{
    [TranslatesTo("{0} = {1}")]
    public static bool EqualTo<T>(T operand1, T operand2) => DynamoDBMethod<bool>();

    [TranslatesTo("{0} <> {1}")]
    public static bool NotEqualTo<T>(T operand1, T operand2) => DynamoDBMethod<bool>();
    
    [TranslatesTo("{0} < {1}")]
    public static bool LessThan<T>(T operand1, T operand2) => DynamoDBMethod<bool>();
    
    [TranslatesTo("{0} <= {1}")]
    public static bool LessThanOrEqualTo<T>(T operand1, T operand2) => DynamoDBMethod<bool>();

    [TranslatesTo("{0} > {1}")]
    public static bool GreaterThan<T>(T operand1, T operand2) => DynamoDBMethod<bool>();

    [TranslatesTo("{0} >= {1}")]
    public static bool GreaterThanOrEqualTo<T>(T operand1, T operand2) => DynamoDBMethod<bool>();

    [TranslatesTo("{0} BETWEEN {1} AND {2}")]
    public static bool Between<T>(T operand1, T operand2, T c) => DynamoDBMethod<bool>();

    [TranslatesTo("{0} IN ({1})", hasParams: true)]
    public static bool In<T>(T operand1, params T[] operands) => DynamoDBMethod<bool>();

    [TranslatesTo("attribute_exists({0})")]
    public static bool AttributeExists<T>(T path) => DynamoDBMethod<bool>();
    
    [TranslatesTo("attribute_not_exists({0})")]
    public static bool AttributeNotExists<T>(T path) => DynamoDBMethod<bool>();

    [TranslatesTo("attribute_type({0}, {1})")]
    public static bool AttributeType<T>(T path, DynamoDBType type) => DynamoDBMethod<bool>();
    
    [TranslatesTo("begins_with({0}, {1})")]
    public static bool BeginsWith<T>(T path, T substr) => DynamoDBMethod<bool>();
    
    [TranslatesTo("contains({0}, {1})")]
    public static bool Contains<T>(T path, T operand) => DynamoDBMethod<bool>();

    [TranslatesTo("contains({0}, {1})")]
    public static bool Contains<T>(IEnumerable<T> path, T operand) => DynamoDBMethod<bool>();

    [TranslatesTo("size({0})")]
    public static int Size<T>(T path) => DynamoDBMethod<int>();

    [TranslatesTo("if_not_exists({0}, {1})")]
    public static T IfNotExists<T>(T path, T operand) => DynamoDBMethod<T>();

    [TranslatesTo("list_append({0}, {1})", arrayConstantKind: ArrayConstantKind.List)]
    public static IEnumerable<T> ListAppend<T>(IEnumerable<T>? operand1, IEnumerable<T>? operand2) => DynamoDBMethod<IEnumerable<T>>();

    [TranslatesTo("ADD {0} {1}")]
    public static UpdateAction Add<T>(T path, T value) => DynamoDBMethod<UpdateAction>();

    [TranslatesTo("ADD {0} {1}", arrayConstantKind: ArrayConstantKind.Set)]
    public static UpdateAction Add<T>(IEnumerable<T>? path, IEnumerable<T>? value) => DynamoDBMethod<UpdateAction>();

    [TranslatesTo("SET {0} = {1}", arrayConstantKind: ArrayConstantKind.FromFirstOperandType)]
    public static UpdateAction Set<T>(T path, T value) => DynamoDBMethod<UpdateAction>();

    [TranslatesTo("REMOVE {0}")]
    public static UpdateAction Remove<T>(T path) => DynamoDBMethod<UpdateAction>();

    [TranslatesTo("REMOVE {0}", hasParams: true)]
    public static UpdateAction Remove(params object[] paths) => DynamoDBMethod<UpdateAction>();

    [TranslatesTo("DELETE {0} {1}", arrayConstantKind: ArrayConstantKind.Set)]
    public static UpdateAction Delete<T>(IEnumerable<T>? path, IEnumerable<T>? value) => DynamoDBMethod<UpdateAction>();


    [TranslatesTo("")]
    public static UpdateAction NoOp<T>(T path) => DynamoDBMethod<UpdateAction>();

    [TranslatesTo("")]
    public static T Constant<T>(T value) => value;

    public abstract class RawExpression
    {
        internal readonly string expression;
        internal Dictionary<string, string>? names;
        internal Dictionary<string, object>? values;

        public RawExpression(string expression)
        {
            ArgumentNullException.ThrowIfNull(expression);
            
            this.expression = expression;
        }
        
        public Dictionary<string, string> Names => names ??= [];
        
        public Dictionary<string, object> Values => values ??= [];
    }

    public sealed class RawExpression<T>(string expression) : RawExpression(expression) where T : class
    {
        public static implicit operator Expression<Func<T, bool>>(RawExpression<T> expression) => expression.ToExpression<bool>();
        
        public static implicit operator Expression<Func<T, UpdateAction>>(RawExpression<T> expression) => expression.ToExpression<UpdateAction>();

        Expression<Func<T, TResult>> ToExpression<TResult>() => _ => (TResult)(object)this;
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal class TranslatesTo(string format, bool hasParams = false, ArrayConstantKind arrayConstantKind = default) : Attribute
    {
        public string Format { get; } = format;

        public bool HasParams { get; } = hasParams;

        public ArrayConstantKind ArrayConstantKind { get; } = arrayConstantKind;
    }

    internal enum ArrayConstantKind
    {
        Unspecified,

        List,

        Set,

        FromFirstOperandType
    }

    public struct UpdateAction
    {
        public static UpdateAction operator &(UpdateAction operand1, UpdateAction operand2) => DynamoDBMethod<UpdateAction>();
    }

    static T DynamoDBMethod<T>([CallerMemberName] string? caller = null) => 
        throw new InvalidOperationException($"Invalid usage of DynamoDBExpressions.{caller}(...), are all argument non-parameter values?");
}
