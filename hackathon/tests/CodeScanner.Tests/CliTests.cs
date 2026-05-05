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
}
