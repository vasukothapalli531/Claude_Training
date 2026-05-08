using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodeScanner;

public static class Report
{
    public static string Serialize(ScanResult result, bool pretty)
        => Serialize(result,
            new AnalysisResult(Array.Empty<SmellFinding>(), Array.Empty<SecurityFinding>(), Array.Empty<ScanError>(), TotalFunctions: 0),
            new ScanOptions(),
            pretty);

    public static string Serialize(ScanResult result, AnalysisResult analysis, ScanOptions options, bool pretty)
    {
        var languages = new JsonObject();
        var totalFiles = 0L;
        var totalLines = 0L;

        var smellsByLang   = BuildSeverityMap(analysis.Smells, f => f.File, result);
        var securityByLang = BuildSeverityMap(analysis.SecurityFindings, f => f.File, result);

        foreach (var group in result.FileEntries.GroupBy(e => e.Language))
        {
            var files = group.LongCount();
            var lines = group.Sum(e => e.Lines);
            var exts  = group.Select(e => e.Extension)
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .OrderBy(s => s, StringComparer.Ordinal)
                             .ToList();

            var langObj = new JsonObject
            {
                ["files"] = files,
                ["lines"] = lines,
                ["extensions"] = new JsonArray(exts.Select(e => (JsonNode?)JsonValue.Create(e)).ToArray()),
            };

            if (options.Smells && smellsByLang.TryGetValue(group.Key, out var smellSummary))
            {
                langObj["smells"] = SeverityToJson(smellSummary);
            }
            if (options.Security && securityByLang.TryGetValue(group.Key, out var secSummary))
            {
                langObj["security"] = SeverityToJson(secSummary);
            }

            languages[group.Key] = langObj;
            totalFiles += files;
            totalLines += lines;
        }

        var allErrors = result.Errors.Concat(analysis.Errors).ToList();

        var doc = new JsonObject
        {
            ["totalFiles"] = totalFiles,
            ["totalLines"] = totalLines,
            ["languages"]  = languages,
        };

        if (options.Smells)
        {
            doc["smells"] = new JsonArray(
                analysis.Smells.Select(s => (JsonNode?)new JsonObject
                {
                    ["type"]      = s.Type,
                    ["severity"]  = s.Severity,
                    ["file"]      = NormalizePath(s.File),
                    ["name"]      = s.Name,
                    ["startLine"] = s.StartLine,
                    ["endLine"]   = s.EndLine,
                    ["value"]     = s.Value,
                    ["threshold"] = s.Threshold,
                    ["message"]   = s.Message,
                }).ToArray());
        }

        if (options.Security)
        {
            doc["securityIssues"] = new JsonArray(
                analysis.SecurityFindings.Select(s => (JsonNode?)new JsonObject
                {
                    ["type"]     = s.Type,
                    ["subtype"]  = s.Subtype,
                    ["severity"] = s.Severity,
                    ["file"]     = NormalizePath(s.File),
                    ["line"]     = s.Line,
                    ["column"]   = s.Column,
                    ["snippet"]  = s.Snippet,
                    ["message"]  = s.Message,
                }).ToArray());
        }

        var skipped = new JsonArray(
            result.SkippedDirs.Select(d => (JsonNode?)JsonValue.Create(d)).ToArray());

        var errorsArr = new JsonArray(
            allErrors.Select(err => (JsonNode?)new JsonObject
            {
                ["path"]   = NormalizePath(err.Path),
                ["reason"] = err.Reason,
            }).ToArray());

        doc["scanned"] = new JsonObject
        {
            ["root"]        = NormalizePath(result.Root),
            ["skippedDirs"] = skipped,
            ["errors"]      = errorsArr,
        };

        var jsonOptions = new JsonSerializerOptions { WriteIndented = pretty };
        return doc.ToJsonString(jsonOptions);
    }

    private sealed class SeveritySummary
    {
        public int Low, Medium, High, Total;
    }

    private static IReadOnlyDictionary<string, SeveritySummary> BuildSeverityMap<T>(
        IEnumerable<T> findings,
        Func<T, string> filePathSelector,
        ScanResult result) where T : class
    {
        var fileToLang = result.FileEntries.ToDictionary(e => e.Path, e => e.Language, StringComparer.OrdinalIgnoreCase);
        var map = new Dictionary<string, SeveritySummary>(StringComparer.Ordinal);

        foreach (var f in findings)
        {
            var path = filePathSelector(f);
            if (!fileToLang.TryGetValue(path, out var lang)) { continue; }

            if (!map.TryGetValue(lang, out var summary))
            {
                summary = new SeveritySummary();
                map[lang] = summary;
            }

            var severity = f switch
            {
                SmellFinding s    => s.Severity,
                SecurityFinding s => s.Severity,
                _                 => "low",
            };

            switch (severity)
            {
                case "high":   summary.High++;   break;
                case "medium": summary.Medium++; break;
                default:       summary.Low++;    break;
            }
            summary.Total++;
        }

        return map;
    }

    private static JsonNode SeverityToJson(SeveritySummary s) => new JsonObject
    {
        ["low"]    = s.Low,
        ["medium"] = s.Medium,
        ["high"]   = s.High,
        ["total"]  = s.Total,
    };

    internal static string NormalizePath(string path) => path.Replace('\\', '/');
}
