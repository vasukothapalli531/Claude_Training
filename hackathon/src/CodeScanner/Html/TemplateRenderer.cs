namespace CodeScanner;

internal static class TemplateRenderer
{
    /// <summary>
    /// Substitutes {{key}} placeholders with values from the dictionary.
    /// Values are substituted in iteration order; a value that itself contains
    /// {{...}} text matching another key may be re-processed by later iterations.
    /// Callers should escape values containing {{ before passing them in if this
    /// matters; for the HTML template's known value set it does not.
    /// </summary>
    public static string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        var sb = new System.Text.StringBuilder(template);
        foreach (var (key, val) in values)
        {
            sb.Replace("{{" + key + "}}", val);
        }
        return sb.ToString();
    }

    public static string HtmlEscape(string s)
    {
        if (string.IsNullOrEmpty(s)) { return s; }
        return s
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    /// <summary>
    /// Escapes JSON for safe embedding inside an HTML &lt;script&gt; tag by
    /// neutralizing any literal &lt;/script&gt; sequences. Other characters are
    /// already legal inside a JSON string literal.
    /// </summary>
    public static string EscapeForScriptTag(string json)
    {
        if (string.IsNullOrEmpty(json)) { return json; }
        return json.Replace("</script>", "<\\/script>", StringComparison.OrdinalIgnoreCase);
    }
}
