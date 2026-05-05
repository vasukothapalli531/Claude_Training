namespace CodeScanner.Tests.Analyzers;

public class SecretScannerTests
{
    private static readonly SecretScanner Scanner = new();

    [Fact]
    public void DetectsAwsKey_AndRedactsSnippet()
    {
        var content = "var key = \"AKIA1234567890ABCDEF\";\n";
        var findings = Scanner.Scan("a.cs", content);

        Assert.Single(findings);
        var f = findings[0];
        Assert.Equal("hardcoded_secret", f.Type);
        Assert.Equal("aws_access_key", f.Subtype);
        Assert.Equal("high", f.Severity);
        Assert.Equal(1, f.Line);
        Assert.Contains("AKIA••••REDACTED", f.Snippet);
        Assert.DoesNotContain("AKIA1234567890ABCDEF", f.Snippet);
    }

    [Fact]
    public void DetectsPrivateKey_FullyRedacted()
    {
        var content = "const X = \"-----BEGIN RSA PRIVATE KEY-----\";";
        var findings = Scanner.Scan("a.js", content);

        Assert.Single(findings);
        Assert.Equal("private_key", findings[0].Subtype);
        Assert.DoesNotContain("BEGIN RSA PRIVATE KEY", findings[0].Snippet);
        Assert.Contains("••••REDACTED", findings[0].Snippet);
    }

    [Fact]
    public void IgnoresLineWithCodescanIgnore()
    {
        // Note: AWS keys are AKIA + exactly 16 [0-9A-Z] chars.
        var content =
            "var x = \"AKIA1234567890ABCDEF\"; // codescan:ignore\n" +
            "var y = \"AKIAZYXWVUTSRQPONMLK\";\n";

        var findings = Scanner.Scan("a.cs", content);

        Assert.Single(findings);
        Assert.Equal(2, findings[0].Line);
    }

    [Fact]
    public void DetectsMultiplePatternsAcrossLines()
    {
        var content =
            "var aws = \"AKIA1234567890ABCDEF\";\n" +
            "var pat = \"ghp_aBcDeFgHiJkLmNoPqRsTuVwXyZ0123456789ab\";\n" +
            "const password = \"hunter2-strong-pw\";\n";

        var findings = Scanner.Scan("a.cs", content);

        Assert.Equal(3, findings.Count);
        Assert.Contains(findings, f => f.Subtype == "aws_access_key");
        Assert.Contains(findings, f => f.Subtype == "github_pat");
        Assert.Contains(findings, f => f.Subtype == "generic_assign");
    }

    [Fact]
    public void Dedupes_HigherSeverityWinsAtSameLineColumn()
    {
        var content = "var k = \"AKIA1234567890ABCDEF\";\n";
        var findings = Scanner.Scan("a.cs", content);

        Assert.Single(findings);
    }

    [Fact]
    public void EmptyContent_ReturnsNoFindings()
    {
        var findings = Scanner.Scan("a.cs", "");
        Assert.Empty(findings);
    }
}
