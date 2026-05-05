# Code Analysis (Smells + Security) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add opt-in `--smells` (Roslyn-based, C# only) and `--security` (regex-based, multi-language) analysis passes to the existing Code Scanner CLI, emitting `smells[]` and `securityIssues[]` arrays in the JSON output, per `docs/superpowers/specs/2026-05-05-code-analysis-design.md`.

**Architecture:** New `Analyzers/` namespace under `src/CodeScanner`. Two scanner types behind interfaces (`ISmellAnalyzer`, `ISecurityScanner`). `AnalyzerHost` orchestrates passes file-by-file, returning enriched findings. `Report.Serialize` is updated to include the new arrays only when the corresponding flags were set (backwards-compatible). Test approach: inline source-string tests for parser-driven smell detection; pattern-string tests for regex security; fixture trees for end-to-end CLI verification.

**Tech Stack:** .NET 9, C#, xUnit, `Microsoft.CodeAnalysis.CSharp` (Roslyn) for smell AST, `Microsoft.Extensions.FileSystemGlobbing` for `--security-skip` glob matching, `System.Text.RegularExpressions` for security patterns.

**Repo additions produced by this plan:**

```
hackathon/src/CodeScanner/Analyzers/
  ├── ISmellAnalyzer.cs
  ├── ISecurityScanner.cs
  ├── Patterns.cs
  ├── IgnoreDirective.cs
  ├── SecretScanner.cs
  ├── DangerousFunctionScanner.cs
  ├── SmellWalker.cs
  ├── CSharpSmellAnalyzer.cs
  └── AnalyzerHost.cs

hackathon/tests/CodeScanner.Tests/Analyzers/
  ├── PatternsTests.cs
  ├── IgnoreDirectiveTests.cs
  ├── SecretScannerTests.cs
  ├── DangerousFunctionScannerTests.cs
  ├── CSharpSmellAnalyzerTests.cs
  └── AnalyzerHostTests.cs

hackathon/tests/CodeScanner.Tests/fixtures/
  ├── Smells/Smelly.cs
  └── Security/secrets.txt, dangerous.js, dangerous.py
```

Working directory throughout: `C:\Cmm-testing\Claude_Training\hackathon`. Branch this work onto `feat/code-analysis` before Task 1.

---

## Task 0: Branch off main

- [ ] **Step 0.1: Create feature branch from main**

```powershell
git -C C:/Cmm-testing/Claude_Training switch -c feat/code-analysis
git -C C:/Cmm-testing/Claude_Training status -sb
```

Expected: `Switched to a new branch 'feat/code-analysis'`. Branch shows clean.

---

## Task 1: Add Roslyn + globbing packages, extend Models

**Files:**
- Modify: `hackathon/src/CodeScanner/CodeScanner.csproj`
- Modify: `hackathon/src/CodeScanner/Models.cs`
- Create: `hackathon/tests/CodeScanner.Tests/Analyzers/ModelsAnalysisTests.cs`

- [ ] **Step 1.1: Add Roslyn + globbing NuGet packages**

```powershell
Set-Location C:\Cmm-testing\Claude_Training\hackathon
dotnet add src/CodeScanner/CodeScanner.csproj package Microsoft.CodeAnalysis.CSharp
dotnet add src/CodeScanner/CodeScanner.csproj package Microsoft.Extensions.FileSystemGlobbing
```

Expected: both packages added; restore succeeds.

- [ ] **Step 1.2: Write failing tests for new model records**

Create `tests/CodeScanner.Tests/Analyzers/ModelsAnalysisTests.cs`:

```csharp
namespace CodeScanner.Tests.Analyzers;

public class ModelsAnalysisTests
{
    [Fact]
    public void SmellFinding_RecordEqualityWorks()
    {
        var a = new SmellFinding(
            Type: "long_function", Severity: "medium",
            File: "x.cs", Name: "Foo",
            StartLine: 1, EndLine: 60,
            Value: 60, Threshold: 50,
            Message: "msg");
        var b = a with { };
        Assert.Equal(a, b);
    }

    [Fact]
    public void SecurityFinding_HasAllExpectedFields()
    {
        var f = new SecurityFinding(
            Type: "hardcoded_secret",
            Subtype: "aws_access_key",
            Severity: "high",
            File: "x.cs",
            Line: 42, Column: 24,
            Snippet: "var k = \"AKIA••••REDACTED\";",
            Message: "AWS Access Key ID detected");

        Assert.Equal("hardcoded_secret", f.Type);
        Assert.Equal("aws_access_key", f.Subtype);
        Assert.Equal("high", f.Severity);
    }

    [Fact]
    public void ScanOptions_AnalysisFlagsDefaultOff()
    {
        var o = new ScanOptions();
        Assert.False(o.Smells);
        Assert.False(o.Security);
        Assert.NotNull(o.SecuritySkipGlobs);
        Assert.Empty(o.SecuritySkipGlobs);
    }
}
```

- [ ] **Step 1.3: Run tests — verify they fail**

```powershell
Set-Location C:\Cmm-testing\Claude_Training\hackathon
dotnet test CodeScanner.sln --nologo
```

Expected: build error "type or namespace name 'SmellFinding'/'SecurityFinding' could not be found", and `ScanOptions` does not contain `Smells`.

- [ ] **Step 1.4: Add records and extend ScanOptions in `Models.cs`**

Append to `src/CodeScanner/Models.cs`:

```csharp
public sealed record SmellFinding(
    string Type,
    string Severity,
    string File,
    string Name,
    int StartLine,
    int EndLine,
    int Value,
    int Threshold,
    string Message);

public sealed record SecurityFinding(
    string Type,
    string Subtype,
    string Severity,
    string File,
    int Line,
    int Column,
    string Snippet,
    string Message);
```

Modify `ScanOptions` in the same file:

```csharp
public sealed record ScanOptions
{
    public bool FollowSymlinks { get; init; }
    public IReadOnlyList<string> ExtraExcludes { get; init; } = Array.Empty<string>();
    public bool Smells { get; init; }
    public bool Security { get; init; }
    public IReadOnlyList<string> SecuritySkipGlobs { get; init; } = Array.Empty<string>();
}
```

- [ ] **Step 1.5: Run tests — verify they pass**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: all tests green (prior + 3 new).

- [ ] **Step 1.6: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/CodeScanner.csproj hackathon/src/CodeScanner/Models.cs hackathon/tests/CodeScanner.Tests/Analyzers/ModelsAnalysisTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(analysis): add Roslyn + globbing deps; SmellFinding, SecurityFinding records"
```

---

## Task 2: Patterns + IgnoreDirective

**Files:**
- Create: `hackathon/src/CodeScanner/Analyzers/Patterns.cs`
- Create: `hackathon/src/CodeScanner/Analyzers/IgnoreDirective.cs`
- Create: `hackathon/tests/CodeScanner.Tests/Analyzers/PatternsTests.cs`
- Create: `hackathon/tests/CodeScanner.Tests/Analyzers/IgnoreDirectiveTests.cs`

- [ ] **Step 2.1: Write failing tests for Patterns**

Create `tests/CodeScanner.Tests/Analyzers/PatternsTests.cs`:

```csharp
using System.Text.RegularExpressions;

namespace CodeScanner.Tests.Analyzers;

public class PatternsTests
{
    [Theory]
    [InlineData("AKIA1234567890ABCDEF", true)]
    [InlineData("ASIA1234567890ABCDEF", true)]
    [InlineData("akia1234567890abcdef", false)]
    [InlineData("AKIA1234", false)]
    public void AwsAccessKey_Match(string input, bool expected)
    {
        var rule = Patterns.SecretRules.Single(r => r.Subtype == "aws_access_key");
        Assert.Equal(expected, rule.Regex.IsMatch(input));
    }

    [Theory]
    [InlineData("ghp_aBcDeFgHiJkLmNoPqRsTuVwXyZ0123456789ab", true)]
    [InlineData("gho_aBcDeFgHiJkLmNoPqRsTuVwXyZ0123456789ab", true)]
    [InlineData("ghx_aBcDeFgHiJkLmNoPqRsTuVwXyZ0123456789ab", false)]
    public void GithubPat_Match(string input, bool expected)
    {
        var rule = Patterns.SecretRules.Single(r => r.Subtype == "github_pat");
        Assert.Equal(expected, rule.Regex.IsMatch(input));
    }

    [Theory]
    [InlineData("password = \"hunter2pass\"", true)]
    [InlineData("ApiKey: \"my-api-key-1234567890\"", true)]
    [InlineData("password=short", false)]
    [InlineData("password = ''", false)]
    public void GenericAssign_Match(string input, bool expected)
    {
        var rule = Patterns.SecretRules.Single(r => r.Subtype == "generic_assign");
        Assert.Equal(expected, rule.Regex.IsMatch(input));
    }

