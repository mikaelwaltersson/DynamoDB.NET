using System;

namespace DynamoDB.Net.Serialization.Parsers;

public sealed class Iso8601DateTimeParser : Iso8601DateTimeParserBase<DateTime>
{
    public Iso8601DateTimeParser(string buffer)
        : base(buffer)
    {
    }

    protected override DateTime Utc(DateTime dateTime) => 
        DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);

    protected override DateTime Offset(DateTime dateTime, TimeSpan offset) =>
        DateTime.SpecifyKind(new DateTimeOffset(dateTime, offset).LocalDateTime, DateTimeKind.Local);

    protected override DateTime Unspecified(DateTime dateTime) => dateTime;
}

