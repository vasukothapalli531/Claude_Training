# AI Fix Suggestions (Phase 2) — Design

**Date:** 2026-05-08
**Status:** Approved (design phase)
**Owner:** vasu.kothapalli@moodys.com
**Builds on:** `2026-05-07-html-report-design.md`, `2026-05-08-dark-dashboard-design.md` (both shipped)

## Goal

Add an opt-in `--fix-suggestions` flag to the existing CLI that, after the analysis pass completes, calls the Anthropic API once per finding (smell or security) at scan time and embeds the AI-generated explanation + fixed snippet into the JSON. The dark dashboard renders these suggestions inline in each expanded finding row, plus a header banner summarising the optimistic quality-score delta if every suggestion were applied.

The integration is **pre-baked at scan time** — the browser never calls the API. The `report.html` remains a single self-contained shareable artifact.

## Non-Goals

- **Runtime "Suggest Fix" button** that fires API calls from the browser. Pre-baked design intentionally precludes this. (See spec §"Why pre-bake".)
- **Auto-applying fixes** to the source. The CLI suggests; humans apply. No file mutation.
- **Continuous / live regeneration.** Each scan is a snapshot.
- **Local LLM fallback** (Ollama, llama.cpp, etc.).
- **Multi-provider abstraction** (OpenAI, Gemini). Anthropic only.
- **Persistent suggestion cache** between scan runs. Each `--fix-suggestions` run hits the API for every finding. (Future work.)

## Why pre-bake (vs. browser fetch / local proxy)

| | API key location | Online to view? | Live "Fix All"? | Privacy surface |
|---|---|---|---|---|
| Browser fetch | sessionStorage in HTML | Yes | Yes | Code → API from each viewer |
| Local proxy | CLI process env, served on localhost | No | Yes | Code → API from CLI host only |
| **Pre-bake** | CLI env at scan time | No | No (re-run scan instead) | Code → API from CLI host at scan time |

Pre-bake wins on the three properties this project values most:
1. **Reproducible artifact.** A `report.html` you check in or email shows the same fixes whenever opened.
2. **No browser-side API calls.** No CORS, no key leak, no `anthropic-dangerous-direct-browser-access` exposure.
3. **Bounded predictable cost.** One call per finding per scan, not per click per viewer.

The "Fix All" button from the original ask is replaced by an aggregate banner showing the optimistic score delta if every suggestion were applied.

## CLI Surface

| Flag | Default | Purpose |
|---|---|---|
| `--fix-suggestions` | off | Enable AI suggestions pass after analysis |
| `--ai-model <id>` | `claude-haiku-4-5` | Override model. `claude-sonnet-4-6` for higher quality at ~3× cost |
| `--ai-concurrency <n>` | `4` | Max parallel API calls |

**Authentication:** `$ANTHROPIC_API_KEY` environment variable. CLI exits 1 with a clear stderr message if `--fix-suggestions` is set and the env var is missing or empty.

**Privacy notice (always shown when enabled):** On startup, when `--fix-suggestions` is set, the CLI prints to stderr:

```
info: --fix-suggestions enabled — code snippets will be sent to api.anthropic.com (model: claude-haiku-4-5)
```

This appears regardless of `--verbose`.

**Skip rules:** Findings whose file path matches an entry in `--security-skip` are also skipped from AI suggestions. Findings inside files larger than 1 MB (the existing security-scan size limit) never reach the analysis pass and so are never sent.

## Per-Finding API Call

### Endpoint

`POST https://api.anthropic.com/v1/messages` with headers:
- `x-api-key: <env>`
- `anthropic-version: 2023-06-01`
- `content-type: application/json`

### Request body

