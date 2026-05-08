# Dark Dashboard (Phase 1) — Design

**Date:** 2026-05-08
**Status:** Approved (design phase)
**Owner:** vasu.kothapalli@moodys.com
**Builds on:** `2026-05-07-html-report-design.md` (shipped)
**Companion:** Phase 2 — AI Fix Suggestions (separate, not yet brainstormed)

## Goal

Replace the existing `--html` report's light theme + 3 charts with a professional dark dashboard: sticky header with a giant A–F grade, 4 summary KPI cards, three charts (severity donut, top-files-by-risk gradient bars, 5-axis radar), and a sortable/filterable issues table with click-to-expand rows. Add the small backend additions needed to power the new visuals (`qualityScore`, `grade`, `estimatedFixMinutes`, `fileRiskScores`, `totalFunctions`).

## Non-Goals

- **AI fix suggestions** — separate spec, Phase 2.
- **Scan history / sparkline time-series** — replaced with per-row severity stack mini-bars.
- **Cyclomatic complexity analyzer** — existing function-length / nesting / parameter-count smells are the complexity proxy.
- **Duplication detector / doc-coverage analyzer** — neither computed; not on the radar.
- **Light theme toggle** — single dark theme for Phase 1.
- **Custom themes / configurable palette.**
- **Report export to other formats** (PDF, image).

## Backend Additions

New top-level JSON fields, all additive (existing fields untouched):

```json
{
  "qualityScore": 67,
  "grade": "C",
  "estimatedFixMinutes": 505,
  "totalFunctions": 47,
  "fileRiskScores": [
    { "file": "src/CodeScanner/Scanner.cs", "riskScore": 38, "high": 1, "medium": 2, "low": 5, "lines": 298 },
    { "file": "src/CodeScanner/Cli.cs",     "riskScore": 27, "high": 1, "medium": 1, "low": 3, "lines": 191 },
    ...
  ]
}
```

### Formulas (v1, documented as tunable)

| Field | Formula |
|---|---|
| `qualityScore` | `clamp(100 - 5×high - 2×medium - 0.5×low, 0, 100)` summed across `smells[]` and `securityIssues[]` |
| `grade` | 90+ A · 80+ B · 70+ C · 60+ D · else F |
| `estimatedFixMinutes` | `30×high + 10×medium + 5×low` summed across `smells[]` and `securityIssues[]` |
| `totalFunctions` | Count of methods / constructors / destructors / operators / conversion operators / local functions visited by `SmellWalker` across all `.cs` files. Empty when `--smells` is off. |
| `fileRiskScores[i].riskScore` | `10×file.high + 4×file.medium + 1×file.low`. Only files with at least one finding are included. Sorted desc by `riskScore`. |

`fileRiskScores` carries severity counts per file plus the file's `lines` so the bar chart can render directly without back-references to other arrays.

`totalFunctions` is needed for the radar's 3 normalised axes (function length / nesting / parameter hygiene). When `--smells` is off, those three radar axes are reported as `null` and rendered as inner-ring (0) values with a note in the legend.

### Where these get computed

- `Report.Serialize` orchestrates: receives `ScanResult` + `AnalysisResult` + `ScanOptions`, computes the new fields from the existing arrays, embeds them.
- A new helper class `RiskScoring` holds the pure formulas (`ComputeQualityScore`, `GradeFor`, `EstimatedFixMinutes`, `BuildFileRiskScores`). Pure functions, easy to unit-test.
- `SmellWalker` gains a `TotalFunctions` counter exposed on a new `SmellAnalysis` result type, returned by `CSharpSmellAnalyzer.Analyze` alongside the findings list. Threaded back through `AnalyzerHost.AnalysisResult`.

