using System.Text.Json;
using Hexalith.Folders.Aggregates.Organization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Organization;

public sealed class OrganizationProviderBindingAggregateTests
{
    [Fact]
    public void AcceptedBindingShouldEmitMetadataOnlyConfigurationEvent()
    {
        ConfigureProviderBinding command = ProviderBindingCommandFactory.Configure();

        OrganizationProviderBindingResult result = OrganizationAggregate.Handle(OrganizationState.Empty, command);

        result.Code.ShouldBe(OrganizationProviderBindingResultCode.Accepted);
        ProviderBindingConfigured configured = result.Events.ShouldHaveSingleItem().ShouldBeOfType<ProviderBindingConfigured>();
        configured.ManagedTenantId.ShouldBe("tenant-a");
        configured.OrganizationId.ShouldBe("organization-a");
        configured.ProviderBindingRef.ShouldBe("binding-a");
        configured.ProviderKind.ShouldBe("github");
        configured.CredentialReferenceId.ShouldBe("credential-a");
        configured.ConfiguredStatus.ShouldBe("configured");
        configured.CorrelationId.ShouldBe("correlation-a");
        configured.TaskId.ShouldBe("task-a");
        configured.IdempotencyKey.ShouldBe("provider-idempotency-a");
        configured.IdempotencyFingerprint.ShouldNotBeNullOrWhiteSpace();
        configured.NamingPolicy.PolicyRef.ShouldBe("naming-policy-a");
        configured.NamingPolicy.Metadata["prefix"].ShouldBe("folders");
        configured.BranchPolicy.PolicyRef.ShouldBe("branch-policy-a");
        configured.BranchPolicy.Metadata["default_branch"].ShouldBe("main");
        configured.OccurredAt.ShouldBe(command.OccurredAt);
    }

    [Fact]
    public void EventReplayShouldHydrateRedactedProviderBindingMetadataOnly()
    {
        ConfigureProviderBinding command = ProviderBindingCommandFactory.Configure();
        OrganizationProviderBindingResult result = OrganizationAggregate.Handle(OrganizationState.Empty, command);

        OrganizationState state = OrganizationState.Empty.Apply(result.Events);

        OrganizationProviderBinding binding = state.ProviderBindings["binding-a"];
        binding.ProviderKind.ShouldBe("github");
        binding.CredentialReferenceId.ShouldBe("credential-a");
        binding.NamingPolicy.Metadata["prefix"].ShouldBe("folders");
        binding.BranchPolicy.Metadata["default_branch"].ShouldBe("main");
        binding.CorrelationId.ShouldBe("correlation-a");
        binding.TaskId.ShouldBe("task-a");
        binding.IdempotencyKey.ShouldBe("provider-idempotency-a");
        binding.IdempotencyFingerprint.ShouldNotBeNullOrWhiteSpace();
        binding.ConfiguredStatus.ShouldBe("configured");
        binding.OccurredAt.ShouldBe(command.OccurredAt);
    }

    [Fact]
    public void AcceptedEventAndReplayStateShouldRemainMetadataOnly()
    {
        ConfigureProviderBinding command = ProviderBindingCommandFactory.Configure();
        OrganizationProviderBindingResult result = OrganizationAggregate.Handle(OrganizationState.Empty, command);
        OrganizationState state = OrganizationState.Empty.Apply(result.Events);

        string serialized = JsonSerializer.Serialize(new { result, state.ProviderBindings });

        serialized.ShouldContain("binding-a", Case.Sensitive);
        serialized.ShouldContain("credential-a", Case.Sensitive);
        serialized.ShouldNotContain("token", Case.Insensitive);
        serialized.ShouldNotContain("password", Case.Insensitive);
        serialized.ShouldNotContain("privateKey", Case.Insensitive);
        serialized.ShouldNotContain("providerPayload", Case.Insensitive);
        serialized.ShouldNotContain("https://user:password@", Case.Insensitive);
    }

