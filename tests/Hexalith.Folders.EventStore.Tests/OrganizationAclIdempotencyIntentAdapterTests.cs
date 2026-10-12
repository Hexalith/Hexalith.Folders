using System.Text;

using Hexalith.EventStore.DomainService;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.EventStore.Tests;

public sealed class OrganizationAclIdempotencyIntentAdapterTests
{
    [Fact]
    public void ReorderedAndDuplicatedBaselineOperationsShouldHaveOneCanonicalIntent()
    {
        OrganizationAclIdempotencyIntentAdapter adapter = new(
            "Hexalith.Folders.Commands.InitializeOrganizationAclBaseline",
            "initialize-organization-acl-baseline");
        IdempotencyCanonicalIntent first = adapter.CreateIntent(Command(
            """{"requestSchemaVersion":"v1","operations":[{"principalKind":"User","principalId":"user-a","action":"create_folder"},{"principalKind":"Role","principalId":"role-a","action":"read_metadata"}]}"""));
        IdempotencyCanonicalIntent repeated = adapter.CreateIntent(Command(
            """{"operations":[{"action":"read_metadata","principalId":"role-a","principalKind":"role"},{"action":"create_folder","principalId":"user-a","principalKind":"user"},{"principalKind":"User","principalId":"user-a","action":"create_folder"}],"requestSchemaVersion":"v1"}"""));

        Encoding.UTF8.GetString(first.SemanticPayload).ShouldBe(Encoding.UTF8.GetString(repeated.SemanticPayload));
        first.SemanticOptions!["semantic_payload_sha256"]
            .ShouldBe(repeated.SemanticOptions!["semantic_payload_sha256"]);
    }

    [Fact]
    public void OppositeOperationIntentShouldNotReplayAsEquivalent()
    {
        OrganizationAclIdempotencyIntentAdapter grant = new(
            "Hexalith.Folders.Commands.GrantOrganizationAclPrincipal",
            "grant-organization-acl-principal");
        OrganizationAclIdempotencyIntentAdapter revoke = new(
            "Hexalith.Folders.Commands.RevokeOrganizationAclPrincipal",
            "revoke-organization-acl-principal");
        IdempotencyIntentCommand command = Command(
            """{"operations":[{"principalKind":"User","principalId":"user-a","action":"create_folder"}]}""");

        IdempotencyCanonicalIntent granted = grant.CreateIntent(command with { CommandType = grant.CommandType });
        IdempotencyCanonicalIntent revoked = revoke.CreateIntent(command with { CommandType = revoke.CommandType });
        Encoding.UTF8.GetString(granted.SemanticPayload).ShouldNotBe(Encoding.UTF8.GetString(revoked.SemanticPayload));
    }

    private static IdempotencyIntentCommand Command(string payload)
        => new("Hexalith.Folders.Commands.InitializeOrganizationAclBaseline",
            "tenant-a", "folders", "tenant-a", Encoding.UTF8.GetBytes(payload), null);
}
