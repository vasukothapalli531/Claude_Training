using System.Text.Json;

namespace CodeScanner.Tests;

public class ReportTests
{
    private static JsonElement Parse(string s) => JsonDocument.Parse(s).RootElement;

    [Fact]
    public void Serialize_EmptyResult_ProducesZerosAndEmptyCollections()
    {
        var result = new ScanResult(
            Root: "C:/x",
            FileEntries: Array.Empty<FileEntry>(),
            SkippedDirs: Array.Empty<string>(),
            Errors: Array.Empty<ScanError>());

        var json = Report.Serialize(result, pretty: false);
        var root = Parse(json);

        Assert.Equal(0, root.GetProperty("totalFiles").GetInt32());
        Assert.Equal(0, root.GetProperty("totalLines").GetInt64());
        Assert.Empty(root.GetProperty("languages").EnumerateObject());
        Assert.Equal(0, root.GetProperty("scanned").GetProperty("errors").GetArrayLength());
    }

    [Fact]
    public void Serialize_AggregatesByLanguage()
    {
        var result = new ScanResult(
            Root: "C:/x",
            FileEntries: new[]
            {
                new FileEntry("a.cs",  ".cs",  "C#",         10, false),
                new FileEntry("b.cs",  ".cs",  "C#",         20, false),
                new FileEntry("c.tsx", ".tsx", "TypeScript",  5, false),
            },
            SkippedDirs: Array.Empty<string>(),
            Errors: Array.Empty<ScanError>());

        var json = Report.Serialize(result, pretty: false);
        var root = Parse(json);

        Assert.Equal(3, root.GetProperty("totalFiles").GetInt32());
        Assert.Equal(35, root.GetProperty("totalLines").GetInt64());

        var cs = root.GetProperty("languages").GetProperty("C#");
        Assert.Equal(2, cs.GetProperty("files").GetInt32());
        Assert.Equal(30, cs.GetProperty("lines").GetInt64());

        var ts = root.GetProperty("languages").GetProperty("TypeScript");
        Assert.Equal(1, ts.GetProperty("files").GetInt32());
        Assert.Equal(5, ts.GetProperty("lines").GetInt64());
    }

    [Fact]
    public void Serialize_BucketsUnknownsAndCollectsExtensions()
    {
        var result = new ScanResult(
            Root: "C:/x",
            FileEntries: new[]
            {
                new FileEntry("a.xyz",   ".xyz", "Unknown", 1, false),
                new FileEntry("Makefile", "",    "Unknown", 2, false),
            },
            SkippedDirs: Array.Empty<string>(),
            Errors: Array.Empty<ScanError>());

        var json = Report.Serialize(result, pretty: false);
        var root = Parse(json);

        var unknown = root.GetProperty("languages").GetProperty("Unknown");
        var exts = unknown.GetProperty("extensions").EnumerateArray().Select(e => e.GetString()).ToHashSet();
        Assert.Contains(".xyz", exts);
        Assert.Contains("",     exts);
    }

    [Fact]
    public void Serialize_IncludesErrorsAndSkippedDirs()
    {
        var result = new ScanResult(
            Root: "C:/x",
            FileEntries: Array.Empty<FileEntry>(),
            SkippedDirs: new[] { ".git", "node_modules" },
            Errors: new[] { new ScanError("a.bin", "binary file, lines not counted") });

        var json = Report.Serialize(result, pretty: false);
        var root = Parse(json);

        var skipped = root.GetProperty("scanned").GetProperty("skippedDirs")
            .EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains(".git", skipped);
        Assert.Contains("node_modules", skipped);

        var errors = root.GetProperty("scanned").GetProperty("errors");
        Assert.Equal(1, errors.GetArrayLength());
        Assert.Equal("a.bin", errors[0].GetProperty("path").GetString());
        Assert.Contains("binary", errors[0].GetProperty("reason").GetString());
    }

    [Fact]
    public void Serialize_PrettyTrueAddsIndentation()
    {
        var result = new ScanResult("C:/x", Array.Empty<FileEntry>(), Array.Empty<string>(), Array.Empty<ScanError>());

        var compact = Report.Serialize(result, pretty: false);
        var pretty  = Report.Serialize(result, pretty: true);

        Assert.DoesNotContain("\n", compact);
        Assert.Contains("\n", pretty);
    }

