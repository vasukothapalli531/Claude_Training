# Code Scanner Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a .NET 9 (C#) CLI that recursively scans a directory and emits a JSON summary of file counts and lines per language, following the design in `docs/superpowers/specs/2026-05-05-code-scanner-design.md`.

**Architecture:** Single solution with two projects: `src/CodeScanner` (console app, contains all logic split across `Scanner`, `Languages`, `Report`, `Models`, `Cli`) and `tests/CodeScanner.Tests` (xUnit, references src). Pure-function core (`Scanner.Scan`) returns immutable `ScanResult` records; `Report` serializes to JSON; `Cli` is the only layer that touches `Console`/`Environment.Exit`.

**Tech Stack:** .NET 9, C# 13, xUnit, `System.CommandLine` v2 (prerelease), `System.Text.Json`. Nullable refs enabled, warnings-as-errors.

**Repo layout produced by this plan:**

```
hackathon/
├── CodeScanner.sln
├── .gitignore
├── README.md
├── src/CodeScanner/
│   ├── CodeScanner.csproj
│   ├── Program.cs
│   ├── Cli.cs
│   ├── Scanner.cs
│   ├── Languages.cs
│   ├── Report.cs
│   └── Models.cs
└── tests/CodeScanner.Tests/
    ├── CodeScanner.Tests.csproj
    ├── LanguagesTests.cs
    ├── ScannerTests.cs
    ├── ReportTests.cs
    └── CliTests.cs
```

---

## Task 1: Solution scaffold

**Files:**
- Create: `hackathon/CodeScanner.sln`
- Create: `hackathon/.gitignore`
- Create: `hackathon/src/CodeScanner/CodeScanner.csproj`
- Create: `hackathon/src/CodeScanner/Program.cs` (placeholder)
- Create: `hackathon/tests/CodeScanner.Tests/CodeScanner.Tests.csproj`
- Create: `hackathon/tests/CodeScanner.Tests/SmokeTests.cs`

- [ ] **Step 1.1: Create solution and projects**

Working directory throughout the plan: `C:\Cmm-testing\Claude_Training\hackathon`.

Run:
```powershell
dotnet new sln -n CodeScanner
dotnet new console -n CodeScanner -o src/CodeScanner --framework net9.0
dotnet new xunit -n CodeScanner.Tests -o tests/CodeScanner.Tests --framework net9.0
dotnet sln add src/CodeScanner/CodeScanner.csproj
dotnet sln add tests/CodeScanner.Tests/CodeScanner.Tests.csproj
dotnet add tests/CodeScanner.Tests/CodeScanner.Tests.csproj reference src/CodeScanner/CodeScanner.csproj
```

Expected: each command prints "successfully created/added".

- [ ] **Step 1.2: Configure src/CodeScanner/CodeScanner.csproj**

Replace the entire contents of `src/CodeScanner/CodeScanner.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <RootNamespace>CodeScanner</RootNamespace>
    <AssemblyName>CodeScanner</AssemblyName>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>code-scanner</ToolCommandName>
    <PackageId>CodeScanner</PackageId>
    <Version>0.1.0</Version>
  </PropertyGroup>
</Project>
```

- [ ] **Step 1.3: Add System.CommandLine to src project**

Run:
```powershell
dotnet add src/CodeScanner/CodeScanner.csproj package System.CommandLine --prerelease
```

Expected: package added; version printed (a 2.0.0-beta release).

- [ ] **Step 1.4: Configure tests project**

Replace the entire contents of `tests/CodeScanner.Tests/CodeScanner.Tests.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\CodeScanner\CodeScanner.csproj" />
  </ItemGroup>
</Project>
```

Note: keep the existing `<PackageReference>` lines for `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, and `coverlet.collector` that `dotnet new xunit` produced. If the template stripped them, run:

```powershell
dotnet add tests/CodeScanner.Tests/CodeScanner.Tests.csproj package Microsoft.NET.Test.Sdk
dotnet add tests/CodeScanner.Tests/CodeScanner.Tests.csproj package xunit
dotnet add tests/CodeScanner.Tests/CodeScanner.Tests.csproj package xunit.runner.visualstudio
```

- [ ] **Step 1.5: Replace `Program.cs` with a placeholder that compiles**

Replace the entire contents of `src/CodeScanner/Program.cs` with:

```csharp
namespace CodeScanner;

public static class Program
{
    public static int Main(string[] args) => 0;
}
```

If the template generated a top-level statement file (`UseTopLevelStatements`), delete that and use the file above. Confirm `Program.cs` contains only the code above.

- [ ] **Step 1.6: Replace test class created by template with a smoke test**

Delete `tests/CodeScanner.Tests/UnitTest1.cs` if present. Create `tests/CodeScanner.Tests/SmokeTests.cs` with:

```csharp
namespace CodeScanner.Tests;

public class SmokeTests
{
    [Fact]
    public void ProgramMain_ReturnsZero()
    {
        var exit = Program.Main(Array.Empty<string>());
        Assert.Equal(0, exit);
    }
}
```

- [ ] **Step 1.7: Create `.gitignore`**

Create `hackathon/.gitignore` with:

```
bin/
obj/
.vs/
*.user
*.suo
```

- [ ] **Step 1.8: Build and test to verify scaffold**

Run from `hackathon/`:
```powershell
dotnet build CodeScanner.sln
dotnet test CodeScanner.sln
```

Expected:
- `dotnet build`: "Build succeeded. 0 Warning(s). 0 Error(s)."
- `dotnet test`: 1 test passed.

- [ ] **Step 1.9: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/CodeScanner.sln hackathon/.gitignore hackathon/src hackathon/tests
git -C C:/Cmm-testing/Claude_Training commit -m "feat(scanner): scaffold .NET 9 solution with src + tests projects"
```

---

## Task 2: Models (record types)

**Files:**
- Create: `hackathon/src/CodeScanner/Models.cs`
- Create: `hackathon/tests/CodeScanner.Tests/ModelsTests.cs`

- [ ] **Step 2.1: Write failing tests**

Create `tests/CodeScanner.Tests/ModelsTests.cs` with:

