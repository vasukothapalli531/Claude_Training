# AI Fix Suggestions (Phase 2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an opt-in `--fix-suggestions` flag that, after the analysis pass, calls the Anthropic API once per finding (smell or security), embeds explanation + fixedSnippet into the JSON, and renders inline suggestions in the dark dashboard, per `docs/superpowers/specs/2026-05-08-ai-fix-suggestions-design.md`.

**Architecture:** Seven implementation tasks. Backend: new `AI/` namespace (interface + HttpClient impl + prompt builder + response parser + concurrency-bounded orchestrator) feeding optional `AiSuggestion` per finding plus a top-level `AiSummary`. CLI: three new flags, env-var auth, in-process test stub hook. Report: additive JSON. Template: banner + inline suggestion toggle.

**Tech Stack:** .NET 9, C#, xUnit, `System.Net.Http.HttpClient`, `System.Text.Json`. No new NuGet packages.

**Repo additions / modifications:**

```
NEW
hackathon/src/CodeScanner/AI/IClaudeClient.cs
hackathon/src/CodeScanner/AI/AnthropicClient.cs
hackathon/src/CodeScanner/AI/PromptBuilder.cs
hackathon/src/CodeScanner/AI/SuggestionParser.cs
hackathon/src/CodeScanner/AI/FixSuggestionService.cs
hackathon/tests/CodeScanner.Tests/AI/PromptBuilderTests.cs
hackathon/tests/CodeScanner.Tests/AI/SuggestionParserTests.cs
hackathon/tests/CodeScanner.Tests/AI/AnthropicClientTests.cs
hackathon/tests/CodeScanner.Tests/AI/FixSuggestionServiceTests.cs

MODIFIED
hackathon/src/CodeScanner/Models.cs
hackathon/src/CodeScanner/Analyzers/AnalyzerHost.cs
hackathon/src/CodeScanner/Html/RiskScoring.cs
hackathon/src/CodeScanner/Report.cs
hackathon/src/CodeScanner/Cli.cs
hackathon/src/CodeScanner/Html/Template.cs
hackathon/tests/CodeScanner.Tests/CliTests.cs
hackathon/README.md
```

Working directory throughout: `C:\Cmm-testing\Claude_Training\hackathon`. Branch this work onto `feat/ai-fix-suggestions` before Task 1.

---

## Task 0: Branch off main

- [ ] **Step 0.1**

```powershell
git -C C:/Cmm-testing/Claude_Training switch -c feat/ai-fix-suggestions
git -C C:/Cmm-testing/Claude_Training status -sb
```

Expected: `Switched to a new branch 'feat/ai-fix-suggestions'`. Branch shows clean.

---

## Task 1: Models — AiSuggestion + AiSummary + optional fields

**Files:**
- Modify: `hackathon/src/CodeScanner/Models.cs`
- Modify: `hackathon/src/CodeScanner/Analyzers/AnalyzerHost.cs`

- [ ] **Step 1.1: Append new records and extend findings + AnalysisResult**

Open `hackathon/src/CodeScanner/Models.cs`. Append at the end of the file:

```csharp
public sealed record AiSuggestion(
    string Explanation,
    string FixedSnippet,
    string Model,
    long ElapsedMs);

public sealed record AiSummary(
    string Model,
    int TotalCalls,
    int Successful,
    int Failed,
    long TotalElapsedMs,
    int QualityScoreIfAllFixed,
    int QualityScoreDelta);
```

Then update the existing `SmellFinding` and `SecurityFinding` records to add an optional `AiSuggestion?` parameter at the end. Replace each declaration in the file.

For `SmellFinding`, replace:

```csharp
public sealed record SmellFinding(
    string Type,
    string Severity,
    string File,
    string Name,
    int StartLine,
    int EndLine,
    int Value,
    int Threshold,
    string Message);
```

with:

```csharp
public sealed record SmellFinding(
    string Type,
    string Severity,
    string File,
    string Name,
    int StartLine,
    int EndLine,
    int Value,
    int Threshold,
    string Message,
    AiSuggestion? AiSuggestion = null);
```

For `SecurityFinding`, replace:

```csharp
public sealed record SecurityFinding(
    string Type,
    string Subtype,
    string Severity,
    string File,
    int Line,
    int Column,
    string Snippet,
    string Message);
```

with:

```csharp
public sealed record SecurityFinding(
    string Type,
    string Subtype,
    string Severity,
    string File,
    int Line,
    int Column,
    string Snippet,
    string Message,
    AiSuggestion? AiSuggestion = null);
```

- [ ] **Step 1.2: Extend `AnalysisResult` with `AiSummary?`**

Open `hackathon/src/CodeScanner/Analyzers/AnalyzerHost.cs`. Replace the `AnalysisResult` record:

```csharp
public sealed record AnalysisResult(
    IReadOnlyList<SmellFinding> Smells,
    IReadOnlyList<SecurityFinding> SecurityFindings,
    IReadOnlyList<ScanError> Errors,
    int TotalFunctions,
    AiSummary? AiSummary = null);
```

- [ ] **Step 1.3: Build to confirm no breakage**

```powershell
Set-Location C:\Cmm-testing\Claude_Training\hackathon
dotnet build CodeScanner.sln --nologo
dotnet test CodeScanner.sln --nologo
```

Expected: clean build, all 173 tests pass. The new optional parameters default to `null`, so all existing constructors still compile.

- [ ] **Step 1.4: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Models.cs hackathon/src/CodeScanner/Analyzers/AnalyzerHost.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(ai): add AiSuggestion + AiSummary records and optional finding fields"
```

---

## Task 2: PromptBuilder + SuggestionParser (pure helpers)

**Files:**
- Create: `hackathon/src/CodeScanner/AI/PromptBuilder.cs`
- Create: `hackathon/src/CodeScanner/AI/SuggestionParser.cs`
- Create: `hackathon/tests/CodeScanner.Tests/AI/PromptBuilderTests.cs`
- Create: `hackathon/tests/CodeScanner.Tests/AI/SuggestionParserTests.cs`

- [ ] **Step 2.1: Write failing tests for PromptBuilder**

Create `hackathon/tests/CodeScanner.Tests/AI/PromptBuilderTests.cs`:

```csharp
namespace CodeScanner.Tests.AI;

public class PromptBuilderTests
{
    private const string SystemExpected =
        "You are a senior code reviewer. For the given finding, propose a minimal fix. " +
        "Reply with strict JSON only — no prose, no code fences. " +
        "Schema: {\"explanation\": string, \"fixedSnippet\": string}.";

    [Fact]
    public void BuildSystem_IsConstantString()
    {
        Assert.Equal(SystemExpected, PromptBuilder.BuildSystem());
    }

    [Fact]
    public void BuildUserContent_LongFunction_IncludesFunctionSpan()
    {
        var source = string.Concat(Enumerable.Range(1, 20).Select(i => $"line{i}\n"));
        var smell = new SmellFinding(
            "long_function", "medium", "x.cs", "Foo",
            StartLine: 5, EndLine: 12, Value: 8, Threshold: 50,
            Message: "Function 'Foo' is 8 lines (threshold: 50)");

        var content = PromptBuilder.BuildUserContent(smell, source);

        Assert.Contains("Finding: long_function (medium)", content);
        Assert.Contains("File: x.cs", content);
        Assert.Contains("line5", content);
        Assert.Contains("line12", content);
        Assert.DoesNotContain("line1\n", content); // not in window
        Assert.DoesNotContain("line20", content);
    }

