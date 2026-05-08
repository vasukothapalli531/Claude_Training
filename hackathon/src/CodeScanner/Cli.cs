using System.CommandLine;

namespace CodeScanner;

public static class Cli
{
    public static async Task<int> RunAsync(string[] args)
    {
        var pathArg = new Argument<string>("path") { Description = "Directory to scan" };
        var outputOpt = new Option<string?>("--output", "-o") { Description = "Write JSON to this file instead of stdout" };
        var excludeOpt = new Option<string[]>("--exclude", "-e")
        {
            Description = "Extra directory names to skip (additive to defaults)",
            AllowMultipleArgumentsPerToken = true,
        };
        var followOpt = new Option<bool>("--follow-symlinks") { Description = "Follow symlinked directories (default: skip)" };
        var prettyOpt = new Option<bool>("--pretty") { Description = "Pretty-print JSON output" };
        var verboseOpt = new Option<bool>("--verbose", "-v") { Description = "Log scan progress to stderr" };
        var smellsOpt   = new Option<bool>("--smells")    { Description = "Run Roslyn-based smell analyzer on .cs files" };
        var securityOpt = new Option<bool>("--security")  { Description = "Run regex-based security scanners (secrets + dangerous functions)" };
        var analyzeOpt  = new Option<bool>("--analyze")   { Description = "Shorthand for --smells --security" };
        var securitySkipOpt = new Option<string[]>("--security-skip")
        {
            Description = "Glob patterns to skip during security scan only (additive)",
            AllowMultipleArgumentsPerToken = true,
        };
        var htmlOpt = new Option<string?>("--html") { Description = "Write a self-contained HTML report to this file" };

        var root = new RootCommand("Recursively scan a directory and emit JSON file/line statistics.")
        {
            pathArg, outputOpt, excludeOpt, followOpt, prettyOpt, verboseOpt,
            smellsOpt, securityOpt, analyzeOpt, securitySkipOpt, htmlOpt,
        };

        root.SetAction(parseResult =>
        {
            var path     = parseResult.GetValue(pathArg)!;
            var output   = parseResult.GetValue(outputOpt);
            var excludes = parseResult.GetValue(excludeOpt) ?? Array.Empty<string>();
            var follow   = parseResult.GetValue(followOpt);
            var pretty   = parseResult.GetValue(prettyOpt);
            var verbose  = parseResult.GetValue(verboseOpt);
            var smells   = parseResult.GetValue(smellsOpt);
            var security = parseResult.GetValue(securityOpt);
            var analyze  = parseResult.GetValue(analyzeOpt);
            var skip     = parseResult.GetValue(securitySkipOpt) ?? Array.Empty<string>();
            var html     = parseResult.GetValue(htmlOpt);

            if (analyze) { smells = true; security = true; }

            return Execute(path, output, excludes, follow, pretty, verbose, smells, security, skip, html);
        });

        return await root.Parse(args).InvokeAsync();
    }

    private static int Execute(
        string path,
        string? output,
        string[] excludes,
        bool followSymlinks,
        bool pretty,
        bool verbose,
        bool smells,
        bool security,
        string[] securitySkipGlobs,
        string? htmlPath)
    {
        if (!Directory.Exists(path))
        {
            if (File.Exists(path))
            {
                Console.Error.WriteLine($"error: path is a file, not a directory: {path}");
            }
            else
            {
                Console.Error.WriteLine($"error: path does not exist: {path}");
            }
            return 1;
        }

        try
        {
            if (verbose) { Console.Error.WriteLine($"info: scanning {path}"); }

            var options = new ScanOptions
            {
                FollowSymlinks = followSymlinks,
                ExtraExcludes = excludes,
                Smells = smells,
                Security = security,
                SecuritySkipGlobs = securitySkipGlobs,
            };

            var result = Scanner.Scan(path, options);

            AnalysisResult analysis;
            if (smells || security)
            {
                if (verbose) { Console.Error.WriteLine("info: running analysis pass"); }
                var host = new AnalyzerHost();
                analysis = host.Analyze(result, options);
            }
            else
            {
                analysis = new AnalysisResult(
                    Array.Empty<SmellFinding>(),
                    Array.Empty<SecurityFinding>(),
                    Array.Empty<ScanError>(),
                    TotalFunctions: 0);
            }

            // JSON output (stdout or --output file).
            var json = Report.Serialize(result, analysis, options, pretty);
            if (htmlPath is not null)
            {
                if (output is not null)
                {
                    File.WriteAllText(output, json);
                    if (verbose) { Console.Error.WriteLine($"info: wrote {output}"); }
                }
                // else: skip stdout when --html is set without --output, to keep it quiet.
            }
            else if (output is null)
            {
                Console.Out.WriteLine(json);
            }
            else
            {
                File.WriteAllText(output, json);
                if (verbose) { Console.Error.WriteLine($"info: wrote {output}"); }
            }

            // HTML output.
            if (htmlPath is not null)
            {
                try
                {
                    var html = HtmlReport.Render(result, analysis, options, DateTimeOffset.UtcNow);
                    File.WriteAllText(htmlPath, html);
                    if (verbose) { Console.Error.WriteLine($"info: wrote {htmlPath}"); }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"error: cannot write HTML report: {ex.GetType().Name}: {ex.Message}");
                    return 2;
                }
            }

            if (verbose)
            {
                Console.Error.WriteLine(
                    $"info: {result.FileEntries.Count} files, {result.Errors.Count + analysis.Errors.Count} errors, " +
                    $"{analysis.Smells.Count} smells, {analysis.SecurityFindings.Count} security");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.GetType().Name}: {ex.Message}");
            return 2;
        }
    }
}
