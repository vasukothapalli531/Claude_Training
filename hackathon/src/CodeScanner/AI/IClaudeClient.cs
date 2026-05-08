namespace CodeScanner;

internal interface IClaudeClient
{
    Task<string> SendAsync(string requestBodyJson, CancellationToken cancellationToken);
}
