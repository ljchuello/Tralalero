using System.Security.Cryptography;
using System.Text;
using Tralalero.Core.Infrastructure;

namespace Tralalero.Core.Security;

/// <summary>
/// Stores the OpenAI API key encrypted with Windows DPAPI for the current user.
/// The key is never written to settings.json or logs.
/// </summary>
public sealed class ApiKeyStore
{
    private readonly IAppLogger _logger;

    public ApiKeyStore(string path, IAppLogger logger)
    {
        Path = path;
        _logger = logger;
    }

    public string Path { get; }

    public bool HasKey
    {
        get
        {
            try
            {
                return !string.IsNullOrWhiteSpace(Load());
            }
            catch
            {
                return false;
            }
        }
    }

    public string? Load()
    {
        try
        {
            if (!File.Exists(Path))
            {
                return null;
            }

            var encrypted = File.ReadAllBytes(Path);
            var plain = ProtectedData.Unprotect(encrypted, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            _logger.Warn("The stored API key could not be decrypted. It may belong to another Windows user.", ex);
            return null;
        }
    }

    public void Save(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var plain = Encoding.UTF8.GetBytes(apiKey.Trim());
        var encrypted = ProtectedData.Protect(plain, optionalEntropy: null, DataProtectionScope.CurrentUser);
        var temp = Path + ".tmp";

        try
        {
            File.WriteAllBytes(temp, encrypted);
            File.Move(temp, Path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                try
                {
                    File.Delete(temp);
                }
                catch
                {
                    // Best-effort cleanup.
                }
            }
        }
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Warn("The stored API key could not be deleted.", ex);
        }
    }
}
