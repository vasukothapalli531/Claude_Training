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
