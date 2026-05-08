using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeScanner.Tests.Html;

public class HtmlReportTests
{
    private static (ScanResult, AnalysisResult, ScanOptions) Empty() => (
        new ScanResult("/x", Array.Empty<FileEntry>(), Array.Empty<string>(), Array.Empty<ScanError>()),
        new AnalysisResult(Array.Empty<SmellFinding>(), Array.Empty<SecurityFinding>(), Array.Empty<ScanError>()),
        new ScanOptions());

    [Fact]
    public void Render_EmptyResult_ProducesValidHtmlSkeleton()
    {
        var (result, analysis, options) = Empty();
        var html = HtmlReport.Render(result, analysis, options, DateTimeOffset.UtcNow);

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<html lang=\"en\">", html);
        Assert.Contains("id=\"scan-data\"", html);
        Assert.Contains("chart.umd.min.js", html);
        Assert.DoesNotContain("{{", html);
    }

    [Fact]
    public void Render_EmbedsJsonAsScriptTag()
    {
        var (result, analysis, options) = Empty();
        var html = HtmlReport.Render(result, analysis, options, DateTimeOffset.UtcNow);

        var match = Regex.Match(html,
            "<script type=\"application/json\" id=\"scan-data\">(?<j>.*?)</script>",
            RegexOptions.Singleline);
        Assert.True(match.Success);

        var doc = JsonDocument.Parse(match.Groups["j"].Value);
        Assert.Equal(0, doc.RootElement.GetProperty("totalFiles").GetInt32());
    }

    [Fact]
    public void Render_HtmlEscapesRootPath()
    {
        var result = new ScanResult("path<&>\"'", Array.Empty<FileEntry>(),
            Array.Empty<string>(), Array.Empty<ScanError>());
        var analysis = new AnalysisResult(Array.Empty<SmellFinding>(),
            Array.Empty<SecurityFinding>(), Array.Empty<ScanError>());

        var html = HtmlReport.Render(result, analysis, new ScanOptions(), DateTimeOffset.UtcNow);

        Assert.DoesNotContain("path<&>\"'", html);
        Assert.Contains("path&lt;&amp;&gt;&quot;&#39;", html);
    }

    [Fact]
    public void Render_DefaultJsonEncodingPreventsScriptInjection()
    {
        var result = new ScanResult("/x",
            new[] { new FileEntry("a.cs", ".cs", "C#", 1, false) },
            Array.Empty<string>(), Array.Empty<ScanError>());

        var analysis = new AnalysisResult(
            Smells: Array.Empty<SmellFinding>(),
            SecurityFindings: new[]
            {
                new SecurityFinding("dangerous_function", "eval", "high", "x.js",
                    1, 1, "</script><script>alert(1)</script>", "msg"),
            },
            Errors: Array.Empty<ScanError>());

        var html = HtmlReport.Render(result, analysis, new ScanOptions { Security = true }, DateTimeOffset.UtcNow);

        // The injected </script><script>alert sequence must not appear in the
        // rendered HTML — default System.Text.Json encoding emits '<' as &lt;
        // inside JSON string values, so the literal closing tag never reaches
        // the page. (EscapeForScriptTag remains as defense-in-depth.)
        Assert.DoesNotContain("</script><script>alert", html);

        // The data is still parseable JSON, and the original snippet round-trips.
        var dataMatch = Regex.Match(html,
            "<script type=\"application/json\" id=\"scan-data\">(?<j>.*?)</script>",
            RegexOptions.Singleline);
        Assert.True(dataMatch.Success);
        var doc = JsonDocument.Parse(dataMatch.Groups["j"].Value);
        var snippet = doc.RootElement.GetProperty("securityIssues")[0]
            .GetProperty("snippet").GetString();
        Assert.Equal("</script><script>alert(1)</script>", snippet);
    }

    [Fact]
    public void Render_FlagsLine_ListsActiveFlags()
    {
        var (result, analysis, _) = Empty();
        var options = new ScanOptions { Smells = true, Security = true };
        var html = HtmlReport.Render(result, analysis, options, DateTimeOffset.UtcNow);

        Assert.Contains("--analyze", html);
    }

    [Fact]
    public void Render_FlagsLine_NoFlagsShowsBaseScan()
    {
        var (result, analysis, options) = Empty();
        var html = HtmlReport.Render(result, analysis, options, DateTimeOffset.UtcNow);

        Assert.Contains("scan only", html);
    }

    [Fact]
    public void Render_FlagsLine_SmellsOnly()
    {
        var (result, analysis, _) = Empty();
        var options = new ScanOptions { Smells = true };
        var html = HtmlReport.Render(result, analysis, options, DateTimeOffset.UtcNow);

        Assert.Contains("--smells", html);
    }

    [Fact]
    public void Render_FlagsLine_SecurityOnly()
    {
        var (result, analysis, _) = Empty();
        var options = new ScanOptions { Security = true };
        var html = HtmlReport.Render(result, analysis, options, DateTimeOffset.UtcNow);

        Assert.Contains("--security", html);
    }

    [Fact]
    public void Render_TimestampIsIso8601()
    {
        var (result, analysis, options) = Empty();
        var when = new DateTimeOffset(2026, 5, 7, 11, 48, 0, TimeSpan.Zero);
        var html = HtmlReport.Render(result, analysis, options, when);

        Assert.Contains("2026-05-07T11:48:00", html);
    }
}