    [Fact]
    public void DangerousFunctions_KeyedByExtension()
    {
        Assert.True(Patterns.DangerousFunctionsByExt.ContainsKey(".js"));
        Assert.True(Patterns.DangerousFunctionsByExt.ContainsKey(".ts"));
        Assert.True(Patterns.DangerousFunctionsByExt.ContainsKey(".py"));
        Assert.True(Patterns.DangerousFunctionsByExt.ContainsKey(".ps1"));
        Assert.True(Patterns.DangerousFunctionsByExt.ContainsKey(".sh"));
        Assert.True(Patterns.DangerousFunctionsByExt.ContainsKey(".cs"));
    }

    [Fact]
    public void JsEval_HighSeverity()
    {
        var rules = Patterns.DangerousFunctionsByExt[".js"];
        var evalRule = rules.Single(r => r.Subtype == "eval");
        Assert.Equal("high", evalRule.Severity);
        Assert.True(evalRule.Regex.IsMatch("eval('hello')"));
        Assert.False(evalRule.Regex.IsMatch("// eval below"));
    }

    [Fact]
    public void SeverityValues_AreOnlyLowMediumHigh()
    {
        var allowed = new[] { "low", "medium", "high" };
        Assert.All(Patterns.SecretRules, r => Assert.Contains(r.Severity, allowed));
        foreach (var (_, rules) in Patterns.DangerousFunctionsByExt)
        {
            Assert.All(rules, r => Assert.Contains(r.Severity, allowed));
        }
    }
}
```

- [ ] **Step 2.2: Write failing tests for IgnoreDirective**

Create `tests/CodeScanner.Tests/Analyzers/IgnoreDirectiveTests.cs`:

```csharp
namespace CodeScanner.Tests.Analyzers;

public class IgnoreDirectiveTests
{
    [Theory]
    [InlineData("var x = \"AKIA....\"; // codescan:ignore", true)]
    [InlineData("var x = \"AKIA....\"; # codescan:ignore", true)]
    [InlineData("var x = \"AKIA....\"; -- codescan:ignore", true)]
    [InlineData("var x = \"AKIA....\"; ; codescan:ignore", true)]
    [InlineData("var x = \"AKIA....\";", false)]
    [InlineData("var x = \"AKIA....\"; // CODESCAN:IGNORE", false)] // case-sensitive
    [InlineData("var x = \"AKIA....\"; // codescan:ignore something else", false)]
    public void HasIgnore_DetectsTrailingDirective(string line, bool expected)
    {
        Assert.Equal(expected, IgnoreDirective.HasIgnore(line));
    }
}
```

- [ ] **Step 2.3: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: build errors referring to missing `Patterns`, `IgnoreDirective`.

- [ ] **Step 2.4: Implement `Patterns.cs`**

Create `src/CodeScanner/Analyzers/Patterns.cs`:

```csharp
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
```

- [ ] **Step 2.5: Implement `IgnoreDirective.cs`**

Create `src/CodeScanner/Analyzers/IgnoreDirective.cs`:

```csharp
namespace CodeScanner;

public static class IgnoreDirective
{
    private const string Token = "codescan:ignore";

