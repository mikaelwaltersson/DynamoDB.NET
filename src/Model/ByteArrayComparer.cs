using System.Collections;
using System.Collections.Generic;

namespace DynamoDB.Net.Model;

public class ByteArrayComparer : IComparer<byte[]>, IComparer, IEqualityComparer<byte[]>
{
    public static readonly ByteArrayComparer Default = new();

    public int Compare(byte[] x, byte[] y)
    {
        if (ReferenceEquals(x, y))
            return 0;

        if (x == null) return -1;         
        if (y == null) return 1;

        for (var i = 0; i < x.Length && i < y.Length; i++)
        {
            if (x[i] < y[i]) return -1;
            if (x[i] > y[i]) return 1;
        }

        return x.Length.CompareTo(y.Length);
    }

    public bool Equals(byte[] x, byte[] y) =>
        x?.Length == y?.Length && Compare(x, y) == 0;

    public int GetHashCode(byte[] obj)
    {
        if (obj == null) return 0;

        var hash = 17;
        foreach (var b in obj)
            hash = hash * 31 + b.GetHashCode();

        return hash;
    }

    int IComparer.Compare(object x, object y) =>
        (x is byte[] || x == null) && (y is byte[] || y == null)
        ? Compare((byte[])x, (byte[])y)
        : Comparer.Default.Compare(x, y);
}
