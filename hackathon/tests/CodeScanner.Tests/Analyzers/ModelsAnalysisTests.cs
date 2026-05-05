namespace CodeScanner.Tests.Analyzers;

public class ModelsAnalysisTests
{
    [Fact]
    public void SmellFinding_RecordEqualityWorks()
    {
        var a = new SmellFinding(
            Type: "long_function", Severity: "medium",
            File: "x.cs", Name: "Foo",
            StartLine: 1, EndLine: 60,
            Value: 60, Threshold: 50,
            Message: "msg");
        var b = a with { };
        Assert.Equal(a, b);
    }

    [Fact]
    public void SecurityFinding_HasAllExpectedFields()
    {
        var f = new SecurityFinding(
            Type: "hardcoded_secret",
            Subtype: "aws_access_key",
            Severity: "high",
            File: "x.cs",
            Line: 42, Column: 24,
            Snippet: "var k = \"AKIA••••REDACTED\";",
            Message: "AWS Access Key ID detected");

        Assert.Equal("hardcoded_secret", f.Type);
        Assert.Equal("aws_access_key", f.Subtype);
        Assert.Equal("high", f.Severity);
    }

    [Fact]
    public void ScanOptions_AnalysisFlagsDefaultOff()
    {
        var o = new ScanOptions();
        Assert.False(o.Smells);
        Assert.False(o.Security);
        Assert.NotNull(o.SecuritySkipGlobs);
        Assert.Empty(o.SecuritySkipGlobs);
    }
}
