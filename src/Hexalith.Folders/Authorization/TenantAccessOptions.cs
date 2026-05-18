namespace Hexalith.Folders.Authorization;

public sealed class TenantAccessOptions
{
    public TimeSpan MutationFreshnessBudget { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan DiagnosticStalenessBudget { get; init; } = TimeSpan.FromMinutes(30);
}
