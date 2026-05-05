# Code Scanner — Design

**Date:** 2026-05-05
**Status:** Approved (design phase)
**Owner:** vasu.kothapalli@moodys.com

## Goal

A Python CLI that recursively scans a directory and emits a JSON summary of file counts and total lines of code, grouped by language. Production-ready: package layout, tests, type hints, lint config.

## Non-Goals

- Comment/blank-line stripping (raw line count only — `wc -l` semantics)
- Per-language parsing or AST analysis
- Network calls, remote scanning, or git history awareness
- Anything beyond a single `scan` command (no subcommands)

## CLI Surface

```
code-scanner <path> [--output <file>] [--exclude <pattern> ...] [--follow-symlinks] [--pretty] [--verbose]
```

| Flag | Default | Purpose |
|---|---|---|
| `<path>` (positional, required) | — | Directory to scan |
| `--output, -o` | stdout | Write JSON to this file instead of stdout |
| `--exclude, -e` | (none, additive) | Extra dir names to skip beyond defaults |
| `--follow-symlinks` | off | Symlinks ignored unless set |
| `--pretty` | off | Pretty-print JSON (2-space indent) |
| `--verbose, -v` | off | Enable INFO-level logging to stderr |

**Default skipped directories:** `.git`, `node_modules`, `__pycache__`, `.venv`, `venv`, `.pytest_cache`, `dist`, `build`, `.mypy_cache`, `.ruff_cache`.

**Dotfiles:** counted (e.g., `.eslintrc.js` is real code).

**Exit codes:**
- `0` — success (even with non-fatal per-file errors logged in output)
- `1` — path doesn't exist or isn't a directory
- `2` — unexpected error (e.g., out of memory, OS-level failure unrelated to a single file)

## Output JSON Shape

```json
{
  "totalFiles": 142,
  "totalLines": 18374,
  "languages": {
    "Python":     { "files": 47, "lines": 8230, "extensions": [".py"] },
    "TypeScript": { "files": 31, "lines": 5102, "extensions": [".ts", ".tsx"] },
    "Markdown":   { "files":  9, "lines":  812, "extensions": [".md"] },
    "Unknown":    { "files":  6, "lines":  402, "extensions": [".xyz", ""] }
  },
  "scanned": {
    "root": "/abs/path/to/scanned/dir",
    "skippedDirs": [".git", "node_modules"],
    "errors": [
      { "path": "broken/link.txt", "reason": "symlink loop" },
      { "path": "secrets.bin",     "reason": "permission denied" }
    ]
  }
}
```

**Rules:**
- Top key under `languages` is a **language label**, not a raw extension. Mapping is a static dict.
- Files with no extension or unmapped extensions go into `Unknown`; their actual extensions (including `""` for none) appear in `Unknown.extensions`.
- Each language entry reports both `files` and `lines`.
- `scanned.errors` is **always present** (empty array if none). Errors are non-fatal — scan continues.
- `scanned.skippedDirs` lists the directory **names** that were pruned during traversal (not full paths).
- Binary files: counted in `totalFiles` and per-language `files`, contribute `0` to `lines`, and appear in `errors` with reason `"binary file, lines not counted"`.
- Encoding: read text as UTF-8 with `errors="replace"`. No file fails the scan due to encoding; line count is accurate (we count `\n` bytes).

## Traversal & Error Handling

**Walk algorithm:** `os.walk(top, followlinks=False)` by default, with in-place pruning of `dirnames` to skip excluded dirs *before* descent (avoids reading huge `node_modules` trees at all).

**Per-file flow:**

```
for each file:
  resolve absolute path
  if symlink and not --follow-symlinks: skip silently (not logged)
  open(rb) -> read first 8 KB -> contains null byte? -> mark binary
  if binary:
    lines = 0; append errors entry "binary file, lines not counted"
  else:
    open(rt, encoding="utf-8", errors="replace") in 64 KB chunks
    lines = count of "\n" + (1 if file non-empty and last byte != "\n" else 0)
  classify by last extension -> language label
  accumulate counters
```

**Edge cases:**

| Case | Behavior |
|---|---|
| Path doesn't exist | Exit 1, error to stderr, no JSON |
| Path is a file, not a dir | Exit 1, error to stderr |
| Path is empty dir | Exit 0, valid JSON with zeros |
| Permission denied on dir | Logged in `errors`, subtree skipped, scan continues |
| Permission denied on file | Logged in `errors`, file not counted |
| Symlink to file (default) | Skipped silently, not logged |
| Symlink loop (`--follow-symlinks`) | Detected via visited-inode set; logged in `errors` |
| File with no extension | Counted, language = `Unknown`, ext recorded as `""` |
| File with multiple dots (`foo.test.tsx`) | Last suffix wins (`.tsx` → TypeScript) |
| Binary file (null bytes in first 8 KB) | Counted, lines=0, logged in `errors` |
| Empty file | Counted, lines=0, no error |
| File with no trailing newline | Last partial line counted (so a 1-byte file = 1 line) |
| Decoding errors | Suppressed via `errors="replace"`; line count still accurate |
| Very large file (>100 MB) | Read in 64 KB chunks; never hold full file in RAM |
| Unexpected exception per file | Caught, logged in `errors` with exception class name, scan continues |
| KeyboardInterrupt | Propagates; partial JSON not written |

**Logging:** stdlib `logging`. INFO+ when `--verbose`, WARNING+ otherwise. Warnings go to stderr so stdout stays pure JSON.

## Project Structure

```
hackathon/
├── pyproject.toml          # build config, runtime deps (none), dev deps (pytest, ruff, mypy), entry point
├── README.md               # usage, install, examples
├── src/
│   └── code_scanner/
│       ├── __init__.py     # __version__
│       ├── __main__.py     # python -m code_scanner -> cli.main()
│       ├── cli.py          # argparse, exit codes, top-level error handling, stdout/file output
│       ├── scanner.py      # walk + file processing; pure functions returning dataclasses
│       ├── languages.py    # EXTENSION_MAP: dict[str, str] (".py" -> "Python", etc.)
│       └── report.py       # convert ScanResult dataclass -> dict, json.dumps
└── tests/
    ├── conftest.py         # fixtures: tmp_path tree builders
    ├── test_scanner.py     # walks, line counts, binary detect, encoding, edge cases
    ├── test_languages.py   # ext mapping, unknown, multi-suffix
    ├── test_cli.py         # argparse, exit codes, --output, --pretty, stderr separation
    └── fixtures/           # sample trees as needed
```

**Module boundaries:**
- `scanner.py` knows nothing about JSON or argparse. Returns a `ScanResult` dataclass.
- `report.py` is the only place that knows the JSON shape.
- `cli.py` is the only place that calls `sys.exit` and writes to `print`/files.

## Testing Strategy

- **Framework:** `pytest`, only dev dep beyond ruff/mypy.
- **Approach:** every test builds a real fixture tree under `tmp_path`. No filesystem mocking.
- **Coverage target:** every row in the edge-cases table has ≥1 test.
- **End-to-end:** `test_cli.py` invokes `python -m code_scanner <fixture>` via `subprocess` and asserts the JSON shape and exit code.
- **Linting/typing:** `ruff check`, `ruff format --check`, `mypy --strict src/code_scanner` all clean.

## Open Questions

None at design time — all major choices pinned above.

## Out-of-Scope Future Work

- `--strict-loc` flag for non-blank, non-comment LOC counting (would require per-language comment maps or `pygount`).
- `--max-file-size` cap.
- Glob-pattern excludes (today's `--exclude` matches dir names only).
- gitignore awareness.
