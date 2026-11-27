using Hexalith.Folders.Queries.Folders;

using Microsoft.Extensions.Logging;

namespace Hexalith.Folders.Tests.Queries.Folders;

/// <summary>Captures lifecycle diagnostic fields and any exception passed to a log provider.</summary>
internal sealed class FolderLifecycleStatusDiagnosticLogger : ILogger<FolderLifecycleStatusQueryHandler>
{
    /// <summary>Gets the diagnostic fields passed to the logger.</summary>
    internal IReadOnlyDictionary<string, object?> Fields { get; private set; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>Gets the exception passed separately from the structured diagnostic state.</summary>
    internal Exception? Exception { get; private set; }

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Fields = ((IEnumerable<KeyValuePair<string, object?>>)(object)state!)
            .ToDictionary(static field => field.Key, static field => field.Value, StringComparer.Ordinal);
        Exception = exception;
    }
}