```csharp
namespace CodeScanner.Tests;

public class ModelsTests
{
    [Fact]
    public void ScanError_RecordEqualityWorks()
    {
        var a = new ScanError("foo.txt", "permission denied");
        var b = new ScanError("foo.txt", "permission denied");
        Assert.Equal(a, b);
    }

    [Fact]
    public void FileEntry_DefaultIsBinaryIsFalse()
    {
        var e = new FileEntry("a.cs", ".cs", "C#", 10, IsBinary: false);
        Assert.False(e.IsBinary);
        Assert.Equal(10, e.Lines);
    }

    [Fact]
    public void ScanResult_HoldsAllCollections()
    {
        var r = new ScanResult(
            Root: "/x",
            FileEntries: new List<FileEntry>(),
            SkippedDirs: new List<string>(),
            Errors: new List<ScanError>());

        Assert.Empty(r.FileEntries);
        Assert.Empty(r.SkippedDirs);
        Assert.Empty(r.Errors);
    }

    [Fact]
    public void ScanOptions_DefaultsAreSafe()
    {
        var o = new ScanOptions();
        Assert.False(o.FollowSymlinks);
        Assert.NotNull(o.ExtraExcludes);
        Assert.Empty(o.ExtraExcludes);
    }
}
```

- [ ] **Step 2.2: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln
```

Expected: build fails with "type or namespace name 'ScanError' could not be found" (and similar).

- [ ] **Step 2.3: Implement `Models.cs`**

Create `src/CodeScanner/Models.cs` with:

```csharp
namespace CodeScanner;

public sealed record ScanError(string Path, string Reason);

public sealed record FileEntry(
    string Path,
    string Extension,
    string Language,
    long Lines,
    bool IsBinary);

public sealed record ScanResult(
    string Root,
    IReadOnlyList<FileEntry> FileEntries,
    IReadOnlyList<string> SkippedDirs,
    IReadOnlyList<ScanError> Errors);

public sealed record ScanOptions
{
    public bool FollowSymlinks { get; init; }
    public IReadOnlyList<string> ExtraExcludes { get; init; } = Array.Empty<string>();
}
```

- [ ] **Step 2.4: Run tests — verify they pass**

```powershell
dotnet test CodeScanner.sln
```

Expected: 5 tests passed (1 smoke + 4 new).

- [ ] **Step 2.5: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Models.cs hackathon/tests/CodeScanner.Tests/ModelsTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(scanner): add ScanError, FileEntry, ScanResult, ScanOptions records"
```

---

## Task 3: Languages mapping

**Files:**
- Create: `hackathon/src/CodeScanner/Languages.cs`
- Create: `hackathon/tests/CodeScanner.Tests/LanguagesTests.cs`

- [ ] **Step 3.1: Write failing tests**

Create `tests/CodeScanner.Tests/LanguagesTests.cs` with:

```csharp
namespace CodeScanner.Tests;

public class LanguagesTests
{
    [Theory]
    [InlineData(".cs", "C#")]
    [InlineData(".py", "Python")]
    [InlineData(".ts", "TypeScript")]
    [InlineData(".tsx", "TypeScript")]
    [InlineData(".js", "JavaScript")]
    [InlineData(".jsx", "JavaScript")]
    [InlineData(".md", "Markdown")]
    [InlineData(".json", "JSON")]
    [InlineData(".yml", "YAML")]
    [InlineData(".yaml", "YAML")]
    [InlineData(".html", "HTML")]
    [InlineData(".css", "CSS")]
    [InlineData(".go", "Go")]
    [InlineData(".rs", "Rust")]
    [InlineData(".java", "Java")]
    public void Classify_KnownExtension_ReturnsLanguage(string ext, string expected)
    {
        Assert.Equal(expected, Languages.Classify(ext));
    }

    [Fact]
    public void Classify_UnknownExtension_ReturnsUnknown()
    {
        Assert.Equal("Unknown", Languages.Classify(".xyz"));
    }

    [Fact]
    public void Classify_EmptyExtension_ReturnsUnknown()
    {
        Assert.Equal("Unknown", Languages.Classify(""));
    }

    [Fact]
    public void Classify_IsCaseInsensitive()
    {
        Assert.Equal("C#", Languages.Classify(".CS"));
        Assert.Equal("TypeScript", Languages.Classify(".TSX"));
    }
}
```

- [ ] **Step 3.2: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln
```

Expected: build error "Languages does not exist".

- [ ] **Step 3.3: Implement `Languages.cs`**

Create `src/CodeScanner/Languages.cs` with:

```csharp
namespace CodeScanner;

public static class Languages
{
    private static readonly IReadOnlyDictionary<string, string> Map =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".cs"]    = "C#",
            [".fs"]    = "F#",
            [".vb"]    = "VB.NET",
            [".py"]    = "Python",
            [".pyi"]   = "Python",
            [".ts"]    = "TypeScript",
            [".tsx"]   = "TypeScript",
            [".js"]    = "JavaScript",
            [".jsx"]   = "JavaScript",
            [".mjs"]   = "JavaScript",
            [".cjs"]   = "JavaScript",
            [".java"]  = "Java",
            [".kt"]    = "Kotlin",
            [".kts"]   = "Kotlin",
            [".go"]    = "Go",
            [".rs"]    = "Rust",
            [".rb"]    = "Ruby",
            [".php"]   = "PHP",
            [".swift"] = "Swift",
            [".c"]     = "C",
            [".h"]     = "C",
            [".cpp"]   = "C++",
            [".cc"]    = "C++",
            [".hpp"]   = "C++",
            [".m"]     = "Objective-C",
            [".sh"]    = "Shell",
            [".bash"]  = "Shell",
            [".ps1"]   = "PowerShell",
            [".psm1"]  = "PowerShell",
            [".sql"]   = "SQL",
            [".html"]  = "HTML",
            [".htm"]   = "HTML",
            [".css"]   = "CSS",
            [".scss"]  = "SCSS",
            [".sass"]  = "Sass",
            [".less"]  = "Less",
            [".md"]    = "Markdown",
            [".rst"]   = "reStructuredText",
            [".txt"]   = "Text",
            [".json"]  = "JSON",
            [".xml"]   = "XML",
            [".yml"]   = "YAML",
            [".yaml"]  = "YAML",
            [".toml"]  = "TOML",
            [".ini"]   = "INI",
            [".csv"]   = "CSV",
            [".tsv"]   = "TSV",
        };

    public static string Classify(string extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return "Unknown";
        }
        return Map.TryGetValue(extension, out var lang) ? lang : "Unknown";
    }
}
```

- [ ] **Step 3.4: Run tests — verify they pass**

```powershell
dotnet test CodeScanner.sln
```

Expected: all tests pass (5 from Task 2 + 19 new).

- [ ] **Step 3.5: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Languages.cs hackathon/tests/CodeScanner.Tests/LanguagesTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(scanner): add extension-to-language classifier"
```

