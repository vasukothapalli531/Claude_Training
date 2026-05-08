using System.Text;

namespace CodeScanner;

internal static class PromptBuilder
{
    private const int SmellWindowMax = 100;
    private const int SecurityContextRadius = 3;

    public static string BuildSystem() =>
        "You are a senior code reviewer. For the given finding, propose a minimal fix. " +
        "Reply with strict JSON only — no prose, no code fences. " +
        "Schema: {\"explanation\": string, \"fixedSnippet\": string}.";

    public static string BuildUserContent(SmellFinding finding, string sourceContent)
    {
        var window = WindowForSmell(sourceContent, finding.StartLine, finding.EndLine);
        return BuildEnvelope(
            type: finding.Type,
            severity: finding.Severity,
            file: finding.File,
            message: finding.Message,
            window: window);
    }

    public static string BuildUserContent(SecurityFinding finding, string sourceContent)
    {
        var window = WindowForSecurity(sourceContent, finding.Line, finding.Snippet);
        return BuildEnvelope(
            type: finding.Type,
            severity: finding.Severity,
            file: finding.File,
            message: finding.Message,
            window: window);
    }

    private static string BuildEnvelope(string type, string severity, string file, string message, string window)
    {
        var sb = new StringBuilder();
        sb.Append("Finding: ").Append(type).Append(" (").Append(severity).Append(")\n");
        sb.Append("File: ").Append(file).Append('\n');
        sb.Append("Message: ").Append(message).Append("\n\n");
        sb.Append("Context:\n```\n").Append(window).Append("\n```\n\n");
        sb.Append("Return the JSON now.");
        return sb.ToString();
    }

    private static string WindowForSmell(string source, int startLine, int endLine)
    {
        var lines = source.Split('\n');
        var startIdx = Math.Max(0, startLine - 1);
        var endIdx = Math.Min(lines.Length - 1, endLine - 1);
        if (endIdx < startIdx) { return string.Empty; }

        var span = endIdx - startIdx + 1;
        if (span <= SmellWindowMax)
        {
            return string.Join("\n", lines.Skip(startIdx).Take(span));
        }

        var firstHalf = lines.Skip(startIdx).Take(50);
        var secondHalfStart = endIdx - 49;
        var secondHalf = lines.Skip(secondHalfStart).Take(50);
        var elided = span - 100;
        var sb = new StringBuilder();
        sb.AppendJoin('\n', firstHalf);
        sb.Append("\n// ... <").Append(elided).Append(" lines elided> ...\n");
        sb.AppendJoin('\n', secondHalf);
        return sb.ToString();
    }

    private static string WindowForSecurity(string source, int line, string redactedSnippet)
    {
        var lines = source.Split('\n');
        var idx = line - 1;
        var startIdx = Math.Max(0, idx - SecurityContextRadius);
        var endIdx = Math.Min(lines.Length - 1, idx + SecurityContextRadius);
        if (endIdx < startIdx)
        {
            return redactedSnippet;
        }

        var sb = new StringBuilder();
        // Prepend the redacted snippet so it is always visible regardless of source content
        sb.Append(redactedSnippet).Append('\n');
        for (var i = startIdx; i <= endIdx; i++)
        {
            sb.Append(lines[i]);
            if (i < endIdx) { sb.Append('\n'); }
        }
        return sb.ToString();
    }
}
