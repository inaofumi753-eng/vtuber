namespace VtuberBot.Core;

public enum LogLevel
{
    Critical,
    Error,
    Warning,
    Info,
    Debug
}

public interface ILogger
{
    void Info(string message, params object?[] args);
    void Warning(string message, params object?[] args);
    void Error(Exception exception, string message, params object?[] args);
}

public sealed class Logger : ILogger, IDisposable
{
    private const long MaxFileBytes = 1_048_576;
    private const int BackupCount = 2;

    private readonly object gate = new();
    private readonly string logPath;
    private StreamWriter writer;
    private bool disposed;

    public Logger(string directory, LogLevel level)
    {
        Directory.CreateDirectory(directory);
        logPath = Path.Combine(directory, "vtuber.log");
        writer = OpenWriter(append: true);
        Level = level;
    }

    public LogLevel Level { get; }

    public void Info(string message, params object?[] args) =>
        Write(LogLevel.Info, message, args);

    public void Warning(string message, params object?[] args) =>
        Write(LogLevel.Warning, message, args);

    public void Error(Exception exception, string message, params object?[] args) =>
        Write(LogLevel.Error, $"{message} | {exception.Message}", args);

    private void Write(LogLevel level, string message, object?[] args)
    {
        if (level > Level)
            return;

        lock (gate)
        {
            if (disposed)
                return;

            var formatted = args.Length == 0 ? message : string.Format(message, args);
            var line = $"{DateTimeOffset.Now:O} | {level} | {formatted}";

            try
            {
                RotateIfNeeded();
                writer.WriteLine(line);
                writer.Flush();
            }
            catch (IOException)
            {
                TryReopenAfterFileFailure();
            }
            catch (UnauthorizedAccessException)
            {
                TryReopenAfterFileFailure();
            }

            try
            {
                Console.WriteLine(line);
            }
            catch (IOException)
            {
                // A closed or redirected console must not break application logging.
            }
        }
    }

    private void RotateIfNeeded()
    {
        writer.Flush();

        if (!File.Exists(logPath) || new FileInfo(logPath).Length < MaxFileBytes)
            return;

        writer.Dispose();

        var backup2 = logPath + ".2";
        var backup1 = logPath + ".1";

        if (File.Exists(backup2))
            File.Delete(backup2);

        if (File.Exists(backup1))
            File.Move(backup1, backup2);

        File.Move(logPath, backup1);
        writer = OpenWriter(append: false);
    }

    private void TryReopenAfterFileFailure()
    {
        try
        {
            writer.Dispose();
        }
        catch
        {
        }

        try
        {
            writer = OpenWriter(append: true);
        }
        catch
        {
            writer = StreamWriter.Null;
        }
    }

    private StreamWriter OpenWriter(bool append) =>
        new(logPath, append)
        {
            AutoFlush = true
        };

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;

            disposed = true;
            writer.Dispose();
        }
    }
}