---

## Task 4: Binary detection

**Files:**
- Create: `hackathon/src/CodeScanner/Scanner.cs` (initial — only `IsBinary`)
- Create: `hackathon/tests/CodeScanner.Tests/ScannerTests.cs` (initial — only `IsBinary` tests)
- Create: `hackathon/tests/CodeScanner.Tests/TempTree.cs` (test helper)

- [ ] **Step 4.1: Create test helper for temp file trees**

Create `tests/CodeScanner.Tests/TempTree.cs` with:

```csharp
namespace CodeScanner.Tests;

/// <summary>
/// Disposable temp directory; auto-deleted on Dispose.
/// </summary>
public sealed class TempTree : IDisposable
{
    public string Root { get; }

    public TempTree()
    {
        Root = Path.Combine(Path.GetTempPath(), "code-scanner-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string WriteFile(string relativePath, string contents)
    {
        var full = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, contents);
        return full;
    }

    public string WriteBytes(string relativePath, byte[] contents)
    {
        var full = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, contents);
        return full;
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch { /* test cleanup best-effort */ }
    }
}
```

- [ ] **Step 4.2: Write failing tests for `IsBinary`**

Create `tests/CodeScanner.Tests/ScannerTests.cs` with:

```csharp
namespace CodeScanner.Tests;

public class ScannerTests
{
    [Fact]
    public void IsBinary_PlainText_ReturnsFalse()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("a.txt", "hello world\nsecond line\n");

        Assert.False(Scanner.IsBinary(path));
    }

    [Fact]
    public void IsBinary_EmptyFile_ReturnsFalse()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("empty.txt", string.Empty);

        Assert.False(Scanner.IsBinary(path));
    }

    [Fact]
    public void IsBinary_NullByteInFirstFewBytes_ReturnsTrue()
    {
        using var tree = new TempTree();
        var path = tree.WriteBytes("bin.dat", new byte[] { 0x48, 0x00, 0x49 });

        Assert.True(Scanner.IsBinary(path));
    }

    [Fact]
    public void IsBinary_NullByteJustInsideFirst8K_ReturnsTrue()
    {
        using var tree = new TempTree();
        var bytes = new byte[8192];
        Array.Fill<byte>(bytes, (byte)'a');
        bytes[8000] = 0x00;
        var path = tree.WriteBytes("almost.dat", bytes);

        Assert.True(Scanner.IsBinary(path));
    }

    [Fact]
    public void IsBinary_NullByteAfterFirst8K_ReturnsFalse()
    {
        using var tree = new TempTree();
        var bytes = new byte[16384];
        Array.Fill<byte>(bytes, (byte)'a');
        bytes[10000] = 0x00; // null past sniff window
        var path = tree.WriteBytes("late.dat", bytes);

        Assert.False(Scanner.IsBinary(path));
    }
}
```

- [ ] **Step 4.3: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln
```

Expected: build error "Scanner does not exist".

- [ ] **Step 4.4: Implement `IsBinary` in `Scanner.cs`**

Create `src/CodeScanner/Scanner.cs` with:

```csharp
namespace CodeScanner;

public static class Scanner
{
    private const int BinarySniffBytes = 8192;

    public static bool IsBinary(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: BinarySniffBytes,
            options: FileOptions.SequentialScan);

        Span<byte> buffer = stackalloc byte[BinarySniffBytes];
        var read = stream.Read(buffer);
        for (var i = 0; i < read; i++)
        {
            if (buffer[i] == 0x00)
            {
                return true;
            }
        }
        return false;
    }
}
```

- [ ] **Step 4.5: Run tests — verify they pass**

```powershell
dotnet test CodeScanner.sln
```

Expected: all tests green (Task 2-3 tests + 5 new).

- [ ] **Step 4.6: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Scanner.cs hackathon/tests/CodeScanner.Tests/ScannerTests.cs hackathon/tests/CodeScanner.Tests/TempTree.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(scanner): add binary file detection via null-byte sniff"
```

---

## Task 5: Line counter

**Files:**
- Modify: `hackathon/src/CodeScanner/Scanner.cs` — add `CountLines`
- Modify: `hackathon/tests/CodeScanner.Tests/ScannerTests.cs` — add `CountLines` tests

- [ ] **Step 5.1: Append failing tests to `ScannerTests.cs`**

Append inside the existing `ScannerTests` class in `tests/CodeScanner.Tests/ScannerTests.cs`, before the closing `}`:

```csharp
    [Fact]
    public void CountLines_EmptyFile_ReturnsZero()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("empty.txt", string.Empty);

        Assert.Equal(0, Scanner.CountLines(path));
    }

    [Fact]
    public void CountLines_SingleLineWithNewline_ReturnsOne()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("one.txt", "hello\n");

        Assert.Equal(1, Scanner.CountLines(path));
    }

    [Fact]
    public void CountLines_SingleLineNoNewline_ReturnsOne()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("one.txt", "hello");

        Assert.Equal(1, Scanner.CountLines(path));
    }

    [Fact]
    public void CountLines_ThreeLinesWithTrailingNewline_ReturnsThree()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("three.txt", "a\nb\nc\n");

        Assert.Equal(3, Scanner.CountLines(path));
    }

    [Fact]
    public void CountLines_ThreeLinesNoTrailingNewline_ReturnsThree()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("three.txt", "a\nb\nc");

        Assert.Equal(3, Scanner.CountLines(path));
    }

    [Fact]
    public void CountLines_LargeFileAcrossBuffers_ReturnsExactCount()
    {
        using var tree = new TempTree();
        // Build content that crosses the 64KB buffer boundary several times.
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < 10_000; i++) { sb.Append("line ").Append(i).Append('\n'); }
        var path = tree.WriteFile("big.txt", sb.ToString());

        Assert.Equal(10_000, Scanner.CountLines(path));
    }

    [Fact]
    public void CountLines_OneByteFile_ReturnsOne()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("byte.txt", "x");

        Assert.Equal(1, Scanner.CountLines(path));
    }
```

