namespace CodeScanner;

public sealed record FileRiskScore(
    string File,
    int RiskScore,
    int High,
    int Medium,
    int Low,
    long Lines);

public static class RiskScoring
{
    public static int ComputeQualityScore(
        IReadOnlyList<SmellFinding> smells,
        IReadOnlyList<SecurityFinding> securityFindings)
    {
        var penalty = 0.0;
        foreach (var s in smells) { penalty += SeverityWeight(s.Severity); }
        foreach (var s in securityFindings) { penalty += SeverityWeight(s.Severity); }
        var score = 100.0 - penalty;
        if (score < 0) { score = 0; }
        return (int)Math.Round(score, MidpointRounding.AwayFromZero);
    }

    public static string GradeFor(int score)
    {
        if (score >= 90) { return "A"; }
        if (score >= 80) { return "B"; }
        if (score >= 70) { return "C"; }
        if (score >= 60) { return "D"; }
        return "F";
    }

    public static int EstimatedFixMinutes(
        IReadOnlyList<SmellFinding> smells,
        IReadOnlyList<SecurityFinding> securityFindings)
    {
        var minutes = 0;
        foreach (var s in smells) { minutes += FixMinutes(s.Severity); }
        foreach (var s in securityFindings) { minutes += FixMinutes(s.Severity); }
        return minutes;
    }

    public static IReadOnlyList<FileRiskScore> BuildFileRiskScores(
        IReadOnlyList<FileEntry> entries,
        IReadOnlyList<SmellFinding> smells,
        IReadOnlyList<SecurityFinding> securityFindings)
    {
        var lookup = entries.ToDictionary(e => e.Path, e => e.Lines, StringComparer.OrdinalIgnoreCase);
        var perFile = new Dictionary<string, (int high, int medium, int low)>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in smells)             { Bump(perFile, s.File, s.Severity); }
        foreach (var s in securityFindings)   { Bump(perFile, s.File, s.Severity); }

        var result = new List<FileRiskScore>(perFile.Count);
        foreach (var (file, counts) in perFile)
        {
            var risk = 10 * counts.high + 4 * counts.medium + 1 * counts.low;
            var lines = lookup.TryGetValue(file, out var l) ? l : 0L;
            result.Add(new FileRiskScore(file, risk, counts.high, counts.medium, counts.low, lines));
        }

        result.Sort((a, b) => b.RiskScore.CompareTo(a.RiskScore));
        return result;
    }

    private static double SeverityWeight(string severity) => severity switch
    {
        "high" => 5.0,
        "medium" => 2.0,
        "low" => 0.5,
        _ => 0.0,
    };

    private static int FixMinutes(string severity) => severity switch
    {
        "high" => 30,
        "medium" => 10,
        "low" => 5,
        _ => 0,
    };

    private static void Bump(
        Dictionary<string, (int high, int medium, int low)> perFile,
        string file,
        string severity)
    {
        perFile.TryGetValue(file, out var c);
        switch (severity)
        {
            case "high":   c.high++;   break;
            case "medium": c.medium++; break;
            case "low":    c.low++;    break;
        }
        perFile[file] = c;
    }
}