    [Fact]
    public void SameIdempotencyKeyAndEquivalentPayloadShouldNotAppendDuplicateEvent()
    {
        ConfigureProviderBinding command = ProviderBindingCommandFactory.Configure(idempotencyKey: "idem-a");
        OrganizationProviderBindingResult first = OrganizationAggregate.Handle(OrganizationState.Empty, command);
        OrganizationState state = OrganizationState.Empty.Apply(first.Events);

        OrganizationProviderBindingResult replay = OrganizationAggregate.Handle(state, command);

        replay.Code.ShouldBe(OrganizationProviderBindingResultCode.AlreadyApplied);
        replay.Events.ShouldBeEmpty();
    }

    [Fact]
    public void SameIdempotencyKeyAndDifferentBindingPayloadShouldReturnConflict()
    {
        ConfigureProviderBinding command = ProviderBindingCommandFactory.Configure(idempotencyKey: "idem-a", credentialReferenceId: "credential-a");
        OrganizationState state = OrganizationState.Empty.Apply(OrganizationAggregate.Handle(OrganizationState.Empty, command).Events);

        OrganizationProviderBindingResult conflict = OrganizationAggregate.Handle(
            state,
            ProviderBindingCommandFactory.Configure(idempotencyKey: "idem-a", credentialReferenceId: "credential-b"));

        conflict.Code.ShouldBe(OrganizationProviderBindingResultCode.IdempotencyConflict);
        conflict.Events.ShouldBeEmpty();
    }

    [Fact]
    public void SameProviderBindingReferenceAndEquivalentPayloadShouldBeIdempotentWithDifferentKey()
    {
        ConfigureProviderBinding command = ProviderBindingCommandFactory.Configure(idempotencyKey: "idem-a");
        OrganizationState state = OrganizationState.Empty.Apply(OrganizationAggregate.Handle(OrganizationState.Empty, command).Events);

        OrganizationProviderBindingResult duplicate = OrganizationAggregate.Handle(
            state,
            ProviderBindingCommandFactory.Configure(idempotencyKey: "idem-b"));

        duplicate.Code.ShouldBe(OrganizationProviderBindingResultCode.AlreadyApplied);
        duplicate.Events.ShouldBeEmpty();
    }

    [Fact]
    public void SameProviderBindingReferenceAndDifferentProtectedMetadataShouldReturnDuplicateConflict()
    {
        ConfigureProviderBinding command = ProviderBindingCommandFactory.Configure(idempotencyKey: "idem-a", providerKind: "github");
        OrganizationState state = OrganizationState.Empty.Apply(OrganizationAggregate.Handle(OrganizationState.Empty, command).Events);

        OrganizationProviderBindingResult duplicate = OrganizationAggregate.Handle(
            state,
            ProviderBindingCommandFactory.Configure(idempotencyKey: "idem-b", providerKind: "forgejo"));

        duplicate.Code.ShouldBe(OrganizationProviderBindingResultCode.DuplicateConflict);
        duplicate.Events.ShouldBeEmpty();
    }

    [Fact]
    public void PolicyMetadataOrderingShouldNotChangeIdempotencyFingerprint()
    {
        ConfigureProviderBinding command = ProviderBindingCommandFactory.Configure(
            namingPolicy: ProviderBindingCommandFactory.Policy("naming-policy-a", ("prefix", "folders"), ("scope", "organization")));
        OrganizationState state = OrganizationState.Empty.Apply(OrganizationAggregate.Handle(OrganizationState.Empty, command).Events);

        OrganizationProviderBindingResult replay = OrganizationAggregate.Handle(
            state,
            command with
            {
                NamingPolicy = ProviderBindingCommandFactory.Policy("naming-policy-a", ("scope", "organization"), ("prefix", "folders")),
            });

        replay.Code.ShouldBe(OrganizationProviderBindingResultCode.AlreadyApplied);
    }
}
