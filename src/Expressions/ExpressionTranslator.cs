using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using DynamoDB.Net.Model;
using DynamoDB.Net.Serialization;
using DynamoDB.Net.Serialization.Converters;

namespace DynamoDB.Net.Expressions;

public static class ExpressionTranslator
{
    public static string? GetIndexName<T>(this (string?, string?) indexProperties) where T : class
    {
        var (partitionKeyName, sortKeyName) = indexProperties;
        if (partitionKeyName == null)
            return null;

        var partitionKey =
            typeof(T).GetMember(partitionKeyName, BindingFlags.Instance | BindingFlags.Public).FirstOrDefault() ??
            throw new ArgumentOutOfRangeException(nameof(indexProperties), $"'{partitionKeyName}' is not a valid instance member of '{typeof(T).FullName}'");

        var sortKey = (MemberInfo?)null;
        if (sortKeyName != null)
        {
            sortKey =
                typeof(T).GetMember(sortKeyName, BindingFlags.Instance | BindingFlags.Public).FirstOrDefault() ??
                throw new ArgumentOutOfRangeException(nameof(indexProperties), $"'{sortKey}' is not a valid instance member of '{typeof(T).FullName}'");
        }

        return TableDescription.Get(typeof(T)).GetIndexName(partitionKey, sortKey);
    }

    public static string? GetIndexName<T>(this Expression<Func<T, bool>> expression) where T : class
    {
        var reducedExpression = expression.TryReduceExpression();

        var partitionKey = GetMemberOf<T>(reducedExpression.FindFirst(IsMemberOfTypeWithAttribute<T, PartitionKeyAttribute>));
        var sortKey = GetMemberOf<T>(reducedExpression.FindFirst(IsMemberOfTypeWithAttribute<T, SortKeyAttribute>));

        return partitionKey != null
            ? TableDescription.Get(typeof(T)).GetIndexName(partitionKey, sortKey)
            : null;
    }

    public static string Translate<T>(this Expression<Func<T, bool>> expression, ExpressionTranslationContext context) where T : class =>
        expression.Body.ResolveExplicitConstants().Translate(context, isPredicate: true);

    public static string Translate<T>(this Expression<Func<T, DynamoDBExpressions.UpdateAction>> expression, ExpressionTranslationContext context) where T : class =>
        expression.Body.ReplaceSetToEmptyWithRemove(context).ResolveExplicitConstants().Translate(context);

    public static Expression<Func<T, TResult>> ReplaceConstantWithParameter<T, TResult>(this Expression<Func<TResult>> expression, T value, [CallerArgumentExpression(nameof(value))] string? parameterName = null) where T : class
    {
        var parameter = Expression.Parameter(typeof(T), parameterName);

        return Expression.Lambda<Func<T, TResult>>(
            expression.Body.ResolveExplicitConstants().FindReplace(
                find: expression => expression.IsReferenceTo(value),
                replace: parameter),
            expression.Parameters.Append(parameter));
    }

    public static string AppendUpdate(string? expression, string action, string arguments)
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

        i = expression.IndexOf('\n', i + action.Length + 1);
        if (i < 0)
            i = expression.Length;

