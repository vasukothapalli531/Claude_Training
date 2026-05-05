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
        bytes[10000] = 0x00;
        var path = tree.WriteBytes("late.dat", bytes);

        Assert.False(Scanner.IsBinary(path));
    }

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
}
