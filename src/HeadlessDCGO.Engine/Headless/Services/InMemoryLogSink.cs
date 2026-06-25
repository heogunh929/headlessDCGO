namespace HeadlessDCGO.Engine.Headless.Services;

using System.Collections.ObjectModel;

public sealed class InMemoryLogSink : ILogSink
{
    private readonly List<LogEntry> _entries = new();
    private long _sequence;

    public void Info(string message)
    {
        Record(LogLevel.Info, message, null);
    }

    public void Warn(string message)
    {
        Record(LogLevel.Warn, message, null);
    }

    public void Error(string message, Exception? exception = null)
    {
        Record(LogLevel.Error, message, exception);
    }

    public IReadOnlyList<LogEntry> Snapshot()
    {
        return _entries.ToArray();
    }

    public void Clear()
    {
        _entries.Clear();
        _sequence = 0;
    }

    private void Record(LogLevel level, string message, Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(message);

        _entries.Add(new LogEntry(
            ++_sequence,
            level,
            message,
            exception?.GetType().FullName,
            exception?.Message));
    }
}

public sealed record LogEntry
{
    public LogEntry(
        long sequence,
        LogLevel level,
        string message,
        string? exceptionType = null,
        string? exceptionMessage = null)
    {
        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Log sequence must be positive.");
        }

        ArgumentNullException.ThrowIfNull(message);

        Sequence = sequence;
        Level = level;
        Message = message;
        ExceptionType = string.IsNullOrWhiteSpace(exceptionType) ? null : exceptionType.Trim();
        ExceptionMessage = string.IsNullOrWhiteSpace(exceptionMessage) ? null : exceptionMessage.Trim();
    }

    public long Sequence { get; init; }

    public LogLevel Level { get; init; }

    public string Message { get; init; }

    public string? ExceptionType { get; init; }

    public string? ExceptionMessage { get; init; }
}

public enum LogLevel
{
    Info,
    Warn,
    Error
}