    [Fact]
    public void BuildUserContent_LongFunction_OverHundredLines_ElidesMiddle()
    {
        var source = string.Concat(Enumerable.Range(1, 200).Select(i => $"line{i}\n"));
        var smell = new SmellFinding(
            "long_function", "high", "x.cs", "Big",
            StartLine: 1, EndLine: 200, Value: 200, Threshold: 50,
            Message: "Function 'Big' is 200 lines (threshold: 50)");

        var content = PromptBuilder.BuildUserContent(smell, source);

        Assert.Contains("line1\n", content);
        Assert.Contains("line50", content);
        Assert.Contains("line151", content);
        Assert.Contains("line200", content);
        Assert.DoesNotContain("line100", content);
        Assert.Contains("// ... <100 lines elided> ...", content);
    }

    [Fact]
    public void BuildUserContent_SecurityFinding_TakesPlusMinus3Lines()
    {
        var source = string.Concat(Enumerable.Range(1, 20).Select(i => $"line{i}\n"));
        var sec = new SecurityFinding(
            "hardcoded_secret", "aws_access_key", "high", "x.cs",
            Line: 10, Column: 5,
            Snippet: "var k = \"AKIA••••REDACTED\";",
            Message: "AWS Access Key ID detected");

        var content = PromptBuilder.BuildUserContent(sec, source);

        Assert.Contains("line7", content);
        Assert.Contains("line10", content);
        Assert.Contains("line13", content);
        Assert.DoesNotContain("line6", content);
        Assert.DoesNotContain("line14", content);
    }

    [Fact]
    public void BuildUserContent_SecurityFinding_UsesRedactedSnippetField()
    {
        var sec = new SecurityFinding(
            "hardcoded_secret", "aws_access_key", "high", "x.cs",
            Line: 1, Column: 1,
            Snippet: "var k = \"AKIA••••REDACTED\";",
            Message: "AWS Access Key ID detected");

        var content = PromptBuilder.BuildUserContent(sec, sourceContent: string.Empty);

        Assert.Contains("AKIA••••REDACTED", content);
    }

    [Fact]
    public void BuildUserContent_Smell_StartLineOutsideSource_TakesAllAvailable()
    {
        var smell = new SmellFinding(
            "long_function", "low", "x.cs", "Foo",
            StartLine: 100, EndLine: 110, Value: 11, Threshold: 50,
            Message: "msg");

        // Source has 5 lines only.
        var content = PromptBuilder.BuildUserContent(smell, "a\nb\nc\nd\ne\n");

        // Should not throw; should produce some context block.
        Assert.Contains("Context:", content);
    }
}
```

- [ ] **Step 2.2: Write failing tests for SuggestionParser**

Create `hackathon/tests/CodeScanner.Tests/AI/SuggestionParserTests.cs`:

```csharp
namespace CodeScanner.Tests.AI;