- [ ] **Step 5.2: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln
```

Expected: build error "Scanner does not contain a definition for 'CountLines'".

- [ ] **Step 5.3: Add `CountLines` to `Scanner.cs`**

Inside the `Scanner` class in `src/CodeScanner/Scanner.cs`, add:

```csharp
    private const int LineCountBufferSize = 64 * 1024;

    public static long CountLines(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: LineCountBufferSize,
            options: FileOptions.SequentialScan);

        long newlineCount = 0;
        byte lastByte = 0;
        var totalRead = 0L;
        var buffer = new byte[LineCountBufferSize];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalRead += read;
            for (var i = 0; i < read; i++)
            {
                if (buffer[i] == 0x0A)
                {
                    newlineCount++;
                }
            }
            lastByte = buffer[read - 1];
        }

        if (totalRead == 0)
        {
            return 0;
        }

        // Add 1 if file is non-empty and final byte is not a newline,
        // i.e., there's a trailing partial line.
        return lastByte == 0x0A ? newlineCount : newlineCount + 1;
    }
```

- [ ] **Step 5.4: Run tests — verify they pass**

```powershell
dotnet test CodeScanner.sln
```

Expected: all tests green (prior + 7 new).

- [ ] **Step 5.5: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Scanner.cs hackathon/tests/CodeScanner.Tests/ScannerTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(scanner): add raw line counter (counts 0x0A + trailing partial line)"
```

---

## Task 6: Single-file processing

**Files:**
- Modify: `hackathon/src/CodeScanner/Scanner.cs` — add `ProcessFile`
- Modify: `hackathon/tests/CodeScanner.Tests/ScannerTests.cs` — add `ProcessFile` tests

- [ ] **Step 6.1: Append failing tests**

Append inside `ScannerTests` (before the closing `}`):

```csharp
    [Fact]
    public void ProcessFile_TextFile_ReturnsEntryWithLanguageAndLines()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("hello.cs", "class C { }\n");

        var (entry, error) = Scanner.ProcessFile(path);

        Assert.Equal(path, entry.Path);
        Assert.Equal(".cs", entry.Extension);
        Assert.Equal("C#", entry.Language);
        Assert.Equal(1, entry.Lines);
        Assert.False(entry.IsBinary);
        Assert.Null(error);
    }

    [Fact]
    public void ProcessFile_BinaryFile_ReturnsEntryWithZeroLinesAndError()
    {
        using var tree = new TempTree();
        var path = tree.WriteBytes("img.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00, 0xFF });

        var (entry, error) = Scanner.ProcessFile(path);

        Assert.Equal(0, entry.Lines);
        Assert.True(entry.IsBinary);
        Assert.Equal(".png", entry.Extension);
        Assert.NotNull(error);
        Assert.Equal(path, error!.Path);
        Assert.Contains("binary", error.Reason);
    }

    [Fact]
    public void ProcessFile_NoExtension_ReturnsUnknown()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("Makefile", "all:\n\techo hi\n");

        var (entry, _) = Scanner.ProcessFile(path);

        Assert.Equal("", entry.Extension);
        Assert.Equal("Unknown", entry.Language);
    }

    [Fact]
    public void ProcessFile_MultiDotName_UsesLastSuffix()
    {
        using var tree = new TempTree();
        var path = tree.WriteFile("foo.test.tsx", "export const x = 1;\n");

        var (entry, _) = Scanner.ProcessFile(path);

        Assert.Equal(".tsx", entry.Extension);
        Assert.Equal("TypeScript", entry.Language);
    }
```