## Layout

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ ┌────────────────────────────────────────────────────────────────────────┐ │
│ │  Code Scanner Report                              ┌────────────────┐   │ │  ← sticky header
│ │  root: ./hackathon · scanned 2026-… · --analyze   │       C        │   │ │     (large grade tile,
│ │                                                    │     67/100      │   │ │      color-keyed)
│ │                                                    └────────────────┘   │ │
│ └────────────────────────────────────────────────────────────────────────┘ │
│                                                                              │
│ ┌──Files──┐ ┌──Quality──┐ ┌──Critical──┐ ┌──Est Fix Time──┐                │
│ │   76    │ │    67     │ │      4      │ │    8h 25m      │                │  ← KPI strip
│ └─────────┘ └───────────┘ └─────────────┘ └────────────────┘                │
│                                                                              │
│ ┌──── Issues by severity ────┐ ┌─── Top files by risk ───┐ ┌── Quality ──┐  │
│ │      [donut chart]          │ │   [horizontal bars,      │ │  [radar]    │  │  ← charts row
│ │   high · medium · low       │ │    gradient red→green]   │ │  5 axes     │  │
│ └─────────────────────────────┘ └──────────────────────────┘ └─────────────┘  │
│                                                                              │
│ ┌────────────────────────────────────────────────────────────────────────┐ │
│ │  Files (sortable)               [All] [High·N] [Medium·N] [Low·N]      │ │  ← table panel
│ │  ──────────────────────────────────────────────────────────────────    │ │
│ │  File         Risk   Lines  Severity        Severity stack             │ │
│ │  Scanner.cs    38     298   ▲1 ●2 •5        █▆▂                       │ │
│ │  Cli.cs        27     191   ▲1 ●1 •3        █▆▂                       │ │
│ │  ...                                                                    │ │
│ └────────────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────────────┘
```

Container: `max-width: 1200px; padding: 24px;` Centred. Below 900px, the 3-chart row collapses to single column; below 700px, KPI cards become 2×2.

## Theme

| Token | Value | Use |
|---|---|---|
| `--bg` | `#0f1117` | Page background |
| `--panel` | `#181b25` | Card / panel surfaces |
| `--panel-hover` | `#1c2030` | Row hover, pill hover |
| `--border` | `#1d2030` | Card borders, table separators |
| `--text` | `#e7e9ee` | Primary text |
| `--text-dim` | `#cbd0dc` | Secondary text |
| `--muted` | `#6b7393` | Tertiary / metadata |
| `--high` | `#ef4444` | Critical / red |
| `--medium` | `#f59e0b` | Warning / orange |
| `--low` | `#10b981` | Good / green |
| `--blue` | `#3b82f6` | Files KPI accent |
| `--violet` | `#a855f7` | Quality KPI accent + radar shape |
| `--amber` | `#f59e0b` | Fix-time KPI accent |
| Grade colors | A green→teal · B lime · C/D/F red→orange | `grade-a` / `grade-b` / `grade-c` etc. |

Font: **Inter** from Google Fonts CDN (`https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap`). Numeric: `font-variant-numeric: tabular-nums` on metric cells.

Animations: 250ms cubic-bezier(0.4, 0, 0.2, 1). Skeleton fades over 300ms after `DOMContentLoaded`. Counter values count up from 0 over 600ms (Chart.js animations enabled). Hover transitions are 120ms.

## Charts (Chart.js v4)

Same CDN as v1 (`chart.umd.min.js@4.5.0`).

| ID | Type | Data |
|---|---|---|
| `chart-severity-donut` | `doughnut` | High / medium / low counts aggregated across `smells[]` + `securityIssues[]`; centre label is total |
| `chart-top-files-bar` | `bar` (`indexAxis: 'y'`) | Top 10 entries from `fileRiskScores`. Bar fill is a per-bar gradient red→green keyed to `riskScore` percentile. |
| `chart-quality-radar` | `radar` | 5 axes: Cleanliness, Security, Function Length, Nesting, Parameter Hygiene. Each 0–100. Filled polygon in violet. |

Per-row "severity stack" mini-bars are pure CSS (three coloured spans sized by count); not a Chart.js instance. This avoids spawning N Chart.js instances on tables with many rows.

### Radar axis formulas