```json
{
  "model": "claude-haiku-4-5",
  "max_tokens": 600,
  "system": "You are a senior code reviewer. For the given finding, propose a minimal fix. Reply with strict JSON only — no prose, no code fences. Schema: {\"explanation\": string, \"fixedSnippet\": string}.",
  "messages": [
    {
      "role": "user",
      "content": "Finding: long_function (medium)\nFile: src/CodeScanner/Scanner.cs\nMessage: Function 'Scan' is 82 lines (threshold: 50)\n\nContext:\n```\n<windowed source>\n```\n\nReturn the JSON now."
    }
  ]
}
```

### Snippet windowing

| Finding type | Window |
|---|---|
| Smells (long_function, deep_nesting, long_parameter_list) | Function span (lines `startLine` through `endLine`), capped at 100 lines. If span > 100 lines, take first 50 + last 50 with a `// ... <N lines elided> ...` marker between. |
| Security (hardcoded_secret, dangerous_function) | The offending line ±3 lines, capped at 7 lines total. The line content is the **already-redacted snippet** stored on the finding — raw secrets are never sent. |

Lines are read from the on-disk file at scan time. If the file moved or changed between analysis and AI pass, the windowing is best-effort; on read failure, the call is skipped.

### Response parsing

Anthropic returns `{ "content": [{"type": "text", "text": "<json string>"}] }`. We extract the first `text` block and `JsonDocument.Parse` it. If the model wrapped the JSON in code fences (` ```json … ``` `), strip the fences first.

The expected payload is:

```json
{ "explanation": "<why this fix>", "fixedSnippet": "<the proposed code>" }
```

Missing keys → log "parse error: missing field <key>" + skip. Malformed JSON → log + skip.

## Output JSON (additive)

Each finding gains an optional `aiSuggestion` field:

```json
{
  "type": "long_function",
  "severity": "medium",
  "file": "src/CodeScanner/Scanner.cs",
  "name": "Scan",
  "startLine": 53, "endLine": 134,
  "value": 82, "threshold": 50,
  "message": "Function 'Scan' is 82 lines (threshold: 50)",
  "aiSuggestion": {
    "explanation": "Extract per-file processing into a private ProcessEntry method.",
    "fixedSnippet": "private void ProcessEntry(...) { ... }",
    "model": "claude-haiku-4-5",
    "elapsedMs": 1820
  }
}
```

The field is **only present when the call succeeded**. Failures append an entry to `scanned.errors`:

```json
{ "path": "src/X.cs:42", "reason": "AI suggestion failed: timeout" }
```

A new top-level `aiSummary` field summarises the run:

```json
"aiSummary": {
  "model": "claude-haiku-4-5",
  "totalCalls": 173,
  "successful": 168,
  "failed": 5,
  "totalElapsedMs": 41200,
  "qualityScoreIfAllFixed": 95,
  "qualityScoreDelta": 28
}
```

`aiSummary` appears only when `--fix-suggestions` was set, regardless of whether any individual call succeeded.

`qualityScoreIfAllFixed` and `qualityScoreDelta` are computed by removing **only** the findings that received a non-empty `aiSuggestion` from the score formula and recomputing. Findings with no suggestion (failed or skipped) still count against the score. Both fields are explicitly labelled "optimistic" in the dashboard.

## Dashboard Additions

The dark dashboard from Phase 1 gains three small additions, all conditional on `aiSummary` being present in the embedded JSON:

1. **Header banner** below the sticky header, above the KPI strip:

   > ✨ **168 AI fixes available** · +28 points if applied · model: `claude-haiku-4-5`

   Pure CSS / static text, no JS state.

2. **Inline suggestion in expanded findings.** When a finding row is expanded (existing click-to-expand behaviour), each finding `<li>` shows below its meta + message:
   - A "Show fix" toggle button.
   - When toggled open: `explanation` paragraph + `<pre>` containing `fixedSnippet` (textContent only, no innerHTML).

3. **Per-row badge** in the file table: a small ✨ glyph next to the severity badges if any of the file's findings has a suggestion. Hover tooltip: "N AI fixes available."

No "Fix All" button. The banner makes the aggregate visible without implying interaction.

## Module Layout

