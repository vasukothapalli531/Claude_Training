namespace CodeScanner;

public static class Scanner
{
    private const int BinarySniffBytes = 8192;

    public static bool IsBinary(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: BinarySniffBytes,
            options: FileOptions.SequentialScan);

        Span<byte> buffer = stackalloc byte[BinarySniffBytes];
        var read = stream.Read(buffer);
        for (var i = 0; i < read; i++)
        {
            if (buffer[i] == 0x00)
            {
                return true;
            }
        }
        return false;
    }
}
