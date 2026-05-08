using System.Net;
using System.Text;

namespace CodeScanner.Tests.AI;

public class AnthropicClientTests
{
    [Fact]
    public async Task SendAsync_PassesApiKeyAndVersionHeaders_AndReturnsContentText()
    {
        var handler = new FakeHandler((req, ct) =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("https://api.anthropic.com/v1/messages", req.RequestUri!.ToString());
            Assert.True(req.Headers.Contains("x-api-key"));
            Assert.Equal("test-key", req.Headers.GetValues("x-api-key").First());
            Assert.True(req.Headers.Contains("anthropic-version"));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"content\":[{\"type\":\"text\",\"text\":\"hello\"}]}",
                    Encoding.UTF8, "application/json"),
            };
        });

        var client = new AnthropicClient(new HttpClient(handler), apiKey: "test-key");
        var text = await client.SendAsync("{\"model\":\"x\"}", CancellationToken.None);

        Assert.Equal("hello", text);
    }

    [Fact]
    public async Task SendAsync_Returns401_ThrowsInvalidKey()
    {
        var handler = new FakeHandler((_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{}"),
        });

        var client = new AnthropicClient(new HttpClient(handler), apiKey: "bad");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SendAsync("{}", CancellationToken.None));
        Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_Returns429_ThrowsHttpRequestException()
    {
        var handler = new FakeHandler((_, _) =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{}"),
            };
            resp.Headers.Add("Retry-After", "1");
            return resp;
        });

        var client = new AnthropicClient(new HttpClient(handler), apiKey: "x");
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SendAsync("{}", CancellationToken.None));
        Assert.Contains("429", ex.Message);
    }

    [Fact]
    public async Task SendAsync_Returns500_ThrowsHttpRequestException()
    {
        var handler = new FakeHandler((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("oops"),
        });

        var client = new AnthropicClient(new HttpClient(handler), apiKey: "x");
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SendAsync("{}", CancellationToken.None));
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _responder;
        public FakeHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
        {
            _responder = responder;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request, cancellationToken));
    }
}