public class SuggestionParserTests
{
    [Fact]
    public void TryParse_ValidJson_Succeeds()
    {
        var ok = SuggestionParser.TryParse(
            "{\"explanation\": \"do X\", \"fixedSnippet\": \"X();\"}",
            out var s, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(s);
        Assert.Equal("do X", s!.Explanation);
        Assert.Equal("X();", s.FixedSnippet);
    }

    [Fact]
    public void TryParse_CodeFencedJson_StripsFencesAndSucceeds()
    {
        var fenced = "```json\n{\"explanation\": \"do X\", \"fixedSnippet\": \"X();\"}\n```";
        var ok = SuggestionParser.TryParse(fenced, out var s, out _);
        Assert.True(ok);
        Assert.Equal("do X", s!.Explanation);
    }

    [Fact]
    public void TryParse_PlainBackticksFence_AlsoStrips()
    {
        var fenced = "```\n{\"explanation\": \"e\", \"fixedSnippet\": \"f\"}\n```";
        var ok = SuggestionParser.TryParse(fenced, out var s, out _);
        Assert.True(ok);
        Assert.NotNull(s);
    }

    [Fact]
    public void TryParse_InvalidJson_ReturnsFalseWithReason()
    {
        var ok = SuggestionParser.TryParse("not json", out var s, out var error);
        Assert.False(ok);
        Assert.Null(s);
        Assert.NotNull(error);
        Assert.Contains("parse", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParse_MissingExplanation_ReturnsFalse()
    {
        var ok = SuggestionParser.TryParse("{\"fixedSnippet\": \"X();\"}", out _, out var error);
        Assert.False(ok);
        Assert.Contains("explanation", error!);
    }

    [Fact]
    public void TryParse_MissingFixedSnippet_ReturnsFalse()
    {
        var ok = SuggestionParser.TryParse("{\"explanation\": \"e\"}", out _, out var error);
        Assert.False(ok);
        Assert.Contains("fixedSnippet", error!);
    }
}
```

- [ ] **Step 2.3: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: build error referring to `PromptBuilder` and `SuggestionParser`.

- [ ] **Step 2.4: Implement PromptBuilder.cs**

Create `hackathon/src/CodeScanner/AI/PromptBuilder.cs`:

```csharp
using System.Text;

namespace CodeScanner;

internal static class PromptBuilder
{
    private const int SmellWindowMax = 100;
    private const int SecurityContextRadius = 3;

    public static string BuildSystem() =>
        "You are a senior code reviewer. For the given finding, propose a minimal fix. " +
        "Reply with strict JSON only — no prose, no code fences. " +
        "Schema: {\"explanation\": string, \"fixedSnippet\": string}.";

    public static string BuildUserContent(SmellFinding finding, string sourceContent)
    {
        var window = WindowForSmell(sourceContent, finding.StartLine, finding.EndLine);
        return BuildEnvelope(
            type: finding.Type,
            severity: finding.Severity,
            file: finding.File,
            message: finding.Message,
            window: window);
    }

    public static string BuildUserContent(SecurityFinding finding, string sourceContent)
    {
        var window = WindowForSecurity(sourceContent, finding.Line, finding.Snippet);
        return BuildEnvelope(
            type: finding.Type,
            severity: finding.Severity,
            file: finding.File,
            message: finding.Message,
            window: window);
    }

    private static string BuildEnvelope(string type, string severity, string file, string message, string window)
    {
        var sb = new StringBuilder();
        sb.Append("Finding: ").Append(type).Append(" (").Append(severity).Append(")\n");
        sb.Append("File: ").Append(file).Append('\n');
        sb.Append("Message: ").Append(message).Append("\n\n");
        sb.Append("Context:\n```\n").Append(window).Append("\n```\n\n");
        sb.Append("Return the JSON now.");
        return sb.ToString();
    }

    private static string WindowForSmell(string source, int startLine, int endLine)
    {
        var lines = source.Split('\n');
        var startIdx = Math.Max(0, startLine - 1);
        var endIdx   = Math.Min(lines.Length - 1, endLine - 1);
        if (endIdx < startIdx) { return string.Empty; }

        var span = endIdx - startIdx + 1;
        if (span <= SmellWindowMax)
        {
            return string.Join("\n", lines.Skip(startIdx).Take(span));
        }

        // Elide middle.
        var firstHalf = lines.Skip(startIdx).Take(50);
        var secondHalfStart = endIdx - 49;
        var secondHalf = lines.Skip(secondHalfStart).Take(50);
        var elided = span - 100;
        var sb = new StringBuilder();
        sb.AppendJoin('\n', firstHalf);
        sb.Append("\n// ... <").Append(elided).Append(" lines elided> ...\n");
        sb.AppendJoin('\n', secondHalf);
        return sb.ToString();
    }

    private static string WindowForSecurity(string source, int line, string redactedSnippet)
    {
        var lines = source.Split('\n');
        var idx = line - 1;
        var startIdx = Math.Max(0, idx - SecurityContextRadius);
        var endIdx   = Math.Min(lines.Length - 1, idx + SecurityContextRadius);
        if (endIdx < startIdx)
        {
            // Source unavailable; fall back to the redacted snippet alone.
            return redactedSnippet;
        }

        // Replace the offending line in the window with the redacted snippet so
        // raw secrets from disk never leak into the prompt.
        var sb = new StringBuilder();
        for (var i = startIdx; i <= endIdx; i++)
        {
            if (i > startIdx) { sb.Append('\n'); }
            sb.Append(i == idx ? redactedSnippet : lines[i]);
        }
        return sb.ToString();
    }
}
```

- [ ] **Step 2.5: Implement SuggestionParser.cs**

Create `hackathon/src/CodeScanner/AI/SuggestionParser.cs`:

```csharp
using System.Text.Json;

namespace CodeScanner;

internal static class SuggestionParser
{
    public static bool TryParse(string responseText, out AiSuggestion? suggestion, out string? error)
    {
        suggestion = null;
        error = null;

        var stripped = StripFences(responseText.Trim());

        try
        {
            using var doc = JsonDocument.Parse(stripped);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "parse error: root is not an object";
                return false;
            }
            if (!doc.RootElement.TryGetProperty("explanation", out var expl) || expl.ValueKind != JsonValueKind.String)
            {
                error = "missing or invalid 'explanation'";
                return false;
            }
            if (!doc.RootElement.TryGetProperty("fixedSnippet", out var fix) || fix.ValueKind != JsonValueKind.String)
            {
                error = "missing or invalid 'fixedSnippet'";
                return false;
            }

            // Model + ElapsedMs are filled by the caller; placeholders here.
            suggestion = new AiSuggestion(
                Explanation: expl.GetString() ?? string.Empty,
                FixedSnippet: fix.GetString() ?? string.Empty,
                Model: string.Empty,
                ElapsedMs: 0);
            return true;
        }
        catch (JsonException ex)
        {
            error = $"parse error: {ex.Message}";
            return false;
        }
    }

    private static string StripFences(string s)
    {
        if (s.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = s.IndexOf('\n');
            if (firstNewline >= 0)
            {
                s = s.Substring(firstNewline + 1);
            }
            if (s.EndsWith("```", StringComparison.Ordinal))
            {
                s = s.Substring(0, s.Length - 3);
            }
        }
        return s.Trim();
    }
}
```

- [ ] **Step 2.6: Run tests — verify they pass**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: all green (173 prior + 11 new = 184).

- [ ] **Step 2.7: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/AI hackathon/tests/CodeScanner.Tests/AI/PromptBuilderTests.cs hackathon/tests/CodeScanner.Tests/AI/SuggestionParserTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(ai): add PromptBuilder and SuggestionParser pure helpers"
```

---

## Task 3: AnthropicClient (HTTP integration)

**Files:**
- Create: `hackathon/src/CodeScanner/AI/IClaudeClient.cs`
- Create: `hackathon/src/CodeScanner/AI/AnthropicClient.cs`
- Create: `hackathon/tests/CodeScanner.Tests/AI/AnthropicClientTests.cs`

- [ ] **Step 3.1: Create the interface**

Create `hackathon/src/CodeScanner/AI/IClaudeClient.cs`:

```csharp
namespace CodeScanner;

internal interface IClaudeClient
{
    Task<string> SendAsync(string requestBodyJson, CancellationToken cancellationToken);
}
```

- [ ] **Step 3.2: Write failing tests for AnthropicClient**

Create `hackathon/tests/CodeScanner.Tests/AI/AnthropicClientTests.cs`:

```csharp
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
    public async Task SendAsync_Returns429_ThrowsRateLimited()
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
    public async Task SendAsync_Returns500_ThrowsServerError()
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
```

- [ ] **Step 3.3: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: build error referring to `AnthropicClient`.

- [ ] **Step 3.4: Implement AnthropicClient.cs**

Create `hackathon/src/CodeScanner/AI/AnthropicClient.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
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
```

- [ ] **Step 3.5: Run tests — verify they pass**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: all green (184 prior + 4 new = 188).

- [ ] **Step 3.6: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/AI/IClaudeClient.cs hackathon/src/CodeScanner/AI/AnthropicClient.cs hackathon/tests/CodeScanner.Tests/AI/AnthropicClientTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(ai): add AnthropicClient HTTP integration with auth + error mapping"
```

---

## Task 4: FixSuggestionService (orchestrator)

**Files:**
- Create: `hackathon/src/CodeScanner/AI/FixSuggestionService.cs`
- Create: `hackathon/tests/CodeScanner.Tests/AI/FixSuggestionServiceTests.cs`

- [ ] **Step 4.1: Write failing tests**

Create `hackathon/tests/CodeScanner.Tests/AI/FixSuggestionServiceTests.cs`:

```csharp
using System.Diagnostics;

namespace CodeScanner.Tests.AI;

public class FixSuggestionServiceTests
{
    [Fact]
    public async Task GenerateAsync_NoFindings_EmptyResultAndZeroSummary()
    {
        var svc = new FixSuggestionService(new FakeClient(_ => "ok"), model: "m", concurrency: 4);
        using var tree = new TempTree();

        var result = await svc.GenerateAsync(
            tree.Root,
            Array.Empty<SmellFinding>(),
            Array.Empty<SecurityFinding>(),
            CancellationToken.None);

        Assert.Empty(result.Smells);
        Assert.Empty(result.SecurityFindings);
        Assert.Empty(result.Errors);
        Assert.Equal(0, result.Summary.TotalCalls);
        Assert.Equal(0, result.Summary.Failed);
    }

    [Fact]
    public async Task GenerateAsync_AllSuccess_AllFindingsHaveSuggestions()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("a.cs", "class C { void Foo() { } }\n");

        var smells = new[]
        {
            new SmellFinding("long_function", "low", path, "Foo", 1, 1, 1, 50, "msg"),
            new SmellFinding("long_function", "low", path, "Bar", 1, 1, 1, 50, "msg"),
        };

        var svc = new FixSuggestionService(
            new FakeClient(_ => "{\"explanation\":\"e\",\"fixedSnippet\":\"f\"}"),
            model: "test-model", concurrency: 4);

        var result = await svc.GenerateAsync(tree.Root, smells, Array.Empty<SecurityFinding>(), CancellationToken.None);

        Assert.Equal(2, result.Smells.Count);
        Assert.All(result.Smells, s => Assert.NotNull(s.AiSuggestion));
        Assert.All(result.Smells, s => Assert.Equal("test-model", s.AiSuggestion!.Model));
        Assert.Equal(2, result.Summary.TotalCalls);
        Assert.Equal(2, result.Summary.Successful);
        Assert.Equal(0, result.Summary.Failed);
    }

    [Fact]
    public async Task GenerateAsync_OneClientThrows_OthersStillSucceed_AndErrorRecorded()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("a.cs", "class C { void Foo() { } }\n");
        var smells = new[]
        {
            new SmellFinding("long_function", "low", path, "Foo", 1, 1, 1, 50, "msg"),
            new SmellFinding("long_function", "low", path, "Bar", 1, 1, 1, 50, "msg"),
            new SmellFinding("long_function", "low", path, "Baz", 1, 1, 1, 50, "msg"),
        };

        var svc = new FixSuggestionService(
            new FakeClient(body =>
            {
                if (body.Contains("\"Bar\"") || body.Contains("Foo() { } } "))
                {
                    // Trigger on second finding by index (Bar) — use a counter on the fake instead.
                    throw new HttpRequestException("simulated 500");
                }
                return "{\"explanation\":\"e\",\"fixedSnippet\":\"f\"}";
            }), model: "m", concurrency: 1);

        var result = await svc.GenerateAsync(tree.Root, smells, Array.Empty<SecurityFinding>(), CancellationToken.None);

        Assert.Equal(3, result.Summary.TotalCalls);
        Assert.True(result.Summary.Successful >= 1);
        Assert.True(result.Summary.Failed >= 1);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task GenerateAsync_ConcurrencyZero_NoCallsMade()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("a.cs", "class C { }\n");
        var smells = new[] { new SmellFinding("long_function", "low", path, "Foo", 1, 1, 1, 50, "msg") };

        var calls = 0;
        var svc = new FixSuggestionService(
            new FakeClient(_ => { Interlocked.Increment(ref calls); return "x"; }),
            model: "m", concurrency: 0);

        var result = await svc.GenerateAsync(tree.Root, smells, Array.Empty<SecurityFinding>(), CancellationToken.None);

        Assert.Equal(0, calls);
        Assert.Equal(0, result.Summary.TotalCalls);
        Assert.Single(result.Smells);
        Assert.Null(result.Smells[0].AiSuggestion);
    }

