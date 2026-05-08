using Microsoft.Extensions.FileSystemGlobbing;

namespace CodeScanner;

public sealed record AnalysisResult(
    IReadOnlyList<SmellFinding> Smells,
    IReadOnlyList<SecurityFinding> SecurityFindings,
    IReadOnlyList<ScanError> Errors,
    int TotalFunctions);

public sealed class AnalyzerHost
{
    private const long SecurityFileSizeLimit = 1_048_576; // 1 MB

    private readonly ISmellAnalyzer _csharpSmells;
    private readonly ISecurityScanner _secrets;
    private readonly ISecurityScanner _dangerousFunctions;

    public AnalyzerHost()
        : this(new CSharpSmellAnalyzer(), new SecretScanner(), new DangerousFunctionScanner()) { }

    public AnalyzerHost(
        ISmellAnalyzer csharpSmells,
        ISecurityScanner secrets,
        ISecurityScanner dangerousFunctions)
    {
        _csharpSmells = csharpSmells;
        _secrets = secrets;
        _dangerousFunctions = dangerousFunctions;
    }

    public AnalysisResult Analyze(ScanResult scan, ScanOptions options)
    {
        var smells = new List<SmellFinding>();
        var securityFindings = new List<SecurityFinding>();
        var errors = new List<ScanError>();
        var totalFunctions = 0;

        Matcher? skipMatcher = null;
        if (options.Security && options.SecuritySkipGlobs.Count > 0)
        {
            skipMatcher = new Matcher();
            foreach (var glob in options.SecuritySkipGlobs)
            {
                skipMatcher.AddInclude(glob);
            }
        }

        foreach (var entry in scan.FileEntries)
        {
            if (entry.IsBinary) { continue; }

            var path = entry.Path;
            string? content = null;

            if (options.Smells && entry.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                content ??= TryRead(path, errors);
                if (content is not null)
                {
                    try
                    {
                        var smellResult = _csharpSmells.Analyze(path, content);
                        smells.AddRange(smellResult.Findings);
                        totalFunctions += smellResult.TotalFunctions;
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new ScanError(path, $"smell analysis failed: {ex.GetType().Name}: {ex.Message}"));
                    }
                }
            }

            if (options.Security)
            {
                if (skipMatcher is not null)
                {
                    var rel = Path.GetRelativePath(scan.Root, path).Replace('\\', '/');
                    if (skipMatcher.Match(rel).HasMatches) { continue; }
                }

                long length;
                try { length = new FileInfo(path).Length; }
                catch (Exception ex)
                {
                    errors.Add(new ScanError(path, $"{ex.GetType().Name}: {ex.Message}"));
                    continue;
                }

                if (length > SecurityFileSizeLimit)
                {
                    errors.Add(new ScanError(path, "file too large for security scan"));
                    continue;
                }

                content ??= TryRead(path, errors);
                if (content is null) { continue; }

                try { securityFindings.AddRange(_secrets.Scan(path, content)); }
                catch (Exception ex)
                {
                    errors.Add(new ScanError(path, $"secret scan failed: {ex.GetType().Name}: {ex.Message}"));
                }

                try { securityFindings.AddRange(_dangerousFunctions.Scan(path, content)); }
                catch (Exception ex)
                {
                    errors.Add(new ScanError(path, $"dangerous-function scan failed: {ex.GetType().Name}: {ex.Message}"));
                }
            }
        }

        return new AnalysisResult(smells, securityFindings, errors, totalFunctions);
    }

    private static string? TryRead(string path, List<ScanError> errors)
    {
        try { return File.ReadAllText(path); }
        catch (Exception ex)
        {
            errors.Add(new ScanError(path, $"{ex.GetType().Name}: {ex.Message}"));
            return null;
        }
    }
}
