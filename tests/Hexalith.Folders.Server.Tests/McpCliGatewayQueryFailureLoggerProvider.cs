using System.Collections.Concurrent;

using Hexalith.Folders.Queries.ContextSearch;
using Hexalith.Folders.Queries.FolderAccess;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Queries.ProviderReadiness;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Records structured diagnostic fields and exception arguments from callback queries.</summary>
internal sealed class McpCliGatewayQueryFailureLoggerProvider : ILoggerProvider, ILogger
{
    private static readonly HashSet<string?> _categories =
    [
        typeof(ContextSearchQueryHandler).FullName,
        typeof(FolderIndexingStatusQueryHandler).FullName,
        typeof(ListFolderAclEntriesQueryHandler).FullName,
        typeof(BranchRefPolicyQueryHandler).FullName,
        typeof(WorkspaceCleanupStatusQueryHandler).FullName,
        typeof(WorkspaceLockStatusQueryHandler).FullName,
        typeof(WorkspaceStatusQueryHandler).FullName,
        typeof(GetProviderBindingQueryHandler).FullName,
    ];

    /// <summary>Gets warning logs from the domain query handlers under test.</summary>
    internal ConcurrentQueue<(object? Type, Exception? Exception, string Message)> Entries { get; } = new();

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName)
        => _categories.Contains(categoryName) ? this : NullLogger.Instance;

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        object? type = (state as IEnumerable<KeyValuePair<string, object?>>)?
            .FirstOrDefault(field => field.Key == "ExceptionType").Value;
        Entries.Enqueue((type, exception, formatter(state, exception)));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}
