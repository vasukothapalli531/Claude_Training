using System.Text.RegularExpressions;

namespace CodeScanner;

public sealed class SecretScanner : ISecurityScanner
{
    public IReadOnlyList<SecurityFinding> Scan(string filePath, string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return Array.Empty<SecurityFinding>();
        }

        var findings = new List<SecurityFinding>();
        var seen = new HashSet<(int line, int column)>();
        var lines = content.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (IgnoreDirective.HasIgnore(line)) { continue; }

            var lineNumber = i + 1;

            foreach (var rule in OrderedSecretRules)
            {
                foreach (Match match in rule.Regex.Matches(line))
                {
                    var column = match.Index + 1;
                    if (!seen.Add((lineNumber, column))) { continue; }

                    var snippet = RedactSnippet(line, match, rule.FullRedact);
                    findings.Add(new SecurityFinding(
                        Type: "hardcoded_secret",
                        Subtype: rule.Subtype,
                        Severity: rule.Severity,
                        File: filePath,
                        Line: lineNumber,
                        Column: column,
                        Snippet: snippet,
                        Message: BuildMessage(rule.Subtype)));
                }
            }
        }

        return findings;
    }

    private static readonly IReadOnlyList<SecretRule> OrderedSecretRules =
        Patterns.SecretRules.OrderByDescending(r => SeverityRank(r.Severity)).ToList();

    private static int SeverityRank(string s) => s switch
    {
        "high" => 3,
        "medium" => 2,
        "low" => 1,
        _ => 0,
    };

    private static string RedactSnippet(string line, Match match, bool fullRedact)
    {
        var matchValue = match.Value;
        string replacement;
        if (fullRedact || matchValue.Length <= 4)
        {
            replacement = "••••REDACTED";
        }
        else
        {
            replacement = matchValue.Substring(0, 4) + "••••REDACTED";
        }
        return line.Substring(0, match.Index) + replacement + line.Substring(match.Index + matchValue.Length);
    }

    private static string BuildMessage(string subtype) => subtype switch
    {
        "aws_access_key"    => "AWS Access Key ID detected",
        "github_pat"        => "GitHub Personal Access Token detected",
        "slack_token"       => "Slack token detected",
        "private_key"       => "Private key block detected",
        "jwt"               => "JWT detected",
        "generic_assign"    => "Hardcoded credential assignment detected",
        "connstr_password"  => "Connection string password detected",
        _                   => $"Secret pattern '{subtype}' matched",
    };
}
