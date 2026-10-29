using Tralalero.Core.Infrastructure;

namespace Tralalero.Tests;

public sealed class AppLoggerTests
{
    [Theory]
    [InlineData("key sk-abcdefghijklmnop", "key sk-***")]
    [InlineData("Authorization: Bearer abc.def.ghi", "Authorization: Bearer ***")]
    public void Sanitize_RedactsSecrets(string input, string expected)
    {
        Assert.Equal(expected, AppLogger.Sanitize(input));
    }
}