    [Fact]
    public async Task GenerateAsync_RespectsConcurrencyLimit()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("a.cs", "class C { }\n");

        var smells = Enumerable.Range(0, 8)
            .Select(i => new SmellFinding("long_function", "low", path, $"F{i}", 1, 1, 1, 50, "msg"))
            .ToArray();

        var inFlight = 0;
        var maxInFlight = 0;

        var svc = new FixSuggestionService(
            new FakeClient(async _ =>
            {
                var n = Interlocked.Increment(ref inFlight);
                int snapshot;
                do
                {
                    snapshot = maxInFlight;
                    if (n <= snapshot) break;
                } while (Interlocked.CompareExchange(ref maxInFlight, n, snapshot) != snapshot);

                await Task.Delay(20).ConfigureAwait(false);
                Interlocked.Decrement(ref inFlight);
                return "{\"explanation\":\"e\",\"fixedSnippet\":\"f\"}";
            }),
            model: "m", concurrency: 3);

        await svc.GenerateAsync(tree.Root, smells, Array.Empty<SecurityFinding>(), CancellationToken.None);

        Assert.True(maxInFlight <= 3, $"max in flight was {maxInFlight}, expected <= 3");
    }

    [Fact]
    public async Task GenerateAsync_QualityDeltaOnlyCountsSuggestionsThatLanded()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("a.cs", "class C { }\n");

        var smells = new[]
        {
            new SmellFinding("long_function", "high", path, "Foo", 1, 1, 1, 50, "msg"),
            new SmellFinding("long_function", "high", path, "Bar", 1, 1, 1, 50, "msg"),
        };

        var i = 0;
        var svc = new FixSuggestionService(
            new FakeClient(_ =>
            {
                var n = Interlocked.Increment(ref i);
                if (n == 1) { throw new HttpRequestException("fail one"); }
                return "{\"explanation\":\"e\",\"fixedSnippet\":\"f\"}";
            }),
            model: "m", concurrency: 1);

        var result = await svc.GenerateAsync(tree.Root, smells, Array.Empty<SecurityFinding>(), CancellationToken.None);

        // Current score: 100 - 5 - 5 = 90. With one suggestion landing, optimistic = 100 - 5 = 95. Delta 5.
        Assert.Equal(95, result.Summary.QualityScoreIfAllFixed);
        Assert.Equal(5,  result.Summary.QualityScoreDelta);
    }

    private sealed class FakeClient : IClaudeClient
    {
        private readonly Func<string, ValueTask<string>>? _asyncFn;
        private readonly Func<string, string>? _syncFn;
        public FakeClient(Func<string, string> fn) { _syncFn = fn; }
        public FakeClient(Func<string, ValueTask<string>> fn) { _asyncFn = fn; }
        public async Task<string> SendAsync(string body, CancellationToken ct)
        {
            if (_asyncFn is not null) { return await _asyncFn(body); }
            return _syncFn!(body);
        }
    }
}
```

- [ ] **Step 4.2: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: build error referring to `FixSuggestionService`.

- [ ] **Step 4.3: Implement FixSuggestionService.cs**

Create `hackathon/src/CodeScanner/AI/FixSuggestionService.cs`:

```csharp
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodeScanner;

internal sealed class FixSuggestionService
{
    public sealed record Result(
        IReadOnlyList<SmellFinding> Smells,
        IReadOnlyList<SecurityFinding> SecurityFindings,
        IReadOnlyList<ScanError> Errors,
        AiSummary Summary);

    private readonly IClaudeClient _client;
    private readonly string _model;
    private readonly int _concurrency;

    public FixSuggestionService(IClaudeClient client, string model, int concurrency)
    {
        _client = client;
        _model = model;
        _concurrency = concurrency;
    }

    public async Task<Result> GenerateAsync(
        string scanRoot,
        IReadOnlyList<SmellFinding> smells,
        IReadOnlyList<SecurityFinding> securityFindings,
        CancellationToken cancellationToken)
    {
        if (_concurrency <= 0)
        {
            var disabledSummary = ComputeSummary(smells, securityFindings, totalElapsedMs: 0L, totalCalls: 0, successful: 0, failed: 0);
            return new Result(smells, securityFindings, Array.Empty<ScanError>(), disabledSummary);
        }

        var totalSw = Stopwatch.StartNew();
        var sem = new SemaphoreSlim(_concurrency);
        var errors = new List<ScanError>();
        var errorLock = new object();

        var smellResults = new SmellFinding[smells.Count];
        var secResults = new SecurityFinding[securityFindings.Count];

        var smellTasks = smells.Select((f, i) => RunSmell(f, i)).ToArray();
        var secTasks   = securityFindings.Select((f, i) => RunSecurity(f, i)).ToArray();

        await Task.WhenAll(smellTasks.Concat(secTasks)).ConfigureAwait(false);
        totalSw.Stop();

        var enrichedSmells = smellResults.ToList();
        var enrichedSec    = secResults.ToList();

        var totalCalls = smells.Count + securityFindings.Count;
        var successful = enrichedSmells.Count(s => s.AiSuggestion is not null) + enrichedSec.Count(s => s.AiSuggestion is not null);
        var failed     = totalCalls - successful;

        var summary = ComputeSummary(enrichedSmells, enrichedSec, totalSw.ElapsedMilliseconds, totalCalls, successful, failed);
        return new Result(enrichedSmells, enrichedSec, errors, summary);

        async Task RunSmell(SmellFinding f, int index)
        {
            await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var enriched = await TryGenerate(f, index, isSmell: true).ConfigureAwait(false);
                smellResults[index] = enriched;
            }
            finally { sem.Release(); }
        }

