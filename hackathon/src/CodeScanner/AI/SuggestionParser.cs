using System.Text.Json;

namespace CodeScanner;

internal static class SuggestionParser
{
    public static bool TryParse(string responseText, out AiSuggestion? suggestion, out string? error)
    {
        suggestion = null;
        error = null;

        var stripped = StripFences(responseText.Trim());

        try
        {
            using var doc = JsonDocument.Parse(stripped);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "parse error: root is not an object";
                return false;
            }
            if (!doc.RootElement.TryGetProperty("explanation", out var expl) || expl.ValueKind != JsonValueKind.String)
            {
                error = "missing or invalid 'explanation'";
                return false;
            }
            if (!doc.RootElement.TryGetProperty("fixedSnippet", out var fix) || fix.ValueKind != JsonValueKind.String)
            {
                error = "missing or invalid 'fixedSnippet'";
                return false;
            }

            suggestion = new AiSuggestion(
                Explanation: expl.GetString() ?? string.Empty,
                FixedSnippet: fix.GetString() ?? string.Empty,
                Model: string.Empty,
                ElapsedMs: 0);
            return true;
        }
        catch (JsonException ex)
        {
            error = $"parse error: {ex.Message}";
            return false;
        }
    }

    private static string StripFences(string s)
    {
        if (s.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = s.IndexOf('\n');
            if (firstNewline >= 0)
            {
                s = s.Substring(firstNewline + 1);
            }
            if (s.EndsWith("```", StringComparison.Ordinal))
            {
                s = s.Substring(0, s.Length - 3);
            }
        }
        return s.Trim();
    }
}
