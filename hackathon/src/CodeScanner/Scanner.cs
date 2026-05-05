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

    public static (FileEntry Entry, ScanError? Error) ProcessFile(string path)
    {
        var extension = Path.GetExtension(path);
        var language = Languages.Classify(extension);

        bool isBinary;
        try
        {
            isBinary = IsBinary(path);
        }
        catch (Exception ex)
        {
            var entryOnError = new FileEntry(path, extension, language, Lines: 0, IsBinary: false);
            return (entryOnError, new ScanError(path, $"{ex.GetType().Name}: {ex.Message}"));
        }

        if (isBinary)
        {
            var binEntry = new FileEntry(path, extension, language, Lines: 0, IsBinary: true);
            return (binEntry, new ScanError(path, "binary file, lines not counted"));
        }

        try
        {
            var lines = CountLines(path);
            return (new FileEntry(path, extension, language, lines, IsBinary: false), null);
        }
        catch (Exception ex)
        {
            var entryOnError = new FileEntry(path, extension, language, Lines: 0, IsBinary: false);
            return (entryOnError, new ScanError(path, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }
}
