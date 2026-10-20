using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Testing.Providers;
using Hexalith.Folders.Tests.Aggregates.Organization;
using Hexalith.Folders.Tests.OperationalStatus;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FinalAclReauthorizationTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 26, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PlainFolderCreationRejectsAclRevocationBeforeAppend()
    {
        RecordingFolderRepository repository = new();
        FinalAclRevokingPermissions permissions = new();
        FolderCreationService service = new(AuthorizationService(permissions), repository, new FinalAclFixedTimeProvider(Now));
        FolderCreationRequest request = new(
            "tenant-a", "principal-a",
            EventStoreClaimTransformEvidence.Allowed("tenant-a", "principal-a", [FolderCreationService.ActionToken]),
            "folder-a", "Folder A", "correlation-a", "task-a", "idempotency-a", null,
            new Dictionary<string, string?>(), new Dictionary<string, string?>());

        FolderResult result = await service.CreateAsync(request, TestContext.Current.CancellationToken);

        result.Code.ShouldBe(FolderResultCode.FolderAclDenied);
        permissions.Calls.ShouldBe(2);
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Fact]
    public async Task ProviderBindingRejectsAclRevocationBeforeAppend()
    {
        RecordingOrganizationProviderBindingRepository repository = new(ProviderBindingCommandFactory.StateWithConfigurePermission());
        FinalAclRevokingPermissions permissions = new();
        ConfigureProviderBindingService service = new(AuthorizationService(permissions), repository, new FinalAclFixedTimeProvider(Now));
        ConfigureProviderBindingServiceRequest request = new(
            "tenant-a", "principal-a",
            EventStoreClaimTransformEvidence.Allowed("tenant-a", "principal-a", [ConfigureProviderBindingService.ActionToken]),
            "binding-a", "github", "credential-a", "correlation-a", "task-a", "provider-binding-new-key", null,
            new Dictionary<string, string?>(), new Dictionary<string, string?>());

        OrganizationProviderBindingResult result = await service.ConfigureAsync(request, TestContext.Current.CancellationToken);

        result.Code.ShouldBe(OrganizationProviderBindingResultCode.MissingPermission);
        permissions.Calls.ShouldBe(2);
        repository.EventsAppended.ShouldBe(0);
    }

    [Fact]
    public async Task GrantedProviderBindingUsesOrganizationScopeForBothPassesAndEmitsEvent()
    {
        RecordingOrganizationProviderBindingRepository repository = new();
        FinalAclRevokingPermissions permissions = new(allowedCalls: 2);
        ConfigureProviderBindingService service = new(AuthorizationService(permissions), repository,
            new FinalAclFixedTimeProvider(Now));
        ConfigureProviderBindingServiceRequest request = new(
            "tenant-a", "principal-a",
            EventStoreClaimTransformEvidence.Allowed("tenant-a", "principal-a",
                [ConfigureProviderBindingService.ActionToken]),
            "binding-a", "github", "credential-a", "correlation-a", "task-a",
            "provider-binding-new-key", null, new Dictionary<string, string?>(),
            new Dictionary<string, string?>());

        OrganizationProviderBindingResult result = await service.ConfigureAsync(request,
            TestContext.Current.CancellationToken);

        result.Code.ShouldBe(OrganizationProviderBindingResultCode.Accepted);
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<ProviderBindingConfigured>()
            .ProviderBindingRef.ShouldBe("binding-a");
        repository.EventsAppended.ShouldBe(1);
        repository.LastStreamName.ShouldBe($"{request.AuthoritativeTenantId}:organizations:{request.ProviderBindingRef}");
        permissions.Requests.Count.ShouldBe(2);
        permissions.Requests.ShouldAllBe(item => item.OperationScope == "organization_baseline"
            && item.ActionToken == ConfigureProviderBindingService.ActionToken);
    }

    [Fact]
    public async Task AcceptedHostProfileSurvivesSourceProjectionAndSuppliesReadinessPoliciesAsync()
    {
        ProviderBindingProfile profile = new()
        {
            ManagedTenantId = "tenant-a",
            ProviderKind = "github",
            NamingPolicy = new("repository-policy", new Dictionary<string, string> { ["repository_owner"] = "private-owner" }),
            BranchPolicy = new("branch-policy", new Dictionary<string, string> { ["selected_ref"] = "private-ref" }),
        };
        var options = Options.Create(new FoldersProviderBindingOptions
        { Profiles = new Dictionary<string, ProviderBindingProfile> { ["known-profile"] = profile } });
        RecordingOrganizationProviderBindingRepository repository = new();
        ConfigureProviderBindingService service = new(AuthorizationService(new FinalAclRevokingPermissions(2)), repository,
            new FinalAclFixedTimeProvider(Now), options);
        var request = new ConfigureProviderBindingServiceRequest("tenant-a", "principal-a",
            EventStoreClaimTransformEvidence.Allowed("tenant-a", "principal-a", [ConfigureProviderBindingService.ActionToken]),
            "binding-a", "github", "credential-a", "correlation-a", "task-a", "profile-key", null,
            new Dictionary<string, string?>(), new Dictionary<string, string?>())
        { CapabilityProfileRef = "known-profile" };
        var result = await service.ConfigureAsync(request, TestContext.Current.CancellationToken);
        result.Code.ShouldBe(OrganizationProviderBindingResultCode.Accepted);
        var configured = result.Events.ShouldHaveSingleItem().ShouldBeOfType<ProviderBindingConfigured>();
        configured.NamingPolicy.Metadata.ShouldNotContainKey("repository_owner");
        var source = new EventStoreProviderReadinessBindingReadModel(new InMemoryReadModelStore(), new OperationalStatusSequenceReader { Head = 1 });
        await source.ApplyAsync("tenant-a", ProviderBindingActorId.Create("binding-a"), "source-1", 1, "source-digest", configured,
            TestContext.Current.CancellationToken);
        var consumed = await new ConfiguredProviderReadinessBindingReader(source, options).GetAsync(new("tenant-a", "binding-a", "corr"), TestContext.Current.CancellationToken);
        consumed.ShouldNotBeNull().NamingPolicy.PolicyRef.ShouldBe(profile.NamingPolicy.PolicyRef);
        consumed.NamingPolicy.Metadata["repository_owner"].ShouldBe("private-owner");
        consumed.BranchPolicy.PolicyRef.ShouldBe(profile.BranchPolicy.PolicyRef);
        consumed.BranchPolicy.Metadata["selected_ref"].ShouldBe("private-ref");
    }

    [Theory]
    [InlineData("tenant-b", "github", "known-profile")]
    [InlineData("tenant-a", "forgejo", "known-profile")]
    [InlineData("tenant-a", "github", "unknown-profile")]
    public async Task ProviderBindingRejectsUnknownForeignTenantAndWrongProviderProfilesBeforeConfigurationAppendAsync(string tenantId, string providerKind, string profileRef)
    {
        RecordingOrganizationProviderBindingRepository repository = new();
        FinalAclRevokingPermissions permissions = new(allowedCalls: 2);
        ConfigureProviderBindingService service = new(AuthorizationService(permissions), repository,
            new FinalAclFixedTimeProvider(Now), Options.Create(new FoldersProviderBindingOptions
            {
                Profiles = new Dictionary<string, ProviderBindingProfile>
                {
                    ["known-profile"] = new() { ManagedTenantId = tenantId, ProviderKind = providerKind },
                },
            }));
        var request = new ConfigureProviderBindingServiceRequest("tenant-a", "principal-a",
            EventStoreClaimTransformEvidence.Allowed("tenant-a", "principal-a", [ConfigureProviderBindingService.ActionToken]),
            "binding-a", "github", "credential-a", "correlation-a", "task-a", "profile-key", null,
            new Dictionary<string, string?>(), new Dictionary<string, string?>())
        { CapabilityProfileRef = profileRef };
        var result = await service.ConfigureAsync(request, TestContext.Current.CancellationToken);
        result.Code.ShouldBe(OrganizationProviderBindingResultCode.InvalidPolicy);
        repository.EventsAppended.ShouldBe(0);
        repository.StreamNamesConstructed.ShouldBe(0);
    }

    [Fact]
    public async Task BindAsyncRejectsAclRevocationBeforeProviderCall()
    {
        FinalAclRevokingPermissions permissions = new();
        RecordingFolderRepository repository = SeededRepository();
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        RepositoryBindingService service = BindingService(permissions, repository, resolver);

        FolderResult result = await service.BindAsync(BindRequest(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(FolderResultCode.FolderAclDenied);
        resolver.ProviderCalls.ShouldBe(0);
        repository.AppendsAttempted.ShouldBe(0);
    }

    [Fact]
    public async Task BindAsyncRejectsAclRevocationAfterProviderReturns()
    {
        FinalAclRevokingPermissions permissions = new(allowedCalls: 2);
        RecordingFolderRepository repository = SeededRepository();
        RecordingProviderCapabilityResolver resolver = new(FakeGitProvider.GitHubLike());
        RepositoryBindingService service = BindingService(permissions, repository, resolver);

        FolderResult result = await service.BindAsync(BindRequest(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Code.ShouldBe(FolderResultCode.FolderAclDenied);
        resolver.ProviderCalls.ShouldBe(1);
        repository.AppendsAttempted.ShouldBe(0);
    }

    private static RepositoryBindingService BindingService(
        FinalAclRevokingPermissions permissions,
        RecordingFolderRepository repository,
        RecordingProviderCapabilityResolver resolver)
        => new(
            AuthorizationService(permissions),
            new AllowingRepositoryBindingReadinessValidator(),
            new FixedProviderBindingReader(),
            resolver,
            repository,
            new FinalAclFixedTimeProvider(Now));

    private static RecordingFolderRepository SeededRepository()
    {
        RecordingFolderRepository repository = new();
        FolderStreamName streamName = FolderStreamName.Create("tenant-a", "folder-a");
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        repository.Seed(streamName, created.Events);
        return repository;
    }

    private static BindRepositoryRequest BindRequest()
        => new(
            "tenant-a",
            "principal-a",
            EventStoreClaimTransformEvidence.Allowed(
                "tenant-a",
                "principal-a",
                [RepositoryBindingService.ActionToken, ProviderReadinessValidationService.ReadActionToken]),
            FolderId: "folder-a",
            RequestSchemaVersion: "v1",
            ProviderBindingRef: "provider-binding-a",
            ExternalRepositoryRef: "external-repository-a",
            BranchRefPolicyRef: "branch-ref-policy-a",
            CredentialScopeClass: "tenant-installation",
            CorrelationId: "correlation-a",
            TaskId: "task-a",
            IdempotencyKey: "idempotency-bind-a",
            PayloadTenantId: null,
            ClientControlledTenantValues: new Dictionary<string, string?>(StringComparer.Ordinal),
            ClientControlledPrincipalValues: new Dictionary<string, string?>(StringComparer.Ordinal));

    private sealed class AllowingRepositoryBindingReadinessValidator : IRepositoryBindingReadinessValidator
    {
        public Task<ProviderReadinessValidationResult> ValidateAsync(
            ProviderReadinessValidationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ProviderReadinessValidationResult(
                ProviderReadinessResultCode.Allowed,
                "ready",
                "success",
                "none",
                Retryable: false,
                RetryAfter: null,
                RemediationCategory: "none",
                CorrelationId: "correlation-a",
                ProviderReference: "provider-binding-a",
                ProviderBindingRef: "provider-binding-a",
                CapabilityProfileRef: "profile-a",
                Evidence: null,
                new ProviderReadinessFreshness("snapshot_per_task", Now, "tenant-a:7", Stale: false),
                ProviderFailureCategory.None,
                ProviderFailureCategory.None.ToCategoryCode()));
    }

    private sealed class FixedProviderBindingReader : IProviderReadinessBindingReader
    {
        public Task<OrganizationProviderBinding?> GetAsync(
            ProviderReadinessBindingReadRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<OrganizationProviderBinding?>(new OrganizationProviderBinding(
                "tenant-a",
                "tenant-a",
                "provider-binding-a",
                "github",
                "credential-reference-a",
                new OrganizationProviderBindingPolicy("naming-policy-a", new Dictionary<string, string>(StringComparer.Ordinal)),
                new OrganizationProviderBindingPolicy("branch-policy-a", new Dictionary<string, string>(StringComparer.Ordinal)),
                "correlation-binding-a",
                "task-binding-a",
                "idempotency-provider-binding-a",
                "fingerprint-provider-binding-a",
                "configured",
                Now.AddMinutes(-1)));
    }

    private static LayeredFolderAuthorizationService AuthorizationService(IFolderPermissionEvidenceProvider permissions)
    {
        InMemoryFolderTenantAccessProjectionStore store = new();
        store.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = "tenant-a",
            Enabled = true,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
            {
                ["principal-a"] = new("principal-a", "Member"),
            },
            Watermark = 7,
            ProjectionWatermark = "tenant-a:7",
            LastEventTimestamp = Now.AddMinutes(-1),
        }).GetAwaiter().GetResult();
        FixedUtcClock clock = new(Now);
        return new LayeredFolderAuthorizationService(
            new TenantAccessAuthorizer(store, clock, new TenantAccessOptions
            {
                MutationFreshnessBudget = TimeSpan.FromMinutes(5),
                DiagnosticStalenessBudget = TimeSpan.FromMinutes(5),
            }),
            permissions,
            new FinalAclAllowingValidator(),
            new FinalAclAllowingDapr(),
            clock);
    }
}
