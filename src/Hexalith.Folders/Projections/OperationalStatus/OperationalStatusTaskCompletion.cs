using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Queries.Folders;

namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Retains bounded original closure and expiry authority separately from expiring status rows.</summary>
/// <param name="ManagedTenantId">The source tenant.</param>
/// <param name="FolderId">The source folder.</param>
/// <param name="Closure">The earliest source-confirmed task closure.</param>
/// <param name="Sequence">Its original source position.</param>
/// <param name="SourceDigest">Its original source prefix.</param>
public sealed record OperationalStatusTaskCompletion(string ManagedTenantId, string FolderId,
    FolderTaskClosure Closure, long Sequence, string SourceDigest)
{
    /// <summary>Gets integrity evidence for the original closure metadata.</summary>
    public string IntegrityDigest { get; init; } = string.Empty;

    /// <summary>Creates source-derived closure authority, never a refreshed status result.</summary>
    internal static OperationalStatusTaskCompletion Create(string tenantId, string folderId, FolderTaskClosure closure,
        long sequence, string digest)
    {
        OperationalStatusTaskCompletion value = new(tenantId, folderId, closure, sequence, digest);
        return value with { IntegrityDigest = value.Digest() };
    }

    /// <summary>Validates scope, original position and canonical source-derived closure.</summary>
    internal bool Valid(string tenantId, string folderId, string taskId, long head)
        => ManagedTenantId == tenantId && FolderId == folderId && Sequence > 0 && Sequence <= head
            && SourceDigest is { Length: 64 } && Closure is { } closure && closure.TaskId == taskId
            && !string.IsNullOrWhiteSpace(closure.WorkspaceId) && !string.IsNullOrWhiteSpace(closure.TaskId)
            && !string.IsNullOrWhiteSpace(closure.OperationId) && !string.IsNullOrWhiteSpace(closure.CorrelationId)
            && closure.ClosedAt != default && closure.TerminalState is "ready" or "committed" or "native_completed"
            && IntegrityDigest == Digest();

    /// <summary>Rebinds an operational candidate to its original terminal result and expiry.</summary>
    internal TaskStatusReadModelSnapshot Restore(TaskStatusReadModelSnapshot candidate)
    {
        string terminal = Closure.TerminalState == "native_completed" ? "ready" : Closure.TerminalState;
        return candidate with
        {
            CurrentState = terminal,
            TerminalState = terminal,
            LastOperationId = Closure.OperationId,
            LastFailureCategory = null,
            RetryEligibility = new(false, "retry_not_required"),
            Freshness = candidate.Freshness with
            {
                ObservedAt = Closure.ClosedAt,
                ProjectionWatermark = Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            },
            EvidenceScope = candidate.EvidenceScope with { CorrelationId = Closure.CorrelationId },
        };
    }

    private string Digest() => OperationalStatusIntegrity.Digest(new { ManagedTenantId, FolderId, Closure, Sequence, SourceDigest });
}
