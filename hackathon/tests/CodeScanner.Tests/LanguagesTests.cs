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
