namespace Hexalith.Folders.Authorization;

public sealed class TenantAccessOptions
{
    public const string SectionName = "Folders:TenantAccess";

    public TimeSpan MutationFreshnessBudget { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan DiagnosticStalenessBudget { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Tolerance applied when comparing an event's <c>Timestamp</c> against the local UTC clock.
    /// A producer clock running slightly ahead of the projection host is normal; flagging any
    /// future-dated event as malformed would brick the tenant on benign drift.
    /// </summary>
    public TimeSpan ClockSkewTolerance { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Number of read-modify-write attempts the projection handler will make before surfacing
    /// a <see cref="Hexalith.Folders.Projections.TenantAccess.TenantAccessConcurrencyException"/>.
    /// </summary>
    public int ConcurrencyRetryAttempts { get; set; } = 3;
}
