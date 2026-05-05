using System.Text.RegularExpressions;

namespace CodeScanner;

public sealed record SecretRule(string Subtype, string Severity, Regex Regex, bool FullRedact);

public sealed record DangerousFunctionRule(string Subtype, string Severity, Regex Regex);

public static class Patterns
{
    private const RegexOptions Opts =
        RegexOptions.Compiled | RegexOptions.CultureInvariant;

    public static readonly IReadOnlyList<SecretRule> SecretRules = new List<SecretRule>
    {
        new("aws_access_key", "high",
            new Regex(@"\b(AKIA|ASIA)[0-9A-Z]{16}\b", Opts),
            FullRedact: false),

        new("github_pat", "high",
            new Regex(@"\b(ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9]{36,}\b", Opts),
            FullRedact: false),

        new("slack_token", "high",
            new Regex(@"\bxox[baprs]-[A-Za-z0-9-]{10,48}\b", Opts),
            FullRedact: false),

        new("private_key", "high",
            new Regex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----", Opts),
            FullRedact: true),

        new("jwt", "medium",
            new Regex(@"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b", Opts),
            FullRedact: false),

        new("generic_assign", "medium",
            new Regex(
                @"(?i)(password|secret|api[_-]?key|token)\s*[:=]\s*[""'][^""'\s]{8,}[""']",
                Opts),
            FullRedact: false),

        new("connstr_password", "medium",
            new Regex(@"(?i)(Password|Pwd)=([^;'""\s]+)", Opts),
            FullRedact: true),
    };

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<DangerousFunctionRule>>
        DangerousFunctionsByExt = BuildDangerousFunctionMap();

    private static IReadOnlyDictionary<string, IReadOnlyList<DangerousFunctionRule>> BuildDangerousFunctionMap()
    {
        var jsLikeExts = new[] { ".js", ".ts", ".jsx", ".tsx", ".mjs", ".cjs" };
        var jsLikeRules = new List<DangerousFunctionRule>
        {
            new("eval",        "high",   new Regex(@"\beval\s*\(", Opts)),
            new("new_function","high",   new Regex(@"\bnew\s+Function\s*\(", Opts)),
            new("set_timeout_string", "medium", new Regex(@"\bsetTimeout\s*\(\s*['""]", Opts)),
            new("set_interval_string","medium", new Regex(@"\bsetInterval\s*\(\s*['""]", Opts)),
        };

        var pyExts = new[] { ".py", ".pyi" };
        var pyRules = new List<DangerousFunctionRule>
        {
            new("eval",        "high",   new Regex(@"\beval\s*\(", Opts)),
            new("exec",        "high",   new Regex(@"\bexec\s*\(", Opts)),
            new("subprocess_shell_true","medium",
                new Regex(@"\bsubprocess\.[A-Za-z_]+\([^)]*shell\s*=\s*True", Opts)),
        };

        var psExts = new[] { ".ps1", ".psm1" };
        var psRules = new List<DangerousFunctionRule>
        {
            new("invoke_expression","high", new Regex(@"\bInvoke-Expression\b", Opts)),
            new("iex_alias",        "high", new Regex(@"\biex\s+", Opts)),
        };

        var shExts = new[] { ".sh", ".bash" };
        var shRules = new List<DangerousFunctionRule>
        {
            new("eval", "high", new Regex(@"^\s*eval\s+", Opts | RegexOptions.Multiline)),
        };

        var csExts = new[] { ".cs" };
        var csRules = new List<DangerousFunctionRule>
        {
            new("assembly_load", "low", new Regex(@"\bAssembly\.Load\s*\(", Opts)),
        };

        var map = new Dictionary<string, IReadOnlyList<DangerousFunctionRule>>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in jsLikeExts) map[e] = jsLikeRules;
        foreach (var e in pyExts)     map[e] = pyRules;
        foreach (var e in psExts)     map[e] = psRules;
        foreach (var e in shExts)     map[e] = shRules;
        foreach (var e in csExts)     map[e] = csRules;
        return map;
    }
}
