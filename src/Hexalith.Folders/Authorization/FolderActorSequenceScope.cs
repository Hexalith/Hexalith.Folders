using System.Threading;

namespace Hexalith.Folders.Authorization;

/// <summary>Exposes a verified folder actor head to authorization during one command invocation.</summary>
public static class FolderActorSequenceScope
{
    private static readonly AsyncLocal<SequenceObservation?> Current = new();

    /// <summary>Enters a folder actor scope for the current asynchronous flow.</summary>
    public static IDisposable Enter(string tenantId, string folderId, long sequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderId);
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        SequenceObservation? previous = Current.Value;
        Current.Value = new SequenceObservation(tenantId, folderId, sequence);
        return new Scope(previous);
    }

    /// <summary>Gets the current head only when both actor identity segments match.</summary>
    public static bool TryGet(string tenantId, string folderId, out long sequence)
    {
        SequenceObservation? current = Current.Value;
        if (current is not null
            && string.Equals(current.TenantId, tenantId, StringComparison.Ordinal)
            && string.Equals(current.FolderId, folderId, StringComparison.Ordinal))
        {
            sequence = current.Sequence;
            return true;
        }

        sequence = 0;
        return false;
    }

    private sealed record SequenceObservation(string TenantId, string FolderId, long Sequence);

    private sealed class Scope(SequenceObservation? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
