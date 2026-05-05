namespace CodeScanner.Tests;

public class SmokeTests
{
    [Fact]
    public async Task ProgramMain_NoArgs_ExitsNonZero()
    {
        var exit = await Program.Main(Array.Empty<string>());
        Assert.NotEqual(0, exit);
    }
}
