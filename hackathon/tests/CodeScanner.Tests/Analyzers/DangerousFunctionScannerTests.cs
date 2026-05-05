namespace CodeScanner.Tests.Analyzers;

public class DangerousFunctionScannerTests
{
    private static readonly DangerousFunctionScanner Scanner = new();

    [Fact]
    public void DetectsJsEval()
    {
        var content = "function run(input) { eval(input); }\n";
        var findings = Scanner.Scan("a.js", content);

        Assert.Single(findings);
        Assert.Equal("dangerous_function", findings[0].Type);
        Assert.Equal("eval", findings[0].Subtype);
        Assert.Equal("high", findings[0].Severity);
        Assert.Equal(1, findings[0].Line);
    }

    [Fact]
    public void DetectsPyEvalAndExec()
    {
        var content = "x = eval(s)\ny = exec(s)\n";
        var findings = Scanner.Scan("a.py", content);

        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, f => f.Subtype == "eval");
        Assert.Contains(findings, f => f.Subtype == "exec");
    }

    [Fact]
    public void DetectsPowerShellInvokeExpression()
    {
        var content = "Invoke-Expression $cmd\n";
        var findings = Scanner.Scan("a.ps1", content);

        Assert.Single(findings);
        Assert.Equal("invoke_expression", findings[0].Subtype);
    }

    [Fact]
    public void DetectsBashEval()
    {
        var content = "eval ${cmd}\n";
        var findings = Scanner.Scan("a.sh", content);

        Assert.Single(findings);
        Assert.Equal("eval", findings[0].Subtype);
    }

    [Fact]
    public void DetectsCsAssemblyLoad_LowSeverity()
    {
        var content = "var asm = Assembly.Load(bytes);\n";
        var findings = Scanner.Scan("a.cs", content);

        Assert.Single(findings);
        Assert.Equal("assembly_load", findings[0].Subtype);
        Assert.Equal("low", findings[0].Severity);
    }

    [Fact]
    public void IgnoresUnknownExtension()
    {
        var content = "eval('foo')\n";
        var findings = Scanner.Scan("a.unknown", content);

        Assert.Empty(findings);
    }

    [Fact]
    public void RespectsIgnoreDirective()
    {
        var content = "eval('foo'); // codescan:ignore\n";
        var findings = Scanner.Scan("a.js", content);

        Assert.Empty(findings);
    }

    [Fact]
    public void SnippetIsTheMatchedLine_NoRedaction()
    {
        var content = "eval('hello')\n";
        var findings = Scanner.Scan("a.js", content);

        Assert.Single(findings);
        Assert.Equal("eval('hello')", findings[0].Snippet);
    }
}
