using System.Threading;

namespace Hexalith.Folders.Authorization;

/// <summary>Exposes the trusted current actor head to authorization within one domain invocation.</summary>
public static class OrganizationActorSequenceScope
{
    private static readonly AsyncLocal<SequenceObservation?> Current = new();

    public static IDisposable Enter(string tenantId, string organizationId, long sequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationId);
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        SequenceObservation? previous = Current.Value;
        Current.Value = new SequenceObservation(tenantId, organizationId, sequence);
        return new Scope(previous);
    }

    public static bool TryGet(string tenantId, string organizationId, out long sequence)
    {
        SequenceObservation? current = Current.Value;
        if (current is not null
            && string.Equals(current.TenantId, tenantId, StringComparison.Ordinal)
            && string.Equals(current.OrganizationId, organizationId, StringComparison.Ordinal))
        {
            sequence = current.Sequence;
            return true;
        }

        sequence = 0;
        return false;
    }

    private sealed record SequenceObservation(string TenantId, string OrganizationId, long Sequence);

    private sealed class Scope(SequenceObservation? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
