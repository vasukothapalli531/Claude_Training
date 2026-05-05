using System.Text.RegularExpressions;

namespace CodeScanner.Tests.Analyzers;

public class PatternsTests
{
    [Theory]
    [InlineData("AKIA1234567890ABCDEF", true)]
    [InlineData("ASIA1234567890ABCDEF", true)]
    [InlineData("akia1234567890abcdef", false)]
    [InlineData("AKIA1234", false)]
    public void AwsAccessKey_Match(string input, bool expected)
    {
        var rule = Patterns.SecretRules.Single(r => r.Subtype == "aws_access_key");
        Assert.Equal(expected, rule.Regex.IsMatch(input));
    }

    [Theory]
    [InlineData("ghp_aBcDeFgHiJkLmNoPqRsTuVwXyZ0123456789ab", true)]
    [InlineData("gho_aBcDeFgHiJkLmNoPqRsTuVwXyZ0123456789ab", true)]
    [InlineData("ghx_aBcDeFgHiJkLmNoPqRsTuVwXyZ0123456789ab", false)]
    public void GithubPat_Match(string input, bool expected)
    {
        var rule = Patterns.SecretRules.Single(r => r.Subtype == "github_pat");
        Assert.Equal(expected, rule.Regex.IsMatch(input));
    }

    [Theory]
    [InlineData("password = \"hunter2pass\"", true)]
    [InlineData("ApiKey: \"my-api-key-1234567890\"", true)]
    [InlineData("password=short", false)]
    [InlineData("password = ''", false)]
    public void GenericAssign_Match(string input, bool expected)
    {
        var rule = Patterns.SecretRules.Single(r => r.Subtype == "generic_assign");
        Assert.Equal(expected, rule.Regex.IsMatch(input));
    }

    [Fact]
    public void DangerousFunctions_KeyedByExtension()
    {
        Assert.True(Patterns.DangerousFunctionsByExt.ContainsKey(".js"));
        Assert.True(Patterns.DangerousFunctionsByExt.ContainsKey(".ts"));
        Assert.True(Patterns.DangerousFunctionsByExt.ContainsKey(".py"));
        Assert.True(Patterns.DangerousFunctionsByExt.ContainsKey(".ps1"));
        Assert.True(Patterns.DangerousFunctionsByExt.ContainsKey(".sh"));
        Assert.True(Patterns.DangerousFunctionsByExt.ContainsKey(".cs"));
    }

    [Fact]
    public void JsEval_HighSeverity()
    {
        var rules = Patterns.DangerousFunctionsByExt[".js"];
        var evalRule = rules.Single(r => r.Subtype == "eval");
        Assert.Equal("high", evalRule.Severity);
        Assert.Matches(evalRule.Regex, "eval('hello')");
        Assert.DoesNotMatch(evalRule.Regex, "// eval below");
    }

    [Fact]
    public void SeverityValues_AreOnlyLowMediumHigh()
    {
        var allowed = new[] { "low", "medium", "high" };
        Assert.All(Patterns.SecretRules, r => Assert.Contains(r.Severity, allowed));
        foreach (var (_, rules) in Patterns.DangerousFunctionsByExt)
        {
            Assert.All(rules, r => Assert.Contains(r.Severity, allowed));
        }
    }
}
