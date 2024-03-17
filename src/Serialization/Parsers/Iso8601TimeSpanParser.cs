using System;

namespace DynamoDB.Net.Serialization.Parsers;

public sealed class Iso8601TimeSpanParser : ParserBase
{
    public Iso8601TimeSpanParser(string buffer)
        : base(buffer)
    {
    }

    public TimeSpan Parse()
    {
        var duration = ParseTimeSpan();
        End();
        return duration;
    }

    public bool TryParse(out TimeSpan duration)
    {
        duration = ParseTimeSpan();
        return TryEnd();
    }

    TimeSpan ParseTimeSpan()
    {
        var sign = TryToken('-') ? -1 : 1;

        Token('P');

        var ticks = 0L;

        double n;            
        bool hasDecimalFraction;

        if (!TryToken('T'))
        {
            n = Decimal(out hasDecimalFraction);

            if (TryToken('Y') || TryToken('M'))
                throw new FormatException("Years and Month in time span is ambiguous and not permitted");
            
            if (TryToken('W'))
            {
                ticks = (long)(n*TimeSpan.TicksPerDay*7);                    
                return TimeSpan.FromTicks(ticks*sign);
            }
                            
            Token('D');
            ticks = (long)(n*TimeSpan.TicksPerDay);

            if (hasDecimalFraction || TryEnd())
                return TimeSpan.FromTicks(ticks*sign);
            
            Token('T');
        }

        n = Decimal(out hasDecimalFraction);

        if (TryToken('H'))
        {
            ticks += (long)(n*TimeSpan.TicksPerHour);

            if (hasDecimalFraction || TryEnd())
                return TimeSpan.FromTicks(ticks*sign);

            n = Decimal(out hasDecimalFraction);
        }

        if (TryToken('M'))
        {
            ticks += (long)(n*TimeSpan.TicksPerMinute);

            if (hasDecimalFraction || TryEnd())
                return TimeSpan.FromTicks(ticks*sign);

            n = Decimal(out hasDecimalFraction);
        }

        Token('S');
        ticks += (long)(n*TimeSpan.TicksPerSecond);

        return TimeSpan.FromTicks(ticks*sign);
    }


    double Decimal(out bool hasDecimalFraction)
    {
        var n = (double)Integer();
        
        hasDecimalFraction = TryToken(',') || TryToken('.');
        if (hasDecimalFraction)
            n += DecimalFraction();
        
        return n;
    }
}
