using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Tralalero.Core.Infrastructure;

namespace Tralalero.Core.OpenAI;

/// <summary>Response returned by the transcription endpoint.</summary>
public sealed record OpenAiTranscriptionResponse(string Text, IReadOnlyList<string> Languages);

/// <summary>
/// Thin, dependency-free client for the OpenAI audio transcription endpoint.
/// It handles authentication, multipart encoding, retries, and safe error mapping.
/// </summary>
public sealed class OpenAiClient
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly IAppLogger _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public OpenAiClient(
        HttpClient httpClient,
        string apiKey,
        string baseUrl,
        IAppLogger logger,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        _httpClient = httpClient;
        _apiKey = apiKey.Trim();
        _baseUrl = baseUrl.TrimEnd('/');
        _logger = logger;
        _delay = delay ?? Task.Delay;
    }

    /// <summary>Validates the key without spending transcription credits.</summary>
    public async Task<bool> ValidateApiKeyAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Headers.UserAgent.ParseAdd("Tralalero/0.1.0");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return false;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new OpenAiRateLimitException(
                    "OpenAI rate limit reached while checking the key. Try again shortly.",
                    response.StatusCode);
            }

            if ((int)response.StatusCode >= 500)
            {
                throw new OpenAiServiceException(
                    "OpenAI is temporarily unavailable while checking the key.",
                    response.StatusCode);
            }

            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OpenAiTimeoutException("The OpenAI key check timed out. Check your connection.");
        }
        catch (HttpRequestException ex)
        {
            throw new OpenAiNetworkException("Could not reach OpenAI. Check your internet connection.", ex);
        }
    }

    /// <summary>Transcribes one WAV segment.</summary>
    public async Task<OpenAiTranscriptionResponse> TranscribeAsync(
        byte[] wavBytes,
        string model,
        string? language,
        string? prompt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(wavBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var request = BuildTranscriptionRequest(wavBytes, model, language, prompt);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RequestTimeout);

            try
            {
                using var response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                    .ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                    return ParseResponse(body);
                }

                var errorBody = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                var message = ExtractErrorMessage(errorBody) ?? $"OpenAI returned HTTP {(int)response.StatusCode}.";

                if (ShouldRetry(response.StatusCode) && attempt < MaxAttempts)
                {
                    var delay = GetRetryDelay(response, attempt);
                    _logger.Warn(
                        $"OpenAI request failed with {(int)response.StatusCode}. " +
                        $"Retrying in {delay.TotalSeconds:0.0}s (attempt {attempt}/{MaxAttempts}).");
                    await _delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                throw MapError(response.StatusCode, message, innerException: null);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt < MaxAttempts)
                {
                    var delay = TimeSpan.FromSeconds(Math.Min(4, Math.Pow(2, attempt - 1)));
                    _logger.Warn($"OpenAI request timed out. Retrying in {delay.TotalSeconds:0.0}s.");
                    await _delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                throw new OpenAiTimeoutException("OpenAI did not answer in time. Try again.");
            }
            catch (HttpRequestException ex)
            {
                if (attempt < MaxAttempts)
                {
                    var delay = TimeSpan.FromSeconds(Math.Min(4, Math.Pow(2, attempt - 1)));
                    _logger.Warn($"OpenAI network error. Retrying in {delay.TotalSeconds:0.0}s.", ex);
                    await _delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                throw new OpenAiNetworkException("Could not reach OpenAI. Check your internet connection.", ex);
            }
        }

        throw new OpenAiServiceException("OpenAI is temporarily unavailable. Try again.", HttpStatusCode.ServiceUnavailable);
    }

    private HttpRequestMessage BuildTranscriptionRequest(byte[] wavBytes, string model, string? language, string? prompt)
    {
        var content = new MultipartFormDataContent();
        var audio = new ByteArrayContent(wavBytes);
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(audio, "file", "segment.wav");
        content.Add(new StringContent(model), "model");

        if (!string.IsNullOrWhiteSpace(language) &&
            !string.Equals(language, "auto", StringComparison.OrdinalIgnoreCase))
        {
            // gpt-transcribe expects languages[]; it intentionally rejects the old singular field.
            content.Add(new StringContent(language.Trim()), "languages[]");
        }

        if (!string.IsNullOrWhiteSpace(prompt))
        {
            content.Add(new StringContent(prompt.Trim()), "prompt");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/audio/transcriptions")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Headers.UserAgent.ParseAdd("Tralalero/0.1.0");
        return request;
    }

    private static bool ShouldRetry(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : delta;
        }

        return TimeSpan.FromSeconds(Math.Min(8, Math.Pow(2, attempt - 1)));
    }

    private static OpenAiTranscriptionResponse ParseResponse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new OpenAiTranscriptionResponse(string.Empty, Array.Empty<string>());
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new OpenAiTranscriptionResponse(body.Trim(), Array.Empty<string>());
            }

            var text = document.RootElement.TryGetProperty("text", out var textElement)
                ? textElement.GetString() ?? string.Empty
                : string.Empty;

            var languages = new List<string>();
            if (document.RootElement.TryGetProperty("languages", out var languagesElement) &&
                languagesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in languagesElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object &&
                        item.TryGetProperty("code", out var code) &&
                        code.GetString() is { Length: > 0 } language)
                    {
                        languages.Add(language);
                    }
                    else if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } plain)
                    {
                        languages.Add(plain);
                    }
                }
            }

            return new OpenAiTranscriptionResponse(text, languages);
        }
        catch (JsonException)
        {
            // Defensive fallback for API-compatible servers returning plain text.
            return new OpenAiTranscriptionResponse(body.Trim(), Array.Empty<string>());
        }
    }

    private static string? ExtractErrorMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString();
            }
        }
        catch (JsonException)
        {
            // Ignore malformed error bodies.
        }

        return null;
    }

    private static OpenAiException MapError(HttpStatusCode statusCode, string message, Exception? innerException)
    {
        var code = (int)statusCode;
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new OpenAiAuthenticationException(
                "OpenAI rejected the API key. Check it in Settings.",
                statusCode,
                innerException),
            HttpStatusCode.TooManyRequests => new OpenAiRateLimitException(
                "OpenAI rate limit reached. Wait a moment and try again.",
                statusCode,
                innerException),
            HttpStatusCode.RequestEntityTooLarge => new OpenAiRequestException(
                "The audio segment is too large. Try a shorter recording.",
                statusCode,
                innerException),
            _ when code >= 500 => new OpenAiServiceException(
                "OpenAI is temporarily unavailable. Try again.",
                statusCode,
                innerException),
            _ => new OpenAiRequestException(message, statusCode, innerException)
        };
    }
}
