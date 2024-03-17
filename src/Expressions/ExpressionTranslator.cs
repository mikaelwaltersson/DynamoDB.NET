using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using DynamoDB.Net.Model;
using DynamoDB.Net.Serialization;

namespace DynamoDB.Net.Expressions;

public static class ExpressionTranslator
{
    public static string GetIndexName<T>(this (string, string) index, ExpressionTranslationContext<T> context) where T : class
    {
        ArgumentNullException.ThrowIfNull(context);

        var (partitionKeyName, sortKeyName) = index;
        if (partitionKeyName == null)
            return null;

        var partitionKey = typeof(T).GetMember(partitionKeyName, BindingFlags.Instance | BindingFlags.Public).FirstOrDefault();
        if (partitionKey == null)
            throw new ArgumentOutOfRangeException(nameof(index), $"'{partitionKeyName}' is not a valid instance member of '{typeof(T).FullName}'");

        var sortKey = (MemberInfo)null;
        if (sortKeyName != null) 
        {
            sortKey = typeof(T).GetMember(sortKeyName, BindingFlags.Instance | BindingFlags.Public).FirstOrDefault();
            if (sortKey == null)
                throw new ArgumentOutOfRangeException(nameof(index), $"'{sortKey}' is not a valid instance member of '{typeof(T).FullName}'");
        }

        return context.TableDescription.GetIndexName(partitionKey, sortKey);
    }

    public static string GetIndexName<T>(this Expression<Func<T, bool>> expression, ExpressionTranslationContext<T> context) where T : class
    {
        ArgumentNullException.ThrowIfNull(context);

        expression = expression.Apply(new ReduceExpressionVisitor());

        var partitionKey = expression.FindFirst(IsMemberOfTypeWithAttribute<T, PartitionKeyAttribute>, GetMemberOf<T>);
        var sortKey = expression.FindFirst(IsMemberOfTypeWithAttribute<T, SortKeyAttribute>, GetMemberOf<T>);

        return
            partitionKey != null
            ? context.TableDescription.GetIndexName(partitionKey, sortKey)
            : null;
    }

    public static string Translate<T>(this Expression<Func<T, bool>> expression, ExpressionTranslationContext<T> context) where T : class => 
        expression.Body.Translate(context);

    public static string Translate<T>(this Expression<Func<T, DynamoDBExpressions.UpdateAction>> expression, ExpressionTranslationContext<T> context) where T : class => 
        expression.Apply(new HandleEmptyValuesForUpdateVisitor(context.IsSerializedToEmpty)).Body.Translate(context);

    public static Expression<Func<T, TResult>> ReplaceConstantWithParameter<T, TResult>(this Expression<Func<TResult>> expression, T value, [CallerArgumentExpression(nameof(value))] string parameterName = null)
    {
        var parameter = Expression.Parameter(typeof(T), parameterName);

        return Expression.Lambda<Func<T, TResult>>(
            expression.Body.FindReplace(
                find: expression => expression.IsReferenceTo(value),
                replace: parameter),
            expression.Parameters.Append(parameter));
    }

    public static Expression FindReplace(this Expression expression, Func<Expression, bool> find, Expression replace) =>
        new FindReplaceVisitor(find, replace).Visit(expression);

    static string SurroundWithParenthesesIfNeeded(string expression)
    {
        return
            expression.IndexOf(" OR ", StringComparison.OrdinalIgnoreCase) >= 0 ||
            expression.IndexOf(" AND ", StringComparison.OrdinalIgnoreCase) >= 0
                ? $"({expression})" :
                expression;
    }

    public static string AppendUpdate(string expression, string action, string arguments)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(arguments);

        if (string.IsNullOrEmpty(expression))
            return $"{action} {arguments}";

        var i =
            expression.StartsWith($"{action} ", StringComparison.OrdinalIgnoreCase)
                ? 0
                : expression.IndexOf($"\n{action} ", StringComparison.OrdinalIgnoreCase);

        if (i < 0)
            return $"{expression}\n{action} {arguments}";

        i = expression.IndexOf("\n", i + action.Length + 1, StringComparison.Ordinal);
        if (i < 0)
            i = expression.Length;

