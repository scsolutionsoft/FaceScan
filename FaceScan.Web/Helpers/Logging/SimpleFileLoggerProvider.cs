namespace FaceScan.Web.Helpers.Logging;

public class SimpleFileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;

    public SimpleFileLoggerProvider(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new SimpleFileLogger(categoryName, _logDirectory);
    }

    public void Dispose()
    {
    }
}
