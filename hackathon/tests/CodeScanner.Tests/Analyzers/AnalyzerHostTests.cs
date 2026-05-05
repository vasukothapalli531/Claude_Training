namespace CodeScanner.Tests.Analyzers;

public class AnalyzerHostTests
{
    [Fact]
    public void NoFlags_ReturnsEmptyResults()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs", "class C {}");

        var scan = Scanner.Scan(tree.Root, new ScanOptions());
        var host = new AnalyzerHost();
        var result = host.Analyze(scan, new ScanOptions());

        Assert.Empty(result.Smells);
        Assert.Empty(result.SecurityFindings);
    }

    [Fact]
    public void SmellsFlag_ProcessesCsharpFilesOnly()
    {
        using var tree = new TempTree();
        var manyLines = string.Concat(Enumerable.Repeat("var x = 1;\n", 60));
        tree.WriteFile("Big.cs", $"class C {{ void Foo() {{\n{manyLines}}} }}");
        tree.WriteFile("ignore.py", "x = 1");

        var scan = Scanner.Scan(tree.Root, new ScanOptions());
        var host = new AnalyzerHost();
        var result = host.Analyze(scan, new ScanOptions { Smells = true });

        Assert.NotEmpty(result.Smells);
        Assert.All(result.Smells, s => Assert.EndsWith(".cs", s.File));
    }

    [Fact]
    public void SecurityFlag_ProcessesAllNonBinaryFiles()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs",  "var k = \"AKIA1234567890ABCDEF\";");
        tree.WriteFile("b.txt", "password = \"hunter2-strong-pw\"");

        var scan = Scanner.Scan(tree.Root, new ScanOptions());
        var host = new AnalyzerHost();
        var result = host.Analyze(scan, new ScanOptions { Security = true });

        Assert.True(result.SecurityFindings.Count >= 2);
        Assert.Contains(result.SecurityFindings, f => f.File.EndsWith(".cs", StringComparison.Ordinal));
        Assert.Contains(result.SecurityFindings, f => f.File.EndsWith(".txt", StringComparison.Ordinal));
    }

    [Fact]
    public void SecuritySkipGlob_SkipsMatchingFiles()
    {
        using var tree = new TempTree();
        tree.WriteFile("src/code.cs",   "var k = \"AKIA1234567890ABCDEF\";");
        tree.WriteFile("tests/fixtures/keys.txt", "AKIA1234567890ABCDEF");

        var scan = Scanner.Scan(tree.Root, new ScanOptions());
        var host = new AnalyzerHost();
        var result = host.Analyze(scan, new ScanOptions
        {
            Security = true,
            SecuritySkipGlobs = new[] { "tests/**" },
        });

        Assert.Single(result.SecurityFindings);
        Assert.EndsWith("code.cs", result.SecurityFindings[0].File);
    }

    [Fact]
    public void LargeFile_LoggedAndSkippedForSecurity()
    {
        using var tree = new TempTree();
        var huge = new string('a', 1_100_000); // > 1 MB
        tree.WriteFile("big.txt", huge);

        var scan = Scanner.Scan(tree.Root, new ScanOptions());
        var host = new AnalyzerHost();
        var result = host.Analyze(scan, new ScanOptions { Security = true });

        Assert.Empty(result.SecurityFindings);
        Assert.Contains(result.Errors, e => e.Reason.Contains("too large"));
    }
}
