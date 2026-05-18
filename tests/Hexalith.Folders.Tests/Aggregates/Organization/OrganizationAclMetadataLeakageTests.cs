using System.Text.Json;
using Hexalith.Folders.Aggregates.Organization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Organization;

public sealed class OrganizationAclMetadataLeakageTests
{
    [Theory]
    [InlineData("credential-secret-value")]
    [InlineData("provider-token-value")]
    [InlineData("repository-name-value")]
    [InlineData("branch-name-value")]
    [InlineData("file content sentinel")]
    [InlineData("diff --git a/secret b/secret")]
    [InlineData("generated-context-payload")]
    [InlineData("unauthorized-resource-name")]
    [InlineData("user@example.test")]
    public void EventsAndResultsShouldNotContainForbiddenSentinelValues(string forbidden)
    {
        GrantOrganizationAclPrincipal command = AclCommandFactory.Grant(
            principalId: "principal-a",
            correlationId: "correlation-a",
            taskId: "task-a",
            idempotencyKey: "idempotency-a");

        OrganizationAclResult result = OrganizationAggregate.Handle(OrganizationState.Empty, command);

        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain(forbidden, Case.Insensitive);
        serialized.ShouldContain("tenant-a");
        serialized.ShouldContain("organization-a");
        serialized.ShouldContain("principal-a");
        serialized.ShouldContain("read_metadata");
    }
}
