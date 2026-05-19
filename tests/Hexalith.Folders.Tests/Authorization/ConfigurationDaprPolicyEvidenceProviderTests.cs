using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class ConfigurationDaprPolicyEvidenceProviderTests
{
    [Fact]
    public async Task ProviderShouldReturnAllowedWhenNeitherRequestNorOptionsRequireEvidence()
    {
        ConfigurationDaprPolicyEvidenceProvider provider = new(new DaprPolicyEvidenceOptions { Enabled = false });

        DaprPolicyEvidenceResult result = await provider.GetEvidenceAsync(
            new DaprPolicyEvidenceRequest("folders", "domain_service", RequiresPolicyEvidence: false, CorrelationId: null, TaskId: null),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(DaprPolicyEvidenceStatus.Allowed);
        result.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.Allowed);
    }

    [Fact]
    public async Task ProviderShouldFailClosedWhenRequiredAndDisabled()
    {
        ConfigurationDaprPolicyEvidenceProvider provider = new(new DaprPolicyEvidenceOptions { Enabled = false, RequirePolicyEvidence = true });

        DaprPolicyEvidenceResult result = await provider.GetEvidenceAsync(
            new DaprPolicyEvidenceRequest("folders", "domain_service", RequiresPolicyEvidence: true, CorrelationId: null, TaskId: null),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(DaprPolicyEvidenceStatus.Unavailable);
        result.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.DaprPolicyDenied);
        result.Retryable.ShouldBeTrue();
    }

    [Fact]
    public async Task ProviderShouldDenyWhenTargetAppIsNotInAllowList()
    {
        ConfigurationDaprPolicyEvidenceProvider provider = new(new DaprPolicyEvidenceOptions
        {
            Enabled = true,
            AllowedTargetAppIds = ["folders"],
            AllowedServiceInvocationClasses = ["domain_service"],
        });

        DaprPolicyEvidenceResult result = await provider.GetEvidenceAsync(
            new DaprPolicyEvidenceRequest("unknown-app", "domain_service", RequiresPolicyEvidence: true, CorrelationId: null, TaskId: null),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(DaprPolicyEvidenceStatus.Denied);
        result.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.DaprPolicyDenied);
        result.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task ProviderShouldDenyWhenServiceInvocationClassIsNotInAllowList()
    {
        ConfigurationDaprPolicyEvidenceProvider provider = new(new DaprPolicyEvidenceOptions
        {
            Enabled = true,
            AllowedTargetAppIds = ["folders"],
            AllowedServiceInvocationClasses = ["domain_service"],
        });

        DaprPolicyEvidenceResult result = await provider.GetEvidenceAsync(
            new DaprPolicyEvidenceRequest("folders", "unknown_class", RequiresPolicyEvidence: true, CorrelationId: null, TaskId: null),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(DaprPolicyEvidenceStatus.Denied);
    }

    [Fact]
    public async Task ProviderShouldAllowWhenEnabledTargetAndClassAreInAllowList()
    {
        ConfigurationDaprPolicyEvidenceProvider provider = new(new DaprPolicyEvidenceOptions
        {
            Enabled = true,
            AllowedTargetAppIds = ["folders"],
            AllowedServiceInvocationClasses = ["domain_service"],
        });

        DaprPolicyEvidenceResult result = await provider.GetEvidenceAsync(
            new DaprPolicyEvidenceRequest("folders", "domain_service", RequiresPolicyEvidence: true, CorrelationId: null, TaskId: null),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(DaprPolicyEvidenceStatus.Allowed);
        result.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.Allowed);
    }
}
