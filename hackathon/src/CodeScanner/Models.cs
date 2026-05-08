namespace CodeScanner;

public sealed record ScanError(string Path, string Reason);

public sealed record FileEntry(
    string Path,
    string Extension,
    string Language,
    long Lines,
    bool IsBinary);

public sealed record ScanResult(
    string Root,
    IReadOnlyList<FileEntry> FileEntries,
    IReadOnlyList<string> SkippedDirs,
    IReadOnlyList<ScanError> Errors);

public sealed record ScanOptions
{
    public bool FollowSymlinks { get; init; }
    public IReadOnlyList<string> ExtraExcludes { get; init; } = Array.Empty<string>();
    public bool Smells { get; init; }
    public bool Security { get; init; }
    public IReadOnlyList<string> SecuritySkipGlobs { get; init; } = Array.Empty<string>();
}

public sealed record SmellFinding(
    string Type,
    string Severity,
    string File,
    string Name,
    int StartLine,
    int EndLine,
    int Value,
    int Threshold,
    string Message,
    AiSuggestion? AiSuggestion = null);

public sealed record SecurityFinding(
    string Type,
    string Subtype,
    string Severity,
    string File,
    int Line,
    int Column,
    string Snippet,
    string Message,
    AiSuggestion? AiSuggestion = null);

public sealed record SmellAnalysisResult(
    IReadOnlyList<SmellFinding> Findings,
    int TotalFunctions);

public sealed record AiSuggestion(
    string Explanation,
    string FixedSnippet,
    string Model,
    long ElapsedMs);

public sealed record AiSummary(
    string Model,
    int TotalCalls,
    int Successful,
    int Failed,
    long TotalElapsedMs,
    int QualityScoreIfAllFixed,
    int QualityScoreDelta);
