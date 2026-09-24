using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace Hexalith.Folders.IntegrationTests.EndToEnd;

internal sealed class RecordingPd10LoggerProvider : ILoggerProvider, ILogger
{
    public ConcurrentQueue<(EventId EventId, string Message, Exception? Exception)> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => this;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Entries.Enqueue((eventId, formatter(state, exception), exception));

    public void Dispose()
    {
    }
}
