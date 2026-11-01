using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Folders.Routing;

namespace Hexalith.Folders.EventStore;

/// <summary>Trusted canonical intent adapter for ConfigureProviderBinding.</summary>
internal sealed class ConfigureProviderBindingIdempotencyIntentAdapter : IDeclaredIdempotencyIntentAdapter
{
    /// <inheritdoc/>
    public CommandIntentDeclaration Declaration => FoldersCommandCatalog.Commands.Single(declaration => declaration.Route.MessageType == CommandType);

    /// <inheritdoc/>
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
    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command)
    {
        using JsonDocument document = FoldersCanonicalIntentBuilder.ParsePayload(command);
        JsonElement root = document.RootElement;
        return FoldersCanonicalIntentBuilder.Create(
            command,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["capability_profile_ref"] = FoldersCanonicalIntentBuilder.ReadString(root, "capabilityProfileRef"),
                ["non_secret_credential_reference"] = FoldersCanonicalIntentBuilder.ReadString(root, "nonSecretCredentialReference"),
                ["provider_binding_ref"] = FoldersCanonicalIntentBuilder.ReadString(root, "providerBindingRef"),
                ["provider_family_ref"] = FoldersCanonicalIntentBuilder.ReadString(root, "providerFamilyRef"),
            });
    }
}
