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
}
