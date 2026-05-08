# CLAUDE.md — Working Notes for the Code Scanner Project

Lightweight ground rules learned from past sessions. Apply automatically; you don't need to ask before following them.

## 1. Verify the toolchain before writing a spec

Before turning a feature idea into a design doc, confirm the runtime / build tools the spec assumes are actually installed and runnable on this machine. A 5-second check now beats rewriting a finished spec.

```powershell
dotnet --version    # for any .NET work in this repo
python --version    # if the design assumes Python
node --version      # if the design assumes Node
```

If a tool is missing or returns a Microsoft Store stub instead of the real binary, surface that before continuing the brainstorm and offer alternatives (different language, install, switch repos).

## 2. xUnit assertion patterns that break this repo's build

`tests/CodeScanner.Tests/CodeScanner.Tests.csproj` has `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and ships with the xUnit analyzers. The following patterns trigger analyzer errors and fail the build. Use the alternatives:

| Don't write | Analyzer | Use instead |
|---|---|---|
| `Assert.Empty(coll.Where(predicate))` | xUnit2029 | `Assert.DoesNotContain(coll, predicate)` |
| `Assert.True(regex.IsMatch(input))` | xUnit2008 | `Assert.Matches(regex, input)` |
| `Assert.False(regex.IsMatch(input))` | xUnit2008 | `Assert.DoesNotMatch(regex, input)` |
| `Assert.Equal(0, coll.Count())` | xUnit2013 | `Assert.Empty(coll)` |
| `Assert.Equal(0, coll.EnumerateObject().Count())` | xUnit2013 | `Assert.Empty(coll.EnumerateObject())` |

Pre-emptively use the right form when generating new tests. Don't wait for the build to fail.

## 3. Check whether a "new" feature already exists before brainstorming

If a request looks like "add X to the scanner", scan recent state before starting a brainstorm:

```powershell
git log --oneline -30
git log --oneline --grep "<feature keyword>"
```

Also grep the source for the feature's vocabulary. If the feature is already shipped on `main`, surface that to the user and ask whether they want changes / additions / a fresh take — don't silently re-brainstorm.

## Project quick reference

- **Stack:** .NET 9, C#, xUnit. Roslyn (`Microsoft.CodeAnalysis.CSharp`), `System.CommandLine` v3 prerelease, `Microsoft.Extensions.FileSystemGlobbing`.
- **Layout:** `src/CodeScanner/` (Analyzers/, Html/) + `tests/CodeScanner.Tests/`.
- **Build:** `dotnet build CodeScanner.sln`. Strict mode: add `/warnaserror`.
- **Test:** `dotnet test CodeScanner.sln`.
- **Run:** `dotnet run --project src/CodeScanner -- <path> [--analyze] [--html report.html]`.
- **Working directory:** `C:\Cmm-testing\Claude_Training\hackathon`.
- **Use PowerShell** for `dotnet` commands with leading-slash flags (`/warnaserror`); Bash on Windows mangles them as paths.
