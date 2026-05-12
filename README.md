# Claude_Training

Workspace for Claude Code experiments. The main project lives in [`hackathon/`](./hackathon/README.md).

## hackathon/ — Code Scanner

A .NET 9 CLI that scans a directory and produces a JSON summary plus a single-file HTML dashboard. Optional opt-in passes detect C# code smells and basic security issues, with optional AI-generated fix suggestions.

### What's built

- **Scanner core** — recursive directory walker, binary-file detection, raw line counting, extension-to-language classifier, per-language aggregation, exit codes.
- **Analyzers (`--analyze`)**
  - C# code smells via Roslyn: long functions, deep nesting, long parameter lists.
  - Security passes: regex-based secret detection (AWS keys, GitHub PATs, Slack tokens, JWTs, private keys, connection-string passwords) and dangerous-function detection (`eval`, `new Function`, `Invoke-Expression`, `Assembly.Load`, etc.). Snippets are redacted in output.
  - `// codescan:ignore` line-level suppression.
- **HTML dashboard (`--html report.html`)**
  - Self-contained dark-themed report with sticky grade tile (A–F), 4 KPI cards, Chart.js donut/bar/radar visuals, and a sortable/filterable file table with expandable findings.
  - Adds `qualityScore`, `grade`, `estimatedFixMinutes`, `totalFunctions`, `fileRiskScores` to the JSON output.
- **AI fix suggestions (`--fix-suggestions`)**
  - Calls the Anthropic API once per finding, bakes the explanation + fixed snippet into the JSON, and renders an inline "✨ Show fix" toggle in the HTML report.
  - Configurable model (`--ai-model`) and parallelism (`--ai-concurrency`); requires `ANTHROPIC_API_KEY`.
- **Packaging** — installable as a global `dotnet` tool (`code-scanner`).

See [`hackathon/README.md`](./hackathon/README.md) for build/run/test commands, flag reference, output schema, and rule thresholds.

### Quick start

```powershell
cd hackathon
dotnet run --project src/CodeScanner -- . --analyze --html report.html
start report.html
```
