# Code Scanner

A small .NET 9 CLI that recursively scans a directory and reports a JSON summary of files and lines, grouped by language.

## Build

```powershell
dotnet build CodeScanner.sln
```

## Run (development)

```powershell
dotnet run --project src/CodeScanner -- <path> [--output report.json] [--pretty] [--exclude name ...] [--follow-symlinks] [--verbose]
```

## Install as a global tool

```powershell
dotnet pack src/CodeScanner -o ./nupkg
dotnet tool install --global --add-source ./nupkg CodeScanner
code-scanner <path>
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
    "C#":         { "files": 47, "lines": 8230, "extensions": [".cs"] },
    "TypeScript": { "files": 31, "lines": 5102, "extensions": [".ts", ".tsx"] },
    "Unknown":    { "files":  6, "lines":  402, "extensions": [".xyz", ""] }
  },
  "scanned": {
    "root": "C:/scanned/dir",
    "skippedDirs": [".git", "node_modules"],
    "errors": [
      { "path": "C:/scanned/dir/blob.bin", "reason": "binary file, lines not counted" }
    ]
  }
}
```

## Default skipped directories

`.git`, `node_modules`, `__pycache__`, `.venv`, `venv`, `.pytest_cache`, `dist`, `build`, `.mypy_cache`, `.ruff_cache`, `bin`, `obj`.

Use `--exclude <name>` (repeatable) to add more.

## Exit codes

| Code | Meaning |
|------|---------|
| 0    | Success (per-file errors, if any, are inside the JSON) |
| 1    | Path doesn't exist or isn't a directory |
| 2    | Unexpected error |
