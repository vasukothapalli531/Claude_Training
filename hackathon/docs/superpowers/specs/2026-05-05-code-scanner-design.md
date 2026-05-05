# Code Scanner — Design

**Date:** 2026-05-05
**Status:** Approved (design phase, .NET 9 / C#)
**Owner:** vasu.kothapalli@moodys.com

## Goal

A .NET 9 (C#) CLI that recursively scans a directory and emits a JSON summary of file counts and total lines of code, grouped by language. Production-ready: solution + project layout, xUnit tests, nullable enabled, warnings-as-errors.

## Non-Goals

- Comment/blank-line stripping (raw line count only — `wc -l` semantics)
- Per-language parsing or AST analysis
- Network calls, remote scanning, or git history awareness
- Anything beyond a single `scan` command (no subcommands)

## CLI Surface

```
code-scanner <path> [--output <file>] [--exclude <pattern> ...] [--follow-symlinks] [--pretty] [--verbose]
```

During development: `dotnet run --project src/CodeScanner -- <path> [flags]`.
After `dotnet pack` + tool install: `code-scanner <path>`.

| Flag | Default | Purpose |
|---|---|---|
| `<path>` (positional, required) | — | Directory to scan |
| `--output, -o` | stdout | Write JSON to this file instead of stdout |
| `--exclude, -e` | (none, additive) | Extra dir names to skip beyond defaults |
| `--follow-symlinks` | off | Symlinks ignored unless set |
| `--pretty` | off | Pretty-print JSON (2-space indent) |
| `--verbose, -v` | off | Enable INFO-level logging to stderr |

**Default skipped directories:** `.git`, `node_modules`, `__pycache__`, `.venv`, `venv`, `.pytest_cache`, `dist`, `build`, `.mypy_cache`, `.ruff_cache`, `bin`, `obj`.

**Dotfiles:** counted (e.g., `.eslintrc.js` is real code).

**Exit codes:**
- `0` — success (even with non-fatal per-file errors logged in output)
- `1` — path doesn't exist or isn't a directory
- `2` — unexpected error (uncaught exception at the top level)

## Output JSON Shape

```json
{
  "totalFiles": 142,
  "totalLines": 18374,
  "languages": {
    "C#":         { "files": 47, "lines": 8230, "extensions": [".cs"] },
    "TypeScript": { "files": 31, "lines": 5102, "extensions": [".ts", ".tsx"] },
    "Markdown":   { "files":  9, "lines":  812, "extensions": [".md"] },
    "Unknown":    { "files":  6, "lines":  402, "extensions": [".xyz", ""] }
  },
  "scanned": {
    "root": "C:/abs/path/to/scanned/dir",
    "skippedDirs": [".git", "node_modules"],
    "errors": [
      { "path": "broken/link.txt", "reason": "symlink loop" },
      { "path": "secrets.bin",     "reason": "permission denied" }
    ]
  }
}
```

**Rules:**
- Top key under `languages` is a **language label**, not a raw extension. Mapping is a static dictionary.
- Files with no extension or unmapped extensions go into `Unknown`; their actual extensions (including `""` for none) appear in `Unknown.extensions`.
- Each language entry reports both `files` and `lines`.
- `scanned.errors` is **always present** (empty array if none). Errors are non-fatal — scan continues.
- `scanned.skippedDirs` lists the directory **names** that were pruned during traversal (not full paths).
- Binary files: counted in `totalFiles` and per-language `files`, contribute `0` to `lines`, and appear in `errors` with reason `"binary file, lines not counted"`.
- JSON is serialized with `System.Text.Json`, `JsonNamingPolicy.CamelCase`, optional `WriteIndented=true` when `--pretty`.
- Path strings use forward slashes for cross-platform consistency in output (Windows backslashes are normalized).

## Traversal & Error Handling

**Walk algorithm:** `Directory.EnumerateFileSystemEntries(root, "*", SearchOption.TopDirectoryOnly)` recursively, with our own descent (so we can prune excluded dirs **before** entering them). `EnumerationOptions { IgnoreInaccessible = false, RecurseSubdirectories = false }` so we can handle errors per-level.

**Per-file flow:**

```csharp
foreach (var path in files):
  if (path is symlink && !followSymlinks): continue           // silent skip, not logged
  open FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)
  read first 8 KB into byte[] -> contains 0x00? mark binary
  if (binary):
    lines = 0; errors.Add(new ScanError(path, "binary file, lines not counted"))
  else:
    seek to 0, read in 64 KB buffers, count 0x0A bytes
    if file non-empty and last byte != 0x0A: lines += 1
  classify by Path.GetExtension(path) -> language label
  accumulate counters
```

**Edge cases:**

| Case | Behavior |
|---|---|
| Path doesn't exist | Exit 1, error to stderr, no JSON |
| Path is a file, not a dir | Exit 1, error to stderr |
| Path is empty dir | Exit 0, valid JSON with zeros |
| `UnauthorizedAccessException` on dir | Logged in `errors`, subtree skipped, scan continues |
| `UnauthorizedAccessException` on file | Logged in `errors`, file not counted |
| `IOException` on file (e.g., file in use) | Logged in `errors`, file not counted |
| Symlink to file (default) | Skipped silently, not logged |
| Symlink loop (`--follow-symlinks`) | Detected via visited-realpath set; logged in `errors` |
| File with no extension | Counted, language = `Unknown`, ext recorded as `""` |
| File with multiple dots (`foo.test.tsx`) | `Path.GetExtension` returns last suffix only (`.tsx` → TypeScript) |
| Binary file (null bytes in first 8 KB) | Counted, lines=0, logged in `errors` |
| Empty file | Counted, lines=0, no error |
| File with no trailing newline | Last partial line counted (1-byte file = 1 line) |
| Encoding | N/A — line counter operates on raw bytes (`0x0A`), never decodes text |
| Very large file (>100 MB) | Read in 64 KB buffers; never load full file into memory |
| Unexpected exception per file | Caught, logged in `errors` with exception type name, scan continues |
| `Ctrl-C` / `SIGINT` | Default behavior — process terminates; partial JSON not written |

**Logging:** `Console.Error.WriteLine` for warnings/errors. INFO-level messages only when `--verbose` is set. `stdout` is reserved for the JSON payload so it can be piped cleanly.

## Project Structure

```
hackathon/
├── CodeScanner.sln
├── README.md
├── .gitignore                                # bin/, obj/
├── src/
│   └── CodeScanner/
│       ├── CodeScanner.csproj                # net9.0, exec output, ToolCommandName=code-scanner
│       ├── Program.cs                        # entry point: invokes Cli.RunAsync
│       ├── Cli.cs                            # System.CommandLine setup, IO, exit codes
│       ├── Scanner.cs                        # walk + per-file processing (pure logic)
│       ├── Languages.cs                      # static ExtensionMap, Classify(ext)
│       ├── Report.cs                         # ScanResult -> JSON
│       └── Models.cs                         # records: ScanResult, FileEntry, ScanError
└── tests/
    └── CodeScanner.Tests/
        ├── CodeScanner.Tests.csproj          # xUnit + ProjectReference to src
        ├── ScannerTests.cs                   # walks, line counts, binary detect, errors
        ├── LanguagesTests.cs                 # ext mapping, unknown, multi-suffix
        ├── ReportTests.cs                    # JSON shape, language aggregation
        └── CliTests.cs                       # end-to-end via Process.Start
```

**Module boundaries:**
- `Scanner.cs` knows nothing about JSON or the CLI. Public API: `static ScanResult Scan(string root, ScanOptions options)`.
- `Report.cs` is the only place that knows the JSON shape. Public API: `static string Serialize(ScanResult result, bool pretty)`.
- `Cli.cs` is the only place that calls `Environment.Exit`, reads `Console.In`, or writes to `Console.Out`/`Console.Error`.
- `Models.cs` defines `record` types so equality comparisons in tests are free.

**Project file settings (`CodeScanner.csproj`):**
- `<TargetFramework>net9.0</TargetFramework>`
- `<Nullable>enable</Nullable>`
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- `<ImplicitUsings>enable</ImplicitUsings>`
- `<OutputType>Exe</OutputType>`
- `<PackAsTool>true</PackAsTool>`, `<ToolCommandName>code-scanner</ToolCommandName>` (enables `dotnet pack` + global install)

## Testing Strategy

- **Framework:** xUnit (project ref + `Microsoft.NET.Test.Sdk` + `xunit` + `xunit.runner.visualstudio`).
- **Approach:** every test builds a real fixture tree under `Path.GetTempPath()` per test (with `IDisposable` cleanup). No filesystem mocking.
- **Coverage target:** every row in the edge-cases table has ≥1 test.
- **End-to-end:** `CliTests.cs` invokes the built binary via `Process.Start("dotnet", "run --project ../../../../src/CodeScanner -- <fixture>")` and asserts JSON shape + exit code. Alternative: build once via fixture and exec the binary directly.
- **Verification command:** `dotnet test` from solution root must report all green; `dotnet build /warnaserror` must succeed.

## Open Questions

None at design time — all major choices pinned above.

## Out-of-Scope Future Work

- `--strict-loc` flag for non-blank, non-comment LOC counting (would require per-language comment maps).
- `--max-file-size` cap.
- Glob-pattern excludes (today's `--exclude` matches dir names only).
- `.gitignore` awareness.
- AOT / single-file publishing.