        return $"{expression[..i]}, {arguments}{expression[i..]}";
    }

    public static string AppendCondition(string? expression, string condition, string binaryOperator)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(binaryOperator);


        if (string.IsNullOrEmpty(expression))
            return condition;

        var left = SurroundWithParenthesesIfNeeded(expression);
        var right = SurroundWithParenthesesIfNeeded(condition);

        return $"{left} {binaryOperator} {right}";
    }

    internal static Expression? FindFirst(this Expression? expression, Func<Expression, bool> predicate)
    {
        var search = new FindFirstVisitor(predicate);

        search.Visit(expression);

        return search.Result;
    }

    [return: NotNullIfNotNull(nameof(expression))]
    internal static Expression? FindReplace(this Expression? expression, Func<Expression, bool> find, Expression replace) =>
        new FindReplaceVisitor(find, replace).Visit(expression);

    internal static Expression ResolveExplicitConstants(this Expression expression) =>
        new ResolveExplicitConstantsVisitor().Visit(expression);

    internal static Expression ReplaceSetToEmptyWithRemove(this Expression expression, ExpressionTranslationContext context) =>
        new ReplaceSetToEmptyWithRemoveVisitor(context).Visit(expression);

    static string SurroundWithParenthesesIfNeeded(string expression) =>
        expression.Contains(" OR ", StringComparison.OrdinalIgnoreCase) ||
        expression.Contains(" AND ", StringComparison.OrdinalIgnoreCase)
            ? $"({expression})"
            : expression;

    static string Translate(this Expression expression, ExpressionTranslationContext context, bool isPredicate = false)
    {
        ArgumentNullException.ThrowIfNull(context);

        expression = expression.TryReduceExpression();

        return expression.NodeType switch
        {
            ExpressionType.Convert or
            ExpressionType.ConvertChecked =>
                ((UnaryExpression)expression).Operand.Translate(context),

            ExpressionType.Call =>
                ((MethodCallExpression)expression).TranslateCall(context),

            ExpressionType.Constant =>
                ((ConstantExpression)expression).TranslateConstant(context),

            ExpressionType.MemberAccess =>
                ((MemberExpression)expression).TranslateMember(context, isPredicate),

            ExpressionType.Index or
            ExpressionType.ArrayIndex =>
                ((IndexExpression)expression).TranslateIndex(context),

            ExpressionType.Add or
            ExpressionType.AddChecked =>
                ((BinaryExpression)expression).TranslateBinary("+", context),

            ExpressionType.Subtract or
            ExpressionType.SubtractChecked =>
                ((BinaryExpression)expression).TranslateBinary("-", context),

            ExpressionType.Equal =>
                ((BinaryExpression)expression).TranslateBinary("=", context),

            ExpressionType.NotEqual =>
                ((BinaryExpression)expression).TranslateBinary("<>", context),

            ExpressionType.LessThan =>
                ((BinaryExpression)expression).TranslateBinary("<", context),

            ExpressionType.LessThanOrEqual =>
                ((BinaryExpression)expression).TranslateBinary("<=", context),

            ExpressionType.GreaterThan =>
                ((BinaryExpression)expression).TranslateBinary(">", context),

            ExpressionType.GreaterThanOrEqual =>
                ((BinaryExpression)expression).TranslateBinary(">=", context),

            ExpressionType.AndAlso =>
                ((BinaryExpression)expression).TranslateBinary("AND", context, isPredicate: true),

            ExpressionType.OrElse =>
                ((BinaryExpression)expression).TranslateBinary("OR", context, isPredicate: true),

            ExpressionType.Not =>
                ((UnaryExpression)expression).TranslateUnary("NOT", context, isPredicate: true),

            ExpressionType.And =>
                ((BinaryExpression)expression).CombineUpdateActions(context),

            ExpressionType.TypeIs =>
                ((TypeBinaryExpression)expression).TranslateTypeIs(context),

            _ => throw Unsupported(expression)
        };
    }

    static string TranslateCall(this MethodCallExpression expression, ExpressionTranslationContext context)
    {
        var method = expression.Method;
        var arguments = expression.Arguments.ToArray();
        var translatesTo = method.GetCustomAttribute<DynamoDBExpressions.TranslatesTo>();

        if (expression.Object is not null)
        {
            if (method.Name.Equals("get_Item", StringComparison.Ordinal))
                return expression.Object.TranslateIndex(arguments, context);
        
            arguments = [expression.Object, .. arguments];
        }

        translatesTo ??=
            ((method.Name, arguments)) switch
            {
                ("StartsWith", { Length: 2 }) => new("begins_with({0}, {1})"),
                ("Contains", { Length: 2 }) => new("contains({0}, {1})"),
                ("Count", { Length: 1 }) => new("size({0})"),
                _ => null
            };

        if (translatesTo is not null)
        {
            var arrayConstantKind = translatesTo.ArrayConstantKind;

            if (arrayConstantKind == DynamoDBExpressions.ArrayConstantKind.FromFirstOperandType)
                arrayConstantKind = DetermineArrayConstantKindFromType(arguments.FirstOrDefault()?.Type);

            if (arrayConstantKind != DynamoDBExpressions.ArrayConstantKind.Unspecified)
                context.ArrayConstantKind.Push(arrayConstantKind);

            try
            {
                var translatedArguments = arguments.TranslateArguments(context);

                if (translatesTo.HasParams)
                {
                    var paramsArgument = arguments.Last();

                    var translatedParamsArgument = ((
                        (paramsArgument as NewArrayExpression)?.Expressions?.AsEnumerable() ??
                        ((paramsArgument.TryReduceExpression() as ConstantExpression)?.Value as IEnumerable)?
                            .Cast<object>()?.Select(Expression.Constant))?
                            .TranslateArguments(context)) ??
                        throw Unsupported(expression);

                    translatedArguments =
                        translatedArguments
                            .Take(arguments.Length - 1)
                            .Append(string.Join(", ", translatedParamsArgument));
                }

                return string.Format(translatesTo.Format, translatedArguments.Cast<object>().ToArray());
            }
            finally
            {
                if (arrayConstantKind != DynamoDBExpressions.ArrayConstantKind.Unspecified)
                    context.ArrayConstantKind.Pop();
            }
        }

        throw Unsupported(expression);
    }

    static string TranslateConstant(this ConstantExpression expression, ExpressionTranslationContext context)
    {
        var value = expression.Value;

        if (value is DynamoDBExpressions.RawExpression raw)
        {
            context.Add(raw);
            return raw.expression;
        }

        if (value is DynamoDBType dynamoDBType)
            return TranslateConstant(Expression.Constant(ToDynamoDBTypeString(dynamoDBType)), context);

        var type = expression.Type;

        if (value is Array array)
        {
            switch (context.ArrayConstantKind.Peek())
            {
                case DynamoDBExpressions.ArrayConstantKind.Set:
                    value = DynamoDBSet.CreateSet(array);
                    type = value.GetType();
                    break;

                case DynamoDBExpressions.ArrayConstantKind.List:
                    value = DynamoDBList.CreateList(array);
                    type = value.GetType();
                    break;
            }
        }

        var serializedValue = context.Serializer.SerializeDynamoDBValue(value, type);

        return context.GetOrAddAttributeValue(serializedValue);
    }

    static string TranslateMember(this MemberExpression expression, ExpressionTranslationContext context, bool isPredicate = false)
    {
        if (expression.Expression == null)
            throw Unsupported(expression);

        if (isPredicate && expression.Type == typeof(bool))
            return Expression.Equal(expression, Expression.Constant(true)).Translate(context);

        var member = expression.Member;
        var memberPropertyInfo = context.Serializer.GetPropertyAttributeInfo((expression.Expression.Type, member.Name));

        if (memberPropertyInfo.NotSerialized)
            throw Unsupported($"Property '{member.Name}' of type '{member.DeclaringType?.FullName}' is not serialized");

        return expression.Expression.TranslateMember(memberPropertyInfo.AttributeName, context);
    }

    static string TranslateMember(this Expression expression, string memberName, ExpressionTranslationContext context)
    {
        if (Identifier.NeedToBeAliased(memberName))
            memberName = context.GetOrAddAttributeName(memberName);

        if (expression.NodeType == ExpressionType.Parameter)
            return memberName;

        return expression.Translate(context) + "." + memberName;
    }

    static string TranslateIndex(this IndexExpression expression, ExpressionTranslationContext context) =>
        (expression.Object ?? throw Unsupported(expression)).TranslateIndex(expression.Arguments, context);

    static string TranslateIndex(this Expression expression, IList<Expression> arguments, ExpressionTranslationContext context)
    {
        if (arguments.Count == 1)
        {
            var indexValue = (arguments[0].TryReduceExpression() as ConstantExpression)?.Value;
            if (indexValue != null)
            {
                var serializedIndexValue = context.Serializer.SerializeDynamoDBValue(indexValue, indexValue.GetType());
                var typeInfo = GenericTypeInfo.Get(expression.Type);
                if (typeInfo.IsDictionaryType)
                {
                    var memberName = serializedIndexValue.S ?? serializedIndexValue.N ?? serializedIndexValue.B?.ToBase64String();

                    if (!string.IsNullOrEmpty(memberName))
                        return expression.TranslateMember(memberName, context);
                }
                else if (typeInfo.IsListType)
                {
                    if (!string.IsNullOrEmpty(serializedIndexValue.N))
                        return $"{expression.Translate(context)}[{serializedIndexValue.N}]";
                }
            }
        }

        throw Unsupported(expression);
    }

    static string TranslateBinary(this BinaryExpression expression, string binaryOperation, ExpressionTranslationContext context, bool isPredicate = false)
    {
        var left =
            expression.Left is ConstantExpression && IsConvertFromEnum(expression.Right)
                ? TranslateConstant(
                    Expression.Constant(
                        Enum.ToObject(
                            ((UnaryExpression)expression.Right).Operand.Type,
                            ((ConstantExpression)expression.Left).Value ?? throw Unsupported(expression))),
                    context)
                : expression.Left.Translate(context, isPredicate);

        var right =
            IsConvertFromEnum(expression.Left) && expression.Right is ConstantExpression
                ? TranslateConstant(
                    Expression.Constant(
                        Enum.ToObject(
                            ((UnaryExpression)expression.Left).Operand.Type,
                            ((ConstantExpression)expression.Right).Value ?? throw Unsupported(expression))),
                    context)
                : expression.Right.Translate(context, isPredicate);

        return AppendCondition(left, right, binaryOperation);
    }

    static string TranslateUnary(this UnaryExpression expression, string unaryOperation, ExpressionTranslationContext context, bool isPredicate = false)
    {
        var operand = expression.Operand.Translate(context, isPredicate);

        return $"{unaryOperation} {SurroundWithParenthesesIfNeeded(operand)}";
    }

    static string CombineUpdateActions(this BinaryExpression expression, ExpressionTranslationContext context)
    {
        if (expression.Type != typeof(DynamoDBExpressions.UpdateAction))
            throw Unsupported(expression);

        var left = expression.Left.Translate(context);
        var right = expression.Right.Translate(context);

        if (string.IsNullOrEmpty(right))
            return left;

        var i = right.IndexOf(' ');
        var action = right[..Math.Max(i, 0)];
        var arguments = right[(i + 1)..];

        return AppendUpdate(left, action, arguments);
    }

    static string TranslateTypeIs(this TypeBinaryExpression expression, ExpressionTranslationContext context)
    {
        var typeNameResolver = context.Serializer.ObjectTypeNameResolver;
        var typeDiscriminatorAttribute = context.GetOrAddAttributeName(typeNameResolver.Attribute);
        var typeNameValue = context.GetOrAddAttributeValue(new() { S = typeNameResolver.GetTypeName(expression.TypeOperand) });

        return $"{typeDiscriminatorAttribute} = {typeNameValue}";
    }

    static DynamoDBExpressions.ArrayConstantKind DetermineArrayConstantKindFromType(Type? type)
    {
        if (type != null)
        {
            var typeInfo = GenericTypeInfo.Get(type);

            if (DynamoDBSet.IsSupportedType(type, typeInfo, out var _))
                return DynamoDBExpressions.ArrayConstantKind.Set;

            if (DynamoDBList.IsSupportedType(type, typeInfo, out var _))
                return DynamoDBExpressions.ArrayConstantKind.List;
        }

        return DynamoDBExpressions.ArrayConstantKind.Unspecified;
    }


    static bool IsConvertFromEnum(this Expression expression) =>
        expression is UnaryExpression unaryExpression &&
        unaryExpression.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked &&
        unaryExpression.Operand.Type.IsEnum;

    static bool IsReferenceTo<T>(this Expression expression, T value) =>
        expression.TryResolveConstantValue<T>(out var constantValue) && ReferenceEquals(value, constantValue);

    static IEnumerable<string> TranslateArguments(this IEnumerable<Expression> arguments, ExpressionTranslationContext context) =>
        arguments.Select(argument => argument.Translate(context));

    static Expression TryReduceExpression(this Expression expression)
    {
        expression = TryReduceConditionalExpression(expression);

        if (!(expression.IsConvertFromEnum() || expression.Contains(IsParameterOrRawExpression)))
            expression = Expression.Constant(expression.Invoke(), expression.Type);

        return expression;
    }

    static Expression TryReduceConditionalExpression(Expression expression)
    {
        if (expression is ConditionalExpression conditionalExpression &&
            conditionalExpression.Test.TryReduceExpression() is ConstantExpression { Value: bool testValue })
        {
            expression = testValue ? conditionalExpression.IfTrue : conditionalExpression.IfFalse;
        }

        return expression;
    }

    static bool TryResolveConstantValue<T>(this Expression expression, out object? constantValue)
    {
        if (typeof(T) == expression.Type && expression.CanResolveConstantValue())
        {
            constantValue = expression.Invoke();
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
            memberExpression.Expression?.CanResolveConstantValue() is true) ||
        (expression is IndexExpression indexExpression &&
            indexExpression.Object?.CanResolveConstantValue() is true &&
            indexExpression.Arguments.All(argument => argument.CanResolveConstantValue()));

    static bool IsParameterOrRawExpression(Expression node) =>
        (node is ParameterExpression || (node as ConstantExpression)?.Value is DynamoDBExpressions.RawExpression);

    static bool IsMemberOfTypeWithAttribute<T, TAttribute>(Expression expression) =>
        GetMemberOf<T>(expression)?.GetCustomAttributes(typeof(TAttribute), true)?.Length > 0;

    static MemberInfo? GetMemberOf<T>(Expression? expression) =>
        expression is MemberExpression memberExpression &&
        memberExpression.Expression != null &&
        typeof(T).IsAssignableFrom(memberExpression.Expression.Type)
            ? memberExpression.Member
            : null;

    static bool Contains(this Expression expression, Func<Expression, bool> predicate) =>
        expression.FindFirst(predicate) != null;

    static string? ToDynamoDBTypeString(DynamoDBType dynamoDBType) =>
        dynamoDBType switch
        {
            DynamoDBType.Null => "NULL",
            DynamoDBType.Bool => "BOOL",
            DynamoDBType.String => "S",
            DynamoDBType.Number => "N",
            DynamoDBType.Binary => "B",
            DynamoDBType.StringSet => "SS",
            DynamoDBType.NumberSet => "NS",
            DynamoDBType.BinarySet => "BS",
            DynamoDBType.List => "L",
            DynamoDBType.Map => "M",
            _ => null
        };

    static InvalidOperationException Unsupported(Expression expression) => Unsupported($"Expression is unsupported: {expression}");

    static InvalidOperationException Unsupported(string message) => new(message);

    class FindFirstVisitor(Func<Expression, bool> predicate) : ExpressionVisitor
    {
        public Expression? Result { get; private set; }

        public bool HasResult { get; private set; }

        [return: NotNullIfNotNull(nameof(node))]
        public override Expression? Visit(Expression? node)
        {
            if (!HasResult && node != null && predicate(node))
                Result = node;

            return base.Visit(node);
        }
    }

    class FindReplaceVisitor(Func<Expression, bool> find, Expression replace) : ExpressionVisitor
    {
        [return: NotNullIfNotNull(nameof(node))]
        public override Expression? Visit(Expression? node) =>
            node != null && find(node) ? replace : base.Visit(node);
    }

    class ResolveExplicitConstantsVisitor : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(DynamoDBExpressions) &&
                node.Method.Name == nameof(DynamoDBExpressions.Constant))
            {
                if (node.Arguments[0].TryReduceExpression() is not ConstantExpression constantExpression)
                    throw Unsupported($"Expression can not be resolved as constant: {node.Arguments[0]}");

                return constantExpression;
            }

            return base.VisitMethodCall(node);
        }
    }

    class ReplaceSetToEmptyWithRemoveVisitor(ExpressionTranslationContext context) : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node) =>
            base.VisitMethodCall(TryReplace(node));

        MethodCallExpression TryReplace(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(DynamoDBExpressions) &&
                node.Method.Name == nameof(DynamoDBExpressions.Set) &&
                TryReduceExpression(node.Arguments[1]) is ConstantExpression operandArgument &&
                node.Arguments[0] is MemberExpression { Member: var member } &&
                !IsSerializedAttributeValue(operandArgument.Value, member))
            {
                var pathArgument = node.Arguments[0];
                return Expression.Call(
                    typeof(DynamoDBExpressions),
                    nameof(DynamoDBExpressions.Remove),
                    [pathArgument.Type],
                    pathArgument);
            }

            return node;
        }

        bool IsSerializedAttributeValue(object? value, MemberInfo property) =>
            DefaultDynamoDBTypeConverter.IsSerializedAttributeValue(
                context.Serializer.GetPropertyAttributeInfo(property), 
                property.GetPropertyType(), 
                value) &&
            !context.Serializer.SerializeDynamoDBValue(value, property.GetPropertyType()).IsEmpty();
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
            new(StringComparer.OrdinalIgnoreCase)
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
}
