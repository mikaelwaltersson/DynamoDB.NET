using System;

namespace DynamoDB.Net.Model
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TableAttribute : Attribute
    {
        public string Name { get; set; }
    }
}