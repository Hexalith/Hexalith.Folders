using Hexalith.EventStore.Client.Subscriptions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.Folders.Workers;

/// <summary>Requires an explicitly configured isolated inbound component outside Development.</summary>
/// <param name="configuration">The Workers subscription configuration.</param>
/// <param name="environment">The hosting environment.</param>
internal sealed class FoldersTenantEventSubscriptionOptionsValidator(
    IConfiguration configuration, IHostEnvironment environment)
    : IValidateOptions<EventStoreDomainEventsOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, EventStoreDomainEventsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string? configured = configuration["Folders:Workers:PubSubName"];
        if (!environment.IsDevelopment()
            && (string.IsNullOrWhiteSpace(configured)
                || string.Equals(configured, FoldersWorkersModule.TenantEventsPubSubName, StringComparison.Ordinal)))
        {
            return ValidateOptionsResult.Fail(
                "Folders:Workers:PubSubName must name an isolated inbound subscription component outside Development.");
        }

        if (!string.Equals(options.PubSubName, FoldersWorkersModule.SubscriptionPubSubName(configuration), StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail("Tenants PubSubName must match the configured Workers subscription component.");
        }

        if (!string.Equals(options.TopicName, FoldersWorkersModule.TenantEventsTopicName, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail($"Tenants TopicName must be '{FoldersWorkersModule.TenantEventsTopicName}'.");
        }

        if (!string.Equals(options.SubscriptionRoute, FoldersWorkersModule.TenantEventsRoute, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail($"Tenants SubscriptionRoute must be '{FoldersWorkersModule.TenantEventsRoute}'.");
        }

        return ValidateOptionsResult.Success;
    }
}
