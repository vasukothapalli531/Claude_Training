using System.Net;
using System.Text;
using System.Text.Json;

namespace CodeScanner;

internal sealed class AnthropicClient : IClaudeClient
{
    private const string Endpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";

    private readonly HttpClient _http;
    private readonly string _apiKey;

    public AnthropicClient(HttpClient http, string apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    public async Task<string> SendAsync(string requestBodyJson, CancellationToken cancellationToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        req.Headers.Add("x-api-key", _apiKey);
        req.Headers.Add("anthropic-version", AnthropicVersion);
        req.Content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);

        if (resp.StatusCode == HttpStatusCode.Unauthorized || resp.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException("invalid ANTHROPIC_API_KEY");
        }

        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Anthropic API returned {(int)resp.StatusCode} ({resp.StatusCode}).");
        }

        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array || content.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Anthropic response missing 'content' array");
        }

        var first = content[0];
        if (!first.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Anthropic response missing 'content[0].text' string");
        }

        return text.GetString() ?? string.Empty;
    }
}
