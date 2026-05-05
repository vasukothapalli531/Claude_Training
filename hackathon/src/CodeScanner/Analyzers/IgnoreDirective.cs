namespace CodeScanner;

public static class IgnoreDirective
{
    private const string Token = "codescan:ignore";

    public static bool HasIgnore(string line)
    {
        var trimmed = line.TrimEnd();
        if (!trimmed.EndsWith(Token, StringComparison.Ordinal))
        {
            return false;
        }

        var idx = trimmed.Length - Token.Length;
        var before = trimmed.AsSpan(0, idx).TrimEnd();
        if (before.Length == 0)
        {
            return false;
        }

        return before.EndsWith("//", StringComparison.Ordinal)
            || before.EndsWith("#",  StringComparison.Ordinal)
            || before.EndsWith("--", StringComparison.Ordinal)
            || before.EndsWith(";",  StringComparison.Ordinal);
    }
}
