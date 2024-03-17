using System;

namespace DynamoDB.Net.Serialization.Parsers;

public sealed class Iso8601DateTimeOffsetParser : Iso8601DateTimeParserBase<DateTimeOffset>
{
    public Iso8601DateTimeOffsetParser(string buffer)
        : base(buffer)
    {
    }

    protected override DateTimeOffset Utc(DateTime dateTime) => new DateTimeOffset(dateTime, TimeSpan.Zero);

    protected override DateTimeOffset Offset(DateTime dateTime, TimeSpan offset) => new DateTimeOffset(dateTime, offset);

    protected override DateTimeOffset Unspecified(DateTime dateTime) => dateTime;
}
