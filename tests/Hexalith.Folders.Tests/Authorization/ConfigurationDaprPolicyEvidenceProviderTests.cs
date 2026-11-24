using Hexalith.Folders.Authorization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class ConfigurationDaprPolicyEvidenceProviderTests
{
    [Fact]
    public void APolicySwitchAlonePreservesTheBuiltInLists()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration[$"{DaprPolicyEvidenceOptions.SectionName}:RequirePolicyEvidence"] = "true";
        builder.Services.AddFoldersLayeredAuthorization();
        using IHost host = builder.Build();

        DaprPolicyEvidenceOptions options = host.Services.GetRequiredService<DaprPolicyEvidenceOptions>();
        options.RequirePolicyEvidence.ShouldBeTrue();
        options.AllowedTargetAppIds.ShouldBe(["folders"]);
        options.AllowedServiceInvocationClasses.ShouldBe(["domain_service", "query", "diagnostic_read"]);
    }

    [Theory]
    [InlineData(null, "query")]
    [InlineData("", "query")]
    [InlineData("folders", null)]
    [InlineData("folders", " ")]
    public async Task EnabledPolicyWithIncompleteAllowListsFailsAtStartup(string? targetAppId, string? invocationClass)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(PolicyAllowLists(targetAppId, invocationClass));
        builder.Services.AddFoldersLayeredAuthorization();
        using IHost host = builder.Build();

        OptionsValidationException failure = await Should.ThrowAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));
        failure.OptionsType.ShouldBe(typeof(DaprPolicyEvidenceOptions));
    }

    [Fact]
    public async Task ExplicitPolicyReplacesDefaultTargetsAndInvocationClasses()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(PolicyAllowLists("configured-app", "configured_class"));
        builder.Services.AddFoldersLayeredAuthorization();
        using IHost host = builder.Build();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await host.StartAsync(cancellationToken);
        try
        {
            IDaprPolicyEvidenceProvider provider = host.Services.GetRequiredService<IDaprPolicyEvidenceProvider>();
            async Task<DaprPolicyEvidenceStatus> Status(string target, string invocation)
                => (await provider.GetEvidenceAsync(new(target, invocation, true, null, null), cancellationToken)
                    .ConfigureAwait(true)).Status;

            (await Status("configured-app", "configured_class")).ShouldBe(DaprPolicyEvidenceStatus.Allowed);
            (await Status("folders", "configured_class")).ShouldBe(DaprPolicyEvidenceStatus.Denied);
            (await Status("configured-app", "domain_service")).ShouldBe(DaprPolicyEvidenceStatus.Denied);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

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

    private static Dictionary<string, string?> PolicyAllowLists(string? target, string? invocation)
    {
        const string prefix = DaprPolicyEvidenceOptions.SectionName + ":";
        Dictionary<string, string?> configuration = new() { [prefix + "Enabled"] = "true" };
        if (target is not null)
        {
            configuration[prefix + "AllowedTargetAppIds:0"] = target;
        }

        if (invocation is not null)
        {
            configuration[prefix + "AllowedServiceInvocationClasses:0"] = invocation;
        }

        return configuration;
    }
}
