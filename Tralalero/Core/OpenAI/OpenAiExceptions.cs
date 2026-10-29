using System.Net;

namespace Tralalero.Core.OpenAI;

/// <summary>Base exception for OpenAI API failures. Messages are user-safe.</summary>
public class OpenAiException : Exception
{
    public OpenAiException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}

public sealed class OpenAiAuthenticationException : OpenAiException
{
    public OpenAiAuthenticationException(string message, HttpStatusCode statusCode, Exception? innerException = null)
        : base(message, statusCode, innerException)
    {
    }
}

public sealed class OpenAiRateLimitException : OpenAiException
{
    public OpenAiRateLimitException(string message, HttpStatusCode statusCode, Exception? innerException = null)
        : base(message, statusCode, innerException)
    {
    }
}

public sealed class OpenAiRequestException : OpenAiException
{
    public OpenAiRequestException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, statusCode, innerException)
    {
    }
}

public sealed class OpenAiServiceException : OpenAiException
{
    public OpenAiServiceException(string message, HttpStatusCode statusCode, Exception? innerException = null)
        : base(message, statusCode, innerException)
    {
    }
}

public sealed class OpenAiNetworkException : OpenAiException
{
    public OpenAiNetworkException(string message, Exception? innerException = null)
        : base(message, null, innerException)
    {
    }
}

public sealed class OpenAiTimeoutException : OpenAiException
{
    public OpenAiTimeoutException(string message, Exception? innerException = null)
        : base(message, null, innerException)
    {
    }
}
