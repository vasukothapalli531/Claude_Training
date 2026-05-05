using System.CommandLine;

namespace CodeScanner;

public static class Cli
{
    public static async Task<int> RunAsync(string[] args)
    {
        var pathArg = new Argument<string>("path")
        {
            Description = "Directory to scan",
        };
        var outputOpt = new Option<string?>("--output", "-o")
        {
            Description = "Write JSON to this file instead of stdout",
        };
        var excludeOpt = new Option<string[]>("--exclude", "-e")
        {
            Description = "Extra directory names to skip (additive to defaults)",
            AllowMultipleArgumentsPerToken = true,
        };
        var followOpt = new Option<bool>("--follow-symlinks")
        {
            Description = "Follow symlinked directories (default: skip)",
        };
        var prettyOpt = new Option<bool>("--pretty")
        {
            Description = "Pretty-print JSON output",
        };
        var verboseOpt = new Option<bool>("--verbose", "-v")
        {
            Description = "Log scan progress to stderr",
        };

        var root = new RootCommand("Recursively scan a directory and emit JSON file/line statistics.")
        {
            pathArg,
            outputOpt,
            excludeOpt,
            followOpt,
            prettyOpt,
            verboseOpt,
        };

        root.SetAction(parseResult =>
        {
            var path     = parseResult.GetValue(pathArg)!;
            var output   = parseResult.GetValue(outputOpt);
            var excludes = parseResult.GetValue(excludeOpt) ?? Array.Empty<string>();
            var follow   = parseResult.GetValue(followOpt);
            var pretty   = parseResult.GetValue(prettyOpt);
            var verbose  = parseResult.GetValue(verboseOpt);

            return Execute(path, output, excludes, follow, pretty, verbose);
        });

        return await root.Parse(args).InvokeAsync();
    }

    private static int Execute(
        string path,
        string? output,
        string[] excludes,
        bool followSymlinks,
        bool pretty,
        bool verbose)
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
            };
            var result = Scanner.Scan(path, options);
            var json = Report.Serialize(result, pretty);

            if (output is null)
            {
                Console.Out.WriteLine(json);
            }
            else
            {
                File.WriteAllText(output, json);
                if (verbose) { Console.Error.WriteLine($"info: wrote {output}"); }
            }

            if (verbose) { Console.Error.WriteLine($"info: {result.FileEntries.Count} files, {result.Errors.Count} errors"); }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.GetType().Name}: {ex.Message}");
            return 2;
        }
    }
}