| Axis | Formula | Notes |
|---|---|---|
| Cleanliness | `100 - clamp(50 × smells/totalFunctions, 0, 100)` | Lower smell density → higher score |
| Security | `100 - clamp(50 × securityIssues/totalFiles, 0, 100)` | |
| Function Length | `100 - clamp(100 × longFunctions/totalFunctions, 0, 100)` | |
| Nesting | `100 - clamp(100 × deepNestings/totalFunctions, 0, 100)` | |
| Parameter Hygiene | `100 - clamp(100 × longParamLists/totalFunctions, 0, 100)` | |

If `--smells` is off, the last three axes return `null` and the radar shows them at 0 with a "needs --smells" annotation. If `--security` is off, Security axis is `null`. The radar is hidden if all 5 axes are `null`.

## Interactions

| Trigger | Effect |
|---|---|
| Click donut segment (high/medium/low) | Smooth scroll to issues table; activate the matching pill filter |
| Click bar in top-files chart | Smooth scroll to that file's row; expand it |
| Click pill (`All` / `High` / `Medium` / `Low`) | Filter table rows; pill becomes active |
| Click table column header | Sort by that column (toggle asc/desc); arrow indicator |
| Click table row | Expand row to show inline list of that file's specific findings |
| Hover any chart | Chart.js native tooltip |
| Hover table row | Background brightens (`--panel-hover`) |
| Hover severity badge | Tooltip shows count + severity name |

Smooth scroll uses native `element.scrollIntoView({ behavior: 'smooth', block: 'start' })`.

Sort/filter state lives in plain JS variables on the bootstrap closure; no framework. Filter pills act as a multi-select OR (active pills are unioned). Default state: `All` active, all others inactive.

## Loading skeleton

Three skeleton blocks (KPI strip, charts row, table) shown immediately on parse. After `DOMContentLoaded`, set a `data-loaded="true"` attribute on `body`; CSS transitions the skeletons to `opacity: 0` over 300ms and the real content from `opacity: 0` to 1. Pure CSS — no JS animation library.

## Module Changes

```
src/CodeScanner/
├── Html/
│   ├── HtmlReport.cs        # unchanged signature; adds RiskScoring call before template render
│   ├── RiskScoring.cs       # NEW — pure helpers: ComputeQualityScore, GradeFor,
│   │                          EstimatedFixMinutes, BuildFileRiskScores
│   ├── Template.cs          # REWRITTEN — new dark HTML + CSS + bootstrap JS
│   └── TemplateRenderer.cs  # unchanged
├── Analyzers/
│   ├── SmellWalker.cs       # adds TotalFunctions counter
│   ├── CSharpSmellAnalyzer.cs # returns SmellAnalysis(findings, totalFunctions)
│   └── AnalyzerHost.cs      # AnalysisResult gains TotalFunctions field
├── Models.cs                # add SmellAnalysis record (or reuse AnalysisResult enrichment)
└── Report.cs                # writes the new top-level JSON fields
```

The HTML template grows from ~250 lines to ~500–600 lines (more CSS, more bootstrap JS). Stays as one const string — splitting into embedded resources is deferred until the file becomes painful to maintain.

## Backwards compatibility

- The CLI flag set is unchanged. `--html report.html` produces the new dashboard.
- The JSON output (`--output`) gains five new top-level fields: `qualityScore`, `grade`, `estimatedFixMinutes`, `totalFunctions`, `fileRiskScores`. All additive. Existing fields untouched.
- When `--smells` and `--security` are both off, `qualityScore` is `100`, `grade` is `A`, `estimatedFixMinutes` is `0`, `totalFunctions` is `0`, `fileRiskScores` is `[]`. The dashboard renders with skeleton-empty charts and a friendly "No analysis run — try --analyze" message in place of the radar.
- Programmatic JSON consumers continue to work; new fields are additions only.

## Edge Cases

