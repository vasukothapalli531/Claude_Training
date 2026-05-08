namespace CodeScanner.Tests.Html;

public class RiskScoringTests
{
    [Fact]
    public void ComputeQualityScore_NoFindings_Is100()
    {
        Assert.Equal(100, RiskScoring.ComputeQualityScore(
            Array.Empty<SmellFinding>(), Array.Empty<SecurityFinding>()));
    }

    [Fact]
    public void ComputeQualityScore_OneHigh_Is95()
    {
        var smells = new[] { Smell("high") };
        Assert.Equal(95, RiskScoring.ComputeQualityScore(smells, Array.Empty<SecurityFinding>()));
    }

    [Fact]
    public void ComputeQualityScore_OneOfEachInSecurity_Is93()
    {
        var sec = new[] { Sec("high"), Sec("medium"), Sec("low") };
        // 100 - 5 - 2 - 0.5 = 92.5 -> rounded to 93 (we round to nearest int, away-from-zero)
        Assert.Equal(93, RiskScoring.ComputeQualityScore(Array.Empty<SmellFinding>(), sec));
    }

    [Fact]
    public void ComputeQualityScore_FloorIsZero()
    {
        var smells = Enumerable.Repeat(Smell("high"), 30).ToArray();
        Assert.Equal(0, RiskScoring.ComputeQualityScore(smells, Array.Empty<SecurityFinding>()));
    }

    [Theory]
    [InlineData(100, "A")]
    [InlineData(95,  "A")]
    [InlineData(90,  "A")]
    [InlineData(89,  "B")]
    [InlineData(80,  "B")]
    [InlineData(79,  "C")]
    [InlineData(70,  "C")]
    [InlineData(69,  "D")]
    [InlineData(60,  "D")]
    [InlineData(59,  "F")]
    [InlineData(0,   "F")]
    public void GradeFor_AtAllBoundaries(int score, string expected)
    {
        Assert.Equal(expected, RiskScoring.GradeFor(score));
    }

    [Fact]
    public void EstimatedFixMinutes_OneOfEach_Is45()
    {
        var smells = new[] { Smell("high") };
        var sec    = new[] { Sec("medium"), Sec("low") };
        // 30*1 + 10*1 + 5*1 = 45
        Assert.Equal(45, RiskScoring.EstimatedFixMinutes(smells, sec));
    }

    [Fact]
    public void BuildFileRiskScores_OnlyIncludesFilesWithFindings()
    {
        var smells = new[] { Smell("high", "a.cs"), Smell("low", "a.cs") };
        var sec    = new[] { Sec("medium", "b.cs") };
        var entries = new[]
        {
            new FileEntry("a.cs", ".cs", "C#", 100, false),
            new FileEntry("b.cs", ".cs", "C#", 50,  false),
            new FileEntry("c.cs", ".cs", "C#", 30,  false), // no findings
        };

        var risks = RiskScoring.BuildFileRiskScores(entries, smells, sec);

        Assert.Equal(2, risks.Count);
        Assert.DoesNotContain(risks, r => r.File == "c.cs");
    }

    [Fact]
    public void BuildFileRiskScores_SortsByRiskDesc()
    {
        var smells = new[]
        {
            Smell("high", "big.cs"), Smell("medium", "big.cs"),
            Smell("low",  "small.cs"),
        };
        var entries = new[]
        {
            new FileEntry("big.cs",   ".cs", "C#", 100, false),
            new FileEntry("small.cs", ".cs", "C#", 20,  false),
        };

        var risks = RiskScoring.BuildFileRiskScores(entries, smells, Array.Empty<SecurityFinding>());

        Assert.Equal("big.cs",   risks[0].File);
        Assert.Equal("small.cs", risks[1].File);
        Assert.True(risks[0].RiskScore > risks[1].RiskScore);
    }

    [Fact]
    public void BuildFileRiskScores_AggregatesPerFileSeverityCounts()
    {
        var smells = new[]
        {
            Smell("high",   "x.cs"),
            Smell("medium", "x.cs"),
            Smell("medium", "x.cs"),
            Smell("low",    "x.cs"),
        };
        var entries = new[] { new FileEntry("x.cs", ".cs", "C#", 200, false) };

        var risks = RiskScoring.BuildFileRiskScores(entries, smells, Array.Empty<SecurityFinding>());

        Assert.Single(risks);
        var x = risks[0];
        Assert.Equal(1, x.High);
        Assert.Equal(2, x.Medium);
        Assert.Equal(1, x.Low);
        // 10*1 + 4*2 + 1*1 = 19
        Assert.Equal(19, x.RiskScore);
        Assert.Equal(200, x.Lines);
    }

    private static SmellFinding Smell(string severity, string file = "a.cs") =>
        new("long_function", severity, file, "Foo", 1, 80, 80, 50, "msg");

    private static SecurityFinding Sec(string severity, string file = "a.cs") =>
        new("hardcoded_secret", "aws_access_key", severity, file, 1, 1, "snip", "msg");
}
