using System;

namespace DynamoDB.Net.Model
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class VersionAttribute : Attribute
    {
    }
}