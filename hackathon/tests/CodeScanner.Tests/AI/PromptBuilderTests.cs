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
        Assert.DoesNotContain("line1\n", content);
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
            Snippet: "REDACTED-LINE",
            Message: "AWS Access Key ID detected");

        var content = PromptBuilder.BuildUserContent(sec, source);

        // Surrounding source lines (7..9 and 11..13) appear; the offending
        // line (10) is REPLACED with the redacted snippet, so "line10" must
        // NOT appear, only "REDACTED-LINE" appears in its place.
        Assert.Contains("line7", content);
        Assert.Contains("line9", content);
        Assert.Contains("REDACTED-LINE", content);
        Assert.Contains("line11", content);
        Assert.Contains("line13", content);
        Assert.DoesNotContain("line10", content);
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

        var content = PromptBuilder.BuildUserContent(smell, "a\nb\nc\nd\ne\n");

        Assert.Contains("Context:", content);
    }
}
