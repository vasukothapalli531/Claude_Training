# HTML Report Generator — Design

**Date:** 2026-05-07
**Status:** Approved (design phase)
**Owner:** vasu.kothapalli@moodys.com
**Builds on:** `2026-05-05-code-scanner-design.md`, `2026-05-05-code-analysis-design.md` (both shipped)

## Goal

Add a `--html` flag to the existing Code Scanner CLI that produces a single self-contained HTML report. The report embeds the scan JSON, loads Chart.js from a CDN, and renders a professional dashboard: KPI tiles, two file/line charts, a severity bar, and a per-language breakdown table. When `--analyze` data is present, an expandable findings list appears.

## Non-Goals

- Real 3D charts (Chart.js doesn't do them; a "professional" report doesn't need them)
- Dark mode toggle (single light theme for v1)
- Multi-scan comparison or trend charts
- Headless-browser end-to-end tests (no Playwright/Puppeteer dependency)
- Server-side rendering, hosted dashboards, live refresh
- Custom themes / configurable colors

## CLI Surface (additions)

| Flag | Default | Purpose |
|---|---|---|
| `--html <file>` | (none) | When set, write a self-contained HTML report to this path. Compatible with every other flag, including `--analyze`. |

`--html` and `--output` are independent. Running with both produces both files in one invocation. Running with neither preserves the v1 default behavior (JSON to stdout).

When the path's directory doesn't exist, the CLI exits 2 with `error: cannot write HTML report: <reason>`.

## Output Structure (single HTML file)

```
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>Code Scanner Report — {{rootPath}}</title>
  <style>/* hand-rolled, ~2 KB */</style>
</head>
<body>
  <header>
    <h1>Code Scanner Report</h1>
    <p>root: <code>{{rootPath}}</code> · scanned {{timestamp}} · {{flagsLine}}</p>
  </header>

  <section id="kpis">  <!-- 4 tiles: files, lines, languages, issues --> </section>
  <section id="charts-row">
    <div class="panel"><h3>Lines by language</h3><canvas id="chart-lines-donut"></canvas></div>
    <div class="panel"><h3>Files by language</h3><canvas id="chart-files-bar"></canvas></div>
  </section>
  <section id="severity">
    <h3>Findings by severity</h3>
    <canvas id="chart-severity-bar"></canvas>
  </section>
  <section id="file-breakdown">
    <h3>File breakdown</h3>
    <table>...</table>
  </section>
  <section id="findings-detail" hidden>  <!-- only shown when smells[] or securityIssues[] exist -->
    <details>...</details>
  </section>

  <script type="application/json" id="scan-data">{{dataJson}}</script>
  <script src="https://cdn.jsdelivr.net/npm/chart.js@4.5.0/dist/chart.umd.min.js"></script>
  <script>/* small bootstrap script reads #scan-data and renders */</script>
</body>
</html>
```

Sections in order:

1. **Header** — title, scan root, ISO-8601 timestamp, the active flags (e.g., `--analyze --pretty`).
2. **KPI strip** — 4 tiles: `Files`, `Lines`, `Languages`, `Issues`. `Issues = smells.length + securityIssues.length`. Issues tile shows `0` (in muted color) when no analysis data.
3. **Charts row** — two side-by-side panels: donut for lines-by-language, horizontal bar for files-by-language.
4. **Severity bar** — single full-width stacked horizontal bar with three segments: low (green), medium (amber), high (red). Counts pulled from both `smells` and `securityIssues`. Hidden when both arrays absent.
5. **File breakdown table** — one row per language: language name, files count, lines count, extensions list, smells severity badges (`high · medium · low`), security severity badges. Sorted by lines desc.
6. **Findings detail** — collapsed `<details>` list. One entry per finding from `smells[]` and `securityIssues[]`, file → line → message → redacted snippet. Hidden when both arrays empty/absent.

## Charts (Chart.js v4)

| ID | Type | Data |
|---|---|---|
| `chart-lines-donut`     | `doughnut` | `languages.<lang>.lines` per language; center label is `totalLines` |
| `chart-files-bar`       | `bar` (`indexAxis: 'y'`) | `languages.<lang>.files` per language; sorted desc |
| `chart-severity-bar`    | `bar` (`stacked: true`, `indexAxis: 'y'`) | counts of `low`/`medium`/`high` aggregated across smells + securityIssues |

Chart palette (matches mockup):

| Color | Use |
|---|---|
| `#2563eb` | language A / files |
| `#7c3aed` | language B |
| `#059669` | language C / low severity |
| `#94a3b8` | language D / "Other" |
| `#f59e0b` | medium severity |
| `#dc2626` | high severity |

When more than 4 languages are present, the 4 largest by lines get distinct colors; the rest collapse to "Other" (`#94a3b8`).

## Libraries (CDN-loaded)

- **Chart.js v4** — `https://cdn.jsdelivr.net/npm/chart.js@4.5.0/dist/chart.umd.min.js`. Single `<script src>`. The version is pinned in the spec; bumps require a code change.
- **No CSS framework.** Inline `<style>` (~2 KB) styled to the mockup.
- **No font CDN.** System font stack: `-apple-system, "Segoe UI", system-ui, sans-serif`.
- **Total over the wire** on first view: ~80 KB.

