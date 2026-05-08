namespace CodeScanner.Tests.AI;

public class SuggestionParserTests
{
    [Fact]
    public void TryParse_ValidJson_Succeeds()
    {
        var ok = SuggestionParser.TryParse(
            "{\"explanation\": \"do X\", \"fixedSnippet\": \"X();\"}",
            out var s, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(s);
        Assert.Equal("do X", s!.Explanation);
        Assert.Equal("X();", s.FixedSnippet);
    }

    [Fact]
    public void TryParse_CodeFencedJson_StripsFencesAndSucceeds()
    {
        var fenced = "```json\n{\"explanation\": \"do X\", \"fixedSnippet\": \"X();\"}\n```";
        var ok = SuggestionParser.TryParse(fenced, out var s, out _);
        Assert.True(ok);
        Assert.Equal("do X", s!.Explanation);
    }

    [Fact]
    public void TryParse_PlainBackticksFence_AlsoStrips()
    {
        var fenced = "```\n{\"explanation\": \"e\", \"fixedSnippet\": \"f\"}\n```";
        var ok = SuggestionParser.TryParse(fenced, out var s, out _);
        Assert.True(ok);
        Assert.NotNull(s);
    }

    [Fact]
    public void TryParse_InvalidJson_ReturnsFalseWithReason()
    {
        var ok = SuggestionParser.TryParse("not json", out var s, out var error);
        Assert.False(ok);
        Assert.Null(s);
        Assert.NotNull(error);
        Assert.Contains("parse", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParse_MissingExplanation_ReturnsFalse()
    {
        var ok = SuggestionParser.TryParse("{\"fixedSnippet\": \"X();\"}", out _, out var error);
        Assert.False(ok);
        Assert.Contains("explanation", error!);
    }

    [Fact]
    public void TryParse_MissingFixedSnippet_ReturnsFalse()
    {
        var ok = SuggestionParser.TryParse("{\"explanation\": \"e\"}", out _, out var error);
        Assert.False(ok);
        Assert.Contains("fixedSnippet", error!);
    }
}
