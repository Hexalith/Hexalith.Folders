using Hexalith.Folders.Aggregates.Organization;

namespace Hexalith.Folders.Tests.Aggregates.Organization;

internal static class ProviderBindingCommandFactory
{
    public static ConfigureProviderBinding Configure(
        string managedTenantId = "tenant-a",
        string organizationId = "organization-a",
        OrganizationAclPrincipalKind actorPrincipalKind = OrganizationAclPrincipalKind.User,
        string actorPrincipalId = "principal-a",
        string providerBindingRef = "binding-a",
        string providerKind = "github",
        string credentialReferenceId = "credential-a",
        OrganizationProviderBindingPolicy? namingPolicy = null,
        OrganizationProviderBindingPolicy? branchPolicy = null,
        string correlationId = "correlation-a",
        string taskId = "task-a",
        string idempotencyKey = "provider-idempotency-a",
        string? payloadTenantId = null)
        => new(
            managedTenantId,
            organizationId,
            actorPrincipalKind,
            actorPrincipalId,
            providerBindingRef,
            providerKind,
            credentialReferenceId,
            namingPolicy ?? Policy("naming-policy-a", ("prefix", "folders")),
            branchPolicy ?? Policy("branch-policy-a", ("default_branch", "main")),
            correlationId,
            taskId,
            idempotencyKey,
            new DateTimeOffset(2026, 5, 24, 8, 0, 0, TimeSpan.Zero),
            payloadTenantId);

    public static OrganizationProviderBindingPolicy Policy(
        string? policyRef,
        params (string Key, string Value)[] metadata)
        => new(
            policyRef,
            metadata.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal));

    public static OrganizationState StateWithConfigurePermission()
    {
        OrganizationAclResult grant = OrganizationAggregate.Handle(
            OrganizationState.Empty,
            AclCommandFactory.Grant(action: "configure_provider_binding"));

        return OrganizationState.Empty.Apply(grant.Events);
    }
}
