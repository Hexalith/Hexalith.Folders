using System.Text.Json;
using Hexalith.Folders.Aggregates.Organization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Organization;

public sealed class OrganizationProviderBindingValidationTests
{
    [Theory]
    [InlineData("github", OrganizationProviderBindingResultCode.Accepted)]
    [InlineData("forgejo", OrganizationProviderBindingResultCode.Accepted)]
    [InlineData("gitlab", OrganizationProviderBindingResultCode.Accepted)]
    [InlineData("bitbucket", OrganizationProviderBindingResultCode.Accepted)]
    [InlineData("azure-devops", OrganizationProviderBindingResultCode.Accepted)]
    [InlineData("GitHub", OrganizationProviderBindingResultCode.UnsupportedProviderKind)]
    [InlineData("github-enterprise-old", OrganizationProviderBindingResultCode.UnsupportedProviderKind)]
    [InlineData("", OrganizationProviderBindingResultCode.UnsupportedProviderKind)]
    public void ProviderKindValidationShouldBeStableAndExtensible(string providerKind, OrganizationProviderBindingResultCode expected)
    {
        OrganizationProviderBindingResult result = OrganizationAggregate.Handle(
            OrganizationState.Empty,
            ProviderBindingCommandFactory.Configure(providerKind: providerKind));

        result.Code.ShouldBe(expected);
    }

    [Theory]
    [InlineData("provider binding with spaces", OrganizationProviderBindingResultCode.InvalidProviderBindingReference)]
    [InlineData("credential/ref", OrganizationProviderBindingResultCode.InvalidCredentialReference)]
    [InlineData("system", OrganizationProviderBindingResultCode.ReservedTenant)]
    [InlineData("organization with spaces", OrganizationProviderBindingResultCode.InvalidOrganization)]
    [InlineData("tenant with spaces", OrganizationProviderBindingResultCode.InvalidTenant)]
    public void InvalidIdentifiersShouldRejectBeforeMutation(string value, OrganizationProviderBindingResultCode expected)
    {
        ConfigureProviderBinding command = expected switch
        {
            OrganizationProviderBindingResultCode.InvalidProviderBindingReference =>
                ProviderBindingCommandFactory.Configure(providerBindingRef: value),
            OrganizationProviderBindingResultCode.InvalidCredentialReference =>
                ProviderBindingCommandFactory.Configure(credentialReferenceId: value),
            OrganizationProviderBindingResultCode.InvalidOrganization =>
                ProviderBindingCommandFactory.Configure(organizationId: value),
            OrganizationProviderBindingResultCode.InvalidTenant =>
                ProviderBindingCommandFactory.Configure(managedTenantId: value),
            _ => ProviderBindingCommandFactory.Configure(managedTenantId: value),
        };

        OrganizationProviderBindingResult result = OrganizationAggregate.Handle(OrganizationState.Empty, command);

        result.Code.ShouldBe(expected);
        result.Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("principal with spaces")]
    [InlineData("correlation with spaces")]
    [InlineData("task with spaces")]
    [InlineData("idempotency with spaces")]
    public void MalformedActorAndRequestEvidenceShouldRejectBeforeMutation(string value)
    {
        ConfigureProviderBinding command = value switch
        {
            "principal with spaces" => ProviderBindingCommandFactory.Configure(actorPrincipalId: value),
            "correlation with spaces" => ProviderBindingCommandFactory.Configure(correlationId: value),
            "task with spaces" => ProviderBindingCommandFactory.Configure(taskId: value),
            _ => ProviderBindingCommandFactory.Configure(idempotencyKey: value),
        };

        OrganizationProviderBindingResult result = OrganizationAggregate.Handle(OrganizationState.Empty, command);

        result.Code.ShouldBe(OrganizationProviderBindingResultCode.MalformedEvidence);
        result.Events.ShouldBeEmpty();
        JsonSerializer.Serialize(result).ShouldNotContain(value, Case.Insensitive);
    }

    [Theory]
    [InlineData("Bearer abcdefghijklmnopqrstuvwxyz")]
    [InlineData("-----BEGIN PRIVATE KEY-----")]
    [InlineData("aaaaaaaaaa.bbbbbbbbbb.cccccccccc")]
    [InlineData("https://user:password@example.test/repo.git")]
    [InlineData("AccountKey=credential-secret-value")]
    [InlineData("diff --git a/secret b/secret")]
    [InlineData("{\"clientSecret\":\"credential-secret-value\"}")]
    [InlineData("generated-context-payload")]
    [InlineData("AKIASYNTHETIC0000000")]
    [InlineData("synthetic_pat_SENTINEL_NEVER_USABLE_0000000000")]
    [InlineData("FILE_CONTENT_SYNTHETIC_NEVER_ECHO")]
    [InlineData("PROVIDER_PAYLOAD_SYNTHETIC_BODY")]
    [InlineData("UNAUTHORIZED_RESOURCE_SYNTHETIC_EXISTS_HINT")]
    [InlineData("DIFF_SYNTHETIC_REMOVED_LINE_MARKER")]
    public void SecretShapedValuesShouldRejectAndNeverAppearInResult(string forbidden)
    {
        ConfigureProviderBinding command = ProviderBindingCommandFactory.Configure(
            namingPolicy: ProviderBindingCommandFactory.Policy("naming-policy-a", ("prefix", forbidden)));

        OrganizationProviderBindingResult result = OrganizationAggregate.Handle(OrganizationState.Empty, command);

        result.Code.ShouldBeOneOf(
            OrganizationProviderBindingResultCode.ForbiddenCredentialMaterial,
            OrganizationProviderBindingResultCode.InvalidPolicy);
        result.Events.ShouldBeEmpty();
        JsonSerializer.Serialize(result).ShouldNotContain(forbidden, Case.Insensitive);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("secret")]
    [InlineData("token")]
    [InlineData("clientSecret")]
    public void SensitiveNestedMetadataKeysShouldRejectWithoutLeakingValue(string key)
    {
        ConfigureProviderBinding command = ProviderBindingCommandFactory.Configure(
            branchPolicy: ProviderBindingCommandFactory.Policy("branch-policy-a", (key, "credential-secret-value")));

        OrganizationProviderBindingResult result = OrganizationAggregate.Handle(OrganizationState.Empty, command);

        result.Code.ShouldBe(OrganizationProviderBindingResultCode.ForbiddenCredentialMaterial);
        JsonSerializer.Serialize(result).ShouldNotContain("credential-secret-value", Case.Insensitive);
    }

    [Fact]
    public void ValidPolicySyntaxShouldAllowBranchRefPatternsWithoutProviderCalls()
    {
        ConfigureProviderBinding command = ProviderBindingCommandFactory.Configure(
            branchPolicy: ProviderBindingCommandFactory.Policy("branch-policy-a", ("default_ref", "refs/heads/*")));

        OrganizationProviderBindingResult result = OrganizationAggregate.Handle(OrganizationState.Empty, command);

        result.Code.ShouldBe(OrganizationProviderBindingResultCode.Accepted);
    }
}
