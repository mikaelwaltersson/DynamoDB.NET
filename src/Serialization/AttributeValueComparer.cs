using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Amazon.DynamoDBv2.Model;

namespace DynamoDB.Net
{
    public class AttributeValueComparer : IEqualityComparer<AttributeValue>
    {
        public static AttributeValueComparer Default { get; } = new AttributeValueComparer();
        
        public bool Equals(AttributeValue x, AttributeValue y)
        {
            if (x != null && y != null)
            {
                if (x.NULL)
                    return y.NULL;
                else if (x.IsBOOLSet)
                    return y.IsBOOLSet && x.BOOL == y.BOOL;
                else if (x.S != null)
                    return y.S != null && x.S == y.S;
                else if (x.N != null)
                    return y.N != null && x.N == y.N;
                else if (x.B != null)
                    return y.B != null && Equals(x.B, y.B);
                else if (x.SS != null && x.SS.Count > 0)
                    return y.SS != null && x.SS.SequenceEqual(y.SS);
                else if (x.NS != null && x.NS.Count > 0)
                    return y.NS != null && x.NS.SequenceEqual(y.NS);
                else if (x.BS != null && x.BS.Count > 0)
                    return y.BS != null && x.BS.SequenceEqual(y.BS, MemoryStreamComparer.Default);
                else if (x.IsLSet)
                    return y.IsLSet && x.L.SequenceEqual(y.L, Default);
                else if (x.IsMSet)
                    return y.IsMSet && x.M.Count == y.M.Count && x.M.All(entry => y.M.TryGetValue(entry.Key, out var value) && Equals(entry.Value, value));
            }

            return Object.ReferenceEquals(x, y);
        }

        public int GetHashCode(AttributeValue obj)
        {
            if (obj != null)
            {
                if (obj.NULL)
                    return 0;
                else if (obj.IsBOOLSet)
                    return obj.BOOL.GetHashCode();
                else if (obj.S != null)
                    return obj.S.GetHashCode();
                else if (obj.N != null)
                    return obj.N.GetHashCode();
                else if (obj.B != null)
                    return MemoryStreamComparer.Default.GetHashCode(obj.B);
                else if (obj.SS != null && obj.SS.Count > 0)
                    return CombineHashCodes(obj.SS.Select(s => s.GetHashCode()));
                else if (obj.NS != null && obj.NS.Count > 0)
                    return CombineHashCodes(obj.NS.Select(n => n.GetHashCode()));
                else if (obj.SS != null && obj.SS.Count > 0)
                    return CombineHashCodes(obj.BS.Select(MemoryStreamComparer.Default.GetHashCode));
                else if (obj.IsLSet)
                    return CombineHashCodes(obj.L.Select(GetHashCode));
                else if (obj.IsMSet)
                    return CombineHashCodes(obj.M.Select(entry => CombineHashCodes(entry.Key.GetHashCode(), GetHashCode(entry.Value))));
            }
            
            return -1;
        }

        const int InitialHashCode = 13;

        static int CombineHashCodes(int h1, int h2) => ((h1 << 5) + h1) ^ h2;
        static int CombineHashCodes(IEnumerable<int> hashCodes) => hashCodes.Aggregate(InitialHashCode, CombineHashCodes);

        class MemoryStreamComparer : IEqualityComparer<MemoryStream>
        {
            public static MemoryStreamComparer Default { get; } = new MemoryStreamComparer();

            static ArraySegment<byte> BufferOf(MemoryStream obj) => 
                obj.TryGetBuffer(out var xbuffer) 
                    ? xbuffer 
                    : new ArraySegment<byte>(obj.ToArray());


            public bool Equals(MemoryStream x, MemoryStream y) =>
                BufferOf(x).SequenceEqual(BufferOf(y));

            public int GetHashCode(MemoryStream obj) =>
                CombineHashCodes(BufferOf(obj).Select(b => b.GetHashCode()));
        }
    }
}