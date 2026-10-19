using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Projections.OperationalStatus;

namespace Hexalith.Folders.Queries.ProviderReadiness;

/// <summary>Binds a full configuration to its original configured-event position and bytes.</summary>
/// <param name="Sequence">The configured event's source position.</param>
/// <param name="SourceDigest">The original configured-event digest.</param>
/// <param name="ValueDigest">Canonical evidence for the complete source-folded configuration.</param>
public sealed record ProviderBindingConfigurationEvidence(long Sequence, string SourceDigest, string ValueDigest)
{
    /// <summary>Derives configuration evidence solely from an original configured event.</summary>
    public static ProviderBindingConfigurationEvidence Create(long sequence, string sourceDigest, ProviderBindingConfigured configured)
    {
        ArgumentNullException.ThrowIfNull(configured);
        return new(sequence, sourceDigest, OperationalStatusIntegrity.Digest(
            OrganizationState.Empty.Apply([configured]).ProviderBindings[configured.ProviderBindingRef]));
    }
}
