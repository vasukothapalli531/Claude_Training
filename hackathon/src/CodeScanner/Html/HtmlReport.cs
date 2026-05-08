namespace CodeScanner;

public static class HtmlReport
{
    public static string Render(
        ScanResult result,
        AnalysisResult analysis,
        ScanOptions options,
        DateTimeOffset timestamp)
    {
        var json = Report.Serialize(result, analysis, options, pretty: false);
        var safeJson = TemplateRenderer.EscapeForScriptTag(json);
        var rootPath = TemplateRenderer.HtmlEscape(NormalizePath(result.Root));
        var ts = TemplateRenderer.HtmlEscape(timestamp.ToString("yyyy-MM-ddTHH:mm:ssK"));
        var flagsLine = TemplateRenderer.HtmlEscape(BuildFlagsLine(options));

        var values = new Dictionary<string, string>
        {
            ["rootPath"]  = rootPath,
            ["timestamp"] = ts,
            ["flagsLine"] = flagsLine,
            ["dataJson"]  = safeJson,
        };

        return TemplateRenderer.Render(Template.Html, values);
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static string BuildFlagsLine(ScanOptions options)
    {
        if (options.Smells && options.Security) { return "--analyze"; }
        if (options.Smells)   { return "--smells"; }
        if (options.Security) { return "--security"; }
        return "scan only";
    }
}
