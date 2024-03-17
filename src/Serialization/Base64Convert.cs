using System;
using System.IO;

namespace DynamoDB.Net.Serialization;

public static class Base64Convert
{
    public static byte[] ToByteArrayFromBase64(this string value) => 
        Convert.FromBase64String(value);

    public static MemoryStream ToMemoryStreamFromBase64(this string value) => 
        new MemoryStream(value.ToByteArrayFromBase64());

    public static string ToBase64String(this byte[] value) => 
        Convert.ToBase64String(value);

    public static string ToBase64String(this MemoryStream value) => 
        value.ToArray().ToBase64String();
}