        return $"{expression.Substring(0, i)}, {arguments}{expression.Substring(i)}";
    }

    public static string AppendCondition(string expression, string condition, string binaryOperator)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(binaryOperator);


        if (string.IsNullOrEmpty(expression))
            return condition;

        var left = SurroundWithParenthesesIfNeeded(expression);
        var right = SurroundWithParenthesesIfNeeded(condition);

        return $"{left} {binaryOperator} {right}";
    }

    static string Translate<T>(this Expression expression, ExpressionTranslationContext<T> context) where T : class
    {
        ArgumentNullException.ThrowIfNull(context);

        expression = expression.TryReduceExpression();

        switch (expression.NodeType)
        {
            case ExpressionType.Convert:
            case ExpressionType.ConvertChecked:
                return ((UnaryExpression)expression).Operand.Translate(context);

            case ExpressionType.Call:
                return ((MethodCallExpression)expression).TranslateCall(context);

            case ExpressionType.Constant:
                return ((ConstantExpression)expression).TranslateConstant(context);

            case ExpressionType.MemberAccess:
                return ((MemberExpression)expression).TranslateMember(context);

            case ExpressionType.Index:
            case ExpressionType.ArrayIndex:
                return ((IndexExpression)expression).TranslateIndex(context);

            case ExpressionType.Add:
            case ExpressionType.AddChecked:
                return ((BinaryExpression)expression).TranslateBinary("+", context);

            case ExpressionType.Subtract:
            case ExpressionType.SubtractChecked:
                return ((BinaryExpression)expression).TranslateBinary("-", context);

            case ExpressionType.Equal:
                return ((BinaryExpression)expression).TranslateBinary("=", context);

            case ExpressionType.NotEqual:
                return ((BinaryExpression)expression).TranslateBinary("<>", context);

            case ExpressionType.LessThan:
                return ((BinaryExpression)expression).TranslateBinary("<", context);

            case ExpressionType.LessThanOrEqual:
                return ((BinaryExpression)expression).TranslateBinary("<=", context);

            case ExpressionType.GreaterThan:
                return ((BinaryExpression)expression).TranslateBinary(">", context);

            case ExpressionType.GreaterThanOrEqual:
                return ((BinaryExpression)expression).TranslateBinary(">=", context);

            case ExpressionType.AndAlso:
                return ((BinaryExpression)expression).TranslateBinary("AND", context);

            case ExpressionType.OrElse:
                return ((BinaryExpression)expression).TranslateBinary("OR", context);

            case ExpressionType.Not:
                return ((UnaryExpression)expression).TranslateUnary("NOT", context);

            case ExpressionType.And:
                return ((BinaryExpression)expression).CombineUpdateActions(context);
        }

        return Unsupported(expression);
    }

    static string TranslateCall<T>(this MethodCallExpression expression, ExpressionTranslationContext<T> context) where T : class
    {
        var method = expression.Method;
        var translatesTo = method.GetCustomAttribute<DynamoDBExpressions.TranslatesTo>();

        if (expression.Object != null && method.Name.Equals("get_Item", StringComparison.Ordinal))
            return expression.Object.TranslateIndex(expression.Arguments, context);

        if (method.DeclaringType == typeof(DynamoDBExpressions) && translatesTo != null)
        {
            var arguments = expression.Arguments.ToArray();
            var translatedArguments = arguments.TranslateArguments(context);

            if (translatesTo.HasParams)
            {
                var paramsArgument = arguments.Last();
                var translatedParamsArgument = (
                    (paramsArgument as NewArrayExpression)?.Expressions?.AsEnumerable() ??
                    ((paramsArgument.TryReduceExpression() as ConstantExpression)?.Value as IEnumerable)?.Cast<object>()?.Select(Expression.Constant))?.
                    TranslateArguments(context);

                if (translatedParamsArgument == null)
                    return Unsupported(expression);

                translatedArguments =
                    translatedArguments.
                        Take(arguments.Length - 1).
                        Append(string.Join(", ", translatedParamsArgument));
            }

            return string.Format(translatesTo.Format, translatedArguments.Cast<object>().ToArray());
        }

        return Unsupported(expression);
    }

    static string TranslateConstant<T>(this ConstantExpression expression, ExpressionTranslationContext<T> context) where T : class
    {
        var value = expression.Value;

        if (value is DynamoDBExpressions.RawExpression)
        {
            var raw = (DynamoDBExpressions.RawExpression)value;
            context.Add(raw);
            return raw.expression;
        }

        if (value is Array && value is not byte[])
        {
            var elementType = value.GetType().GetElementType();
            if (context.Serializer.TryCreateDynamoDBSet(elementType, (IEnumerable)value, out var dynamoDBSet))
                value = dynamoDBSet;
        }

        var serializedValue = context.Serializer.SerializeDynamoDBValue(value, expression.Type, context.SerializeFlags | SerializeDynamoDBValueFlags.PersistAll);

        return context.GetOrAddAttributeValue(serializedValue);
    }

    static string TranslateMember<T>(this MemberExpression expression, ExpressionTranslationContext<T> context) where T : class
    {
        var member = expression.Member;
        var memberPropertyName = context.Serializer.GetSerializedPropertyName(member);

        if (memberPropertyName == null)
            throw new InvalidOperationException($"No property name defined for member {member.Name} of type {member.DeclaringType.FullName}");

        return expression.Expression.TranslateMember(memberPropertyName, context);
    }

    static string TranslateMember<T>(this Expression expression, string memberName, ExpressionTranslationContext<T> context) where T : class
    {
        if (Identifier.NeedToBeAliased(memberName))
            memberName = context.GetOrAddAttributeName(memberName);

        if (expression.NodeType == ExpressionType.Parameter)
            return memberName;

        return expression.Translate(context) + "." + memberName;
    }

    static string TranslateIndex<T>(this IndexExpression expression, ExpressionTranslationContext<T> context) where T : class => 
        expression.Object.TranslateIndex(expression.Arguments, context);

    static string TranslateIndex<T>(this Expression expression, IList<Expression> arguments, ExpressionTranslationContext<T> context) where T : class
    {
        if (arguments.Count == 1)
        {
            var indexValue = (arguments[0].TryReduceExpression() as ConstantExpression)?.Value;
            if (indexValue != null)
            {
                var serializedIndexValue = context.Serializer.SerializeDynamoDBValue(indexValue, indexValue.GetType(), context.SerializeFlags | SerializeDynamoDBValueFlags.PersistAll);

                if (!string.IsNullOrEmpty(serializedIndexValue.S))
                    return expression.TranslateMember(serializedIndexValue.S, context);

                if (!string.IsNullOrEmpty(serializedIndexValue.N))
                    return $"{expression.Translate(context)}[{serializedIndexValue.N}]";
            }
        }

        return Unsupported(expression);
    }

    static string TranslateBinary<T>(this BinaryExpression expression, string binaryOperation, ExpressionTranslationContext<T> context) where T : class
    {
        var left =
            (expression.Left is ConstantExpression && IsConvertFromEnum(expression.Right))
            ? TranslateConstant(
                Expression.Constant(
                    Enum.ToObject(
                        ((UnaryExpression)expression.Right).Operand.Type,
                        ((ConstantExpression)expression.Left).Value)),
                context)
            : expression.Left.Translate(context);

        var right =
            (IsConvertFromEnum(expression.Left) && expression.Right is ConstantExpression)
            ? TranslateConstant(
                Expression.Constant(
                    Enum.ToObject(
                        ((UnaryExpression)expression.Left).Operand.Type,
                        ((ConstantExpression)expression.Right).Value)),
                context)
            : expression.Right.Translate(context);

        return AppendCondition(left, right, binaryOperation);
    }

    static string TranslateUnary<T>(this UnaryExpression expression, string unaryOperation, ExpressionTranslationContext<T> context) where T : class
    {
        var operand = expression.Operand.Translate(context);

        return $"{unaryOperation} {SurroundWithParenthesesIfNeeded(operand)}";
    }

    static string CombineUpdateActions<T>(this BinaryExpression expression, ExpressionTranslationContext<T> context) where T : class
    {
        if (expression.Type != typeof(DynamoDBExpressions.UpdateAction))
            return Unsupported(expression);

        var left = expression.Left.Translate(context);
        var right = expression.Right.Translate(context);

        if (string.IsNullOrEmpty(right))
            return left;

        var i = right.IndexOf(" ", StringComparison.Ordinal);
        var action = right.Substring(0, Math.Max(i, 0));
        var arguments = right.Substring(i + 1);

        return AppendUpdate(left, action, arguments);
    }

    static bool IsConvertFromEnum(this Expression expression)
    {
        switch (expression.NodeType)
        {
            case ExpressionType.Convert:
            case ExpressionType.ConvertChecked:
                return ((UnaryExpression)expression).Operand.Type.GetTypeInfo().IsEnum;

            default:
                return false;
        }
    }
    
    static bool IsReferenceTo(this Expression expression, object value) =>
        expression.TryResolveConstantValue(out var constantValue) && ReferenceEquals(value, constantValue);

    static IEnumerable<string> TranslateArguments<T>(this IEnumerable<Expression> arguments, ExpressionTranslationContext<T> context) where T : class =>
        arguments.Select(argument => argument.Translate(context));

    static object InvokeExpression(Expression expression) =>
        ExpressionInvoker.Get(expression.Type).Invoke(expression);

    static Expression TryReduceExpression(this Expression expression)
    {
        expression = TryReduceConditionalExpression(expression);
    
        if (!(expression.IsConvertFromEnum() || expression.Contains(IsParameterOrRawExpression)))
            expression = Expression.Constant(InvokeExpression(expression), expression.Type);

        return expression;
    }

    static Expression TryReduceConditionalExpression(Expression expression)
    {
        if (expression is ConditionalExpression)
        {
            var conditional = (ConditionalExpression)expression;
            var testValue = (conditional.Test.TryReduceExpression() as ConstantExpression)?.Value;
            if (testValue is bool)
                expression = ((bool)testValue ? conditional.IfTrue : conditional.IfFalse);
        }

        return expression;
    }

    static bool TryResolveConstantValue(this Expression expression, out object constantValue)
    {
        if (expression.CanResolveConstantValue())
        {
            constantValue = InvokeExpression(expression);
            return true;
        }
        else
        {
            constantValue = null;
            return false;
        }
    }

    static bool CanResolveConstantValue(this Expression expression) =>
        expression is ConstantExpression constantExpression ||
        (expression is MemberExpression memberExpression && 
            memberExpression.Expression.CanResolveConstantValue()) ||
        (expression is IndexExpression indexExpression && 
            indexExpression.Object.CanResolveConstantValue() &&
            indexExpression.Arguments.All(argument => argument.CanResolveConstantValue()));

    static T Apply<T>(this T expression, ExpressionVisitor expressionVisitor) where T : LambdaExpression =>
        (T)Expression.Lambda(expressionVisitor.Visit(expression.Body), expression.Parameters);

    static bool IsParameterOrRawExpression(Expression node) =>
        (node is ParameterExpression || (node as ConstantExpression)?.Value is DynamoDBExpressions.RawExpression);

    static bool IsMemberOfTypeWithAttribute<T, TAttribute>(Expression expression) =>
        (GetMemberOf<T>(expression)?.GetCustomAttributes(typeof(TAttribute), true)?.Any() == true);

    static MemberInfo GetMemberOf<T>(Expression expression)
    {
        var memberExpression = expression as MemberExpression;

        if (memberExpression != null && 
            memberExpression.Expression != null &&
            typeof(T).IsAssignableFrom(memberExpression.Expression.Type))
            return memberExpression.Member;

        return null;
    }

    static bool Contains(this Expression expression, Func<Expression, bool> predicate) =>
        expression.FindFirst(predicate, node => true);

    static TValue FindFirst<TValue>(this Expression expression, Func<Expression, bool> predicate, Func<Expression, TValue> selector)
    {
        var search = new FindFirstVisitor<TValue>(predicate, selector);
        
        search.Visit(expression);

        return search.Result;
    }

    static string Unsupported(Expression expression)
    {
        throw new InvalidOperationException($"Unsupported expression: {expression.Type} {expression}");
    }

    class FindFirstVisitor<TValue> : ExpressionVisitor
    {
        Func<Expression, bool> predicate;
        Func<Expression, TValue> selector;

        public FindFirstVisitor(Func<Expression, bool> predicate, Func<Expression, TValue> selector)
        {
            this.predicate = predicate;
            this.selector = selector;
        }

        public TValue Result { get; private set; }
        public bool HasResult { get; private set; }

        public override Expression Visit(Expression node)
        {
            if (!HasResult && predicate(node))
                Result = selector(node);

            return base.Visit(node);
        }
    }

    class ReduceExpressionVisitor : ExpressionVisitor
    {
        public override Expression Visit(Expression node) =>
            base.Visit(node)?.TryReduceExpression();
    }

    class HandleEmptyValuesForUpdateVisitor : ExpressionVisitor
    {
        Func<object, bool> isSerializedToEmpty;

        public HandleEmptyValuesForUpdateVisitor(Func<object, bool> isSerializedToEmpty)
        {
            this.isSerializedToEmpty = isSerializedToEmpty;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node) =>
            base.VisitMethodCall(ReplaceSetWithRemoveForEmptyValues(node));
    
        private MethodCallExpression ReplaceSetWithRemoveForEmptyValues(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(DynamoDBExpressions) &&
                node.Method.Name == nameof(DynamoDBExpressions.Set))
            {
                var operandArgument = TryReduceExpression(node.Arguments[1]) as ConstantExpression;
                if (operandArgument != null && isSerializedToEmpty(operandArgument.Value))
                {
                    var pathArgument = node.Arguments[0];
                    return Expression.Call(
                        typeof(DynamoDBExpressions),
                        nameof(DynamoDBExpressions.Remove),
                        new[] { pathArgument.Type },
                        pathArgument);
                }
            }

            return node;
        }           
    }

    class FindReplaceVisitor : ExpressionVisitor
    {
        Func<Expression, bool> find;
        Expression replace;

        public FindReplaceVisitor(Func<Expression, bool> find, Expression replace)
        {
            this.find = find;
            this.replace = replace;
        }

        public override Expression Visit(Expression node) =>
            find(node) ? replace : base.Visit(node);
    }

    static class Identifier
    {
        public static bool NeedToBeAliased(string name) =>
            string.IsNullOrEmpty(name) || IsDigit(name[0]) || !name.All(IsLetterOrDigit) || reservedKeywords.Contains(name);

        static bool IsLowerCaseLetter(char c) => (c >= 'a' && c <= 'z');
        static bool IsUpperCaseLetter(char c) => (c >= 'A' && c <= 'Z');
        static bool IsDigit(char c) => (c >= '0' && c <= '9');
        static bool IsLetterOrDigit(char c) => IsLowerCaseLetter(c) || IsUpperCaseLetter(c) || IsDigit(c);

        static readonly HashSet<string> reservedKeywords =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ABORT",
                "ABSOLUTE",
                "ACTION",
                "ADD",
                "AFTER",
                "AGENT",
                "AGGREGATE",
                "ALL",
                "ALLOCATE",
                "ALTER",
                "ANALYZE",
                "AND",
                "ANY",
                "ARCHIVE",
                "ARE",
                "ARRAY",
                "AS",
                "ASC",
                "ASCII",
                "ASENSITIVE",
                "ASSERTION",
                "ASYMMETRIC",
                "AT",
                "ATOMIC",
                "ATTACH",
                "ATTRIBUTE",
                "AUTH",
                "AUTHORIZATION",
                "AUTHORIZE",
                "AUTO",
                "AVG",
                "BACK",
                "BACKUP",
                "BASE",
                "BATCH",
                "BEFORE",
                "BEGIN",
                "BETWEEN",
                "BIGINT",
                "BINARY",
                "BIT",
                "BLOB",
                "BLOCK",
                "BOOLEAN",
                "BOTH",
                "BREADTH",
                "BUCKET",
                "BULK",
                "BY",
                "BYTE",
                "CALL",
                "CALLED",
                "CALLING",
                "CAPACITY",
                "CASCADE",
                "CASCADED",
                "CASE",
                "CAST",
                "CATALOG",
                "CHAR",
                "CHARACTER",
                "CHECK",
                "CLASS",
                "CLOB",
                "CLOSE",
                "CLUSTER",
                "CLUSTERED",
                "CLUSTERING",
                "CLUSTERS",
                "COALESCE",
                "COLLATE",
                "COLLATION",
                "COLLECTION",
                "COLUMN",
                "COLUMNS",
                "COMBINE",
                "COMMENT",
                "COMMIT",
                "COMPACT",
                "COMPILE",
                "COMPRESS",
                "CONDITION",
                "CONFLICT",
                "CONNECT",
                "CONNECTION",
                "CONSISTENCY",
                "CONSISTENT",
                "CONSTRAINT",
                "CONSTRAINTS",
                "CONSTRUCTOR",
                "CONSUMED",
                "CONTINUE",
                "CONVERT",
                "COPY",
                "CORRESPONDING",
                "COUNT",
                "COUNTER",
                "CREATE",
                "CROSS",
                "CUBE",
                "CURRENT",
                "CURSOR",
                "CYCLE",
                "DATA",
                "DATABASE",
                "DATE",
                "DATETIME",
                "DAY",
                "DEALLOCATE",
                "DEC",
                "DECIMAL",
                "DECLARE",
                "DEFAULT",
                "DEFERRABLE",
                "DEFERRED",
                "DEFINE",
                "DEFINED",
                "DEFINITION",
                "DELETE",
                "DELIMITED",
                "DEPTH",
                "DEREF",
                "DESC",
                "DESCRIBE",
                "DESCRIPTOR",
                "DETACH",
                "DETERMINISTIC",
                "DIAGNOSTICS",
                "DIRECTORIES",
                "DISABLE",
                "DISCONNECT",
                "DISTINCT",
                "DISTRIBUTE",
                "DO",
                "DOMAIN",
                "DOUBLE",
                "DROP",
                "DUMP",
                "DURATION",
                "DYNAMIC",
                "EACH",
                "ELEMENT",
                "ELSE",
                "ELSEIF",
                "EMPTY",
                "ENABLE",
                "END",
                "EQUAL",
                "EQUALS",
                "ERROR",
                "ESCAPE",
                "ESCAPED",
                "EVAL",
                "EVALUATE",
                "EXCEEDED",
                "EXCEPT",
                "EXCEPTION",
                "EXCEPTIONS",
                "EXCLUSIVE",
                "EXEC",
                "EXECUTE",
                "EXISTS",
                "EXIT",
                "EXPLAIN",
                "EXPLODE",
                "EXPORT",
                "EXPRESSION",
                "EXTENDED",
                "EXTERNAL",
                "EXTRACT",
                "FAIL",
                "FALSE",
                "FAMILY",
                "FETCH",
                "FIELDS",
                "FILE",
                "FILTER",
                "FILTERING",
                "FINAL",
                "FINISH",
                "FIRST",
                "FIXED",
                "FLATTERN",
                "FLOAT",
                "FOR",
                "FORCE",
                "FOREIGN",
                "FORMAT",
                "FORWARD",
                "FOUND",
                "FREE",
                "FROM",
                "FULL",
                "FUNCTION",
                "FUNCTIONS",
                "GENERAL",
                "GENERATE",
                "GET",
                "GLOB",
                "GLOBAL",
                "GO",
                "GOTO",
                "GRANT",
                "GREATER",
                "GROUP",
                "GROUPING",
                "HANDLER",
                "HASH",
                "HAVE",
                "HAVING",
                "HEAP",
                "HIDDEN",
                "HOLD",
                "HOUR",
                "IDENTIFIED",
                "IDENTITY",
                "IF",
                "IGNORE",
                "IMMEDIATE",
                "IMPORT",
                "IN",
                "INCLUDING",
                "INCLUSIVE",
                "INCREMENT",
                "INCREMENTAL",
                "INDEX",
                "INDEXED",
                "INDEXES",
                "INDICATOR",
                "INFINITE",
                "INITIALLY",
                "INLINE",
                "INNER",
                "INNTER",
                "INOUT",
                "INPUT",
                "INSENSITIVE",
                "INSERT",
                "INSTEAD",
                "INT",
                "INTEGER",
                "INTERSECT",
                "INTERVAL",
                "INTO",
                "INVALIDATE",
                "IS",
                "ISOLATION",
                "ITEM",
                "ITEMS",
                "ITERATE",
                "JOIN",
                "KEY",
                "KEYS",
                "LAG",
                "LANGUAGE",
                "LARGE",
                "LAST",
                "LATERAL",
                "LEAD",
                "LEADING",
                "LEAVE",
                "LEFT",
                "LENGTH",
                "LESS",
                "LEVEL",
                "LIKE",
                "LIMIT",
                "LIMITED",
                "LINES",
                "LIST",
                "LOAD",
                "LOCAL",
                "LOCALTIME",
                "LOCALTIMESTAMP",
                "LOCATION",
                "LOCATOR",
                "LOCK",
                "LOCKS",
                "LOG",
                "LOGED",
                "LONG",
                "LOOP",
                "LOWER",
                "MAP",
                "MATCH",
                "MATERIALIZED",
                "MAX",
                "MAXLEN",
                "MEMBER",
                "MERGE",
                "METHOD",
                "METRICS",
                "MIN",
                "MINUS",
                "MINUTE",
                "MISSING",
                "MOD",
                "MODE",
                "MODIFIES",
                "MODIFY",
                "MODULE",
                "MONTH",
                "MULTI",
                "MULTISET",
                "NAME",
                "NAMES",
                "NATIONAL",
                "NATURAL",
                "NCHAR",
                "NCLOB",
                "NEW",
                "NEXT",
                "NO",
                "NONE",
                "NOT",
                "NULL",
                "NULLIF",
                "NUMBER",
                "NUMERIC",
                "OBJECT",
                "OF",
                "OFFLINE",
                "OFFSET",
                "OLD",
                "ON",
                "ONLINE",
                "ONLY",
                "OPAQUE",
                "OPEN",
                "OPERATOR",
                "OPTION",
                "OR",
                "ORDER",
                "ORDINALITY",
                "OTHER",
                "OTHERS",
                "OUT",
                "OUTER",
                "OUTPUT",
                "OVER",
                "OVERLAPS",
                "OVERRIDE",
                "OWNER",
                "PAD",
                "PARALLEL",
                "PARAMETER",
                "PARAMETERS",
                "PARTIAL",
                "PARTITION",
                "PARTITIONED",
                "PARTITIONS",
                "PATH",
                "PERCENT",
                "PERCENTILE",
                "PERMISSION",
                "PERMISSIONS",
                "PIPE",
                "PIPELINED",
                "PLAN",
                "POOL",
                "POSITION",
                "PRECISION",
                "PREPARE",
                "PRESERVE",
                "PRIMARY",
                "PRIOR",
                "PRIVATE",
                "PRIVILEGES",
                "PROCEDURE",
                "PROCESSED",
                "PROJECT",
                "PROJECTION",
                "PROPERTY",
                "PROVISIONING",
                "PUBLIC",
                "PUT",
                "QUERY",
                "QUIT",
                "QUORUM",
                "RAISE",
                "RANDOM",
                "RANGE",
                "RANK",
                "RAW",
                "READ",
                "READS",
                "REAL",
                "REBUILD",
                "RECORD",
                "RECURSIVE",
                "REDUCE",
                "REF",
                "REFERENCE",
                "REFERENCES",
                "REFERENCING",
                "REGEXP",
                "REGION",
                "REINDEX",
                "RELATIVE",
                "RELEASE",
                "REMAINDER",
                "RENAME",
                "REPEAT",
                "REPLACE",
                "REQUEST",
                "RESET",
                "RESIGNAL",
                "RESOURCE",
                "RESPONSE",
                "RESTORE",
                "RESTRICT",
                "RESULT",
                "RETURN",
                "RETURNING",
                "RETURNS",
                "REVERSE",
                "REVOKE",
                "RIGHT",
                "ROLE",
                "ROLES",
                "ROLLBACK",
                "ROLLUP",
                "ROUTINE",
                "ROW",
                "ROWS",
                "RULE",
                "RULES",
                "SAMPLE",
                "SATISFIES",
                "SAVE",
                "SAVEPOINT",
                "SCAN",
                "SCHEMA",
                "SCOPE",
                "SCROLL",
                "SEARCH",
                "SECOND",
                "SECTION",
                "SEGMENT",
                "SEGMENTS",
                "SELECT",
                "SELF",
                "SEMI",
                "SENSITIVE",
                "SEPARATE",
                "SEQUENCE",
                "SERIALIZABLE",
                "SESSION",
                "SET",
                "SETS",
                "SHARD",
                "SHARE",
                "SHARED",
                "SHORT",
                "SHOW",
                "SIGNAL",
                "SIMILAR",
                "SIZE",
                "SKEWED",
                "SMALLINT",
                "SNAPSHOT",
                "SOME",
                "SOURCE",
                "SPACE",
                "SPACES",
                "SPARSE",
                "SPECIFIC",
                "SPECIFICTYPE",
                "SPLIT",
                "SQL",
                "SQLCODE",
                "SQLERROR",
                "SQLEXCEPTION",
                "SQLSTATE",
                "SQLWARNING",
                "START",
                "STATE",
                "STATIC",
                "STATUS",
                "STORAGE",
                "STORE",
                "STORED",
                "STREAM",
                "STRING",
                "STRUCT",
                "STYLE",
                "SUB",
                "SUBMULTISET",
                "SUBPARTITION",
                "SUBSTRING",
                "SUBTYPE",
                "SUM",
                "SUPER",
                "SYMMETRIC",
                "SYNONYM",
                "SYSTEM",
                "TABLE",
                "TABLESAMPLE",
                "TEMP",
                "TEMPORARY",
                "TERMINATED",
                "TEXT",
                "THAN",
                "THEN",
                "THROUGHPUT",
                "TIME",
                "TIMESTAMP",
                "TIMEZONE",
                "TINYINT",
                "TO",
                "TOKEN",
                "TOTAL",
                "TOUCH",
                "TRAILING",
                "TRANSACTION",
                "TRANSFORM",
                "TRANSLATE",
                "TRANSLATION",
                "TREAT",
                "TRIGGER",
                "TRIM",
                "TRUE",
                "TRUNCATE",
                "TTL",
                "TUPLE",
                "TYPE",
                "UNDER",
                "UNDO",
                "UNION",
                "UNIQUE",
                "UNIT",
                "UNKNOWN",
                "UNLOGGED",
                "UNNEST",
                "UNPROCESSED",
                "UNSIGNED",
                "UNTIL",
                "UPDATE",
                "UPPER",
                "URL",
                "USAGE",
                "USE",
                "USER",
                "USERS",
                "USING",
                "UUID",
                "VACUUM",
                "VALUE",
                "VALUED",
                "VALUES",
                "VARCHAR",
                "VARIABLE",
                "VARIANCE",
                "VARINT",
                "VARYING",
                "VIEW",
                "VIEWS",
                "VIRTUAL",
                "VOID",
                "WAIT",
                "WHEN",
                "WHENEVER",
                "WHERE",
                "WHILE",
                "WINDOW",
                "WITH",
                "WITHIN",
                "WITHOUT",
                "WORK",
                "WRAPPED",
                "WRITE",
                "YEAR",
                "ZONE"
            };
    }

    abstract class ExpressionInvoker
    {
        static ConcurrentDictionary<Type, ExpressionInvoker> _invokers = new ConcurrentDictionary<Type, ExpressionInvoker>();

        static ExpressionInvoker CreateInstance(Type type) =>
                (ExpressionInvoker)System.Activator.CreateInstance(typeof(ExpressionInvoker<>).MakeGenericType(type));
    
        public abstract object Invoke(Expression expression);

        public static ExpressionInvoker Get(Type type) => _invokers.GetOrAdd(type, CreateInstance);
    }

    class ExpressionInvoker<T> : ExpressionInvoker
    {
        public override object Invoke(Expression expression) => 
            Expression.Lambda<Func<T>>(expression).Compile()();
    }
}