    public static bool HasIgnore(string line)
    {
        var trimmed = line.TrimEnd();
        if (!trimmed.EndsWith(Token, StringComparison.Ordinal))
        {
            return false;
        }

        // Must be preceded by a comment marker plus optional whitespace.
        var idx = trimmed.Length - Token.Length;
        var before = trimmed.AsSpan(0, idx).TrimEnd();
        if (before.Length == 0)
        {
            return false;
        }

        return before.EndsWith("//", StringComparison.Ordinal)
            || before.EndsWith("#",  StringComparison.Ordinal)
            || before.EndsWith("--", StringComparison.Ordinal)
            || before.EndsWith(";",  StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2.6: Run tests — verify they pass**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: all green (prior + new pattern + ignore tests).

- [ ] **Step 2.7: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Analyzers/Patterns.cs hackathon/src/CodeScanner/Analyzers/IgnoreDirective.cs hackathon/tests/CodeScanner.Tests/Analyzers/PatternsTests.cs hackathon/tests/CodeScanner.Tests/Analyzers/IgnoreDirectiveTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(analysis): add secret + dangerous-function regex catalog and ignore directive"
```

---

## Task 3: Secret scanner

**Files:**
- Create: `hackathon/src/CodeScanner/Analyzers/ISecurityScanner.cs`
- Create: `hackathon/src/CodeScanner/Analyzers/SecretScanner.cs`
- Create: `hackathon/tests/CodeScanner.Tests/Analyzers/SecretScannerTests.cs`

- [ ] **Step 3.1: Write failing tests**

Create `tests/CodeScanner.Tests/Analyzers/SecretScannerTests.cs`:

```csharp
namespace CodeScanner.Tests.Analyzers;

public class SecretScannerTests
{
    private static readonly SecretScanner Scanner = new();

    [Fact]
    public void DetectsAwsKey_AndRedactsSnippet()
    {
        var content = "var key = \"AKIA1234567890ABCDEF\";\n";
        var findings = Scanner.Scan("a.cs", content);

        Assert.Single(findings);
        var f = findings[0];
        Assert.Equal("hardcoded_secret", f.Type);
        Assert.Equal("aws_access_key", f.Subtype);
        Assert.Equal("high", f.Severity);
        Assert.Equal(1, f.Line);
        Assert.Contains("AKIA••••REDACTED", f.Snippet);
        Assert.DoesNotContain("AKIA1234567890ABCDEF", f.Snippet);
    }

    [Fact]
    public void DetectsPrivateKey_FullyRedacted()
    {
        var content = "const X = \"-----BEGIN RSA PRIVATE KEY-----\";";
        var findings = Scanner.Scan("a.js", content);

        Assert.Single(findings);
        Assert.Equal("private_key", findings[0].Subtype);
        Assert.DoesNotContain("BEGIN RSA PRIVATE KEY", findings[0].Snippet);
        Assert.Contains("••••REDACTED", findings[0].Snippet);
    }

    [Fact]
    public void IgnoresLineWithCodescanIgnore()
    {
        var content =
            "var x = \"AKIA1234567890ABCDEF\"; // codescan:ignore\n" +
            "var y = \"AKIA1234567890XYZWVUTS\";\n";

        var findings = Scanner.Scan("a.cs", content);

        Assert.Single(findings);
        Assert.Equal(2, findings[0].Line);
    }

    [Fact]
    public void DetectsMultiplePatternsAcrossLines()
    {
        var content =
            "var aws = \"AKIA1234567890ABCDEF\";\n" +
            "var pat = \"ghp_aBcDeFgHiJkLmNoPqRsTuVwXyZ0123456789ab\";\n" +
            "var pwd = \"password = \\\"hunter2-strong\\\"\";\n";

        var findings = Scanner.Scan("a.cs", content);

        Assert.Equal(3, findings.Count);
        Assert.Contains(findings, f => f.Subtype == "aws_access_key");
        Assert.Contains(findings, f => f.Subtype == "github_pat");
        Assert.Contains(findings, f => f.Subtype == "generic_assign");
    }

    [Fact]
    public void Dedupes_HigherSeverityWinsAtSameLineColumn()
    {
        // A line containing both an AWS key and a generic password assignment
        // at the same column would be a contrived edge case; instead test that
        // duplicate aws matches at the same offset emit only one finding.
        var content = "var k = \"AKIA1234567890ABCDEF\";\n";
        var findings = Scanner.Scan("a.cs", content);

        Assert.Single(findings);
    }

    [Fact]
    public void EmptyContent_ReturnsNoFindings()
    {
        var findings = Scanner.Scan("a.cs", "");
        Assert.Empty(findings);
    }
}
```

- [ ] **Step 3.2: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: build errors referring to `SecretScanner`.

- [ ] **Step 3.3: Implement `ISecurityScanner.cs` and `SecretScanner.cs`**

Create `src/CodeScanner/Analyzers/ISecurityScanner.cs`:

```csharp
namespace CodeScanner;

public interface ISecurityScanner
{
    IReadOnlyList<SecurityFinding> Scan(string filePath, string content);
}
```

Create `src/CodeScanner/Analyzers/SecretScanner.cs`:

```csharp
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

            // Order rules by severity rank descending so highest-severity wins on dedup.
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
```

- [ ] **Step 3.4: Run tests — verify they pass**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: all green.

- [ ] **Step 3.5: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Analyzers/ISecurityScanner.cs hackathon/src/CodeScanner/Analyzers/SecretScanner.cs hackathon/tests/CodeScanner.Tests/Analyzers/SecretScannerTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(analysis): add secret scanner with redacted snippets and ignore directive"
```

---

## Task 4: Dangerous-function scanner

**Files:**
- Create: `hackathon/src/CodeScanner/Analyzers/DangerousFunctionScanner.cs`
- Create: `hackathon/tests/CodeScanner.Tests/Analyzers/DangerousFunctionScannerTests.cs`

- [ ] **Step 4.1: Write failing tests**

Create `tests/CodeScanner.Tests/Analyzers/DangerousFunctionScannerTests.cs`:

```csharp
namespace CodeScanner.Tests.Analyzers;

public class DangerousFunctionScannerTests
{
    private static readonly DangerousFunctionScanner Scanner = new();

    [Fact]
    public void DetectsJsEval()
    {
        var content = "function run(input) { eval(input); }\n";
        var findings = Scanner.Scan("a.js", content);

        Assert.Single(findings);
        Assert.Equal("dangerous_function", findings[0].Type);
        Assert.Equal("eval", findings[0].Subtype);
        Assert.Equal("high", findings[0].Severity);
        Assert.Equal(1, findings[0].Line);
    }

    [Fact]
    public void DetectsPyEvalAndExec()
    {
        var content = "x = eval(s)\ny = exec(s)\n";
        var findings = Scanner.Scan("a.py", content);

        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, f => f.Subtype == "eval");
        Assert.Contains(findings, f => f.Subtype == "exec");
    }

    [Fact]
    public void DetectsPowerShellInvokeExpression()
    {
        var content = "Invoke-Expression $cmd\n";
        var findings = Scanner.Scan("a.ps1", content);

        Assert.Single(findings);
        Assert.Equal("invoke_expression", findings[0].Subtype);
    }

    [Fact]
    public void DetectsBashEval()
    {
        var content = "eval ${cmd}\n";
        var findings = Scanner.Scan("a.sh", content);

        Assert.Single(findings);
        Assert.Equal("eval", findings[0].Subtype);
    }

    [Fact]
    public void DetectsCsAssemblyLoad_LowSeverity()
    {
        var content = "var asm = Assembly.Load(bytes);\n";
        var findings = Scanner.Scan("a.cs", content);

        Assert.Single(findings);
        Assert.Equal("assembly_load", findings[0].Subtype);
        Assert.Equal("low", findings[0].Severity);
    }

    [Fact]
    public void IgnoresUnknownExtension()
    {
        var content = "eval('foo')\n";
        var findings = Scanner.Scan("a.unknown", content);

        Assert.Empty(findings);
    }

    [Fact]
    public void RespectsIgnoreDirective()
    {
        var content = "eval('foo'); // codescan:ignore\n";
        var findings = Scanner.Scan("a.js", content);

        Assert.Empty(findings);
    }

    [Fact]
    public void SnippetIsTheMatchedLine_NoRedaction()
    {
        var content = "eval('hello')\n";
        var findings = Scanner.Scan("a.js", content);

        Assert.Single(findings);
        Assert.Equal("eval('hello')", findings[0].Snippet);
    }
}
```

- [ ] **Step 4.2: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: build errors referring to `DangerousFunctionScanner`.

- [ ] **Step 4.3: Implement `DangerousFunctionScanner.cs`**

Create `src/CodeScanner/Analyzers/DangerousFunctionScanner.cs`:

```csharp
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
```

- [ ] **Step 4.4: Run tests — verify they pass**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: all green.

- [ ] **Step 4.5: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Analyzers/DangerousFunctionScanner.cs hackathon/tests/CodeScanner.Tests/Analyzers/DangerousFunctionScannerTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(analysis): add dangerous-function scanner dispatched by extension"
```

---

## Task 5: C# smell walker (Roslyn visitor)

**Files:**
- Create: `hackathon/src/CodeScanner/Analyzers/ISmellAnalyzer.cs`
- Create: `hackathon/src/CodeScanner/Analyzers/SmellWalker.cs`
- (Test added in next task — walker is internal helper, exercised through `CSharpSmellAnalyzer`.)

- [ ] **Step 5.1: Add `ISmellAnalyzer.cs`**

Create `src/CodeScanner/Analyzers/ISmellAnalyzer.cs`:

```csharp
namespace CodeScanner;

public interface ISmellAnalyzer
{
    IReadOnlyList<SmellFinding> Analyze(string filePath, string content);
}
```

- [ ] **Step 5.2: Implement `SmellWalker.cs`**

Create `src/CodeScanner/Analyzers/SmellWalker.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeScanner;

internal sealed class SmellWalker : CSharpSyntaxWalker
{
    private sealed class FunctionContext
    {
        public string Name = "";
        public int StartLine;
        public int EndLine;
        public int Lines;
        public int ParamCount;
        public int Depth;
        public int MaxDepth;
        public int DeepestDepthLine;
    }

    public const int LongFunctionThreshold = 50;
    public const int DeepNestingThreshold = 4;
    public const int LongParamListThreshold = 5;

    private readonly string _filePath;
    private readonly List<SmellFinding> _findings;
    private readonly Stack<FunctionContext> _stack = new();

    public SmellWalker(string filePath, List<SmellFinding> findings)
    {
        _filePath = filePath;
        _findings = findings;
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        EnterFunction(node, node.Identifier.ValueText, node.ParameterList.Parameters.Count);
        base.VisitMethodDeclaration(node);
        ExitFunction();
    }

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        EnterFunction(node, node.Identifier.ValueText, node.ParameterList.Parameters.Count);
        base.VisitConstructorDeclaration(node);
        ExitFunction();
    }

    public override void VisitDestructorDeclaration(DestructorDeclarationSyntax node)
    {
        EnterFunction(node, "~" + node.Identifier.ValueText, paramCount: 0);
        base.VisitDestructorDeclaration(node);
        ExitFunction();
    }

    public override void VisitOperatorDeclaration(OperatorDeclarationSyntax node)
    {
        EnterFunction(node, "operator " + node.OperatorToken.ValueText, node.ParameterList.Parameters.Count);
        base.VisitOperatorDeclaration(node);
        ExitFunction();
    }

    public override void VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
    {
        EnterFunction(node, "conversion " + node.Type.ToString(), node.ParameterList.Parameters.Count);
        base.VisitConversionOperatorDeclaration(node);
        ExitFunction();
    }

    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        // Local function declaration adds to enclosing function's nesting.
        if (_stack.Count > 0)
        {
            IncrementCurrentDepth(node);
        }

        EnterFunction(node, node.Identifier.ValueText, node.ParameterList.Parameters.Count);
        base.VisitLocalFunctionStatement(node);
        ExitFunction();

        if (_stack.Count > 0)
        {
            _stack.Peek().Depth--;
        }
    }

    public override void VisitBlock(BlockSyntax node)
    {
        if (_stack.Count > 0)
        {
            IncrementCurrentDepth(node);
            base.VisitBlock(node);
            _stack.Peek().Depth--;
        }
        else
        {
            base.VisitBlock(node);
        }
    }

    private void IncrementCurrentDepth(SyntaxNode node)
    {
        var ctx = _stack.Peek();
        ctx.Depth++;
        if (ctx.Depth > ctx.MaxDepth)
        {
            ctx.MaxDepth = ctx.Depth;
            ctx.DeepestDepthLine = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        }
    }

    private void EnterFunction(SyntaxNode node, string name, int paramCount)
    {
        var span = node.GetLocation().GetLineSpan();
        var startLine = span.StartLinePosition.Line + 1;
        var endLine = span.EndLinePosition.Line + 1;
        var lines = endLine - startLine + 1;

        var ctx = new FunctionContext
        {
            Name = name,
            StartLine = startLine,
            EndLine = endLine,
            Lines = lines,
            ParamCount = paramCount,
            Depth = 0,
            MaxDepth = 0,
            DeepestDepthLine = startLine,
        };
        _stack.Push(ctx);

        if (lines > LongFunctionThreshold)
        {
            _findings.Add(new SmellFinding(
                Type: "long_function",
                Severity: ClassifyLongFunction(lines),
                File: _filePath,
                Name: name,
                StartLine: startLine,
                EndLine: endLine,
                Value: lines,
                Threshold: LongFunctionThreshold,
                Message: $"Function '{name}' is {lines} lines (threshold: {LongFunctionThreshold})"));
        }

        if (paramCount > LongParamListThreshold)
        {
            _findings.Add(new SmellFinding(
                Type: "long_parameter_list",
                Severity: ClassifyLongParamList(paramCount),
                File: _filePath,
                Name: name,
                StartLine: startLine,
                EndLine: startLine,
                Value: paramCount,
                Threshold: LongParamListThreshold,
                Message: $"Function '{name}' has {paramCount} parameters (threshold: {LongParamListThreshold})"));
        }
    }

    private void ExitFunction()
    {
        var ctx = _stack.Pop();
        if (ctx.MaxDepth > DeepNestingThreshold)
        {
            _findings.Add(new SmellFinding(
                Type: "deep_nesting",
                Severity: ClassifyDeepNesting(ctx.MaxDepth),
                File: _filePath,
                Name: ctx.Name,
                StartLine: ctx.DeepestDepthLine,
                EndLine: ctx.DeepestDepthLine,
                Value: ctx.MaxDepth,
                Threshold: DeepNestingThreshold,
                Message: $"Block depth {ctx.MaxDepth} inside '{ctx.Name}' (threshold: {DeepNestingThreshold})"));
        }
    }

    private static string ClassifyLongFunction(int lines) =>
        lines >= 151 ? "high" : lines >= 76 ? "medium" : "low";

    private static string ClassifyDeepNesting(int depth) =>
        depth >= 9 ? "high" : depth >= 7 ? "medium" : "low";

    private static string ClassifyLongParamList(int count) =>
        count >= 11 ? "high" : count >= 8 ? "medium" : "low";
}
```

- [ ] **Step 5.3: Build to verify it compiles**

```powershell
dotnet build src/CodeScanner/CodeScanner.csproj --nologo
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 5.4: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Analyzers/ISmellAnalyzer.cs hackathon/src/CodeScanner/Analyzers/SmellWalker.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(analysis): add Roslyn-based SmellWalker for C# functions"
```

---

## Task 6: C# smell analyzer

**Files:**
- Create: `hackathon/src/CodeScanner/Analyzers/CSharpSmellAnalyzer.cs`
- Create: `hackathon/tests/CodeScanner.Tests/Analyzers/CSharpSmellAnalyzerTests.cs`

- [ ] **Step 6.1: Write failing tests**

Create `tests/CodeScanner.Tests/Analyzers/CSharpSmellAnalyzerTests.cs`:

```csharp
using System.Text;

namespace CodeScanner.Tests.Analyzers;

public class CSharpSmellAnalyzerTests
{
    private static readonly CSharpSmellAnalyzer Analyzer = new();

