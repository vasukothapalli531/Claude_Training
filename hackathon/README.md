# Code Scanner

A small .NET 9 CLI that recursively scans a directory and reports a JSON summary of files and lines, grouped by language. Optional opt-in passes detect C# code smells and basic security issues.

## Build

```powershell
dotnet build CodeScanner.sln
```

## Run (development)

```powershell
dotnet run --project src/CodeScanner -- <path> `
  [--output report.json] [--html report.html] [--pretty] `
  [--exclude name ...] [--follow-symlinks] [--verbose] `
  [--smells] [--security] [--analyze] [--security-skip glob ...]
```

`--analyze` is shorthand for `--smells --security`. `--html` writes a self-contained HTML dashboard (KPI tiles, language charts, severity bar, file breakdown table, expandable findings). `--output` and `--html` may be combined.

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

## HTML report

```powershell
dotnet run --project src/CodeScanner -- . --analyze --html report.html
start report.html  # opens in default browser
```

The generated `report.html` is a single self-contained dark-themed dashboard:

- **Sticky header** with overall **grade A–F** (large coloured tile) and quality score / 100.
- **4 KPI cards**: Total Files · Quality Score · Critical Issues · Estimated Fix Time.
- **Three charts** (Chart.js v4 from CDN, ~80 KB): severity donut, top files by risk (gradient bars), 5-axis quality radar (cleanliness, security, function length, nesting, parameter hygiene).
- **Sortable / filterable file table** with severity badges, per-row severity stack, and click-to-expand findings detail.

Inter font from Google Fonts. The report embeds the full scan JSON in `<script id="scan-data">`, so it renders the same dashboard whenever opened.

The JSON output (`--output`) gains five additive fields: `qualityScore`, `grade`, `estimatedFixMinutes`, `totalFunctions`, and `fileRiskScores`.

## AI fix suggestions (opt-in)

```powershell
$env:ANTHROPIC_API_KEY = "sk-ant-..."
dotnet run --project src/CodeScanner -- . --analyze --fix-suggestions --html report.html
```

When `--fix-suggestions` is set, the CLI calls the Anthropic API once per finding (smell or security), embeds the explanation + fixed snippet into the JSON, and the dashboard renders an inline "✨ Show fix" toggle in each expanded finding plus a top banner summarising the optimistic quality-score delta.

The HTML report stays a single shareable file — fixes are pre-baked at scan time, never requested from the browser.

| Flag | Default | Purpose |
|---|---|---|
| `--fix-suggestions` | off | Enable AI suggestions pass |
| `--ai-model <id>` | `claude-haiku-4-5` | Override model (`claude-sonnet-4-6` for higher quality) |
| `--ai-concurrency <n>` | `4` | Max parallel API calls |

Requires `ANTHROPIC_API_KEY` environment variable. Code snippets are sent to `api.anthropic.com`; secrets in security findings are sent in their already-redacted form.

Cost (informational): ~$0.20 per scan with Haiku, ~$0.60 with Sonnet for ~150 findings.

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
