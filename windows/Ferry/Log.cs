namespace Ferry;

/// <summary>
/// A short diary of what Ferry noticed, in %LOCALAPPDATA%\Ferry\Ferry.log.
/// Drags and drops happen between other apps and Windows, so this is the
/// way to find out where one went wrong on someone's PC.
/// </summary>
internal static class Log
{
    private const long MaxBytes = 512 * 1024;

    private static readonly string FilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ferry", "Ferry.log");
    private static readonly Lock Gate = new();

    public static void Start()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
            {
                File.Delete(FilePath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
        Write($"Ferry {typeof(Log).Assembly.GetName().Version?.ToString(3)} started on {Environment.OSVersion.VersionString}");
    }

    public static void Write(string message)
    {
        lock (Gate)
        {
            try
            {
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