    private static string ManyLines(int n)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < n; i++) { sb.Append("        var x").Append(i).Append(" = 1;\n"); }
        return sb.ToString();
    }

    [Fact]
    public void ShortMethod_NoFindings()
    {
        var src = "class C { void Foo() { var x = 1; } }";
        Assert.Empty(Analyzer.Analyze("a.cs", src));
    }

    [Fact]
    public void LongFunction_50Lines_NoFinding()
    {
        var body = ManyLines(48); // declaration adds ~2 more, total = 50 lines
        var src = $"class C {{\n    void Foo() {{\n{body}    }}\n}}";
        var findings = Analyzer.Analyze("a.cs", src).Where(f => f.Type == "long_function").ToList();
        Assert.Empty(findings);
    }

    [Fact]
    public void LongFunction_60Lines_LowSeverity()
    {
        var body = ManyLines(60);
        var src = $"class C {{\n    void Foo() {{\n{body}    }}\n}}";
        var findings = Analyzer.Analyze("a.cs", src).Where(f => f.Type == "long_function").ToList();
        Assert.Single(findings);
        Assert.Equal("low", findings[0].Severity);
        Assert.Equal("Foo", findings[0].Name);
    }

    [Fact]
    public void LongFunction_100Lines_MediumSeverity()
    {
        var body = ManyLines(100);
        var src = $"class C {{\n    void Foo() {{\n{body}    }}\n}}";
        var findings = Analyzer.Analyze("a.cs", src).Where(f => f.Type == "long_function").ToList();
        Assert.Single(findings);
        Assert.Equal("medium", findings[0].Severity);
    }

    [Fact]
    public void LongFunction_200Lines_HighSeverity()
    {
        var body = ManyLines(200);
        var src = $"class C {{\n    void Foo() {{\n{body}    }}\n}}";
        var findings = Analyzer.Analyze("a.cs", src).Where(f => f.Type == "long_function").ToList();
        Assert.Single(findings);
        Assert.Equal("high", findings[0].Severity);
    }

    [Fact]
    public void LongParameterList_5Params_NoFinding()
    {
        var src = "class C { void Foo(int a, int b, int c, int d, int e) { } }";
        Assert.Empty(Analyzer.Analyze("a.cs", src).Where(f => f.Type == "long_parameter_list"));
    }

    [Fact]
    public void LongParameterList_6Params_LowSeverity()
    {
        var src = "class C { void Foo(int a, int b, int c, int d, int e, int f) { } }";
        var findings = Analyzer.Analyze("a.cs", src).Where(f => f.Type == "long_parameter_list").ToList();
        Assert.Single(findings);
        Assert.Equal("low", findings[0].Severity);
        Assert.Equal(6, findings[0].Value);
    }

    [Fact]
    public void LongParameterList_12Params_HighSeverity()
    {
        var src = "class C { void Foo(int a,int b,int c,int d,int e,int f,int g,int h,int i,int j,int k,int l) { } }";
        var findings = Analyzer.Analyze("a.cs", src).Where(f => f.Type == "long_parameter_list").ToList();
        Assert.Single(findings);
        Assert.Equal("high", findings[0].Severity);
    }

    [Fact]
    public void DeepNesting_4Levels_NoFinding()
    {
        // Body block (1) > if (2) > if (3) > if (4) - threshold is > 4, so 4 doesn't trigger.
        var src = @"
class C {
    void Foo() {
        if (true) {
            if (true) {
                if (true) {
                    var x = 1;
                }
            }
        }
    }
}";
        Assert.Empty(Analyzer.Analyze("a.cs", src).Where(f => f.Type == "deep_nesting"));
    }

    [Fact]
    public void DeepNesting_5Levels_LowSeverity()
    {
        var src = @"
class C {
    void Foo() {
        if (true) {
            if (true) {
                if (true) {
                    if (true) {
                        var x = 1;
                    }
                }
            }
        }
    }
}";
        var findings = Analyzer.Analyze("a.cs", src).Where(f => f.Type == "deep_nesting").ToList();
        Assert.Single(findings);
        Assert.Equal("low", findings[0].Severity);
        Assert.Equal(5, findings[0].Value);
    }

    [Fact]
    public void Lambdas_DoNotTriggerLongFunction()
    {
        var sb = new StringBuilder();
        sb.AppendLine("class C { void Foo() {");
        sb.AppendLine("    System.Action a = () => {");
        for (var i = 0; i < 80; i++) { sb.AppendLine("        var x = 1;"); }
        sb.AppendLine("    }; } }");

        var findings = Analyzer.Analyze("a.cs", sb.ToString())
            .Where(f => f.Type == "long_function").ToList();

        // The enclosing Foo() may be flagged (its body holds an 80+ line lambda),
        // but no separate finding should target the lambda itself (no name, no path).
        Assert.All(findings, f => Assert.NotEqual(string.Empty, f.Name));
    }

    [Fact]
    public void AutoGeneratedFile_IsSkipped()
    {
        var src = "// <auto-generated>\nclass C { void Foo() { " + ManyLines(80) + " } }";
        var findings = Analyzer.Analyze("Generated.cs", src);
        Assert.Empty(findings);
    }

    [Fact]
    public void Constructor_IsAnalyzed()
    {
        var src = "class C { public C(int a, int b, int c, int d, int e, int f) { } }";
        var findings = Analyzer.Analyze("a.cs", src).Where(f => f.Type == "long_parameter_list").ToList();
        Assert.Single(findings);
        Assert.Equal("C", findings[0].Name);
    }
}
```

- [ ] **Step 6.2: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: build errors referring to `CSharpSmellAnalyzer`.

- [ ] **Step 6.3: Implement `CSharpSmellAnalyzer.cs`**

Create `src/CodeScanner/Analyzers/CSharpSmellAnalyzer.cs`:

```csharp
using Microsoft.CodeAnalysis.CSharp;

namespace CodeScanner;

public sealed class CSharpSmellAnalyzer : ISmellAnalyzer
{
    private const int AutoGeneratedScanLines = 5;

    public IReadOnlyList<SmellFinding> Analyze(string filePath, string content)
    {
        if (string.IsNullOrEmpty(content)) { return Array.Empty<SmellFinding>(); }
        if (IsAutoGenerated(content)) { return Array.Empty<SmellFinding>(); }

        var tree = CSharpSyntaxTree.ParseText(content, path: filePath);
        var root = tree.GetRoot();
        var findings = new List<SmellFinding>();
        var walker = new SmellWalker(filePath, findings);
        walker.Visit(root);
        return findings;
    }

