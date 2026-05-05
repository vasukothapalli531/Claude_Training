namespace CodeScanner.Tests;

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
