using DynamoDB.Net.Serialization;

namespace DynamoDB.Net.Tests.UnitTests.Serialization;

public class NameTransformStringExtensionsTests
{
    [Fact]
    public void CanTransformPascalCaseToCamelCase()
    {
        Assert.Equal("", "".ToCamelCase());
        Assert.Equal("ftp", "Ftp".ToCamelCase());
        Assert.Equal("ftp", "FTP".ToCamelCase());
        Assert.Equal("httpConnectionStatus", "HttpConnectionStatus".ToCamelCase());
        Assert.Equal("ioWriteFlag", "IOWriteFlag".ToCamelCase());
    }

    [Fact]
    public void CanTransformPascalCaseToSnakeCase()
    {
        Assert.Equal("", "".ToSnakeCase());
        Assert.Equal("ftp", "Ftp".ToSnakeCase());
        Assert.Equal("ftp", "FTP".ToSnakeCase());
        Assert.Equal("http_connection_status", "HttpConnectionStatus".ToSnakeCase());
        Assert.Equal("io_write_flag", "IOWriteFlag".ToSnakeCase());
    }

    [Fact]
    public void CanTransformPascalCaseToHyphenCase()
    {
        Assert.Equal("", "".ToHyphenCase());
        Assert.Equal("ftp", "Ftp".ToHyphenCase());
        Assert.Equal("ftp", "FTP".ToHyphenCase());
        Assert.Equal("http-connection-status", "HttpConnectionStatus".ToHyphenCase());
        Assert.Equal("io-write-flag", "IOWriteFlag".ToHyphenCase());
    }

    [Fact]
    public void CanNaivelyPluralize()
    {
        Assert.Equal("", "".NaivelyPluralized());
        Assert.Equal("Apples", "Apple".NaivelyPluralized());
        Assert.Equal("Glasses", "Glass".NaivelyPluralized());
        Assert.Equal("Libraries", "Library".NaivelyPluralized());
        Assert.Equal("Bouys", "Bouy".NaivelyPluralized());
    }
}
