namespace CodeScanner.Tests.Html;

public class TemplateRendererTests
{
    [Fact]
    public void Render_SubstitutesAllPlaceholders()
    {
        var template = "<h1>{{title}}</h1><p>{{root}}</p>";
        var result = TemplateRenderer.Render(template, new Dictionary<string, string>
        {
            ["title"] = "Hello",
            ["root"] = "/x",
        });

        Assert.Equal("<h1>Hello</h1><p>/x</p>", result);
    }

    [Fact]
    public void Render_LeavesUnknownPlaceholdersUntouched()
    {
        var template = "{{a}} {{b}}";
        var result = TemplateRenderer.Render(template, new Dictionary<string, string> { ["a"] = "1" });
        Assert.Equal("1 {{b}}", result);
    }

    [Theory]
    [InlineData("plain text", "plain text")]
    [InlineData("a < b", "a &lt; b")]
    [InlineData("a > b", "a &gt; b")]
    [InlineData("Tom & Jerry", "Tom &amp; Jerry")]
    [InlineData("\"quoted\"", "&quot;quoted&quot;")]
    [InlineData("'single'", "&#39;single&#39;")]
    public void HtmlEscape_HandlesAllCharacters(string input, string expected)
    {
        Assert.Equal(expected, TemplateRenderer.HtmlEscape(input));
    }

    [Fact]
    public void EscapeForScriptTag_ReplacesClosingScriptTag()
    {
        var json = "{\"snippet\":\"</script><script>alert(1)</script>\"}";
        var escaped = TemplateRenderer.EscapeForScriptTag(json);
        Assert.DoesNotContain("</script>", escaped);
        Assert.Contains("<\\/script>", escaped);
    }

    [Fact]
    public void EscapeForScriptTag_LeavesNormalContentAlone()
    {
        var json = "{\"x\": 42, \"y\": \"hello\"}";
        Assert.Equal(json, TemplateRenderer.EscapeForScriptTag(json));
    }
}
