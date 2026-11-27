using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Folders.Aggregates.Organization;

namespace Hexalith.Folders.EventStore;

/// <summary>Trusted canonical intent adapter for ConfigureProviderBinding.</summary>
internal sealed class ConfigureProviderBindingIdempotencyIntentAdapter : IDeclaredIdempotencyIntentAdapter
{
    /// <inheritdoc />
    public string CommandType => "Hexalith.Folders.Commands.ConfigureProviderBinding";

    /// <inheritdoc />
    public string AdapterId => FoldersCanonicalIntentBuilder.AdapterId;

    /// <inheritdoc />
    public string OperationId => "configure-provider-binding";

    /// <inheritdoc />
    public int DescriptorVersion => FoldersCanonicalIntentBuilder.DescriptorVersion;

    /// <inheritdoc />
    public IdempotencyReplayRetentionTier RetentionTier => IdempotencyReplayRetentionTier.Mutation;

    /// <inheritdoc />
    public CommandIntentDeclaration Declaration => FoldersCommandIntentDeclarations.For(CommandType);

    /// <inheritdoc />
    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command)
    {
        using JsonDocument document = FoldersCanonicalIntentBuilder.ParsePayload(command);
        JsonElement root = document.RootElement;
        string? providerBindingRef = FoldersCanonicalIntentBuilder.ReadString(root, "providerBindingRef");
        if (string.IsNullOrWhiteSpace(providerBindingRef)
            || string.Equals(command.AggregateId, command.Tenant, StringComparison.Ordinal)
            || !string.Equals(command.AggregateId, ProviderBindingActorId.Create(providerBindingRef), StringComparison.Ordinal))
        {
            throw new JsonException("ConfigureProviderBinding requires the reserved provider-binding actor aggregate identifier.");
        }

        using JsonDocument? normalized = FoldersCanonicalIntentBuilder.IsDecoratedV2Payload(root)
            ? FoldersCanonicalIntentBuilder.BuildNormalizedV2Guard(root, command.AggregateId, stripFolderId: false)
            : null;
        return FoldersCanonicalIntentBuilder.Create(
            command,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["capability_profile_ref"] = FoldersCanonicalIntentBuilder.ReadString(root, "capabilityProfileRef"),
                ["non_secret_credential_reference"] = FoldersCanonicalIntentBuilder.ReadString(root, "nonSecretCredentialReference"),
                ["provider_binding_ref"] = providerBindingRef,
                ["provider_family_ref"] = FoldersCanonicalIntentBuilder.ReadString(root, "providerFamilyRef"),
            },
            canonicalGuardRoot: normalized?.RootElement);
    }
}
