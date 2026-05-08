using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace CodeScanner.Tests;

public class CliTests
{
    private static string FindSrcCsproj()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src", "CodeScanner")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        Assert.NotNull(dir);
        return Path.Combine(dir!, "src", "CodeScanner", "CodeScanner.csproj");
    }

    private static (int ExitCode, string Stdout, string Stderr) RunCli(params string[] args)
    {
        var csproj = FindSrcCsproj();
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--no-build");
        psi.ArgumentList.Add("--project");
        psi.ArgumentList.Add(csproj);
        psi.ArgumentList.Add("--");
        foreach (var a in args) { psi.ArgumentList.Add(a); }

        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, stdout, stderr);
    }

    [Fact]
    public void Cli_MissingPath_ExitsOneAndWritesError()
    {
        var (exit, stdout, stderr) = RunCli("C:/this-path-does-not-exist-123456");

        Assert.Equal(1, exit);
        Assert.Empty(stdout);
        Assert.NotEmpty(stderr);
    }

    [Fact]
    public void Cli_PathIsFile_ExitsOne()
    {
        using var tree = new TempTree();
        var file = tree.WriteFile("a.cs", "class A {}\n");

        var (exit, stdout, stderr) = RunCli(file);

        Assert.Equal(1, exit);
        Assert.Empty(stdout);
        Assert.NotEmpty(stderr);
    }

    [Fact]
    public void Cli_ValidDir_ExitsZeroAndPrintsJsonToStdout()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs",   "class A {}\n");
        tree.WriteFile("b.py",   "x = 1\n");

        var (exit, stdout, _) = RunCli(tree.Root);

        Assert.Equal(0, exit);
        var doc = JsonDocument.Parse(stdout);
        Assert.Equal(2, doc.RootElement.GetProperty("totalFiles").GetInt32());
    }

    [Fact]
    public void Cli_OutputFlag_WritesJsonToFileNotStdout()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs", "class A {}\n");
        var outFile = Path.Combine(tree.Root, "report.json");

        var (exit, stdout, _) = RunCli(tree.Root, "--output", outFile);

        Assert.Equal(0, exit);
        Assert.Empty(stdout.Trim());
        Assert.True(File.Exists(outFile));
        var doc = JsonDocument.Parse(File.ReadAllText(outFile));
        Assert.Equal(1, doc.RootElement.GetProperty("totalFiles").GetInt32());
    }

    [Fact]
    public void Cli_PrettyFlag_ProducesIndentedOutput()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs", "class A {}\n");

        var (exit, stdout, _) = RunCli(tree.Root, "--pretty");

        Assert.Equal(0, exit);
        Assert.Contains("\n", stdout);
    }

    [Fact]
    public void Cli_ExcludeFlag_AdditiveToDefaults()
    {
        using var tree = new TempTree();
        tree.WriteFile("keep.cs",     "class K {}\n");
        tree.WriteFile("skipme/x.cs", "class S {}\n");

        var (exit, stdout, _) = RunCli(tree.Root, "--exclude", "skipme");

        Assert.Equal(0, exit);
        var doc = JsonDocument.Parse(stdout);
        Assert.Equal(1, doc.RootElement.GetProperty("totalFiles").GetInt32());
    }

    [Fact]
    public void Cli_Smells_AddsSmellsArrayToOutput()
    {
        using var tree = new TempTree();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("class C {");
        sb.AppendLine("    void Foo(int a, int b, int c, int d, int e, int f) {");
        for (var i = 0; i < 60; i++) { sb.AppendLine("        var x = 1;"); }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        tree.WriteFile("a.cs", sb.ToString());

        var (exit, stdout, _) = RunCli(tree.Root, "--smells");

        Assert.Equal(0, exit);
        var doc = JsonDocument.Parse(stdout);
        var smells = doc.RootElement.GetProperty("smells");
        Assert.True(smells.GetArrayLength() >= 2);
    }

    [Fact]
    public void Cli_Security_DetectsHardcodedAwsKey()
    {
        using var tree = new TempTree();
        tree.WriteFile("config.cs", "var k = \"AKIA1234567890ABCDEF\";\n");

        var (exit, stdout, _) = RunCli(tree.Root, "--security");

        Assert.Equal(0, exit);
        var doc = JsonDocument.Parse(stdout);
        var sec = doc.RootElement.GetProperty("securityIssues");
        Assert.Equal(1, sec.GetArrayLength());
        Assert.Equal("aws_access_key", sec[0].GetProperty("subtype").GetString());
    }

    [Fact]
    public void Cli_Analyze_EnablesBothFlags()
    {
        using var tree = new TempTree();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("class C {");
        sb.AppendLine("    void Foo() {");
        for (var i = 0; i < 60; i++) { sb.AppendLine("        var x = 1;"); }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine("// AKIA1234567890ABCDEF");
        tree.WriteFile("a.cs", sb.ToString());

        var (exit, stdout, _) = RunCli(tree.Root, "--analyze");

        Assert.Equal(0, exit);
        var root = JsonDocument.Parse(stdout).RootElement;
        Assert.True(root.GetProperty("smells").GetArrayLength() >= 1);
        Assert.True(root.GetProperty("securityIssues").GetArrayLength() >= 1);
    }

    [Fact]
    public void Cli_NoAnalysisFlags_OmitsAnalysisKeys()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs", "class C {}\n");

        var (exit, stdout, _) = RunCli(tree.Root);

        Assert.Equal(0, exit);
        var root = JsonDocument.Parse(stdout).RootElement;
        Assert.False(root.TryGetProperty("smells", out _));
        Assert.False(root.TryGetProperty("securityIssues", out _));
    }

    private static string FindFixturesRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "fixtures")))
        {
            var parent = Path.GetDirectoryName(dir);
            if (parent == dir) { break; }
            dir = parent;
        }
        Assert.NotNull(dir);
        return Path.Combine(dir!, "fixtures");
    }

    [Fact]
    public void Cli_Analyze_OnFixtures_ProducesExpectedFindings()
    {
        var fixtures = FindFixturesRoot();
        Assert.True(Directory.Exists(fixtures), $"fixtures missing at {fixtures}");

        var (exit, stdout, _) = RunCli(fixtures, "--analyze");

        Assert.Equal(0, exit);
        var root = JsonDocument.Parse(stdout).RootElement;

        var smellTypes = root.GetProperty("smells").EnumerateArray()
            .Select(s => s.GetProperty("type").GetString()).ToHashSet();
        Assert.Contains("long_function", smellTypes);
        Assert.Contains("long_parameter_list", smellTypes);
        Assert.Contains("deep_nesting", smellTypes);

        var securitySubtypes = root.GetProperty("securityIssues").EnumerateArray()
            .Select(s => s.GetProperty("subtype").GetString()).ToHashSet();
        Assert.Contains("aws_access_key", securitySubtypes);
        Assert.Contains("github_pat", securitySubtypes);
        Assert.Contains("eval", securitySubtypes);
        Assert.Contains("new_function", securitySubtypes);
    }

    [Fact]
    public void Cli_HtmlFlag_WritesFile()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs", "class A {}\n");
        var outFile = Path.Combine(tree.Root, "report.html");

        var (exit, stdout, _) = RunCli(tree.Root, "--html", outFile);

        Assert.Equal(0, exit);
        Assert.Empty(stdout.Trim());
        Assert.True(File.Exists(outFile));
        var html = File.ReadAllText(outFile);
        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("id=\"scan-data\"", html);
    }

    [Fact]
    public void Cli_HtmlFlag_WithAnalyze_EmbedsFindings()
    {
        using var tree = new TempTree();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("class C {");
        sb.AppendLine("    void Foo(int a, int b, int c, int d, int e, int f) {");
        for (var i = 0; i < 60; i++) { sb.AppendLine("        var x = 1;"); }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        tree.WriteFile("a.cs", sb.ToString());
        var outFile = Path.Combine(tree.Root, "r.html");

        var (exit, _, _) = RunCli(tree.Root, "--html", outFile, "--analyze");

        Assert.Equal(0, exit);
        var html = File.ReadAllText(outFile);
        var match = System.Text.RegularExpressions.Regex.Match(
            html,
            "<script type=\"application/json\" id=\"scan-data\">(?<j>.*?)</script>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(match.Success);
        var doc = System.Text.Json.JsonDocument.Parse(match.Groups["j"].Value);
        Assert.True(doc.RootElement.GetProperty("smells").GetArrayLength() >= 1);
    }

    [Fact]
    public void Cli_HtmlFlag_AndJsonOutput_BothProduced()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs", "class A {}\n");
        var jsonFile = Path.Combine(tree.Root, "out.json");
        var htmlFile = Path.Combine(tree.Root, "out.html");

        var (exit, stdout, _) = RunCli(tree.Root, "--output", jsonFile, "--html", htmlFile);

        Assert.Equal(0, exit);
        Assert.Empty(stdout.Trim());
        Assert.True(File.Exists(jsonFile));
        Assert.True(File.Exists(htmlFile));

        var jsonText = File.ReadAllText(jsonFile);
        var html = File.ReadAllText(htmlFile);
        var match = System.Text.RegularExpressions.Regex.Match(
            html,
            "<script type=\"application/json\" id=\"scan-data\">(?<j>.*?)</script>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(match.Success);

        var embeddedJson = match.Groups["j"].Value.Replace("<\\/script>", "</script>");
        Assert.Equal(jsonText, embeddedJson);
    }

    [Fact]
    public void Cli_HtmlFlag_BadDirectory_ExitsTwo()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs", "class A {}\n");
        var bogus = Path.Combine(tree.Root, "no", "such", "dir", "r.html");

        var (exit, _, stderr) = RunCli(tree.Root, "--html", bogus);

        Assert.Equal(2, exit);
        Assert.Contains("error:", stderr);
    }
}
