using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService;

namespace Task36FoldObserver;

/// <summary>Delegates the complete production handler interface while observing callback entry.</summary>
public class ObservedProjectionHandler : DispatchProxy
{
    private static readonly object Sync = new();
    /// <summary>Gets or sets the unmodified production handler.</summary>
    public IAsyncDomainSharedProjectionStreamingRebuildHandler Inner { get; set; } = null!;

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        if (targetMethod.Name == "FoldCatchUpAsync")
        {
            ArgumentNullException.ThrowIfNull(args);
            var delivery = (SharedProjectionDelivery)args[0]!;
            Record("fold", delivery.SourceStream, delivery.EnvelopePosition);
        }
        try { return targetMethod.Invoke(Inner, args); }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    internal static void Record(string action, string? source, long? position)
    {
        string path = Environment.GetEnvironmentVariable("FOLDERS_TASK36_FOLD_LOG")
            ?? throw new InvalidOperationException("Qualification fold sink is required.");
        lock (Sync)
            File.AppendAllText(path, JsonSerializer.Serialize(new { action, source, position, processId = Environment.ProcessId }) + "\n");
    }
}
