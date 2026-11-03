using Hexalith.Folders.Queries.Folders;

using Microsoft.Extensions.Logging;

namespace Hexalith.Folders.Tests.Queries.Folders;

/// <summary>Captures primitive diagnostic fields and exercises failing logging providers.</summary>
internal sealed class TaskStatusDiagnosticLogger(string mode = "enabled") : ILogger<TaskStatusQueryHandler>
{
    /// <summary>Gets the structured records accepted by the provider.</summary>
    internal List<Dictionary<string, object?>> Entries { get; } = [];

    /// <summary>Gets the number of exception objects passed to the provider.</summary>
    internal int ExceptionObjects { get; private set; }

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => mode == "throw_is_enabled"
        ? throw new InvalidOperationException("provider-private-message") : mode != "disabled";

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (mode == "throw_log") throw new InvalidOperationException("provider-private-message");
        if (exception is not null) ExceptionObjects++;
        Entries.Add(((IEnumerable<KeyValuePair<string, object?>>)(object)state!)
            .ToDictionary(item => item.Key, item => item.Value));
    }
}
