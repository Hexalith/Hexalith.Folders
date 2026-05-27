namespace Hexalith.Folders.Aggregates.Folder;

public sealed record BranchRefPolicyMetadata(
    string RepositoryBindingId,
    string PolicyRef,
    string DefaultRef,
    IReadOnlyList<string> AllowedRefPatterns,
    IReadOnlyList<string> ProtectedRefPatterns,
    string ActorPrincipalId,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string IdempotencyFingerprint,
    DateTimeOffset ConfiguredAt);
