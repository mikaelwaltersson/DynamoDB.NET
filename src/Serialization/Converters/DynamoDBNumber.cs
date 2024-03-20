using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;

namespace DynamoDB.Net.Serialization.Converters;

static class DynamoDBNumber
{
    static readonly IEnumerable<Type> supportedNumberTypes = [
        typeof(decimal),
        typeof(byte),
        typeof(double),
        typeof(Half),
        typeof(short),
        typeof(int),
        typeof(long),
        typeof(Int128),
        typeof(nint),
        typeof(sbyte),
        typeof(float),
        typeof(ushort),
        typeof(uint),
        typeof(ulong),
        typeof(UInt128),
        typeof(nuint),
        typeof(System.Runtime.InteropServices.NFloat),
        typeof(BigInteger)
    ];

    static readonly ConcurrentDictionary<Type, Func<string, object>> compiledStringToNumber = new();

    static Func<string, object> CompileStringToNumber(Type type)
    {
        if (type == typeof(object))
            return value => decimal.Parse(value, CultureInfo.InvariantCulture);

        if (!supportedNumberTypes.Contains(type))
            return value => throw new DynamoDBSerializationException($"Number type not supported: {type}");

        var parameter = Expression.Parameter(typeof(string), "value");
        var compiledCast = 
            Expression
                .Lambda<Func<string, object>>(
                    Expression.Convert(
                        Expression.Call(
                            type, 
                            methodName: nameof(IParsable<decimal>.Parse), 
                            typeArguments: [],
                            arguments: [parameter, Expression.Constant(CultureInfo.InvariantCulture, typeof(IFormatProvider))]), 
                        typeof(object)),
                    parameter)
                .Compile();

        return compiledCast;
    }

    public static object StringToNumber(string value, Type type) =>
        compiledStringToNumber.GetOrAdd(type, CompileStringToNumber)(value);

    internal static bool IsSupportedType(Type type) => 
        supportedNumberTypes.Contains(type); 
}