- [ ] **Step 6.2: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln
```

Expected: "Scanner does not contain a definition for 'ProcessFile'".

- [ ] **Step 6.3: Add `ProcessFile` to `Scanner.cs`**

Inside the `Scanner` class:

```csharp
    public static (FileEntry Entry, ScanError? Error) ProcessFile(string path)
    {
        var extension = Path.GetExtension(path); // includes leading dot, "" if none
        var language = Languages.Classify(extension);

        bool isBinary;
        try
        {
            isBinary = IsBinary(path);
        }
        catch (Exception ex)
        {
            var entryOnError = new FileEntry(path, extension, language, Lines: 0, IsBinary: false);
            return (entryOnError, new ScanError(path, $"{ex.GetType().Name}: {ex.Message}"));
        }

        if (isBinary)
        {
            var binEntry = new FileEntry(path, extension, language, Lines: 0, IsBinary: true);
            return (binEntry, new ScanError(path, "binary file, lines not counted"));
        }

        try
        {
            var lines = CountLines(path);
            return (new FileEntry(path, extension, language, lines, IsBinary: false), null);
        }
        catch (Exception ex)
        {
            var entryOnError = new FileEntry(path, extension, language, Lines: 0, IsBinary: false);
            return (entryOnError, new ScanError(path, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }
```

- [ ] **Step 6.4: Run tests — verify they pass**

```powershell
dotnet test CodeScanner.sln
```

Expected: all green (prior + 4 new).

- [ ] **Step 6.5: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Scanner.cs hackathon/tests/CodeScanner.Tests/ScannerTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(scanner): add per-file processing (binary check + lines + classify)"
```

---

## Task 7: Directory walker

**Files:**
- Modify: `hackathon/src/CodeScanner/Scanner.cs` — add `Scan` and default-excludes constant
- Modify: `hackathon/tests/CodeScanner.Tests/ScannerTests.cs` — add `Scan` tests

- [ ] **Step 7.1: Append failing tests**

Append inside `ScannerTests`:

```csharp
    [Fact]
    public void Scan_EmptyDirectory_ReturnsEmptyResult()
    {
        using var tree = new TempTree();

        var result = Scanner.Scan(tree.Root, new ScanOptions());

        Assert.Equal(tree.Root, result.Root);
        Assert.Empty(result.FileEntries);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Scan_FlatDirectoryWithMixedFiles_ReturnsAllEntries()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs",   "class A {}\n");
        tree.WriteFile("b.py",   "x = 1\ny = 2\n");
        tree.WriteFile("c.md",   "# title\n");

        var result = Scanner.Scan(tree.Root, new ScanOptions());

        Assert.Equal(3, result.FileEntries.Count);
        Assert.Contains(result.FileEntries, e => e.Language == "C#"        && e.Lines == 1);
        Assert.Contains(result.FileEntries, e => e.Language == "Python"    && e.Lines == 2);
        Assert.Contains(result.FileEntries, e => e.Language == "Markdown"  && e.Lines == 1);
    }

    [Fact]
    public void Scan_DefaultExcludedDirsArePruned()
    {
        using var tree = new TempTree();
        tree.WriteFile("real.cs",                "class R {}\n");
        tree.WriteFile(".git/HEAD",              "ref: refs/heads/main\n");
        tree.WriteFile("node_modules/x/index.js","console.log('x');\n");
        tree.WriteFile("bin/Debug/foo.dll",      "binary-ish");
        tree.WriteFile("obj/foo.o",              "binary-ish");

        var result = Scanner.Scan(tree.Root, new ScanOptions());

        Assert.Single(result.FileEntries);
        Assert.Equal("C#", result.FileEntries[0].Language);
        Assert.Contains(".git",         result.SkippedDirs);
        Assert.Contains("node_modules", result.SkippedDirs);
        Assert.Contains("bin",          result.SkippedDirs);
        Assert.Contains("obj",          result.SkippedDirs);
    }

    [Fact]
    public void Scan_ExtraExcludesArePrunedTooAdditively()
    {
        using var tree = new TempTree();
        tree.WriteFile("keep.cs",     "class K {}\n");
        tree.WriteFile("skipme/x.cs", "class S {}\n");

        var options = new ScanOptions { ExtraExcludes = new[] { "skipme" } };
        var result = Scanner.Scan(tree.Root, options);

        Assert.Single(result.FileEntries);
        Assert.Equal("keep.cs", Path.GetFileName(result.FileEntries[0].Path));
        Assert.Contains("skipme", result.SkippedDirs);
    }

    [Fact]
    public void Scan_NestedDirectoriesAreTraversed()
    {
        using var tree = new TempTree();
        tree.WriteFile("top.cs",         "class T {}\n");
        tree.WriteFile("a/mid.cs",       "class M {}\n");
        tree.WriteFile("a/b/leaf.cs",    "class L {}\n");

        var result = Scanner.Scan(tree.Root, new ScanOptions());

        Assert.Equal(3, result.FileEntries.Count);
    }

    [Fact]
    public void Scan_BinaryFileIsCountedButLogsError()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs", "class A {}\n");
        tree.WriteBytes("blob.bin", new byte[] { 0x00, 0x01, 0x02 });

        var result = Scanner.Scan(tree.Root, new ScanOptions());

        Assert.Equal(2, result.FileEntries.Count);
        Assert.Contains(result.FileEntries, e => e.IsBinary);
        Assert.Contains(result.Errors, err => err.Reason.Contains("binary"));
    }

    [Fact]
    public void Scan_DotfilesAreIncluded()
    {
        using var tree = new TempTree();
        tree.WriteFile(".eslintrc.js",  "module.exports = {};\n");
        tree.WriteFile(".env.example",  "FOO=bar\n");

        var result = Scanner.Scan(tree.Root, new ScanOptions());

        Assert.Equal(2, result.FileEntries.Count);
    }
```

- [ ] **Step 7.2: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln
```

Expected: "Scanner does not contain a definition for 'Scan'".

- [ ] **Step 7.3: Add `Scan` and `DefaultExcludedDirs` to `Scanner.cs`**

Inside the `Scanner` class:

```csharp
    public static readonly IReadOnlySet<string> DefaultExcludedDirs =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", "node_modules", "__pycache__",
            ".venv", "venv", ".pytest_cache",
            "dist", "build",
            ".mypy_cache", ".ruff_cache",
            "bin", "obj",
        };

    public static ScanResult Scan(string root, ScanOptions options)
    {
        var entries = new List<FileEntry>();
        var errors = new List<ScanError>();
        var skippedDirs = new HashSet<string>(StringComparer.Ordinal);

        var excludeSet = new HashSet<string>(DefaultExcludedDirs, StringComparer.OrdinalIgnoreCase);
        foreach (var extra in options.ExtraExcludes) { excludeSet.Add(extra); }

        var visitedRealPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Walk(root);

        return new ScanResult(
            Root: root,
            FileEntries: entries,
            SkippedDirs: skippedDirs.OrderBy(s => s, StringComparer.Ordinal).ToList(),
            Errors: errors);

        void Walk(string dir)
        {
            // Loop guard for follow-symlinks mode.
            if (options.FollowSymlinks)
            {
                string real;
                try { real = Path.GetFullPath(dir); }
                catch (Exception ex) { errors.Add(new ScanError(dir, $"{ex.GetType().Name}: {ex.Message}")); return; }
                if (!visitedRealPaths.Add(real))
                {
                    errors.Add(new ScanError(dir, "symlink loop"));
                    return;
                }
            }

            string[] subdirs;
            string[] files;
            try
            {
                subdirs = Directory.GetDirectories(dir);
                files   = Directory.GetFiles(dir);
            }
            catch (UnauthorizedAccessException ex)
            {
                errors.Add(new ScanError(dir, $"UnauthorizedAccessException: {ex.Message}"));
                return;
            }
            catch (Exception ex)
            {
                errors.Add(new ScanError(dir, $"{ex.GetType().Name}: {ex.Message}"));
                return;
            }

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                if (info.LinkTarget is not null && !options.FollowSymlinks)
                {
                    continue; // silent skip
                }
                try
                {
                    var (entry, error) = ProcessFile(file);
                    entries.Add(entry);
                    if (error is not null) { errors.Add(error); }
                }
                catch (Exception ex)
                {
                    errors.Add(new ScanError(file, $"{ex.GetType().Name}: {ex.Message}"));
                }
            }

            foreach (var sub in subdirs)
            {
                var name = Path.GetFileName(sub);
                if (excludeSet.Contains(name))
                {
                    skippedDirs.Add(name);
                    continue;
                }

                var dirInfo = new DirectoryInfo(sub);
                if (dirInfo.LinkTarget is not null && !options.FollowSymlinks)
                {
                    continue; // silent skip
                }

                Walk(sub);
            }
        }
    }
```

- [ ] **Step 7.4: Run tests — verify they pass**

```powershell
dotnet test CodeScanner.sln
```

Expected: all green (prior + 7 new).

- [ ] **Step 7.5: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Scanner.cs hackathon/tests/CodeScanner.Tests/ScannerTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(scanner): add directory walker with default+custom excludes"
```

---

## Task 8: Report builder + JSON serializer

**Files:**
- Create: `hackathon/src/CodeScanner/Report.cs`
- Create: `hackathon/tests/CodeScanner.Tests/ReportTests.cs`

- [ ] **Step 8.1: Write failing tests**

Create `tests/CodeScanner.Tests/ReportTests.cs` with:

```csharp
using System.Text.Json;

namespace CodeScanner.Tests;

public class ReportTests
{
    private static JsonElement Parse(string s) => JsonDocument.Parse(s).RootElement;

    [Fact]
    public void Serialize_EmptyResult_ProducesZerosAndEmptyCollections()
    {
        var result = new ScanResult(
            Root: "C:/x",
            FileEntries: Array.Empty<FileEntry>(),
            SkippedDirs: Array.Empty<string>(),
            Errors: Array.Empty<ScanError>());

        var json = Report.Serialize(result, pretty: false);
        var root = Parse(json);

        Assert.Equal(0, root.GetProperty("totalFiles").GetInt32());
        Assert.Equal(0, root.GetProperty("totalLines").GetInt64());
        Assert.Equal(0, root.GetProperty("languages").EnumerateObject().Count());
        Assert.Equal(0, root.GetProperty("scanned").GetProperty("errors").GetArrayLength());
    }

    [Fact]
    public void Serialize_AggregatesByLanguage()
    {
        var result = new ScanResult(
            Root: "C:/x",
            FileEntries: new[]
            {
                new FileEntry("a.cs",  ".cs",  "C#",         10, false),
                new FileEntry("b.cs",  ".cs",  "C#",         20, false),
                new FileEntry("c.tsx", ".tsx", "TypeScript",  5, false),
            },
            SkippedDirs: Array.Empty<string>(),
            Errors: Array.Empty<ScanError>());

        var json = Report.Serialize(result, pretty: false);
        var root = Parse(json);

        Assert.Equal(3, root.GetProperty("totalFiles").GetInt32());
        Assert.Equal(35, root.GetProperty("totalLines").GetInt64());

        var cs = root.GetProperty("languages").GetProperty("C#");
        Assert.Equal(2, cs.GetProperty("files").GetInt32());
        Assert.Equal(30, cs.GetProperty("lines").GetInt64());

        var ts = root.GetProperty("languages").GetProperty("TypeScript");
        Assert.Equal(1, ts.GetProperty("files").GetInt32());
        Assert.Equal(5, ts.GetProperty("lines").GetInt64());
    }

    [Fact]
    public void Serialize_BucketsUnknownsAndCollectsExtensions()
    {
        var result = new ScanResult(
            Root: "C:/x",
            FileEntries: new[]
            {
                new FileEntry("a.xyz",   ".xyz", "Unknown", 1, false),
                new FileEntry("Makefile", "",    "Unknown", 2, false),
            },
            SkippedDirs: Array.Empty<string>(),
            Errors: Array.Empty<ScanError>());

        var json = Report.Serialize(result, pretty: false);
        var root = Parse(json);

        var unknown = root.GetProperty("languages").GetProperty("Unknown");
        var exts = unknown.GetProperty("extensions").EnumerateArray().Select(e => e.GetString()).ToHashSet();
        Assert.Contains(".xyz", exts);
        Assert.Contains("",     exts);
    }

    [Fact]
    public void Serialize_IncludesErrorsAndSkippedDirs()
    {
        var result = new ScanResult(
            Root: "C:/x",
            FileEntries: Array.Empty<FileEntry>(),
            SkippedDirs: new[] { ".git", "node_modules" },
            Errors: new[] { new ScanError("a.bin", "binary file, lines not counted") });

        var json = Report.Serialize(result, pretty: false);
        var root = Parse(json);

        var skipped = root.GetProperty("scanned").GetProperty("skippedDirs")
            .EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains(".git", skipped);
        Assert.Contains("node_modules", skipped);

        var errors = root.GetProperty("scanned").GetProperty("errors");
        Assert.Equal(1, errors.GetArrayLength());
        Assert.Equal("a.bin", errors[0].GetProperty("path").GetString());
        Assert.Contains("binary", errors[0].GetProperty("reason").GetString());
    }

    [Fact]
    public void Serialize_PrettyTrueAddsIndentation()
    {
        var result = new ScanResult("C:/x", Array.Empty<FileEntry>(), Array.Empty<string>(), Array.Empty<ScanError>());

        var compact = Report.Serialize(result, pretty: false);
        var pretty  = Report.Serialize(result, pretty: true);

        Assert.DoesNotContain("\n", compact);
        Assert.Contains("\n", pretty);
    }

    [Fact]
    public void Serialize_NormalizesPathsToForwardSlashes()
    {
        var result = new ScanResult(
            Root: @"C:\some\dir",
            FileEntries: Array.Empty<FileEntry>(),
            SkippedDirs: Array.Empty<string>(),
            Errors: new[] { new ScanError(@"C:\some\dir\file.bin", "binary file, lines not counted") });

        var json = Report.Serialize(result, pretty: false);
        var root = Parse(json);

        Assert.Equal("C:/some/dir", root.GetProperty("scanned").GetProperty("root").GetString());
        Assert.Equal("C:/some/dir/file.bin",
            root.GetProperty("scanned").GetProperty("errors")[0].GetProperty("path").GetString());
    }
}
```

- [ ] **Step 8.2: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln
```

Expected: "Report does not exist".

- [ ] **Step 8.3: Implement `Report.cs`**

Create `src/CodeScanner/Report.cs` with:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodeScanner;

public static class Report
{
    public static string Serialize(ScanResult result, bool pretty)
    {
        var languages = new JsonObject();
        var totalFiles = 0L;
        var totalLines = 0L;

        foreach (var group in result.FileEntries.GroupBy(e => e.Language))
        {
            var files = group.LongCount();
            var lines = group.Sum(e => e.Lines);
            var exts  = group.Select(e => e.Extension)
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .OrderBy(s => s, StringComparer.Ordinal)
                             .ToList();

            languages[group.Key] = new JsonObject
            {
                ["files"] = files,
                ["lines"] = lines,
                ["extensions"] = new JsonArray(exts.Select(e => (JsonNode?)JsonValue.Create(e)).ToArray()),
            };
            totalFiles += files;
            totalLines += lines;
        }

        var errors = new JsonArray(
            result.Errors.Select(err => (JsonNode?)new JsonObject
            {
                ["path"]   = NormalizePath(err.Path),
                ["reason"] = err.Reason,
            }).ToArray());

        var skipped = new JsonArray(
            result.SkippedDirs.Select(d => (JsonNode?)JsonValue.Create(d)).ToArray());

        var doc = new JsonObject
        {
            ["totalFiles"] = totalFiles,
            ["totalLines"] = totalLines,
            ["languages"]  = languages,
            ["scanned"]    = new JsonObject
            {
                ["root"]        = NormalizePath(result.Root),
                ["skippedDirs"] = skipped,
                ["errors"]      = errors,
            },
        };

        var options = new JsonSerializerOptions { WriteIndented = pretty };
        return doc.ToJsonString(options);
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');
}
```

- [ ] **Step 8.4: Run tests — verify they pass**

```powershell
dotnet test CodeScanner.sln
```

Expected: all green (prior + 6 new).

- [ ] **Step 8.5: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Report.cs hackathon/tests/CodeScanner.Tests/ReportTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(scanner): add JSON report builder with language aggregation"
```

---

## Task 9: CLI

**Files:**
- Create: `hackathon/src/CodeScanner/Cli.cs`
- Modify: `hackathon/src/CodeScanner/Program.cs`
- Create: `hackathon/tests/CodeScanner.Tests/CliTests.cs`

- [ ] **Step 9.1: Write failing tests for the CLI**

Create `tests/CodeScanner.Tests/CliTests.cs` with:

```csharp
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace CodeScanner.Tests;

public class CliTests
{
    private static string FindSrcCsproj()
    {
        // Walk up from test bin folder to repo root, then into src/CodeScanner.
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src", "CodeScanner")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        Assert.NotNull(dir);
        return Path.Combine(dir!, "src", "CodeScanner", "CodeScanner.csproj");
    }

    private static (int ExitCode, string Stdout, string Stderr) RunCli(params string[] args)
    {
        var csproj = FindSrcCsproj();
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--no-build");
        psi.ArgumentList.Add("--project");
        psi.ArgumentList.Add(csproj);
        psi.ArgumentList.Add("--");
        foreach (var a in args) { psi.ArgumentList.Add(a); }

        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, stdout, stderr);
    }

    [Fact]
    public void Cli_MissingPath_ExitsOneAndWritesError()
    {
        var (exit, stdout, stderr) = RunCli("C:/this-path-does-not-exist-123456");

        Assert.Equal(1, exit);
        Assert.Empty(stdout);
        Assert.NotEmpty(stderr);
    }

    [Fact]
    public void Cli_PathIsFile_ExitsOne()
    {
        using var tree = new TempTree();
        var file = tree.WriteFile("a.cs", "class A {}\n");

        var (exit, stdout, stderr) = RunCli(file);

        Assert.Equal(1, exit);
        Assert.Empty(stdout);
        Assert.NotEmpty(stderr);
    }

    [Fact]
    public void Cli_ValidDir_ExitsZeroAndPrintsJsonToStdout()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs",   "class A {}\n");
        tree.WriteFile("b.py",   "x = 1\n");

        var (exit, stdout, stderr) = RunCli(tree.Root);

        Assert.Equal(0, exit);
        var doc = JsonDocument.Parse(stdout);
        Assert.Equal(2, doc.RootElement.GetProperty("totalFiles").GetInt32());
    }

    [Fact]
    public void Cli_OutputFlag_WritesJsonToFileNotStdout()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs", "class A {}\n");
        var outFile = Path.Combine(tree.Root, "report.json");

        var (exit, stdout, _) = RunCli(tree.Root, "--output", outFile);

        Assert.Equal(0, exit);
        Assert.Empty(stdout.Trim());
        Assert.True(File.Exists(outFile));
        var doc = JsonDocument.Parse(File.ReadAllText(outFile));
        Assert.Equal(1, doc.RootElement.GetProperty("totalFiles").GetInt32());
    }

    [Fact]
    public void Cli_PrettyFlag_ProducesIndentedOutput()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.cs", "class A {}\n");

        var (exit, stdout, _) = RunCli(tree.Root, "--pretty");

        Assert.Equal(0, exit);
        Assert.Contains("\n", stdout);
    }

    [Fact]
    public void Cli_ExcludeFlag_AdditiveToDefaults()
    {
        using var tree = new TempTree();
        tree.WriteFile("keep.cs",     "class K {}\n");
        tree.WriteFile("skipme/x.cs", "class S {}\n");

        var (exit, stdout, _) = RunCli(tree.Root, "--exclude", "skipme");

        Assert.Equal(0, exit);
        var doc = JsonDocument.Parse(stdout);
        Assert.Equal(1, doc.RootElement.GetProperty("totalFiles").GetInt32());
    }
}
```

- [ ] **Step 9.2: Run tests — verify they fail**

```powershell
dotnet test CodeScanner.sln
```

Expected: build error referencing missing CLI types/behavior, OR runtime errors when subprocess fails to find a working CLI.

- [ ] **Step 9.3: Implement `Cli.cs`**

Create `src/CodeScanner/Cli.cs` with:

```csharp
using System.CommandLine;

