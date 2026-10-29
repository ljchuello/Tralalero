using System.Net;
using System.Net.Sockets;

namespace Tralalero.Core.OpenAI;

/// <summary>Shared rules for OpenAI and OpenAI-compatible transcription endpoints.</summary>
public static class OpenAiEndpoint
{
    public const string DefaultBaseUrl = "https://api.openai.com/v1";
    public const string TranscriptionPath = "/audio/transcriptions";

    /// <summary>Trims whitespace and trailing slashes, falling back to the OpenAI default.</summary>
    public static string NormalizeBaseUrl(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? DefaultBaseUrl : value.Trim();
        return normalized.TrimEnd('/');
    }

    public static bool TryParse(string? value, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    /// <summary>Validates a base URL and rejects public plain-HTTP endpoints.</summary>
    public static bool TryValidate(string? value, out Uri uri, out string? error)
    {
        error = null;
        if (!TryParse(value, out uri))
        {
            error = "Enter an absolute HTTP or HTTPS URL, for example https://api.openai.com/v1.";
            return false;
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !IsLocalOrPrivate(uri))
        {
            error = "Remote endpoints must use HTTPS. Plain HTTP is only allowed for localhost or a private LAN address.";
            return false;
        }

        return true;
    }

    public static bool IsOpenAi(string? baseUrl)
    {
        return TryParse(baseUrl, out var uri) &&
               string.Equals(uri.Host, "api.openai.com", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetHost(string? baseUrl)
    {
        if (!TryParse(baseUrl, out var uri))
        {
            return "custom endpoint";
        }

        return uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
    }

    public static string BuildTranscriptionUrl(string? baseUrl)
    {
        var normalized = NormalizeBaseUrl(baseUrl);
        return normalized.EndsWith(TranscriptionPath, StringComparison.OrdinalIgnoreCase)
            ? normalized
            : normalized + TranscriptionPath;
    }

    public static string BuildModelsUrl(string? baseUrl)
    {
        var normalized = NormalizeBaseUrl(baseUrl);
        if (normalized.EndsWith(TranscriptionPath, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^TranscriptionPath.Length].TrimEnd('/');
        }

        return normalized + "/models";
    }

    public static bool IsLocalOrPrivate(Uri uri)
    {
        if (uri.IsLoopback)
        {
            return true;
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(uri.Host, out var address))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal ||
                   (address.GetAddressBytes()[0] & 0xFE) == 0xFC;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }
}