    private static bool IsAutoGenerated(string content)
    {
        var marker = "<auto-generated";
        var lines = content.Split('\n');
        var max = Math.Min(AutoGeneratedScanLines, lines.Length);
        for (var i = 0; i < max; i++)
        {
            if (lines[i].Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
```

- [ ] **Step 6.4: Run tests — verify they pass**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: all green. The `Lambdas_DoNotTriggerLongFunction` test should pass because the enclosing Foo only triggers if its full span is > 50 lines (it is, since it contains the 80-line lambda body) — but the assertion only checks that every long_function finding has a non-empty Name (so lambdas, which would have no name, are not surfaced).

- [ ] **Step 6.5: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Analyzers/CSharpSmellAnalyzer.cs hackathon/tests/CodeScanner.Tests/Analyzers/CSharpSmellAnalyzerTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(analysis): add CSharpSmellAnalyzer with auto-generated skip"
```

---

## Task 7: AnalyzerHost (orchestration)

**Files:**
- Create: `hackathon/src/CodeScanner/Analyzers/AnalyzerHost.cs`
- Create: `hackathon/tests/CodeScanner.Tests/Analyzers/AnalyzerHostTests.cs`

- [ ] **Step 7.1: Write failing tests**

Create `tests/CodeScanner.Tests/Analyzers/AnalyzerHostTests.cs`:

```csharp
namespace CodeScanner.Tests.Analyzers;

public class AnalyzerHostTests
{
    [Fact]
    public void NoFlags_ReturnsEmptyResults()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs", "class C {}");

        var scan = Scanner.Scan(tree.Root, new ScanOptions());
        var host = new AnalyzerHost();
        var result = host.Analyze(scan, new ScanOptions());

        Assert.Empty(result.Smells);
        Assert.Empty(result.SecurityFindings);
    }

    [Fact]
    public void SmellsFlag_ProcessesCsharpFilesOnly()
    {
        using var tree = new TempTree();
        var manyLines = string.Concat(Enumerable.Repeat("var x = 1;\n", 60));
        tree.WriteFile("Big.cs", $"class C {{ void Foo() {{\n{manyLines}}} }}");
        tree.WriteFile("ignore.py", "x = 1");

        var scan = Scanner.Scan(tree.Root, new ScanOptions());
        var host = new AnalyzerHost();
        var result = host.Analyze(scan, new ScanOptions { Smells = true });

        Assert.NotEmpty(result.Smells);
        Assert.All(result.Smells, s => Assert.EndsWith(".cs", s.File));
    }

    [Fact]
    public void SecurityFlag_ProcessesAllNonBinaryFiles()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs",  "var k = \"AKIA1234567890ABCDEF\";");
        tree.WriteFile("b.txt", "password = \"hunter2-strong-pw\"");

        var scan = Scanner.Scan(tree.Root, new ScanOptions());
        var host = new AnalyzerHost();
        var result = host.Analyze(scan, new ScanOptions { Security = true });

        Assert.True(result.SecurityFindings.Count >= 2);
        Assert.Contains(result.SecurityFindings, f => f.File.EndsWith(".cs", StringComparison.Ordinal));
        Assert.Contains(result.SecurityFindings, f => f.File.EndsWith(".txt", StringComparison.Ordinal));
    }

    [Fact]
    public void SecuritySkipGlob_SkipsMatchingFiles()
    {
        using var tree = new TempTree();
        tree.WriteFile("src/code.cs",   "var k = \"AKIA1234567890ABCDEF\";");
        tree.WriteFile("tests/fixtures/keys.txt", "AKIA1234567890ABCDEF");

        var scan = Scanner.Scan(tree.Root, new ScanOptions());
        var host = new AnalyzerHost();
        var result = host.Analyze(scan, new ScanOptions
        {
            Security = true,
            SecuritySkipGlobs = new[] { "tests/**" },
        });

        Assert.Single(result.SecurityFindings);
        Assert.EndsWith("code.cs", result.SecurityFindings[0].File);
    }

    [Fact]
    public void LargeFile_LoggedAndSkippedForSecurity()
    {
        using var tree = new TempTree();
        var huge = new string('a', 1_100_000); // > 1 MB
        tree.WriteFile("big.txt", huge);

        var scan = Scanner.Scan(tree.Root, new ScanOptions());
        var host = new AnalyzerHost();
        var result = host.Analyze(scan, new ScanOptions { Security = true });

        Assert.Empty(result.SecurityFindings);
        Assert.Contains(result.Errors, e => e.Reason.Contains("too large"));
    }
}
```

- [ ] **Step 7.2: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: build errors referring to `AnalyzerHost`.

- [ ] **Step 7.3: Implement `AnalyzerHost.cs`**

Create `src/CodeScanner/Analyzers/AnalyzerHost.cs`:

```csharp
using Microsoft.Extensions.FileSystemGlobbing;

namespace CodeScanner;

public sealed record AnalysisResult(
    IReadOnlyList<SmellFinding> Smells,
    IReadOnlyList<SecurityFinding> SecurityFindings,
    IReadOnlyList<ScanError> Errors);

public sealed class AnalyzerHost
{
    private const long SecurityFileSizeLimit = 1_048_576; // 1 MB

    private readonly ISmellAnalyzer _csharpSmells;
    private readonly ISecurityScanner _secrets;
    private readonly ISecurityScanner _dangerousFunctions;

    public AnalyzerHost()
        : this(new CSharpSmellAnalyzer(), new SecretScanner(), new DangerousFunctionScanner()) { }

    public AnalyzerHost(
        ISmellAnalyzer csharpSmells,
        ISecurityScanner secrets,
        ISecurityScanner dangerousFunctions)
    {
        _csharpSmells = csharpSmells;
        _secrets = secrets;
        _dangerousFunctions = dangerousFunctions;
    }

    public AnalysisResult Analyze(ScanResult scan, ScanOptions options)
    {
        var smells = new List<SmellFinding>();
        var securityFindings = new List<SecurityFinding>();
        var errors = new List<ScanError>();

        Matcher? skipMatcher = null;
        if (options.Security && options.SecuritySkipGlobs.Count > 0)
        {
            skipMatcher = new Matcher();
            foreach (var glob in options.SecuritySkipGlobs)
            {
                skipMatcher.AddInclude(glob);
            }
        }

        foreach (var entry in scan.FileEntries)
        {
            if (entry.IsBinary) { continue; }

            var path = entry.Path;
            string? content = null;

            if (options.Smells && entry.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                content ??= TryRead(path, errors);
                if (content is not null)
                {
                    try { smells.AddRange(_csharpSmells.Analyze(path, content)); }
                    catch (Exception ex)
                    {
                        errors.Add(new ScanError(path, $"smell analysis failed: {ex.GetType().Name}: {ex.Message}"));
                    }
                }
            }

            if (options.Security)
            {
                if (skipMatcher is not null)
                {
                    var rel = Path.GetRelativePath(scan.Root, path).Replace('\\', '/');
                    if (skipMatcher.Match(rel).HasMatches) { continue; }
                }

                long length;
                try { length = new FileInfo(path).Length; }
                catch (Exception ex)
                {
                    errors.Add(new ScanError(path, $"{ex.GetType().Name}: {ex.Message}"));
                    continue;
                }

                if (length > SecurityFileSizeLimit)
                {
                    errors.Add(new ScanError(path, "file too large for security scan"));
                    continue;
                }

                content ??= TryRead(path, errors);
                if (content is null) { continue; }

                try { securityFindings.AddRange(_secrets.Scan(path, content)); }
                catch (Exception ex)
                {
                    errors.Add(new ScanError(path, $"secret scan failed: {ex.GetType().Name}: {ex.Message}"));
                }

                try { securityFindings.AddRange(_dangerousFunctions.Scan(path, content)); }
                catch (Exception ex)
                {
                    errors.Add(new ScanError(path, $"dangerous-function scan failed: {ex.GetType().Name}: {ex.Message}"));
                }
            }
        }

        return new AnalysisResult(smells, securityFindings, errors);
    }

    private static string? TryRead(string path, List<ScanError> errors)
    {
        try { return File.ReadAllText(path); }
        catch (Exception ex)
        {
            errors.Add(new ScanError(path, $"{ex.GetType().Name}: {ex.Message}"));
            return null;
        }
    }
}
```

- [ ] **Step 7.4: Run tests — verify they pass**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: all green.

- [ ] **Step 7.5: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Analyzers/AnalyzerHost.cs hackathon/tests/CodeScanner.Tests/Analyzers/AnalyzerHostTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(analysis): add AnalyzerHost orchestrating smell + security passes"
```

---

## Task 8: Update Report serialization

**Files:**
- Modify: `hackathon/src/CodeScanner/Report.cs`
- Modify: `hackathon/tests/CodeScanner.Tests/ReportTests.cs`

- [ ] **Step 8.1: Append failing tests to `ReportTests.cs`**

Append inside the `ReportTests` class (before the closing `}`):

```csharp
    [Fact]
    public void Serialize_BackwardsCompat_NoAnalysisKeysWhenAnalysisEmpty()
    {
        var result = new ScanResult("C:/x", Array.Empty<FileEntry>(), Array.Empty<string>(), Array.Empty<ScanError>());
        var analysis = new AnalysisResult(Array.Empty<SmellFinding>(), Array.Empty<SecurityFinding>(), Array.Empty<ScanError>());
        var options = new ScanOptions();

        var json = Report.Serialize(result, analysis, options, pretty: false);
        var root = Parse(json);

        Assert.False(root.TryGetProperty("smells", out _));
        Assert.False(root.TryGetProperty("securityIssues", out _));
    }

    [Fact]
    public void Serialize_IncludesSmellsWhenSmellsFlagOn()
    {
        var result = new ScanResult(
            "C:/x",
            new[] { new FileEntry("a.cs", ".cs", "C#", 10, false) },
            Array.Empty<string>(), Array.Empty<ScanError>());

        var analysis = new AnalysisResult(
            Smells: new[] { new SmellFinding("long_function", "medium", "a.cs", "Foo", 1, 80, 80, 50, "msg") },
            SecurityFindings: Array.Empty<SecurityFinding>(),
            Errors: Array.Empty<ScanError>());

        var options = new ScanOptions { Smells = true };

        var json = Report.Serialize(result, analysis, options, pretty: false);
        var root = Parse(json);

        Assert.Equal(1, root.GetProperty("smells").GetArrayLength());
        Assert.Equal("long_function", root.GetProperty("smells")[0].GetProperty("type").GetString());

        var lang = root.GetProperty("languages").GetProperty("C#");
        Assert.Equal(1, lang.GetProperty("smells").GetProperty("medium").GetInt32());
        Assert.Equal(1, lang.GetProperty("smells").GetProperty("total").GetInt32());
    }

    [Fact]
    public void Serialize_IncludesSecurityWhenSecurityFlagOn()
    {
        var result = new ScanResult(
            "C:/x",
            new[] { new FileEntry("a.cs", ".cs", "C#", 10, false) },
            Array.Empty<string>(), Array.Empty<ScanError>());

        var analysis = new AnalysisResult(
            Smells: Array.Empty<SmellFinding>(),
            SecurityFindings: new[] {
                new SecurityFinding("hardcoded_secret","aws_access_key","high","a.cs",1,5,"snip","AWS detected")
            },
            Errors: Array.Empty<ScanError>());

        var options = new ScanOptions { Security = true };
        var json = Report.Serialize(result, analysis, options, pretty: false);
        var root = Parse(json);

        Assert.Equal(1, root.GetProperty("securityIssues").GetArrayLength());
        Assert.Equal("aws_access_key",
            root.GetProperty("securityIssues")[0].GetProperty("subtype").GetString());

        var lang = root.GetProperty("languages").GetProperty("C#");
        Assert.Equal(1, lang.GetProperty("security").GetProperty("high").GetInt32());
    }

    [Fact]
    public void Serialize_AnalysisErrorsMergedIntoScannedErrors()
    {
        var result = new ScanResult("C:/x", Array.Empty<FileEntry>(), Array.Empty<string>(),
            new[] { new ScanError("a.bin", "binary file, lines not counted") });

        var analysis = new AnalysisResult(
            Smells: Array.Empty<SmellFinding>(),
            SecurityFindings: Array.Empty<SecurityFinding>(),
            Errors: new[] { new ScanError("big.txt", "file too large for security scan") });

        var options = new ScanOptions { Security = true };
        var json = Report.Serialize(result, analysis, options, pretty: false);
        var root = Parse(json);

        var errs = root.GetProperty("scanned").GetProperty("errors");
        Assert.Equal(2, errs.GetArrayLength());
    }
```

- [ ] **Step 8.2: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: compile error — `Report.Serialize` does not accept `AnalysisResult` and `ScanOptions`.

- [ ] **Step 8.3: Update `Report.cs`**

Replace the entire contents of `src/CodeScanner/Report.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodeScanner;

public static class Report
{
    public static string Serialize(ScanResult result, bool pretty)
        => Serialize(result,
            new AnalysisResult(Array.Empty<SmellFinding>(), Array.Empty<SecurityFinding>(), Array.Empty<ScanError>()),
            new ScanOptions(),
            pretty);

    public static string Serialize(ScanResult result, AnalysisResult analysis, ScanOptions options, bool pretty)
    {
        var languages = new JsonObject();
        var totalFiles = 0L;
        var totalLines = 0L;

        // Per-language summary maps.
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

    private static string NormalizePath(string path) => path.Replace('\\', '/');
}
```

- [ ] **Step 8.4: Run tests — verify they pass**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: all green (existing report tests still pass thanks to the legacy single-arg overload; new tests pass).

- [ ] **Step 8.5: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Report.cs hackathon/tests/CodeScanner.Tests/ReportTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(analysis): extend Report with smells/securityIssues + per-language summaries"
```

---

## Task 9: Wire CLI flags

**Files:**
- Modify: `hackathon/src/CodeScanner/Cli.cs`
- Modify: `hackathon/tests/CodeScanner.Tests/CliTests.cs`

- [ ] **Step 9.1: Append failing CLI tests**

Append inside the `CliTests` class:

```csharp
    [Fact]
    public void Cli_Smells_AddsSmellsArrayToOutput()
    {
        using var tree = new TempTree();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("class C {");
        sb.AppendLine("    void Foo(int a, int b, int c, int d, int e, int f) {");
        for (var i = 0; i < 60; i++) { sb.AppendLine("        var x = 1;"); }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        tree.WriteFile("a.cs", sb.ToString());

        var (exit, stdout, _) = RunCli(tree.Root, "--smells");

        Assert.Equal(0, exit);
        var doc = JsonDocument.Parse(stdout);
        var smells = doc.RootElement.GetProperty("smells");
        Assert.True(smells.GetArrayLength() >= 2); // long_function + long_parameter_list
    }

    [Fact]
    public void Cli_Security_DetectsHardcodedAwsKey()
    {
        using var tree = new TempTree();
        tree.WriteFile("config.cs", "var k = \"AKIA1234567890ABCDEF\";\n");

        var (exit, stdout, _) = RunCli(tree.Root, "--security");

        Assert.Equal(0, exit);
        var doc = JsonDocument.Parse(stdout);
        var sec = doc.RootElement.GetProperty("securityIssues");
        Assert.Equal(1, sec.GetArrayLength());
        Assert.Equal("aws_access_key", sec[0].GetProperty("subtype").GetString());
    }

    [Fact]
    public void Cli_Analyze_EnablesBothFlags()
    {
        using var tree = new TempTree();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("class C {");
        sb.AppendLine("    void Foo() {");
        for (var i = 0; i < 60; i++) { sb.AppendLine("        var x = 1;"); }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine("// AKIA1234567890ABCDEF");
        tree.WriteFile("a.cs", sb.ToString());

        var (exit, stdout, _) = RunCli(tree.Root, "--analyze");

        Assert.Equal(0, exit);
        var root = JsonDocument.Parse(stdout).RootElement;
        Assert.True(root.GetProperty("smells").GetArrayLength() >= 1);
        Assert.True(root.GetProperty("securityIssues").GetArrayLength() >= 1);
    }

    [Fact]
    public void Cli_NoAnalysisFlags_OmitsAnalysisKeys()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs", "class C {}\n");

        var (exit, stdout, _) = RunCli(tree.Root);

        Assert.Equal(0, exit);
        var root = JsonDocument.Parse(stdout).RootElement;
        Assert.False(root.TryGetProperty("smells", out _));
        Assert.False(root.TryGetProperty("securityIssues", out _));
    }
```

- [ ] **Step 9.2: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: test failures because new flags aren't wired (or build error if anything mistyped).

- [ ] **Step 9.3: Update `Cli.cs` to add flags and wire AnalyzerHost**

Replace the entire contents of `src/CodeScanner/Cli.cs`:

```csharp
using System.CommandLine;

namespace CodeScanner;

public static class Cli
{
    public static async Task<int> RunAsync(string[] args)
    {
        var pathArg = new Argument<string>("path") { Description = "Directory to scan" };
        var outputOpt = new Option<string?>("--output", "-o") { Description = "Write JSON to this file instead of stdout" };
        var excludeOpt = new Option<string[]>("--exclude", "-e")
        {
            Description = "Extra directory names to skip (additive to defaults)",
            AllowMultipleArgumentsPerToken = true,
        };
        var followOpt = new Option<bool>("--follow-symlinks") { Description = "Follow symlinked directories (default: skip)" };
        var prettyOpt = new Option<bool>("--pretty") { Description = "Pretty-print JSON output" };
        var verboseOpt = new Option<bool>("--verbose", "-v") { Description = "Log scan progress to stderr" };
        var smellsOpt   = new Option<bool>("--smells")    { Description = "Run Roslyn-based smell analyzer on .cs files" };
        var securityOpt = new Option<bool>("--security")  { Description = "Run regex-based security scanners (secrets + dangerous functions)" };
        var analyzeOpt  = new Option<bool>("--analyze")   { Description = "Shorthand for --smells --security" };
        var securitySkipOpt = new Option<string[]>("--security-skip")
        {
            Description = "Glob patterns to skip during security scan only (additive)",
            AllowMultipleArgumentsPerToken = true,
        };

        var root = new RootCommand("Recursively scan a directory and emit JSON file/line statistics.")
        {
            pathArg, outputOpt, excludeOpt, followOpt, prettyOpt, verboseOpt,
            smellsOpt, securityOpt, analyzeOpt, securitySkipOpt,
        };

        root.SetAction(parseResult =>
        {
            var path     = parseResult.GetValue(pathArg)!;
            var output   = parseResult.GetValue(outputOpt);
            var excludes = parseResult.GetValue(excludeOpt) ?? Array.Empty<string>();
            var follow   = parseResult.GetValue(followOpt);
            var pretty   = parseResult.GetValue(prettyOpt);
            var verbose  = parseResult.GetValue(verboseOpt);
            var smells   = parseResult.GetValue(smellsOpt);
            var security = parseResult.GetValue(securityOpt);
            var analyze  = parseResult.GetValue(analyzeOpt);
            var skip     = parseResult.GetValue(securitySkipOpt) ?? Array.Empty<string>();

            if (analyze) { smells = true; security = true; }

            return Execute(path, output, excludes, follow, pretty, verbose, smells, security, skip);
        });

        return await root.Parse(args).InvokeAsync();
    }

    private static int Execute(
        string path,
        string? output,
        string[] excludes,
        bool followSymlinks,
        bool pretty,
        bool verbose,
        bool smells,
        bool security,
        string[] securitySkipGlobs)
    {
        if (!Directory.Exists(path))
        {
            if (File.Exists(path))
            {
                Console.Error.WriteLine($"error: path is a file, not a directory: {path}");
            }
            else
            {
                Console.Error.WriteLine($"error: path does not exist: {path}");
            }
            return 1;
        }

        try
        {
            if (verbose) { Console.Error.WriteLine($"info: scanning {path}"); }

            var options = new ScanOptions
            {
                FollowSymlinks = followSymlinks,
                ExtraExcludes = excludes,
                Smells = smells,
                Security = security,
                SecuritySkipGlobs = securitySkipGlobs,
            };

            var result = Scanner.Scan(path, options);

            AnalysisResult analysis;
            if (smells || security)
            {
                if (verbose) { Console.Error.WriteLine("info: running analysis pass"); }
                var host = new AnalyzerHost();
                analysis = host.Analyze(result, options);
            }
            else
            {
                analysis = new AnalysisResult(
                    Array.Empty<SmellFinding>(),
                    Array.Empty<SecurityFinding>(),
                    Array.Empty<ScanError>());
            }

            var json = Report.Serialize(result, analysis, options, pretty);

            if (output is null)
            {
                Console.Out.WriteLine(json);
            }
            else
            {
                File.WriteAllText(output, json);
                if (verbose) { Console.Error.WriteLine($"info: wrote {output}"); }
            }

            if (verbose)
            {
                Console.Error.WriteLine(
                    $"info: {result.FileEntries.Count} files, {result.Errors.Count + analysis.Errors.Count} errors, " +
                    $"{analysis.Smells.Count} smells, {analysis.SecurityFindings.Count} security");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.GetType().Name}: {ex.Message}");
            return 2;
        }
    }
}
```

- [ ] **Step 9.4: Build before subprocess CLI tests**

```powershell
dotnet build CodeScanner.sln --nologo
```

Expected: clean build.

- [ ] **Step 9.5: Run all tests**

```powershell
dotnet test CodeScanner.sln --nologo
```

Expected: all green (62 prior + 4 new CLI tests + ones from intermediate tasks).

- [ ] **Step 9.6: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Cli.cs hackathon/tests/CodeScanner.Tests/CliTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(analysis): wire --smells, --security, --analyze, --security-skip flags"
```

---

## Task 10: End-to-end fixture verification

**Files:**
- Create: `hackathon/tests/CodeScanner.Tests/fixtures/Smells/Smelly.cs`
- Create: `hackathon/tests/CodeScanner.Tests/fixtures/Security/secrets.txt`
- Create: `hackathon/tests/CodeScanner.Tests/fixtures/Security/dangerous.js`
- Append to: `hackathon/tests/CodeScanner.Tests/CliTests.cs`

- [ ] **Step 10.1: Create smell fixture**

Create `tests/CodeScanner.Tests/fixtures/Smells/Smelly.cs`:

```csharp
namespace Fixtures;

public class Smelly
{
    public void TooManyParams(int a, int b, int c, int d, int e, int f, int g)
    {
        // 7 params -> long_parameter_list (low)
    }

    public void DeeplyNested()
    {
        if (true)
        {
            for (var i = 0; i < 10; i++)
            {
                while (i > 0)
                {
                    if (i % 2 == 0)
                    {
                        if (i % 3 == 0)
                        {
                            // depth = 6 -> low
                        }
                    }
                }
            }
        }
    }

    public void LongFunction()
    {
        var x0 = 0;  var x1 = 0;  var x2 = 0;  var x3 = 0;  var x4 = 0;
        var x5 = 0;  var x6 = 0;  var x7 = 0;  var x8 = 0;  var x9 = 0;
        var x10 = 0; var x11 = 0; var x12 = 0; var x13 = 0; var x14 = 0;
        var x15 = 0; var x16 = 0; var x17 = 0; var x18 = 0; var x19 = 0;
        var x20 = 0; var x21 = 0; var x22 = 0; var x23 = 0; var x24 = 0;
        var x25 = 0; var x26 = 0; var x27 = 0; var x28 = 0; var x29 = 0;
        var x30 = 0; var x31 = 0; var x32 = 0; var x33 = 0; var x34 = 0;
        var x35 = 0; var x36 = 0; var x37 = 0; var x38 = 0; var x39 = 0;
        var x40 = 0; var x41 = 0; var x42 = 0; var x43 = 0; var x44 = 0;
        var x45 = 0; var x46 = 0; var x47 = 0; var x48 = 0; var x49 = 0;
        var x50 = 0; var x51 = 0; var x52 = 0; var x53 = 0; var x54 = 0;
        var x55 = 0; var x56 = 0; var x57 = 0; var x58 = 0; var x59 = 0;
    }
}
```

- [ ] **Step 10.2: Create security fixtures**

Create `tests/CodeScanner.Tests/fixtures/Security/secrets.txt`:

```
# Fake secrets used as test fixtures only - do NOT use these elsewhere.
AWS_KEY=AKIA1234567890ABCDEF
GITHUB_TOKEN=ghp_aBcDeFgHiJkLmNoPqRsTuVwXyZ0123456789ab
password = "hunter2-strong-password"
```

Create `tests/CodeScanner.Tests/fixtures/Security/dangerous.js`:

```javascript
// Fixture: dangerous JS calls.
function run(input) {
  eval(input);
  setTimeout("doStuff()", 100);
  const f = new Function("return 1");
}
```

- [ ] **Step 10.3: Append end-to-end CLI test**

Append inside the `CliTests` class:

```csharp
    private static string FindFixturesRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "fixtures")))
        {
            // climb up (bin/Debug/net9.0 -> tests/CodeScanner.Tests)
            var parent = Path.GetDirectoryName(dir);
            if (parent == dir) { break; }
            dir = parent;
        }
        Assert.NotNull(dir);
        return Path.Combine(dir!, "fixtures");
    }

    [Fact]
    public void Cli_Analyze_OnFixtures_ProducesExpectedFindings()
    {
        var fixtures = FindFixturesRoot();
        Assert.True(Directory.Exists(fixtures), $"fixtures missing at {fixtures}");

        var (exit, stdout, _) = RunCli(fixtures, "--analyze");

        Assert.Equal(0, exit);
        var root = JsonDocument.Parse(stdout).RootElement;

        // Smells
        var smellTypes = root.GetProperty("smells").EnumerateArray()
            .Select(s => s.GetProperty("type").GetString()).ToHashSet();
        Assert.Contains("long_function", smellTypes);
        Assert.Contains("long_parameter_list", smellTypes);
        Assert.Contains("deep_nesting", smellTypes);

        // Security findings
        var securitySubtypes = root.GetProperty("securityIssues").EnumerateArray()
            .Select(s => s.GetProperty("subtype").GetString()).ToHashSet();
        Assert.Contains("aws_access_key", securitySubtypes);
        Assert.Contains("github_pat", securitySubtypes);
        Assert.Contains("eval", securitySubtypes);
        Assert.Contains("new_function", securitySubtypes);
    }