namespace CodeScanner;

public static class Cli
{
    public static async Task<int> RunAsync(string[] args)
    {
        var pathArg = new Argument<string>("path")
        {
            Description = "Directory to scan",
        };
        var outputOpt = new Option<string?>("--output", "-o")
        {
            Description = "Write JSON to this file instead of stdout",
        };
        var excludeOpt = new Option<string[]>("--exclude", "-e")
        {
            Description = "Extra directory names to skip (additive to defaults)",
            AllowMultipleArgumentsPerToken = true,
        };
        var followOpt = new Option<bool>("--follow-symlinks")
        {
            Description = "Follow symlinked directories (default: skip)",
        };
        var prettyOpt = new Option<bool>("--pretty")
        {
            Description = "Pretty-print JSON output",
        };
        var verboseOpt = new Option<bool>("--verbose", "-v")
        {
            Description = "Log scan progress to stderr",
        };

        var root = new RootCommand("Recursively scan a directory and emit JSON file/line statistics.")
        {
            pathArg,
            outputOpt,
            excludeOpt,
            followOpt,
            prettyOpt,
            verboseOpt,
        };

        root.SetAction(parseResult =>
        {
            var path     = parseResult.GetValue(pathArg)!;
            var output   = parseResult.GetValue(outputOpt);
            var excludes = parseResult.GetValue(excludeOpt) ?? Array.Empty<string>();
            var follow   = parseResult.GetValue(followOpt);
            var pretty   = parseResult.GetValue(prettyOpt);
            var verbose  = parseResult.GetValue(verboseOpt);

            return Execute(path, output, excludes, follow, pretty, verbose);
        });

        return await root.Parse(args).InvokeAsync();
    }

