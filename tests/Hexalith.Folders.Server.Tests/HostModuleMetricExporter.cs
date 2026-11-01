using System.Collections.Concurrent;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Observes subscribed module instruments without asserting OTLP delivery.</summary>
internal sealed class HostModuleMetricExporter : BaseExporter<Metric>
{
    /// <summary>Gets the instrument names collected by the actual provider.</summary>
    internal ConcurrentBag<string> Names { get; } = [];

    /// <summary>Records the bounded instrument names emitted by the provider.</summary>
    /// <param name="batch">The collected metrics.</param>
    /// <returns>A successful local observation.</returns>
    public override ExportResult Export(in Batch<Metric> batch)
    {
        foreach (Metric metric in batch) Names.Add(metric.Name);
        return ExportResult.Success;
    }
}
