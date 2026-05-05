namespace CodeScanner.Tests.Analyzers;

public class IgnoreDirectiveTests
{
    [Theory]
    [InlineData("var x = \"AKIA....\"; // codescan:ignore", true)]
    [InlineData("var x = \"AKIA....\"; # codescan:ignore", true)]
    [InlineData("var x = \"AKIA....\"; -- codescan:ignore", true)]
    [InlineData("var x = \"AKIA....\"; ; codescan:ignore", true)]
    [InlineData("var x = \"AKIA....\";", false)]
    [InlineData("var x = \"AKIA....\"; // CODESCAN:IGNORE", false)]
    [InlineData("var x = \"AKIA....\"; // codescan:ignore something else", false)]
    public void HasIgnore_DetectsTrailingDirective(string line, bool expected)
    {
        Assert.Equal(expected, IgnoreDirective.HasIgnore(line));
    }
}
