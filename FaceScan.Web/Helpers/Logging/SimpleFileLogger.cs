using System.Text;

namespace FaceScan.Web.Helpers.Logging;

public class SimpleFileLogger : ILogger
{
    private static readonly object FileWriteLock = new();
    private readonly string _categoryName;
    private readonly string _logDirectory;

    public SimpleFileLogger(string categoryName, string logDirectory)
    {
        _categoryName = categoryName;
        _logDirectory = logDirectory;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var logLine = new StringBuilder()
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            .Append(" [").Append(logLevel).Append("] ")
            .Append(_categoryName).Append(": ")
            .Append(message);

        if (exception is not null)
        {
            logLine.Append(" | ").Append(exception);
        }

        try
        {
            Directory.CreateDirectory(_logDirectory);
            var content = logLine + Environment.NewLine;
            var primaryPath = Path.Combine(_logDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");

            if (TryWrite(primaryPath, content))
            {
                return;
            }

            // When another process holds a restrictive lock, write to a process-specific fallback file.
            var fallbackPath = Path.Combine(_logDirectory, $"app-{DateTime.Now:yyyyMMdd}-{Environment.ProcessId}.log");
            TryWrite(fallbackPath, content);
        }
        catch (Exception)
        {
            // Best-effort logging only: never break request flow on log issues.
        }
    }

    private static bool TryWrite(string filePath, string content)
    {
        try
        {
            lock (FileWriteLock)
            {
                using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(stream, Encoding.UTF8);
                writer.Write(content);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
