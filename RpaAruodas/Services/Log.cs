using System;
using System.Globalization;
using System.IO;

namespace RpaAruodas.Services;

public interface ILogService
{
    void Info(string message);
    void Error(string message, Exception? exception = null);
}

public class LogService : ILogService
{
    private readonly string _logDirectory;
    private readonly object _sync = new();

    public LogService()
    {
        _logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(_logDirectory);
    }

    private string CurrentFilePath =>
        Path.Combine(_logDirectory, $"logo - {DateTime.Now.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture)}.txt");

    public void Info(string message) => WriteEntry("INFO", message);

    public void Error(string message, Exception? exception = null)
    {
        var suffix = exception is null ? string.Empty : $" | {exception.GetType().Name}: {exception.Message}";
        WriteEntry("ERROR", $"{message}{suffix}");
    }

    private void WriteEntry(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
        lock (_sync)
        {
            File.AppendAllText(CurrentFilePath, line + Environment.NewLine);
        }
    }
}
