using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Tralalero.Core.Infrastructure;
using Tralalero.Core.OpenAI;

namespace Tralalero.Tests;

public sealed class OpenAiClientTests
{
    [Fact]
    public async Task TranscribeAsync_SendsMultipartRequestAndParsesText()
    {
        string? body = null;
        var handler = new FakeHttpMessageHandler(async (request, cancellationToken) =>
        {
            body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://api.test/v1/audio/transcriptions", request.RequestUri!.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("sk-test", request.Headers.Authorization.Parameter);
            return JsonResponse("""{"text":"hola mundo","languages":[{"code":"es"}]}""");
        });

        var client = CreateClient(handler);
        var response = await client.TranscribeAsync(
            new byte[] { 1, 2, 3 },
            "gpt-transcribe",
            "es",
            "contexto previo",
            CancellationToken.None);

        Assert.Equal("hola mundo", response.Text);
        Assert.Equal("es", Assert.Single(response.Languages));
        Assert.NotNull(body);
        Assert.Contains("gpt-transcribe", body);
        Assert.Contains("languages[]", body);
        Assert.Contains("prompt", body);
        Assert.Contains("segment.wav", body);
    }

    [Fact]
    public async Task TranscribeAsync_MapsUnauthorizedToAuthenticationException()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":{"message":"Invalid API key"}}""", Encoding.UTF8, "application/json")
            }));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<OpenAiAuthenticationException>(() =>
            client.TranscribeAsync(new byte[] { 1 }, "gpt-transcribe", "es", null, CancellationToken.None));
    }

    [Fact]
    public async Task TranscribeAsync_RetriesRateLimitsThenSucceeds()
    {
        var calls = 0;
        var handler = new FakeHttpMessageHandler((_, _) =>
        {
            calls++;
            if (calls == 1)
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("""{"error":{"message":"slow down"}}""")
                };
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));
                return Task.FromResult(response);
            }

            return Task.FromResult(JsonResponse("""{"text":"ok"}"""));
        });
        var delays = new List<TimeSpan>();
        var client = CreateClient(handler, (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

        var response = await client.TranscribeAsync(new byte[] { 1 }, "gpt-transcribe", "es", null, CancellationToken.None);

        Assert.Equal("ok", response.Text);
        Assert.Equal(2, calls);
        Assert.Single(delays);
    }

    [Fact]
    public async Task TranscribeAsync_RetriesServerErrorsUpToThreeTimes()
    {
        var calls = 0;
        var handler = new FakeHttpMessageHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error":{"message":"boom"}}""")
            });
        });
        var client = CreateClient(handler, (_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<OpenAiServiceException>(() =>
            client.TranscribeAsync(new byte[] { 1 }, "gpt-transcribe", "es", null, CancellationToken.None));

        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_ReturnsFalseForUnauthorized()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var client = CreateClient(handler);

        Assert.False(await client.ValidateApiKeyAsync());
    }

    [Fact]
    public async Task ValidateApiKeyAsync_ReturnsTrueForSuccess()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var client = CreateClient(handler);

        Assert.True(await client.ValidateApiKeyAsync());
    }

    private static OpenAiClient CreateClient(
        HttpMessageHandler handler,
        Func<TimeSpan, CancellationToken, Task>? delay = null) =>
        new(new HttpClient(handler), "sk-test", "https://api.test/v1", NullAppLogger.Instance, delay);

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => _handler(request, cancellationToken);
    }
}