        async Task RunSecurity(SecurityFinding f, int index)
        {
            await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var enriched = await TryGenerateSec(f, index).ConfigureAwait(false);
                secResults[index] = enriched;
            }
            finally { sem.Release(); }
        }

        async Task<SmellFinding> TryGenerate(SmellFinding f, int index, bool isSmell)
        {
            try
            {
                string source;
                try { source = await File.ReadAllTextAsync(f.File, cancellationToken).ConfigureAwait(false); }
                catch (Exception ex)
                {
                    AppendError(f.File, $"AI suggestion failed: source unavailable: {ex.GetType().Name}");
                    return f;
                }

                var userContent = PromptBuilder.BuildUserContent(f, source);
                var sw = Stopwatch.StartNew();
                var responseText = await _client.SendAsync(BuildRequestBody(userContent), cancellationToken).ConfigureAwait(false);
                sw.Stop();

                if (!SuggestionParser.TryParse(responseText, out var suggestion, out var error) || suggestion is null)
                {
                    AppendError(f.File, $"AI suggestion failed: {error}");
                    return f;
                }
                return f with { AiSuggestion = suggestion with { Model = _model, ElapsedMs = sw.ElapsedMilliseconds } };
            }
            catch (Exception ex)
            {
                AppendError(f.File, $"AI suggestion failed: {ex.GetType().Name}: {ex.Message}");
                return f;
            }
        }

        async Task<SecurityFinding> TryGenerateSec(SecurityFinding f, int index)
        {
            try
            {
                string source;
                try { source = await File.ReadAllTextAsync(f.File, cancellationToken).ConfigureAwait(false); }
                catch (Exception ex)
                {
                    AppendError(f.File, $"AI suggestion failed: source unavailable: {ex.GetType().Name}");
                    return f;
                }

                var userContent = PromptBuilder.BuildUserContent(f, source);
                var sw = Stopwatch.StartNew();
                var responseText = await _client.SendAsync(BuildRequestBody(userContent), cancellationToken).ConfigureAwait(false);
                sw.Stop();

                if (!SuggestionParser.TryParse(responseText, out var suggestion, out var error) || suggestion is null)
                {
                    AppendError(f.File, $"AI suggestion failed: {error}");
                    return f;
                }
                return f with { AiSuggestion = suggestion with { Model = _model, ElapsedMs = sw.ElapsedMilliseconds } };
            }
            catch (Exception ex)
            {
                AppendError(f.File, $"AI suggestion failed: {ex.GetType().Name}: {ex.Message}");
                return f;
            }
        }

        void AppendError(string path, string reason)
        {
            lock (errorLock) { errors.Add(new ScanError(path, reason)); }
        }
    }

    private string BuildRequestBody(string userContent)
    {
        var doc = new JsonObject
        {
            ["model"] = _model,
            ["max_tokens"] = 600,
            ["system"] = PromptBuilder.BuildSystem(),
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = userContent,
                },
            },
        };
        return doc.ToJsonString();
    }

    private AiSummary ComputeSummary(
        IReadOnlyList<SmellFinding> smells,
        IReadOnlyList<SecurityFinding> security,
        long totalElapsedMs,
        int totalCalls,
        int successful,
        int failed)
    {
        var currentScore = RiskScoring.ComputeQualityScore(smells, security);
        var smellsRemaining = smells.Where(s => s.AiSuggestion is null).ToList();
        var secRemaining    = security.Where(s => s.AiSuggestion is null).ToList();
        var optimistic = RiskScoring.ComputeQualityScore(smellsRemaining, secRemaining);
        var delta = optimistic - currentScore;

        return new AiSummary(
            Model: _model,
            TotalCalls: totalCalls,
            Successful: successful,
            Failed: failed,
            TotalElapsedMs: totalElapsedMs,
            QualityScoreIfAllFixed: optimistic,
            QualityScoreDelta: delta);
    }
}
```

- [ ] **Step 4.4: Run tests — verify they pass**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: all green (188 prior + 6 new = 194).

- [ ] **Step 4.5: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/AI/FixSuggestionService.cs hackathon/tests/CodeScanner.Tests/AI/FixSuggestionServiceTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(ai): add FixSuggestionService orchestrator with bounded concurrency"
```

---

## Task 5: CLI integration + Report.Serialize emit

**Files:**
- Modify: `hackathon/src/CodeScanner/Cli.cs`
- Modify: `hackathon/src/CodeScanner/Report.cs`
- Modify: `hackathon/tests/CodeScanner.Tests/CliTests.cs`

- [ ] **Step 5.1: Append failing CLI tests**

Open `hackathon/tests/CodeScanner.Tests/CliTests.cs`. Add inside the class, before closing `}`:

```csharp
    [Fact]
    public void Cli_FixSuggestions_NoApiKey_ExitsOne()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs", "class A {}\n");
        // Use --analyze so there's at least nothing to skip; the key check happens before the AI pass.

        // Run with no ANTHROPIC_API_KEY env var (RunCli does not set one).
        var (exit, _, stderr) = RunCli(tree.Root, "--analyze", "--fix-suggestions");

        Assert.Equal(1, exit);
        Assert.Contains("ANTHROPIC_API_KEY", stderr);
    }

    [Fact]
    public void Cli_FixSuggestions_StubMode_EmbedsAiSuggestionInJson()
    {
        // The CLI honours CODESCANNER_TEST_AI_STUB=1 to swap in a deterministic stub IClaudeClient.
        // The stub returns: {"explanation":"stub","fixedSnippet":"stub-fix"} for every call.

        using var tree = new TempTree();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("class C {");
        sb.AppendLine("    void Foo(int a, int b, int c, int d, int e, int f) {");
        for (var i = 0; i < 60; i++) { sb.AppendLine("        var x = 1;"); }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        tree.WriteFile("a.cs", sb.ToString());

        var psi = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        psi.Environment["CODESCANNER_TEST_AI_STUB"] = "1";
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--no-build");
        psi.ArgumentList.Add("--project");
        psi.ArgumentList.Add(FindSrcCsproj());
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add(tree.Root);
        psi.ArgumentList.Add("--analyze");
        psi.ArgumentList.Add("--fix-suggestions");

        using var p = System.Diagnostics.Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();

        Assert.Equal(0, p.ExitCode);

        var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.TryGetProperty("aiSummary", out var summary));
        Assert.True(summary.GetProperty("totalCalls").GetInt32() >= 1);
        Assert.True(summary.GetProperty("successful").GetInt32() >= 1);

        var smells = doc.RootElement.GetProperty("smells");
        Assert.True(smells.GetArrayLength() >= 1);
        var first = smells[0];
        Assert.True(first.TryGetProperty("aiSuggestion", out var ai));
        Assert.Equal("stub", ai.GetProperty("explanation").GetString());
        Assert.Equal("stub-fix", ai.GetProperty("fixedSnippet").GetString());
    }
```

