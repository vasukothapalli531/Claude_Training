namespace CodeScanner;

public static class Languages
{
    private static readonly IReadOnlyDictionary<string, string> Map =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".cs"]    = "C#",
            [".fs"]    = "F#",
            [".vb"]    = "VB.NET",
            [".py"]    = "Python",
            [".pyi"]   = "Python",
            [".ts"]    = "TypeScript",
            [".tsx"]   = "TypeScript",
            [".js"]    = "JavaScript",
            [".jsx"]   = "JavaScript",
            [".mjs"]   = "JavaScript",
            [".cjs"]   = "JavaScript",
            [".java"]  = "Java",
            [".kt"]    = "Kotlin",
            [".kts"]   = "Kotlin",
            [".go"]    = "Go",
            [".rs"]    = "Rust",
            [".rb"]    = "Ruby",
            [".php"]   = "PHP",
            [".swift"] = "Swift",
            [".c"]     = "C",
            [".h"]     = "C",
            [".cpp"]   = "C++",
            [".cc"]    = "C++",
            [".hpp"]   = "C++",
            [".m"]     = "Objective-C",
            [".sh"]    = "Shell",
            [".bash"]  = "Shell",
            [".ps1"]   = "PowerShell",
            [".psm1"]  = "PowerShell",
            [".sql"]   = "SQL",
            [".html"]  = "HTML",
            [".htm"]   = "HTML",
            [".css"]   = "CSS",
            [".scss"]  = "SCSS",
            [".sass"]  = "Sass",
            [".less"]  = "Less",
            [".md"]    = "Markdown",
            [".rst"]   = "reStructuredText",
            [".txt"]   = "Text",
            [".json"]  = "JSON",
            [".xml"]   = "XML",
            [".yml"]   = "YAML",
            [".yaml"]  = "YAML",
            [".toml"]  = "TOML",
            [".ini"]   = "INI",
            [".csv"]   = "CSV",
            [".tsv"]   = "TSV",
        };

    public static string Classify(string extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return "Unknown";
        }
        return Map.TryGetValue(extension, out var lang) ? lang : "Unknown";
    }
}