```

- [ ] **Step 10.4: Set fixture files to copy to test output directory**

The fixtures need to be reachable from `AppContext.BaseDirectory` (the test bin folder) at runtime. Add this `<ItemGroup>` to `tests/CodeScanner.Tests/CodeScanner.Tests.csproj` (insert before the closing `</Project>`):

```xml
  <ItemGroup>
    <None Include="fixtures\**\*.*" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

- [ ] **Step 10.5: Build + run all tests**

```powershell
dotnet build CodeScanner.sln --nologo
dotnet test CodeScanner.sln --nologo
```

Expected: clean build, all tests green.

- [ ] **Step 10.6: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/tests/CodeScanner.Tests/fixtures hackathon/tests/CodeScanner.Tests/CliTests.cs hackathon/tests/CodeScanner.Tests/CodeScanner.Tests.csproj
git -C C:/Cmm-testing/Claude_Training commit -m "test(analysis): add fixture-based end-to-end --analyze test"
```

---

## Task 11: README + final verification

**Files:**
- Modify: `hackathon/README.md`

- [ ] **Step 11.1: Update README**

Replace the entire contents of `hackathon/README.md`:

````markdown
# Code Scanner

A small .NET 9 CLI that recursively scans a directory and reports a JSON summary of files and lines, grouped by language. Optional opt-in passes detect C# code smells and basic security issues.

## Build

```powershell
dotnet build CodeScanner.sln
```

## Run (development)

```powershell
dotnet run --project src/CodeScanner -- <path> \
  [--output report.json] [--pretty] \
  [--exclude name ...] [--follow-symlinks] [--verbose] \
  [--smells] [--security] [--analyze] [--security-skip glob ...]