- [ ] **Step 5.2: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: failures (CLI doesn't recognise `--fix-suggestions` yet).

- [ ] **Step 5.3: Update Report.Serialize**

Open `hackathon/src/CodeScanner/Report.cs`. In the `Serialize(ScanResult, AnalysisResult, ScanOptions, bool)` method, find the block that emits `smells` (when `options.Smells` is set). Replace the block:

```csharp
        if (options.Smells)
        {
            doc["smells"] = new JsonArray(
                analysis.Smells.Select(s => (JsonNode?)new JsonObject
                {
                    ["type"]      = s.Type,
                    ["severity"]  = s.Severity,
                    ["file"]      = NormalizePath(s.File),
                    ["name"]      = s.Name,
                    ["startLine"] = s.StartLine,
                    ["endLine"]   = s.EndLine,
                    ["value"]     = s.Value,
                    ["threshold"] = s.Threshold,
                    ["message"]   = s.Message,
                }).ToArray());
        }
```

with:

```csharp
        if (options.Smells)
        {
            doc["smells"] = new JsonArray(
                analysis.Smells.Select(s =>
                {
                    var obj = new JsonObject
                    {
                        ["type"]      = s.Type,
                        ["severity"]  = s.Severity,
                        ["file"]      = NormalizePath(s.File),
                        ["name"]      = s.Name,
                        ["startLine"] = s.StartLine,
                        ["endLine"]   = s.EndLine,
                        ["value"]     = s.Value,
                        ["threshold"] = s.Threshold,
                        ["message"]   = s.Message,
                    };
                    if (s.AiSuggestion is not null)
                    {
                        obj["aiSuggestion"] = SuggestionToJson(s.AiSuggestion);
                    }
                    return (JsonNode?)obj;
                }).ToArray());
        }
```

Find the corresponding `securityIssues` block and add the same `aiSuggestion` conditional. Replace:

```csharp
        if (options.Security)
        {
            doc["securityIssues"] = new JsonArray(
                analysis.SecurityFindings.Select(s => (JsonNode?)new JsonObject
                {
                    ["type"]     = s.Type,
                    ["subtype"]  = s.Subtype,
                    ["severity"] = s.Severity,
                    ["file"]     = NormalizePath(s.File),
                    ["line"]     = s.Line,
                    ["column"]   = s.Column,
                    ["snippet"]  = s.Snippet,
                    ["message"]  = s.Message,
                }).ToArray());
        }
```

with:

```csharp
        if (options.Security)
        {
            doc["securityIssues"] = new JsonArray(
                analysis.SecurityFindings.Select(s =>
                {
                    var obj = new JsonObject
                    {
                        ["type"]     = s.Type,
                        ["subtype"]  = s.Subtype,
                        ["severity"] = s.Severity,
                        ["file"]     = NormalizePath(s.File),
                        ["line"]     = s.Line,
                        ["column"]   = s.Column,
                        ["snippet"]  = s.Snippet,
                        ["message"]  = s.Message,
                    };
                    if (s.AiSuggestion is not null)
                    {
                        obj["aiSuggestion"] = SuggestionToJson(s.AiSuggestion);
                    }
                    return (JsonNode?)obj;
                }).ToArray());
        }
```

Also, after `doc["scanned"] = ...` at the end of the method (just before the JsonSerializerOptions creation), add:

```csharp
        if (analysis.AiSummary is not null)
        {
            doc["aiSummary"] = new JsonObject
            {
                ["model"]                  = analysis.AiSummary.Model,
                ["totalCalls"]             = analysis.AiSummary.TotalCalls,
                ["successful"]             = analysis.AiSummary.Successful,
                ["failed"]                 = analysis.AiSummary.Failed,
                ["totalElapsedMs"]         = analysis.AiSummary.TotalElapsedMs,
                ["qualityScoreIfAllFixed"] = analysis.AiSummary.QualityScoreIfAllFixed,
                ["qualityScoreDelta"]      = analysis.AiSummary.QualityScoreDelta,
            };
        }
```

Add a private helper at the bottom of the class (next to `NormalizePath`):

```csharp
    private static JsonNode SuggestionToJson(AiSuggestion s) => new JsonObject
    {
        ["explanation"]  = s.Explanation,
        ["fixedSnippet"] = s.FixedSnippet,
        ["model"]        = s.Model,
        ["elapsedMs"]    = s.ElapsedMs,
    };
```

- [ ] **Step 5.4: Update Cli.cs to add flags + invoke FixSuggestionService**

Replace the entire contents of `hackathon/src/CodeScanner/Cli.cs` with:

```csharp
using System.CommandLine;

namespace CodeScanner;

public static class Cli
{
    public static async Task<int> RunAsync(string[] args)
    {
        var pathArg = new Argument<string>("path") { Description = "Directory to scan" };
        var outputOpt = new Option<string?>("--output", "-o") { Description = "Write JSON to this file instead of stdout" };
        var excludeOpt = new Option<string[]>("--exclude", "-e")
        {
            Description = "Extra directory names to skip (additive to defaults)",
            AllowMultipleArgumentsPerToken = true,
        };
        var followOpt = new Option<bool>("--follow-symlinks") { Description = "Follow symlinked directories (default: skip)" };
        var prettyOpt = new Option<bool>("--pretty") { Description = "Pretty-print JSON output" };
        var verboseOpt = new Option<bool>("--verbose", "-v") { Description = "Log scan progress to stderr" };
        var smellsOpt   = new Option<bool>("--smells")    { Description = "Run Roslyn-based smell analyzer on .cs files" };
        var securityOpt = new Option<bool>("--security")  { Description = "Run regex-based security scanners (secrets + dangerous functions)" };
        var analyzeOpt  = new Option<bool>("--analyze")   { Description = "Shorthand for --smells --security" };
        var securitySkipOpt = new Option<string[]>("--security-skip")
        {
            Description = "Glob patterns to skip during security scan only (additive)",
            AllowMultipleArgumentsPerToken = true,
        };
        var htmlOpt = new Option<string?>("--html") { Description = "Write a self-contained HTML report to this file" };
        var fixOpt  = new Option<bool>("--fix-suggestions") { Description = "Call Anthropic API per finding to embed AI fix suggestions (requires ANTHROPIC_API_KEY)" };
        var aiModelOpt = new Option<string>("--ai-model") { Description = "Override the AI model id", DefaultValueFactory = _ => "claude-haiku-4-5" };
        var aiConcurrencyOpt = new Option<int>("--ai-concurrency") { Description = "Max parallel AI calls", DefaultValueFactory = _ => 4 };

        var root = new RootCommand("Recursively scan a directory and emit JSON file/line statistics.")
        {
            pathArg, outputOpt, excludeOpt, followOpt, prettyOpt, verboseOpt,
            smellsOpt, securityOpt, analyzeOpt, securitySkipOpt, htmlOpt,
            fixOpt, aiModelOpt, aiConcurrencyOpt,
        };

        root.SetAction(async parseResult =>
        {
            var path     = parseResult.GetValue(pathArg)!;
            var output   = parseResult.GetValue(outputOpt);
            var excludes = parseResult.GetValue(excludeOpt) ?? Array.Empty<string>();
            var follow   = parseResult.GetValue(followOpt);
            var pretty   = parseResult.GetValue(prettyOpt);
            var verbose  = parseResult.GetValue(verboseOpt);
            var smells   = parseResult.GetValue(smellsOpt);
            var security = parseResult.GetValue(securityOpt);
            var analyze  = parseResult.GetValue(analyzeOpt);
            var skip     = parseResult.GetValue(securitySkipOpt) ?? Array.Empty<string>();
            var html     = parseResult.GetValue(htmlOpt);
            var fix      = parseResult.GetValue(fixOpt);
            var aiModel  = parseResult.GetValue(aiModelOpt) ?? "claude-haiku-4-5";
            var aiConc   = parseResult.GetValue(aiConcurrencyOpt);

            if (analyze) { smells = true; security = true; }

            return await ExecuteAsync(path, output, excludes, follow, pretty, verbose, smells, security, skip, html, fix, aiModel, aiConc).ConfigureAwait(false);
        });

        return await root.Parse(args).InvokeAsync();
    }

    private static async Task<int> ExecuteAsync(
        string path,
        string? output,
        string[] excludes,
        bool followSymlinks,
        bool pretty,
        bool verbose,
        bool smells,
        bool security,
        string[] securitySkipGlobs,
        string? htmlPath,
        bool fixSuggestions,
        string aiModel,
        int aiConcurrency)
    {
        if (!Directory.Exists(path))
        {
            if (File.Exists(path))
            {
                Console.Error.WriteLine($"error: path is a file, not a directory: {path}");
            }
            else
            {
                Console.Error.WriteLine($"error: path does not exist: {path}");
            }
            return 1;
        }

        var stubMode = Environment.GetEnvironmentVariable("CODESCANNER_TEST_AI_STUB") == "1";
        if (fixSuggestions && !stubMode)
        {
            var key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (string.IsNullOrEmpty(key))
            {
                Console.Error.WriteLine("error: --fix-suggestions requires ANTHROPIC_API_KEY");
                return 1;
            }
            Console.Error.WriteLine($"info: --fix-suggestions enabled — code snippets will be sent to api.anthropic.com (model: {aiModel})");
        }

        try
        {
            if (verbose) { Console.Error.WriteLine($"info: scanning {path}"); }

            var options = new ScanOptions
            {
                FollowSymlinks = followSymlinks,
                ExtraExcludes = excludes,
                Smells = smells,
                Security = security,
                SecuritySkipGlobs = securitySkipGlobs,
            };

            var result = Scanner.Scan(path, options);

            AnalysisResult analysis;
            if (smells || security)
            {
                if (verbose) { Console.Error.WriteLine("info: running analysis pass"); }
                var host = new AnalyzerHost();
                analysis = host.Analyze(result, options);
            }
            else
            {
                analysis = new AnalysisResult(
                    Array.Empty<SmellFinding>(),
                    Array.Empty<SecurityFinding>(),
                    Array.Empty<ScanError>(),
                    TotalFunctions: 0);
            }

            if (fixSuggestions)
            {
                IClaudeClient client = stubMode
                    ? new StubClaudeClient()
                    : new AnthropicClient(new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
                        Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!);

                var svc = new FixSuggestionService(client, aiModel, aiConcurrency);
                var aiResult = await svc.GenerateAsync(path, analysis.Smells, analysis.SecurityFindings, CancellationToken.None).ConfigureAwait(false);
                var mergedErrors = analysis.Errors.Concat(aiResult.Errors).ToList();
                analysis = analysis with
                {
                    Smells = aiResult.Smells,
                    SecurityFindings = aiResult.SecurityFindings,
                    Errors = mergedErrors,
                    AiSummary = aiResult.Summary,
                };
                if (verbose) { Console.Error.WriteLine($"info: AI suggestions: {aiResult.Summary.Successful}/{aiResult.Summary.TotalCalls} succeeded"); }
            }

            var json = Report.Serialize(result, analysis, options, pretty);
            if (htmlPath is not null)
            {
                if (output is not null)
                {
                    File.WriteAllText(output, json);
                    if (verbose) { Console.Error.WriteLine($"info: wrote {output}"); }
                }
            }
            else if (output is null)
            {
                Console.Out.WriteLine(json);
            }
            else
            {
                File.WriteAllText(output, json);
                if (verbose) { Console.Error.WriteLine($"info: wrote {output}"); }
            }

            if (htmlPath is not null)
            {
                try
                {
                    var html = HtmlReport.Render(result, analysis, options, DateTimeOffset.UtcNow);
                    File.WriteAllText(htmlPath, html);
                    if (verbose) { Console.Error.WriteLine($"info: wrote {htmlPath}"); }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"error: cannot write HTML report: {ex.GetType().Name}: {ex.Message}");
                    return 2;
                }
            }

            if (verbose)
            {
                Console.Error.WriteLine(
                    $"info: {result.FileEntries.Count} files, {result.Errors.Count + analysis.Errors.Count} errors, " +
                    $"{analysis.Smells.Count} smells, {analysis.SecurityFindings.Count} security");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.GetType().Name}: {ex.Message}");
            return 2;
        }
    }

    private sealed class StubClaudeClient : IClaudeClient
    {
        public Task<string> SendAsync(string body, CancellationToken ct) =>
            Task.FromResult("{\"explanation\":\"stub\",\"fixedSnippet\":\"stub-fix\"}");
    }
}
```

- [ ] **Step 5.5: Build, run all tests**

```powershell
Set-Location C:\Cmm-testing\Claude_Training\hackathon
dotnet build CodeScanner.sln --nologo
dotnet test CodeScanner.sln --nologo
```

Expected: all green (194 prior + 2 new = 196).

- [ ] **Step 5.6: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Cli.cs hackathon/src/CodeScanner/Report.cs hackathon/tests/CodeScanner.Tests/CliTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(ai): wire --fix-suggestions through CLI + Report.Serialize emit"
```

---

## Task 6: Template additions (banner + inline suggestion)

**Files:**
- Modify: `hackathon/src/CodeScanner/Html/Template.cs`

The dashboard gains an opt-in section that renders only when `aiSummary` is present in the embedded JSON.

- [ ] **Step 6.1: Add CSS for the banner and inline suggestion**

Open `hackathon/src/CodeScanner/Html/Template.cs`. Inside the `<style>` block, find the closing `}` of the last `@media` rule. Immediately BEFORE the closing `</style>` tag, add:

```css
    /* AI banner */
    .ai-banner { display: none; align-items: center; gap: 14px;
      background: linear-gradient(135deg, rgba(168,85,247,0.18), rgba(59,130,246,0.18));
      border: 1px solid rgba(168,85,247,0.4); border-radius: 10px;
      padding: 12px 16px; margin-bottom: 18px; }
    .ai-banner.shown { display: flex; }
    .ai-banner .glyph { font-size: 22px; }
    .ai-banner .text { color: var(--text); font-size: 13px; }
    .ai-banner .text strong { font-weight: 700; }
    .ai-banner .delta { color: #6ee7b7; font-weight: 700; margin-left: 8px; }
    .ai-banner .model { color: var(--muted); font-size: 11px; margin-left: auto; font-family: "JetBrains Mono", "Cascadia Mono", monospace; }

    .ai-pill { display: inline-block; font-size: 9px; padding: 1px 6px; border-radius: 8px; font-weight: 700;
      letter-spacing: 0.04em; text-transform: uppercase; margin-right: 4px;
      background: rgba(168,85,247,0.18); color: #c4b5fd; }

    .ai-suggestion { margin-top: 8px; padding: 8px 10px; border-left: 2px solid #a855f7;
      background: rgba(168,85,247,0.06); border-radius: 0 4px 4px 0; }
    .ai-suggestion .ai-toggle { cursor: pointer; color: #c4b5fd; font-size: 11px; font-weight: 600;
      letter-spacing: 0.04em; text-transform: uppercase; user-select: none; }
    .ai-suggestion .ai-toggle:hover { color: #ddd6fe; }
    .ai-suggestion .ai-body { margin-top: 6px; }
    .ai-suggestion .ai-body.hidden { display: none; }
    .ai-suggestion .ai-body p { margin: 0 0 6px 0; font-size: 12px; color: var(--text-dim); }
    .ai-suggestion .ai-body pre { margin: 0; padding: 8px 10px; background: #0a0c12;
      border: 1px solid var(--border); border-radius: 4px; font-size: 11px;
      overflow-x: auto; white-space: pre-wrap; word-break: break-all; }
```

- [ ] **Step 6.2: Add the banner element to the HTML body**

Inside the `<div class="container">`, immediately AFTER the closing `</header>` tag and BEFORE the `<section class="kpis">`, add:

```html
    <div class="ai-banner" id="ai-banner">
      <div class="glyph">✨</div>
      <div class="text">
        <strong id="ai-banner-count">0</strong> AI fixes available
        <span class="delta" id="ai-banner-delta">+0</span> points if applied
      </div>
      <div class="model" id="ai-banner-model">—</div>
    </div>
```

- [ ] **Step 6.3: Add JS to populate the banner and inline suggestions**

Inside the existing IIFE in the `<script>` block, immediately after the line `var risks  = data.fileRiskScores || [];`, add:

```javascript
    var aiSummary = data.aiSummary || null;
    if (aiSummary && aiSummary.successful > 0) {
      document.getElementById('ai-banner-count').textContent = aiSummary.successful.toString();
      var deltaSign = (aiSummary.qualityScoreDelta >= 0) ? '+' : '';
      document.getElementById('ai-banner-delta').textContent = deltaSign + aiSummary.qualityScoreDelta;
      document.getElementById('ai-banner-model').textContent = aiSummary.model;
      document.getElementById('ai-banner').classList.add('shown');
    }
```

In the same IIFE, find the `findingLi(f)` function. Replace its entire body with:

```javascript
    function findingLi(f) {
      var li = document.createElement('li');
      var title = document.createElement('strong');
      title.textContent = (f.subtype || f.type) + ' (' + f.severity + ')';
      li.appendChild(title);
      if (f.aiSuggestion) {
        var pill = document.createElement('span');
        pill.className = 'ai-pill';
        pill.textContent = 'AI fix';
        title.appendChild(document.createTextNode(' '));
        title.appendChild(pill);
      }
      var meta = document.createElement('div');
      meta.className = 'meta';
      var lineNum = (f.line !== undefined ? f.line : f.startLine);
      meta.textContent = (lineNum ? ('line ' + lineNum + ' · ') : '') + f.message;
      li.appendChild(meta);
      if (f.snippet) { var pre = document.createElement('pre'); pre.textContent = f.snippet; li.appendChild(pre); }
      if (f.aiSuggestion) {
        li.appendChild(buildAiSuggestion(f.aiSuggestion));
      }
      return li;
    }
    function buildAiSuggestion(ai) {
      var wrap = document.createElement('div');
      wrap.className = 'ai-suggestion';
      var toggle = document.createElement('span');
      toggle.className = 'ai-toggle';
      toggle.textContent = '✨ Show fix';
      wrap.appendChild(toggle);
      var body = document.createElement('div');
      body.className = 'ai-body hidden';
      var expl = document.createElement('p');
      expl.textContent = ai.explanation;
      body.appendChild(expl);
      var pre = document.createElement('pre');
      pre.textContent = ai.fixedSnippet;
      body.appendChild(pre);
      wrap.appendChild(body);
      toggle.addEventListener('click', function (e) {
        e.stopPropagation();
        body.classList.toggle('hidden');
        toggle.textContent = body.classList.contains('hidden') ? '✨ Show fix' : '✨ Hide fix';
      });
      return wrap;
    }
```

(That replaces the existing 14-line `findingLi` and adds a new helper `buildAiSuggestion`.)

- [ ] **Step 6.4: Build, run all tests**

```powershell
Set-Location C:\Cmm-testing\Claude_Training\hackathon
dotnet build CodeScanner.sln --nologo
dotnet test CodeScanner.sln --nologo
```

Expected: all green (no new tests; existing dashboard tests still pass).

- [ ] **Step 6.5: Manual smoke**

```powershell
$env:CODESCANNER_TEST_AI_STUB = "1"
dotnet run --no-build --project src/CodeScanner -- ../ --analyze --fix-suggestions --html report.html
Test-Path report.html
$env:CODESCANNER_TEST_AI_STUB = $null
```

Expected: `True`. Open `report.html`; the purple ✨ banner should appear under the header, every finding row's expand-detail should show "✨ Show fix" toggles, clicking expands the explanation + snippet.

```powershell
Remove-Item report.html -ErrorAction SilentlyContinue
```

- [ ] **Step 6.6: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Html/Template.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(html): add AI banner + inline suggestion toggle in dashboard"
```

---

## Task 7: README + final verification

**Files:**
- Modify: `hackathon/README.md`

- [ ] **Step 7.1: Update README**

Open `hackathon/README.md`. Find the existing `## HTML report` section. Immediately AFTER it (before any later `##` heading), add:

```markdown
## AI fix suggestions (opt-in)

```powershell
$env:ANTHROPIC_API_KEY = "sk-ant-..."
dotnet run --project src/CodeScanner -- . --analyze --fix-suggestions --html report.html
```

When `--fix-suggestions` is set, the CLI calls the Anthropic API once per finding (smell or security), embeds the explanation + fixed snippet into the JSON, and the dashboard renders an inline "✨ Show fix" toggle in each expanded finding plus a top banner summarising the optimistic quality-score delta.

The HTML report stays a single shareable file — fixes are pre-baked at scan time, never requested from the browser.

| Flag | Default | Purpose |
|---|---|---|
| `--fix-suggestions` | off | Enable AI suggestions pass |
| `--ai-model <id>` | `claude-haiku-4-5` | Override model (`claude-sonnet-4-6` for higher quality) |
| `--ai-concurrency <n>` | `4` | Max parallel API calls |

Requires `ANTHROPIC_API_KEY` environment variable. Code snippets are sent to `api.anthropic.com`; secrets in security findings are sent in their already-redacted form.

Cost (informational): ~$0.20 per scan with Haiku, ~$0.60 with Sonnet for ~150 findings.
```

- [ ] **Step 7.2: Final verification — `/warnaserror` build (PowerShell, not bash)**

```powershell
Set-Location C:\Cmm-testing\Claude_Training\hackathon
dotnet build CodeScanner.sln /warnaserror --nologo
```

Expected: 0 warnings, 0 errors.

- [ ] **Step 7.3: Final verification — full test run**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: all tests pass (target: 196).

- [ ] **Step 7.4: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/README.md
git -C C:/Cmm-testing/Claude_Training commit -m "docs(ai): document --fix-suggestions, --ai-model, --ai-concurrency flags"
```

---

## Self-Review Checklist (executed before handoff)

**1. Spec coverage:**

- `--fix-suggestions` / `--ai-model` / `--ai-concurrency` CLI flags → Task 5
- `ANTHROPIC_API_KEY` env var auth + missing-key exit-1 → Task 5 + tested in Step 5.1
- Privacy notice on stderr → Task 5 `Console.Error.WriteLine`
- Per-finding API call shape (system/user, max_tokens 600, anthropic-version header) → Task 4 `BuildRequestBody` + Task 3 `AnthropicClient`
- Snippet windowing (smell ±5 around span; security ±3; elide >100) → Task 2 `PromptBuilder`
- Redacted snippet field used for security → Task 2 `WindowForSecurity`
- `aiSuggestion` per-finding JSON field → Task 5 `Report.Serialize` updates
- `aiSummary` top-level JSON field → Task 5 `Report.Serialize` updates
- `qualityScoreIfAllFixed` formula (only successful suggestions removed from score) → Task 4 `ComputeSummary`
- Failure handling matrix (401, 429, 5xx, timeout, parse error) → Task 3 + Task 4
- `--ai-concurrency 0` hard-disable → Task 4 first branch of `GenerateAsync`
- Bounded concurrency via `SemaphoreSlim` → Task 4 + tested in Step 4.1
- Test stub via `CODESCANNER_TEST_AI_STUB=1` env var → Task 5 `StubClaudeClient` + tested in Step 5.1
- Dashboard banner (✨ N AI fixes available · +Δ points) → Task 6
- Inline "Show fix" toggle in finding rows → Task 6 `buildAiSuggestion`
- "AI fix" pill on finding titles when suggestion present → Task 6 `findingLi` change
- Backwards compat (no flag → no `aiSummary`/`aiSuggestion` fields) → Task 5 `if (analysis.AiSummary is not null)` and `if (s.AiSuggestion is not null)`
- No new NuGet deps → confirmed (HttpClient + System.Text.Json only)

**2. Placeholder scan:** No `TBD`/`TODO`/`add tests for above` patterns. Every code-mutating step shows full code; every command states expected output.

**3. Type consistency:**

- `IClaudeClient.SendAsync(string body, CancellationToken) → Task<string>` — defined Task 3, used Tasks 4, 5.
- `AiSuggestion(string Explanation, string FixedSnippet, string Model, long ElapsedMs)` — defined Task 1, populated Task 4, serialised Task 5, rendered Task 6.
- `AiSummary(string Model, int TotalCalls, int Successful, int Failed, long TotalElapsedMs, int QualityScoreIfAllFixed, int QualityScoreDelta)` — defined Task 1, computed Task 4, serialised Task 5, rendered Task 6 banner.
- `SmellFinding` / `SecurityFinding` gain optional `AiSuggestion?` parameter — defined Task 1, set via `with { AiSuggestion = ... }` in Task 4, read in Task 5 + Task 6.
- `AnalysisResult` gains optional `AiSummary?` parameter — defined Task 1, set in Task 5, read by `Report.Serialize` Task 5.
- `FixSuggestionService.Result(IReadOnlyList<SmellFinding>, IReadOnlyList<SecurityFinding>, IReadOnlyList<ScanError>, AiSummary)` — defined Task 4, consumed Task 5.
- `RiskScoring.ComputeQualityScore` (existing) is reused for the optimistic recomputation — Task 4 calls it twice (current and remaining-after-suggestions) and subtracts.

No drift detected.