## Data Embedding

The full scanner JSON is embedded as:

```html
<script type="application/json" id="scan-data">{{dataJson}}</script>
```

`{{dataJson}}` is the same string that `Report.Serialize(...)` produces in compact form (no pretty-printing — pretty layout in HTML adds noise without value). The JSON is HTML-escaped only for `<` (replaced with `<`) to prevent any embedded `</script>` from terminating the script tag prematurely.

Bootstrap script reads `JSON.parse(document.getElementById('scan-data').textContent)`.

## Module Layout

```
src/CodeScanner/
├── Html/
│   ├── HtmlReport.cs          # public API: static string Render(ScanResult, AnalysisResult, ScanOptions, DateTimeOffset)
│   ├── Template.cs            # const string with {{title}}, {{rootPath}}, {{timestamp}}, {{flagsLine}}, {{dataJson}}
│   └── TemplateRenderer.cs    # internal: ApplyPlaceholders(string template, IReadOnlyDictionary<string,string>) using string.Replace
└── Cli.cs                     # add --html option, call HtmlReport.Render, write to file
```

**Module boundaries:**
- `HtmlReport.Render` is pure: it takes the data and returns a string. No file IO, no exceptions for valid input.
- `Template` is just the const string and the placeholder list. No logic.
- `TemplateRenderer` does naive `string.Replace` for each placeholder. Order is fixed; placeholders never appear inside data values because all data placeholders are JSON/HTML-escaped before substitution.
- `Cli` is the only place that opens a file and calls `Environment.Exit`.

**No new NuGet dependencies.** Pure stdlib + the existing `System.Text.Json`.

## Edge Cases

| Case | Behavior |
|---|---|
| No `--html` flag | Unchanged behavior — JSON to stdout or `--output` file |
| `--html <file>` with no analysis flags | HTML rendered with empty `smells[]` / `securityIssues[]`; severity panel and findings detail hidden |
| Output path's directory doesn't exist | Exit 2; stderr message; no JSON written either |
| Output path is read-only / IOException | Exit 2; stderr message |
| `--html` plus `--output` | Both files produced; HTML to `--html` path, JSON to `--output` path |
| Empty scan (zero files) | Charts render with empty datasets; KPI tiles show 0; table shows "No files scanned" row |
| Single language | Donut shows one full slice; bar shows single bar |
| 5+ languages | Top 4 by lines get colors; remainder bucketed as "Other" |
| Findings exceed 1,000 entries | All rendered; table within `<details>` is virtualized via CSS `max-height: 50vh; overflow-y: auto` (no JS virtualization) |
| Embedded `</script>` in a finding's snippet | `<` is encoded as `<` in the JSON, preventing premature script termination |
| User opens HTML without internet | Layout, KPIs, table render correctly; charts show "Loading…" canvases that never resolve. Acceptable v1 limitation. |
| Path contains characters that need HTML escaping (`&`, `<`, `>`) | Each text-substituted placeholder is HTML-escaped via `WebUtility.HtmlEncode` before insertion |

## Testing Strategy

**Unit tests** (`tests/CodeScanner.Tests/Html/HtmlReportTests.cs`):
- `Render_EmptyResult_ProducesValidHtmlSkeleton` — `<!DOCTYPE>`, `<html>`, `<script id="scan-data">` present.
- `Render_EmbedsJsonAsScriptTag` — extracted text equals `Report.Serialize(...)` non-pretty.
- `Render_NoAnalysisData_HidesFindingsAndSeverity` — `<section id="findings-detail" hidden>` present, severity section hidden.
- `Render_WithAnalysisData_ShowsFindingsSection` — `hidden` attribute absent on detail section.
- `Render_HtmlEscapesRootPath` — paths with `<` / `&` are escaped in header.
- `Render_EscapesScriptTagsInJsonPayload` — `</script>` inside a finding snippet appears as `</script>` in output.
- `Render_TopFourLanguages_RestBucketed` — 6-language input produces 4 named buckets + "Other".

**CLI end-to-end** (extends `tests/CodeScanner.Tests/CliTests.cs`):
- `Cli_HtmlFlag_WritesFile` — runs CLI with `--html out.html`, asserts file exists and starts with `<!DOCTYPE`.
- `Cli_HtmlFlag_WithAnalyze_EmbedsFindings` — runs with `--html out.html --analyze` against the smelly fixture, parses the embedded JSON, asserts `smells.length > 0`.
- `Cli_HtmlFlag_AndJsonOutput_BothProduced` — runs with `--html h.html --output j.json`, asserts both exist and the embedded JSON in HTML equals contents of the JSON file.
- `Cli_HtmlFlag_BadDirectory_ExitsTwo` — runs with `--html /nonexistent/dir/r.html`, asserts exit 2 and stderr message.

No headless-browser tests in v1 — visual correctness is verified manually after first generation.

## Open Questions

None at design time — all major choices pinned above.

## Out-of-Scope Future Work

- Dark theme toggle
- Multi-scan / trend dashboard (compare JSON files over time)
- Self-contained Chart.js (no CDN dependency) — would 3-4× the file size
- Bundled CSS variables for theming
- Per-finding deep-link to the matching file path on disk / git host
- SARIF export
- Live-reload watch mode (`code-scanner watch`)
