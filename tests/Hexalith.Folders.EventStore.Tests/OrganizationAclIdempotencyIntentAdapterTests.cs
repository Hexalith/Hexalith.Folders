using System.Text;
using System.Text.Json;

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

    [Theory]
    [InlineData("initialize-organization-acl-baseline")]
    [InlineData("grant-organization-acl-principal")]
    [InlineData("revoke-organization-acl-principal")]
    public void DelegatedServiceWireTokenAndNamedKindShouldHaveOneCanonicalIntent(string operationId)
    {
        OrganizationAclIdempotencyIntentAdapter adapter = new("acl-command", operationId);
        IdempotencyCanonicalIntent wire = adapter.CreateIntent(Command(
            """{"operations":[{"principalKind":"delegated_service_agent","principalId":"agent-a","action":"read_metadata"}]}"""));
        IdempotencyCanonicalIntent named = adapter.CreateIntent(Command(
            """{"operations":[{"principalKind":"DelegatedServiceAgent","principalId":"agent-a","action":"read_metadata"}]}"""));

        Encoding.UTF8.GetString(wire.SemanticPayload).ShouldBe(Encoding.UTF8.GetString(named.SemanticPayload));
        wire.SemanticOptions!["semantic_payload_sha256"].ShouldBe(named.SemanticOptions!["semantic_payload_sha256"]);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    [InlineData("unknown")]
    public void NumericOrUnknownPrincipalKindShouldFailAdmission(string principalKind)
    {
        OrganizationAclIdempotencyIntentAdapter adapter = new("acl-command", "initialize-organization-acl-baseline");
        string payload = JsonSerializer.Serialize(new
        {
            operations = new[] { new { principalKind, principalId = "agent-a", action = "read_metadata" } },
        });

        Should.Throw<JsonException>(() => adapter.CreateIntent(Command(payload)));
    }

    private static IdempotencyIntentCommand Command(string payload)
        => new("Hexalith.Folders.Commands.InitializeOrganizationAclBaseline",
            "tenant-a", "folders", "tenant-a", Encoding.UTF8.GetBytes(payload), null);
}