    [Fact]
    public void Serialize_NormalizesPathsToForwardSlashes()
    {
        var result = new ScanResult(
            Root: @"C:\some\dir",
            FileEntries: Array.Empty<FileEntry>(),
            SkippedDirs: Array.Empty<string>(),
            Errors: new[] { new ScanError(@"C:\some\dir\file.bin", "binary file, lines not counted") });

        var json = Report.Serialize(result, pretty: false);
        var root = Parse(json);

        Assert.Equal("C:/some/dir", root.GetProperty("scanned").GetProperty("root").GetString());
        Assert.Equal("C:/some/dir/file.bin",
            root.GetProperty("scanned").GetProperty("errors")[0].GetProperty("path").GetString());
    }

    [Fact]
    public void Serialize_BackwardsCompat_NoAnalysisKeysWhenAnalysisEmpty()
    {
        var result = new ScanResult("C:/x", Array.Empty<FileEntry>(), Array.Empty<string>(), Array.Empty<ScanError>());
        var analysis = new AnalysisResult(Array.Empty<SmellFinding>(), Array.Empty<SecurityFinding>(), Array.Empty<ScanError>(), TotalFunctions: 0);
        var options = new ScanOptions();

        var json = Report.Serialize(result, analysis, options, pretty: false);
        var root = Parse(json);

        Assert.False(root.TryGetProperty("smells", out _));
        Assert.False(root.TryGetProperty("securityIssues", out _));
    }

    [Fact]
    public void Serialize_IncludesSmellsWhenSmellsFlagOn()
    {
        var result = new ScanResult(
            "C:/x",
            new[] { new FileEntry("a.cs", ".cs", "C#", 10, false) },
            Array.Empty<string>(), Array.Empty<ScanError>());

        var analysis = new AnalysisResult(
            Smells: new[] { new SmellFinding("long_function", "medium", "a.cs", "Foo", 1, 80, 80, 50, "msg") },
            SecurityFindings: Array.Empty<SecurityFinding>(),
            Errors: Array.Empty<ScanError>(),
            TotalFunctions: 0);

        var options = new ScanOptions { Smells = true };

        var json = Report.Serialize(result, analysis, options, pretty: false);
        var root = Parse(json);

        Assert.Equal(1, root.GetProperty("smells").GetArrayLength());
        Assert.Equal("long_function", root.GetProperty("smells")[0].GetProperty("type").GetString());

        var lang = root.GetProperty("languages").GetProperty("C#");
        Assert.Equal(1, lang.GetProperty("smells").GetProperty("medium").GetInt32());
        Assert.Equal(1, lang.GetProperty("smells").GetProperty("total").GetInt32());
    }

    [Fact]
    public void Serialize_IncludesSecurityWhenSecurityFlagOn()
    {
        var result = new ScanResult(
            "C:/x",
            new[] { new FileEntry("a.cs", ".cs", "C#", 10, false) },
            Array.Empty<string>(), Array.Empty<ScanError>());

        var analysis = new AnalysisResult(
            Smells: Array.Empty<SmellFinding>(),
            SecurityFindings: new[] {
                new SecurityFinding("hardcoded_secret","aws_access_key","high","a.cs",1,5,"snip","AWS detected")
            },
            Errors: Array.Empty<ScanError>(),
            TotalFunctions: 0);

        var options = new ScanOptions { Security = true };
        var json = Report.Serialize(result, analysis, options, pretty: false);
        var root = Parse(json);

        Assert.Equal(1, root.GetProperty("securityIssues").GetArrayLength());
        Assert.Equal("aws_access_key",
            root.GetProperty("securityIssues")[0].GetProperty("subtype").GetString());

        var lang = root.GetProperty("languages").GetProperty("C#");
        Assert.Equal(1, lang.GetProperty("security").GetProperty("high").GetInt32());
    }

    [Fact]
    public void Serialize_AnalysisErrorsMergedIntoScannedErrors()
    {
        var result = new ScanResult("C:/x", Array.Empty<FileEntry>(), Array.Empty<string>(),
            new[] { new ScanError("a.bin", "binary file, lines not counted") });

        var analysis = new AnalysisResult(
            Smells: Array.Empty<SmellFinding>(),
            SecurityFindings: Array.Empty<SecurityFinding>(),
            Errors: new[] { new ScanError("big.txt", "file too large for security scan") },
            TotalFunctions: 0);

        var options = new ScanOptions { Security = true };
        var json = Report.Serialize(result, analysis, options, pretty: false);
        var root = Parse(json);

        var errs = root.GetProperty("scanned").GetProperty("errors");
        Assert.Equal(2, errs.GetArrayLength());
    }
}
