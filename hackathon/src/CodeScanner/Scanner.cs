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

    public static readonly IReadOnlySet<string> DefaultExcludedDirs =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", "node_modules", "__pycache__",
            ".venv", "venv", ".pytest_cache",
            "dist", "build",
            ".mypy_cache", ".ruff_cache",
            "bin", "obj",
        };

    public static ScanResult Scan(string root, ScanOptions options)
    {
        var entries = new List<FileEntry>();
        var errors = new List<ScanError>();
        var skippedDirs = new HashSet<string>(StringComparer.Ordinal);

        var excludeSet = new HashSet<string>(DefaultExcludedDirs, StringComparer.OrdinalIgnoreCase);
        foreach (var extra in options.ExtraExcludes) { excludeSet.Add(extra); }

        var visitedRealPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Walk(root);

        return new ScanResult(
            Root: root,
            FileEntries: entries,
            SkippedDirs: skippedDirs.OrderBy(s => s, StringComparer.Ordinal).ToList(),
            Errors: errors);

        void Walk(string dir)
        {
            if (options.FollowSymlinks)
            {
                string real;
                try { real = Path.GetFullPath(dir); }
                catch (Exception ex) { errors.Add(new ScanError(dir, $"{ex.GetType().Name}: {ex.Message}")); return; }
                if (!visitedRealPaths.Add(real))
                {
                    errors.Add(new ScanError(dir, "symlink loop"));
                    return;
                }
            }

            string[] subdirs;
            string[] files;
            try
            {
                subdirs = Directory.GetDirectories(dir);
                files   = Directory.GetFiles(dir);
            }
            catch (UnauthorizedAccessException ex)
            {
                errors.Add(new ScanError(dir, $"UnauthorizedAccessException: {ex.Message}"));
                return;
            }
            catch (Exception ex)
            {
                errors.Add(new ScanError(dir, $"{ex.GetType().Name}: {ex.Message}"));
                return;
            }

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                if (info.LinkTarget is not null && !options.FollowSymlinks)
                {
                    continue;
                }
                try
                {
                    var (entry, error) = ProcessFile(file);
                    entries.Add(entry);
                    if (error is not null) { errors.Add(error); }
                }
                catch (Exception ex)
                {
                    errors.Add(new ScanError(file, $"{ex.GetType().Name}: {ex.Message}"));
                }
            }

            foreach (var sub in subdirs)
            {
                var name = Path.GetFileName(sub);
                if (excludeSet.Contains(name))
                {
                    skippedDirs.Add(name);
                    continue;
                }

                var dirInfo = new DirectoryInfo(sub);
                if (dirInfo.LinkTarget is not null && !options.FollowSymlinks)
                {
                    continue;
                }

                Walk(sub);
            }
        }
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
