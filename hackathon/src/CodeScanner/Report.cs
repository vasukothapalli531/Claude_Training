using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodeScanner;

public static class Report
{
    public static string Serialize(ScanResult result, bool pretty)
    {
        var languages = new JsonObject();
        var totalFiles = 0L;
        var totalLines = 0L;

        foreach (var group in result.FileEntries.GroupBy(e => e.Language))
        {
            var files = group.LongCount();
            var lines = group.Sum(e => e.Lines);
            var exts  = group.Select(e => e.Extension)
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .OrderBy(s => s, StringComparer.Ordinal)
                             .ToList();

            languages[group.Key] = new JsonObject
            {
                ["files"] = files,
                ["lines"] = lines,
                ["extensions"] = new JsonArray(exts.Select(e => (JsonNode?)JsonValue.Create(e)).ToArray()),
            };
            totalFiles += files;
            totalLines += lines;
        }

        var errors = new JsonArray(
            result.Errors.Select(err => (JsonNode?)new JsonObject
            {
                ["path"]   = NormalizePath(err.Path),
                ["reason"] = err.Reason,
            }).ToArray());

        var skipped = new JsonArray(
            result.SkippedDirs.Select(d => (JsonNode?)JsonValue.Create(d)).ToArray());

        var doc = new JsonObject
        {
            ["totalFiles"] = totalFiles,
            ["totalLines"] = totalLines,
            ["languages"]  = languages,
            ["scanned"]    = new JsonObject
            {
                ["root"]        = NormalizePath(result.Root),
                ["skippedDirs"] = skipped,
                ["errors"]      = errors,
            },
        };

        var options = new JsonSerializerOptions { WriteIndented = pretty };
        return doc.ToJsonString(options);
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');
}