    private static int Execute(
        string path,
        string? output,
        string[] excludes,
        bool followSymlinks,
        bool pretty,
        bool verbose)
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
            };
            var result = Scanner.Scan(path, options);
            var json = Report.Serialize(result, pretty);

            if (output is null)
            {
                Console.Out.WriteLine(json);
            }
            else
            {
                File.WriteAllText(output, json);
                if (verbose) { Console.Error.WriteLine($"info: wrote {output}"); }
            }

            if (verbose) { Console.Error.WriteLine($"info: {result.FileEntries.Count} files, {result.Errors.Count} errors"); }
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

- [ ] **Step 9.4: Wire up `Program.cs`**

Replace `src/CodeScanner/Program.cs` with:

```csharp
namespace CodeScanner;

public static class Program
{
    public static Task<int> Main(string[] args) => Cli.RunAsync(args);
}
```

Note: this changes the smoke test in Task 1 (which called `Program.Main(...)` and asserted equal to `0` synchronously). Update `SmokeTests.cs`:

Replace `tests/CodeScanner.Tests/SmokeTests.cs` with:

```csharp
namespace CodeScanner.Tests;

public class SmokeTests
{
    [Fact]
    public async Task ProgramMain_NoArgs_ExitsNonZero()
    {
        // No args means missing required positional; CLI parser should non-zero.
        var exit = await Program.Main(Array.Empty<string>());
        Assert.NotEqual(0, exit);
    }
}
```

