namespace Hexalith.Folders.Authorization;

public sealed record EffectivePermissionEvidenceRow(
    EffectivePermissionEvidenceSource Source,
    EffectivePermissionPrincipal Principal,
    string Action,
    long Sequence,
    DateTimeOffset EffectiveAt);