```
src/CodeScanner/
├── AI/
│   ├── IClaudeClient.cs            # interface: Task<string> SendAsync(string body, CancellationToken)
│   ├── AnthropicClient.cs          # HttpClient impl; reads ANTHROPIC_API_KEY at construction
│   ├── PromptBuilder.cs            # static: BuildSystem(), BuildUserContent(finding, sourcePath)
│   ├── SuggestionParser.cs         # static: TryParse(responseText, out AiSuggestion?)
│   ├── FixSuggestionService.cs     # orchestrator: GenerateAsync(findings, options) → updated findings
│   └── AiSuggestion.cs             # record: Explanation, FixedSnippet, Model, ElapsedMs
└── Cli.cs                          # add --fix-suggestions, --ai-model, --ai-concurrency
```

**Module boundaries:**

- `IClaudeClient.SendAsync(string body, CancellationToken) → Task<string>` is the only network surface. Returns the raw response body.
- `PromptBuilder` knows about findings and source files. Pure functions; no I/O of its own (takes `string sourceContent` as input).
- `SuggestionParser` knows the response shape. Pure parsing.
- `FixSuggestionService` is the only orchestrator: bounded-concurrency `SemaphoreSlim`, retries, error capture into a list of `ScanError`. Returns enriched findings + `AiSummary`.
- `Cli.cs` calls `FixSuggestionService.GenerateAsync(...)` after `AnalyzerHost.Analyze(...)` returns, when `--fix-suggestions` is set.

**Models extension** in `Models.cs`:

```csharp
public sealed record AiSuggestion(
    string Explanation,
    string FixedSnippet,
    string Model,
    long ElapsedMs);

public sealed record AiSummary(
    string Model,
    int TotalCalls,
    int Successful,
    int Failed,
    long TotalElapsedMs,
    int QualityScoreIfAllFixed,
    int QualityScoreDelta);
```

`SmellFinding` and `SecurityFinding` gain an optional `AiSuggestion?` parameter (default null). `AnalysisResult` gains an `AiSummary?` field.

`Report.Serialize` emits the new fields and the new top-level `aiSummary` entry.

## Failure Handling

| Case | Behavior |
|---|---|
| `--fix-suggestions` without `$ANTHROPIC_API_KEY` set | Exit 1, stderr `"error: --fix-suggestions requires ANTHROPIC_API_KEY"`. No JSON written. |
| HTTP 401 / 403 | Exit 1, stderr `"error: invalid ANTHROPIC_API_KEY"`. No fallback to "skip and continue" — credentials problems are fatal. |
| HTTP 429 with `Retry-After` header | Wait the header value (capped at 30s), retry once. If second attempt also 429, skip that finding. |
| HTTP 5xx | Retry once after 1s with jitter. If second attempt fails, skip. |
| Network timeout (30s default per request) | Skip. |
| Response is malformed JSON (after fence stripping) | Skip with reason `"parse error"`. |
| Source file unreadable when building prompt context | Skip with reason `"source unavailable"`. |
| `--ai-concurrency 0` | Hard-disable: returns empty `AiSummary` with `totalCalls: 0`. Useful for offline runs and tests. |

All skips append a `ScanError` to `scanned.errors`. The scan itself never fails because of AI errors.

## Concurrency

`FixSuggestionService.GenerateAsync` uses a `SemaphoreSlim(initialCount: aiConcurrency)`. Each finding's call is a separate `Task` awaiting the semaphore. `Task.WhenAll` coordinates completion. Each task's result (`AiSuggestion?` + optional error) is collected into per-finding slots; ordering preserved.

Default 4 is conservative for the standard Anthropic rate limits and won't exhaust HttpClient sockets.

## Testing Strategy

### Unit tests (new)

`tests/CodeScanner.Tests/AI/PromptBuilderTests.cs`:
- `BuildUserContent_LongFunction_IncludesFunctionSpan`
- `BuildUserContent_DeepNesting_TakesContextAroundDeepestLine`
- `BuildUserContent_LongFunction_OverHundredLines_ElidesMiddle`
- `BuildUserContent_SecurityFinding_TakesPlusMinus3Lines`
- `BuildUserContent_RedactsRawSecrets` — assert no `AKIA` literal slips through