- [ ] **Step 9.5: Build first so `dotnet run --no-build` works in CLI tests**

```powershell
dotnet build CodeScanner.sln
```

Expected: clean build.

- [ ] **Step 9.6: Run all tests**

```powershell
dotnet test CodeScanner.sln
```

Expected: all green (prior + 6 new CLI tests).

- [ ] **Step 9.7: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/src/CodeScanner/Cli.cs hackathon/src/CodeScanner/Program.cs hackathon/tests/CodeScanner.Tests/CliTests.cs hackathon/tests/CodeScanner.Tests/SmokeTests.cs
git -C C:/Cmm-testing/Claude_Training commit -m "feat(scanner): wire CLI with System.CommandLine and exit codes"
```

---

## Task 10: README + final verification

**Files:**
- Create: `hackathon/README.md`

- [ ] **Step 10.1: Write the README**

Create `hackathon/README.md` with:

````markdown
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
````

- [ ] **Step 10.2: Final verification — build with warnings as errors**

```powershell
dotnet build CodeScanner.sln /warnaserror
```

Expected: 0 warnings, 0 errors.

- [ ] **Step 10.3: Final verification — full test run**

```powershell
dotnet test CodeScanner.sln --logger "console;verbosity=normal"
```

Expected: all tests passed (smoke + models + languages + scanner + report + cli).

- [ ] **Step 10.4: Final verification — manual smoke against the parent repo**

```powershell
dotnet run --no-build --project src/CodeScanner -- ../ --pretty
```

Expected: valid JSON to stdout summarizing files in the parent `Claude_Training` directory; no stderr noise; exit 0.

- [ ] **Step 10.5: Pack the tool to verify packaging works**

```powershell
dotnet pack src/CodeScanner -o ./nupkg
```

Expected: `nupkg/CodeScanner.0.1.0.nupkg` created.

- [ ] **Step 10.6: Commit**

```powershell
git -C C:/Cmm-testing/Claude_Training add hackathon/README.md
git -C C:/Cmm-testing/Claude_Training commit -m "docs(scanner): add README with build/run/test instructions"
```

---

## Self-Review Checklist (executed before handoff)

- **Spec coverage:** Each spec section is implemented:
  - CLI surface → Task 9
  - Default skipped dirs → Task 7 `DefaultExcludedDirs` (includes `bin`, `obj` per spec)
  - Exit codes → Task 9 `Execute`
  - Output JSON shape → Task 8
  - Forward-slash path normalization → Task 8
  - Binary detection (8 KB sniff) → Task 4
  - Line counting (raw, last-partial-line) → Task 5
  - Per-file flow → Task 6
  - Edge cases table → covered across Task 4-9 tests
  - Project structure → Task 1
  - xUnit + project reference + warnings as errors → Task 1
  - End-to-end CLI test via Process.Start → Task 9
- **Placeholder scan:** No "TBD"/"TODO"/"add tests for above" patterns; every code-mutating step shows the code; every command shows expected output.
- **Type consistency:** Method signatures referenced in later tasks match earlier definitions:
  - `Languages.Classify(string ext)` — defined Task 3, used Task 6.
  - `Scanner.IsBinary(string path)` — defined Task 4, used Task 6.
  - `Scanner.CountLines(string path)` — defined Task 5, used Task 6.
  - `Scanner.ProcessFile(string path)` returns `(FileEntry, ScanError?)` — defined Task 6, used Task 7.
  - `Scanner.Scan(string root, ScanOptions options)` returns `ScanResult` — defined Task 7, used Task 9.
  - `Report.Serialize(ScanResult, bool pretty)` returns `string` — defined Task 8, used Task 9.
  - `ScanResult`/`FileEntry`/`ScanError`/`ScanOptions` records — defined Task 2, used everywhere after.
