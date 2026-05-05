using System.Text.RegularExpressions;

namespace CodeScanner;

public sealed class DangerousFunctionScanner : ISecurityScanner
{
    public IReadOnlyList<SecurityFinding> Scan(string filePath, string content)
    {
        if (string.IsNullOrEmpty(content)) { return Array.Empty<SecurityFinding>(); }

        var ext = Path.GetExtension(filePath);
        if (!Patterns.DangerousFunctionsByExt.TryGetValue(ext, out var rules))
        {
            return Array.Empty<SecurityFinding>();
        }

        var findings = new List<SecurityFinding>();
        var seen = new HashSet<(int line, int column, string subtype)>();
        var lines = content.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (IgnoreDirective.HasIgnore(line)) { continue; }
            var lineNumber = i + 1;

            foreach (var rule in rules)
            {
                foreach (Match match in rule.Regex.Matches(line))
                {
                    var column = match.Index + 1;
                    if (!seen.Add((lineNumber, column, rule.Subtype))) { continue; }

                    findings.Add(new SecurityFinding(
                        Type: "dangerous_function",
                        Subtype: rule.Subtype,
                        Severity: rule.Severity,
                        File: filePath,
                        Line: lineNumber,
                        Column: column,
                        Snippet: line.TrimEnd('\r'),
                        Message: BuildMessage(rule.Subtype)));
                }
            }
        }

        return findings;
    }

    private static string BuildMessage(string subtype) => subtype switch
    {
        "eval"                    => "Use of eval() — arbitrary code execution risk",
        "exec"                    => "Use of exec() — arbitrary code execution risk",
        "new_function"            => "new Function() — eval-equivalent code execution",
        "set_timeout_string"      => "setTimeout with string argument — eval-like",
        "set_interval_string"     => "setInterval with string argument — eval-like",
        "subprocess_shell_true"   => "subprocess called with shell=True",
        "invoke_expression"       => "Invoke-Expression — arbitrary command execution",
        "iex_alias"               => "iex (Invoke-Expression alias) — arbitrary command execution",
        "assembly_load"           => "Assembly.Load — dynamic code loading",
        _                         => $"Dangerous function pattern '{subtype}' matched",
    };
}
