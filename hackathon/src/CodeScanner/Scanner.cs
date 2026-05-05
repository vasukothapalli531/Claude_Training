namespace CodeScanner;

public static class Scanner
{
    private const int BinarySniffBytes = 8192;
    private const int LineCountBufferSize = 64 * 1024;

    public static long CountLines(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: LineCountBufferSize,
            options: FileOptions.SequentialScan);

        long newlineCount = 0;
        byte lastByte = 0;
        var totalRead = 0L;
        var buffer = new byte[LineCountBufferSize];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalRead += read;
            for (var i = 0; i < read; i++)
            {
                if (buffer[i] == 0x0A)
                {
                    newlineCount++;
                }
            }
            lastByte = buffer[read - 1];
        }

        if (totalRead == 0)
        {
            return 0;
        }

        return lastByte == 0x0A ? newlineCount : newlineCount + 1;
    }

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
