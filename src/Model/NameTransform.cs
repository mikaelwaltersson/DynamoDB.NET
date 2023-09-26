using System.Linq;

namespace DynamoDB.Net.Model
{
    static class NameTransform
    {
        public static string NaivelyPluralized(this string s) => 
            s.EndsWith("y") && !new[] { "ay", "ey", "iy", "oy", "uy" }.Any(s.EndsWith)
            ? s.Substring(0, s.Length - 1) + "ies" 
            : s + (new[] { "s", "x", "z", "ch", "sh" }.Any(s.EndsWith) ? "es" : "s");


        public static string ToHyphenCasing(this string s)
        {
            var numberOfHyphens = 0;

            for (int i = 1; i < s.Length; i++)
            {
                if (IsNewWord(s, i))
                    numberOfHyphens++;
            }

            if (numberOfHyphens == 0)
                return s.ToLowerInvariant();

            var result = new char[s.Length + numberOfHyphens];

            result[0] = char.ToLowerInvariant(s[0]);
            for (int i = 1, j = 1; i < s.Length; i++)
            {
                if (IsNewWord(s, i))
                    result[j++] = '-';

                result[j++] = char.ToLowerInvariant(s[i]);
            }

            return new string(result);
        }

        static bool IsNewWord(string s, int i) => (char.IsLower(s[i - 1]) && char.IsUpper(s[i]));
    }
}