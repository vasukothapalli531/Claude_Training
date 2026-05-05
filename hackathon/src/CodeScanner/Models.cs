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
}