| Case | Behavior |
|---|---|
| `--html` without `--analyze` | Quality score 100, grade A; KPI cards show 0 issues; donut shows "No findings"; radar hidden with a "Run with --analyze for quality dimensions" note; table shows file count only |
| `--smells` only | Radar shows 4 axes (Cleanliness, Function Length, Nesting, Params); Security axis hidden / dimmed |
| `--security` only | Radar shows 1 axis (Security); 4 smell-derived axes hidden / dimmed |
| Zero files scanned | All charts hidden; "No files scanned" message in place of charts row |
| Single file with one finding | All charts render; bar chart shows one bar |
| 100+ files with findings | Table renders all rows; sticky header stays put; per-row mini-bars don't degrade perf because they're CSS-only |
| `fileRiskScores` is empty (no findings) | Top-files chart hidden; donut shows 0; the section's heading states "No risk data" |
| `totalFunctions` is 0 with smells > 0 | Radar still computes — denominator falls back to 1 to avoid divide-by-zero; an annotation notes the data is unreliable. (This is a rare degenerate case.) |
| Path strings contain HTML metacharacters | Already escaped by existing `TemplateRenderer.HtmlEscape`; new fields piggyback on the same path |
| Embedded JSON contains `</script>` (real or escaped) | Default System.Text.Json encoding handles this (already verified in v1) |

## Testing Strategy

### Unit tests (new)

`tests/CodeScanner.Tests/Html/RiskScoringTests.cs`:
- `ComputeQualityScore_NoFindings_Is100`
- `ComputeQualityScore_OneHigh_Is95`
- `ComputeQualityScore_OneOfEach_Is92dot5`
- `ComputeQualityScore_FloorIsZero` (10+ highs)
- `GradeFor_AllBoundaries` (Theory: 89→B, 90→A, 70→C, 69→D, 0→F)
- `EstimatedFixMinutes_OneOfEach_Is45`
- `BuildFileRiskScores_SortsDescByRisk`
- `BuildFileRiskScores_OnlyIncludesFilesWithFindings`
- `BuildFileRiskScores_AggregatesPerFileSeverityCounts`

`tests/CodeScanner.Tests/Analyzers/CSharpSmellAnalyzerTests.cs` (extend existing):
- `Analyze_TotalFunctionsCountsAllFunctionDeclarations` (methods, ctors, dtors, ops, local fns, but NOT lambdas)
- `Analyze_EmptyFile_TotalFunctionsIsZero`

### Existing tests (extend)

`HtmlReportTests`:
- `Render_NoAnalysis_GradeIsAandQualityIs100`
- `Render_WithFindings_EmbedsQualityScoreAndGradeInJson`
- `Render_WithFindings_EmbedsFileRiskScoresArray`
- `Render_FileRiskScoresArraySortedDesc`
- `Render_DarkThemeMarker_PresentInHtml` — assert `data-theme="dark"` or equivalent root attribute is present

`ReportTests`:
- `Serialize_AddsQualityScoreGradeFixTimeFields_AsAdditive` — confirm existing keys all still present
- `Serialize_NoAnalysis_QualityScoreIs100AndGradeIsA`

`CliTests`:
- `Cli_HtmlFlag_AnalyzeAndOutput_HtmlContainsDashboardMarkup` — looks for `id="dashboard"` and `class="grade-tile"` (or whichever stable hooks the template uses)

### Manual verification

After implementation, generate a report against `hackathon/` with `--analyze --html`. Confirm:
1. Dark theme renders correctly in Chrome / Edge
2. All 4 KPI cards animate the count-up
3. Three charts render with correct data
4. Clicking a donut segment scrolls to and filters the table
5. Clicking a file row expands to show its findings
6. Severity-pill multi-select works as expected

No headless-browser tests in v1.

## Open Questions

None at design time — every choice pinned above.

## Out-of-Scope Future Work

- AI fix suggestions (Phase 2 — separate spec)
- Scan history persistence + true sparkline trends
- Cyclomatic complexity analyzer (Roslyn-feasible, deferred)
- Duplication detector
- Doc coverage analyzer
- Light-theme toggle, theme variants
- PDF / image export
- Per-finding "Suggest Fix" button (Phase 2)
- Live-refresh / `code-scanner watch` mode
- Custom thresholds configurable via flag
