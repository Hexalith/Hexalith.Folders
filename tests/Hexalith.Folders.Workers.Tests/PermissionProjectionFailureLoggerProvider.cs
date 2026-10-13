using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Workers.Tests;

/// <summary>Captures only application permission-callback logs for metadata-only assertions.</summary>
internal sealed class PermissionProjectionFailureLoggerProvider : ILoggerProvider, ILogger
{
    /// <summary>Gets captured log metadata, structured fields, and formatted messages.</summary>
    internal List<(LogLevel Level, EventId EventId, IReadOnlyDictionary<string, object?> Fields,
        Exception? Exception, string Message)> Entries { get; } = [];

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
        => categoryName == typeof(FoldersWorkersModule).FullName ? this : NullLogger.Instance;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Add((logLevel, eventId,
            ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(
                item => item.Key, item => item.Value, StringComparer.Ordinal),
            exception, formatter(state, exception)));

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