```

`--analyze` is shorthand for `--smells --security`.

## Install as a global tool

```powershell
dotnet pack src/CodeScanner -o ./nupkg
dotnet tool install --global --add-source ./nupkg CodeScanner
code-scanner <path> --analyze
```

## Test

```powershell
dotnet test CodeScanner.sln
```

## Output shape

```json
{
  "totalFiles": 142,
  "totalLines": 18374,
  "languages": {
    "C#": {
      "files": 47, "lines": 8230, "extensions": [".cs"],
      "smells":   { "low": 2, "medium": 1, "high": 0, "total": 3 },
      "security": { "low": 0, "medium": 1, "high": 0, "total": 1 }
    }
  },
  "smells": [
    { "type": "long_function", "severity": "medium", "file": "...", "name": "Foo",
      "startLine": 53, "endLine": 134, "value": 82, "threshold": 50, "message": "..." }
  ],
  "securityIssues": [
    { "type": "hardcoded_secret", "subtype": "aws_access_key", "severity": "high",
      "file": "...", "line": 42, "column": 24,
      "snippet": "var key = \"AKIA••••REDACTED\";",
      "message": "AWS Access Key ID detected" }
  ],
  "scanned": {
    "root": "C:/scanned/dir",
    "skippedDirs": [".git", "node_modules"],
    "errors": [
      { "path": "C:/scanned/dir/blob.bin", "reason": "binary file, lines not counted" }
    ]
  }
}
```

When neither `--smells` nor `--security` is set, the `smells`, `securityIssues`, and per-language `smells`/`security` keys are omitted (backwards-compatible with the original output).

## Default skipped directories

`.git`, `node_modules`, `__pycache__`, `.venv`, `venv`, `.pytest_cache`, `dist`, `build`, `.mypy_cache`, `.ruff_cache`, `bin`, `obj`.

Use `--exclude <name>` (repeatable) to add more.

## Smell rules (C# only, requires `--smells`)

| Smell | Threshold | low | medium | high |
|---|---|---|---|---|
| Long function | > 50 lines | 51–75 | 76–150 | 151+ |
| Deep nesting | > 4 levels | 5–6 | 7–8 | 9+ |
| Long parameter list | > 5 params | 6–7 | 8–10 | 11+ |

## Security rules (requires `--security`)

- **Hardcoded secrets** (regex): AWS keys, GitHub PATs, Slack tokens, JWTs, private keys, generic password/api-key assignments, connection-string passwords. Snippets in JSON are redacted.
- **Dangerous functions** (regex, dispatched by extension): `eval`/`exec` in JS/TS/Python, `setTimeout`/`setInterval` with string arg, `new Function`, `subprocess.shell=True`, `Invoke-Expression`/`iex` in PowerShell, bash `eval`, `Assembly.Load` in C#.

Append `// codescan:ignore` (or `#`, `--`, `;` comment markers) at end of a line to suppress findings on that line.