`tests/CodeScanner.Tests/AI/SuggestionParserTests.cs`:
- `TryParse_ValidJson_Succeeds`
- `TryParse_CodeFencedJson_StripsFencesAndSucceeds`
- `TryParse_InvalidJson_ReturnsFalse`
- `TryParse_MissingExplanation_ReturnsFalse`
- `TryParse_MissingFixedSnippet_ReturnsFalse`

`tests/CodeScanner.Tests/AI/FixSuggestionServiceTests.cs`:
- `GenerateAsync_NoFindings_EmptyResult`
- `GenerateAsync_AllSuccess_AllFindingsHaveSuggestions`
- `GenerateAsync_OneFails_OthersStillSucceed`
- `GenerateAsync_RespectsConcurrencyLimit` — fake client checks max in-flight via interlocked counter
- `GenerateAsync_AiConcurrencyZero_NoCallsMade`
- `GenerateAsync_QualityDeltaReflectsSuggestionsOnly` — failed findings still count against the optimistic score

All tests inject a fake `IClaudeClient` returning canned responses or throwing.

### Integration tests

`CliTests`:
- `Cli_FixSuggestions_NoApiKey_ExitsOne`
- `Cli_FixSuggestions_WithStubClient_EmbedsAiSuggestionsInJson` — uses an env-var hook the CLI honours in test mode (`CODESCANNER_TEST_AI_STUB=1`) to swap in the canned client. Document this hook in code.

### Live API tests

**None.** No test invokes the real Anthropic API. Live verification is manual smoke after implementation.

## Cost Estimate (informational)

Approximate, based on typical scans of mid-sized repos (~150 findings). Each call: ~250 input tokens, ~250 output tokens.

| Model | $/M input | $/M output | Per call | 150 findings |
|---|---|---|---|---|
| claude-haiku-4-5 | ~$1 | ~$5 | ~$0.0014 | ~$0.21 |
| claude-sonnet-4-6 | ~$3 | ~$15 | ~$0.0042 | ~$0.63 |

Default Haiku is cheap enough that interactive iteration is comfortable. Sonnet override is a few cents more per scan for higher-quality fixes.

## Edge Cases

| Case | Behavior |
|---|---|
| `--fix-suggestions` with no findings | `aiSummary` present with `totalCalls: 0`; no API call made; exit 0 |
| `--fix-suggestions` without `--analyze` (no smells, no security) | Equivalent to "no findings"; exits 0 with empty summary |
| Snippet contains the literal sequence ` ``` ` | Sent as-is; the model is instructed to ignore code fences in input |
| Response missing `content` array | Skip with `"empty response"` |
| Response `content[0].type != "text"` | Skip with `"unexpected response shape"` |
| Same finding contributes both a smell and a security match (different scanners) | Two separate calls, two `aiSuggestion` fields |
| User Ctrl-Cs mid-scan | Cancellation token propagates; partial results discarded; no JSON written |
| Snippet length pushes prompt over the model context window | Trim window further; if still over, skip with `"context window exceeded"` |

## Open Questions

None at design time — all major choices pinned above.

## Out-of-Scope Future Work

- Persistent cache across scan runs (hash by file + line + finding type + snippet)
- Browser-side regeneration (the "interactive Fix All" the original ask requested — re-evaluate after seeing pre-bake in production)
- Auto-applying suggestions back to source files
- Multi-provider support (OpenAI, Gemini) behind a strategy interface
- Local LLM fallback for offline / privacy-sensitive runs
- Per-finding cost reporting in `aiSummary`
- A "diff view" widget rendering original vs `fixedSnippet` side-by-side (current design just shows the suggestion as a code block)
- Streaming responses (current design is non-streaming)
