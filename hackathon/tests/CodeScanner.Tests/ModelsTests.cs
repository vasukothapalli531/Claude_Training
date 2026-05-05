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