## Exit codes

| Code | Meaning |
|------|---------|
| 0    | Success (per-file errors, if any, are inside the JSON) |
| 1    | Path doesn't exist or isn't a directory |
| 2    | Unexpected error |
````

- [ ] **Step 11.2: Final verification — `/warnaserror` build**

```powershell
Set-Location C:\Cmm-testing\Claude_Training\hackathon
dotnet build CodeScanner.sln /warnaserror --nologo
```

Expected: 0 warnings, 0 errors.

- [ ] **Step 11.3: Final verification — full test run**

```powershell
dotnet test CodeScanner.sln --nologo --logger "console;verbosity=normal"
```

Expected: every test passes.

- [ ] **Step 11.4: Final verification — manual smoke against parent repo**

```powershell
dotnet run --no-build --project src/CodeScanner -- ../ --analyze --pretty
```

Expected: valid JSON to stdout including `smells` and `securityIssues` arrays. The scanner itself shouldn't have any high-severity findings on its own code; some low/medium long-function findings are likely.

- [ ] **Step 11.5: Final verification — backwards compat**

```powershell
dotnet run --no-build --project src/CodeScanner -- ../ --pretty
```

Expected: JSON shape identical to v1 (no `smells`, no `securityIssues`).

- [ ] **Step 11.6: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/README.md
git -C C:/Cmm-testing/Claude_Training commit -m "docs(analysis): document --smells, --security, --analyze flags and rules"
```

---

## Self-Review Checklist (executed before handoff)

**1. Spec coverage:**

- CLI flags `--smells`, `--security`, `--analyze`, `--security-skip` → Task 9
- Severity buckets for smells (51/76/151, 5/7/9, 6/8/11) → Task 5 `Classify*` helpers
- Function categories (Method/Constructor/Destructor/Operator/ConversionOperator/LocalFunction; skip lambdas) → Task 5 `SmellWalker` overrides
- Auto-generated skip → Task 6 `IsAutoGenerated`
- Lines = `endLine - startLine + 1` → Task 5 `EnterFunction`
- Nesting rule (function body = depth 1, control-flow blocks +1, lambda body +1, local fn declaration +1) → Task 5 `VisitBlock` + `VisitLocalFunctionStatement`
- Secret patterns table → Task 2 `Patterns.SecretRules`
- Dangerous-function patterns by extension → Task 2 `Patterns.DangerousFunctionsByExt`
- `codescan:ignore` directive (`//`, `#`, `--`, `;`) → Task 2 `IgnoreDirective`
- Snippet redaction (first 4 + masked tail; full redact for private_key/connstr) → Task 3 `RedactSnippet`
- 1 MB file size limit for security scan → Task 7 `AnalyzerHost.SecurityFileSizeLimit`
- Glob skip for security → Task 7 `AnalyzerHost` Matcher integration
- Per-line/file/column dedup → Task 3/4 `seen` HashSets, Report dedupes via per-line+column dedup at scanner stage
- JSON: top-level `smells[]` + `securityIssues[]`, per-language summaries → Task 8 `Report.Serialize`
- Backwards-compat omission when flags off → Task 8, verified by Step 9.1 test
- Roslyn parse failure → ScanError, scan continues → Task 7 try/catch in `AnalyzerHost`
- Binary files skipped from analysis → Task 7 `if (entry.IsBinary) continue;`
- Error merge into `scanned.errors` → Task 8 `result.Errors.Concat(analysis.Errors)`

**2. Placeholder scan:** No "TBD"/"TODO"/"add tests for above" patterns. Every code-mutating step shows full code; every command states expected output.

**3. Type consistency:**

- `SmellFinding(Type, Severity, File, Name, StartLine, EndLine, Value, Threshold, Message)` defined Task 1, used Task 5/6/7/8.
- `SecurityFinding(Type, Subtype, Severity, File, Line, Column, Snippet, Message)` defined Task 1, used Task 3/4/7/8.
- `ISmellAnalyzer.Analyze(string filePath, string content)` defined Task 5, implemented Task 6, used Task 7.
- `ISecurityScanner.Scan(string filePath, string content)` defined Task 3, implemented Task 3+4, used Task 7.
- `AnalysisResult(Smells, SecurityFindings, Errors)` defined Task 7, used Task 8/9.
- `ScanOptions` extended with `Smells`, `Security`, `SecuritySkipGlobs` Task 1, consumed Task 7+9.
- `Report.Serialize(result, pretty)` (legacy) and `Report.Serialize(result, analysis, options, pretty)` (new) — Task 8 explicitly keeps the legacy overload to avoid breaking any direct call sites.
- `SmellWalker.LongFunctionThreshold/DeepNestingThreshold/LongParamListThreshold` constants defined Task 5, classification helpers in same file.
- `Patterns.SecretRules` (list) and `Patterns.DangerousFunctionsByExt` (dict) — defined Task 2, used Task 3/4.
- `IgnoreDirective.HasIgnore(string)` — defined Task 2, used Task 3/4.

No type drift detected.
