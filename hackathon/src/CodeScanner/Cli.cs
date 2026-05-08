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
        var fixOpt  = new Option<bool>("--fix-suggestions") { Description = "Call Anthropic API per finding to embed AI fix suggestions (requires ANTHROPIC_API_KEY)" };
        var aiModelOpt = new Option<string>("--ai-model") { Description = "Override the AI model id", DefaultValueFactory = _ => "claude-haiku-4-5" };
        var aiConcurrencyOpt = new Option<int>("--ai-concurrency") { Description = "Max parallel AI calls", DefaultValueFactory = _ => 4 };

        var root = new RootCommand("Recursively scan a directory and emit JSON file/line statistics.")
        {
            pathArg, outputOpt, excludeOpt, followOpt, prettyOpt, verboseOpt,
            smellsOpt, securityOpt, analyzeOpt, securitySkipOpt, htmlOpt,
            fixOpt, aiModelOpt, aiConcurrencyOpt,
        };

        root.SetAction(async parseResult =>
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
            var fix      = parseResult.GetValue(fixOpt);
            var aiModel  = parseResult.GetValue(aiModelOpt) ?? "claude-haiku-4-5";
            var aiConc   = parseResult.GetValue(aiConcurrencyOpt);

            if (analyze) { smells = true; security = true; }

            return await ExecuteAsync(path, output, excludes, follow, pretty, verbose, smells, security, skip, html, fix, aiModel, aiConc).ConfigureAwait(false);
        });

        return await root.Parse(args).InvokeAsync();
    }

    private static async Task<int> ExecuteAsync(
        string path,
        string? output,
        string[] excludes,
        bool followSymlinks,
        bool pretty,
        bool verbose,
        bool smells,
        bool security,
        string[] securitySkipGlobs,
        string? htmlPath,
        bool fixSuggestions,
        string aiModel,
        int aiConcurrency)
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

        var stubMode = Environment.GetEnvironmentVariable("CODESCANNER_TEST_AI_STUB") == "1";
        if (fixSuggestions && !stubMode)
        {
            var key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (string.IsNullOrEmpty(key))
            {
                Console.Error.WriteLine("error: --fix-suggestions requires ANTHROPIC_API_KEY");
                return 1;
            }
            Console.Error.WriteLine($"info: --fix-suggestions enabled — code snippets will be sent to api.anthropic.com (model: {aiModel})");
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

            if (fixSuggestions)
            {
                IClaudeClient client = stubMode
                    ? new StubClaudeClient()
                    : new AnthropicClient(new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
                        Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!);

                var svc = new FixSuggestionService(client, aiModel, aiConcurrency);
                var aiResult = await svc.GenerateAsync(path, analysis.Smells, analysis.SecurityFindings, CancellationToken.None).ConfigureAwait(false);
                var mergedErrors = analysis.Errors.Concat(aiResult.Errors).ToList();
                analysis = analysis with
                {
                    Smells = aiResult.Smells,
                    SecurityFindings = aiResult.SecurityFindings,
                    Errors = mergedErrors,
                    AiSummary = aiResult.Summary,
                };
                if (verbose) { Console.Error.WriteLine($"info: AI suggestions: {aiResult.Summary.Successful}/{aiResult.Summary.TotalCalls} succeeded"); }
            }

            var json = Report.Serialize(result, analysis, options, pretty);
            if (htmlPath is not null)
            {
                if (output is not null)
                {
                    File.WriteAllText(output, json);
                    if (verbose) { Console.Error.WriteLine($"info: wrote {output}"); }
                }
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

    private sealed class StubClaudeClient : IClaudeClient
    {
        public Task<string> SendAsync(string body, CancellationToken ct) =>
            Task.FromResult("{\"explanation\":\"stub\",\"fixedSnippet\":\"stub-fix\"}");
    }
}
