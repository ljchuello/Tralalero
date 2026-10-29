using Tralalero.Core.Infrastructure;
using Tralalero.Core.Security;

namespace Tralalero.Tests;

public sealed class ApiKeyStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "tralalero-tests",
        Guid.NewGuid().ToString("N"));

    public ApiKeyStoreTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsTheKey()
    {
        var path = Path.Combine(_directory, "credentials.bin");
        var store = new ApiKeyStore(path, NullAppLogger.Instance);

        store.Save("sk-test-value");
        var loaded = store.Load();

        Assert.Equal("sk-test-value", loaded);
        Assert.True(store.HasKey);
    }

    [Fact]
    public void Delete_RemovesTheStoredKey()
    {
        var path = Path.Combine(_directory, "credentials.bin");
        var store = new ApiKeyStore(path, NullAppLogger.Instance);
        store.Save("sk-test-value");

        store.Delete();

        Assert.Null(store.Load());
        Assert.False(store.HasKey);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch
        {
            // Best-effort test cleanup.
        }
    }
}
