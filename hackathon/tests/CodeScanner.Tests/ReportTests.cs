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
}
