namespace DynamoDB.Net.Serialization;

static class NameTransformStringExtensions
{
    public static string ToCamelCase(this string s)
    {
        var firstWord = WordsOffsets(s).FirstOrDefault();
        return string.Concat(s[firstWord].ToLowerInvariant(), s[firstWord.End .. s.Length]);
    }

    public static string ToSnakeCase(this string s) =>
        string.Join('_', WordsOffsets(s).Select(range => s[range].ToLowerInvariant()));

    public static string ToHyphenCase(this string s) =>
        string.Join('-', WordsOffsets(s).Select(range => s[range].ToLowerInvariant()));

    public static string NaivelyPluralized(this string s) =>
        s.Length < 2
            ? s
            : s.EndsWith('y') && !s.EndsWithAnyOf("ay", "ey", "iy", "oy", "uy")
                ? string.Concat(s.AsSpan(0, s.Length - 1), "ies")
                : string.Concat(s, s.EndsWithAnyOf("s", "x", "z", "ch", "sh") ? "es" : "s");
        
    static IEnumerable<Range> WordsOffsets(string s)
    {
        if (s.Length == 0)
            yield break;

        var i = 0;

        for (var j = 1; j < s.Length; j++)
        {
            // Split "..aA.." at "..a" and "A.." 
            if (!char.IsUpper(s[j - 1]) && char.IsUpper(s[j]))
            {
                yield return new Range(i, j);
                i = j;
            }
            // Split "..AAa.." at "..A" and "Aa..",  
            else if (j - 2 >= i && char.IsUpper(s[j - 2]) && char.IsUpper(s[j - 1]) && !char.IsUpper(s[j]))
            {
                yield return new Range(i, j - 1);
                i = j - 1;
            }
        }

        yield return new Range(i, s.Length);
    }

    static bool EndsWithAnyOf(this string s, params string[] suffixes) =>
        suffixes.Any(s.EndsWith);
}
