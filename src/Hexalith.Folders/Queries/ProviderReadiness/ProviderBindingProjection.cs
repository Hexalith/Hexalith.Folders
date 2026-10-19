using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.ProviderReadiness;

/// <summary>Source-ordered provider configuration metadata; never contains resolved credentials.</summary>
/// <param name="TenantId">The managed tenant.</param>
/// <param name="ActorId">The reserved provider-binding actor.</param>
/// <param name="LastSequence">The last applied source sequence.</param>
/// <param name="Binding">The latest source-folded configuration, if configured.</param>
/// <param name="Messages">Retained source identities and payload digests.</param>
/// <param name="Configurations">Full configuration evidence at every configured-event position.</param>
public sealed record ProviderBindingProjection(string TenantId, string ActorId, long LastSequence,
    OrganizationProviderBinding? Binding, IReadOnlyList<FolderProjectionMessage> Messages,
    IReadOnlyList<ProviderBindingConfigurationEvidence> Configurations);
