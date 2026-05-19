using System.Text.Json;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class LayeredFolderAuthorizationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AuthorizeShouldEvaluateAllowedPathInCanonicalOrder()
    {
        RecordingFolderPermissionEvidenceProvider folderEvidence = new(FolderPermissionEvidenceResult.Allowed("folder_watermark_v1"));
        RecordingEventStoreAuthorizationValidator validator = new(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1"));
        RecordingDaprPolicyEvidenceProvider dapr = new(DaprPolicyEvidenceResult.Allowed("folders", "service_invocation_v1"));
        LayeredFolderAuthorizationService service = CreateService(
            TenantStore("tenant-a", "user-a"),
            folderEvidence,
            validator,
            dapr);

        LayeredFolderAuthorizationResult result = await service.AuthorizeAsync(Context(), TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeTrue();
        result.Decision.TerminalLayer.ShouldBe(AuthorizationLayer.DaprDenyByDefaultPolicy);
        result.Decision.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.Allowed);
        result.EvaluatedLayers.ShouldBe(AuthorizationOrder.LayeredFolderAuthorization);
        result.AllowedContext.ShouldNotBeNull();
        result.AllowedContext.AuthoritativeTenantId.ShouldBe("tenant-a");
        result.AllowedContext.ActorSafeIdentifier.ShouldBe("actor-user-a");
        result.AllowedContext.ActionToken.ShouldBe("read_metadata");
        result.AllowedContext.CorrelationId.ShouldBe("corr-a");
        result.AllowedContext.TaskId.ShouldBe("task-a");
        result.AllowedContext.PolicyLayers.ShouldBe(AuthorizationOrder.LayeredFolderAuthorization);
        folderEvidence.Requests.Count.ShouldBe(1);
        validator.Requests.Count.ShouldBe(1);
        dapr.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task MissingJwtEvidenceShouldDenyBeforeTenantProjectionOrProtectedResourceTouch()
    {
        RecordingTenantAccessProjectionStore tenantStore = new();
        RecordingFolderPermissionEvidenceProvider folderEvidence = new(FolderPermissionEvidenceResult.Allowed("folder_watermark_v1"));
        RecordingEventStoreAuthorizationValidator validator = new(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1"));
        RecordingDaprPolicyEvidenceProvider dapr = new(DaprPolicyEvidenceResult.Allowed("folders", "service_invocation_v1"));
        LayeredFolderAuthorizationService service = CreateService(tenantStore, folderEvidence, validator, dapr);

        LayeredFolderAuthorizationResult result = await service.AuthorizeAsync(
            Context(authoritativeTenantId: null),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.Decision.TerminalLayer.ShouldBe(AuthorizationLayer.JwtValidation);
        result.Decision.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.AuthenticationDenied);
        result.EvaluatedLayers.ShouldBe([AuthorizationLayer.JwtValidation]);
        tenantStore.GetCalls.ShouldBe(0);
        folderEvidence.Requests.Count.ShouldBe(0);
        validator.Requests.Count.ShouldBe(0);
        dapr.Requests.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ClaimTransformMismatchShouldDenyBeforeTenantProjectionOrFolderEvidence()
    {
        RecordingTenantAccessProjectionStore tenantStore = TenantStore("tenant-a", "user-a");
        RecordingFolderPermissionEvidenceProvider folderEvidence = new(FolderPermissionEvidenceResult.Allowed("folder_watermark_v1"));
        LayeredFolderAuthorizationService service = CreateService(
            tenantStore,
            folderEvidence,
            new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1")),
            new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "service_invocation_v1")));

        LayeredFolderAuthorizationResult result = await service.AuthorizeAsync(
            Context(claimTransformEvidence: EventStoreClaimTransformEvidence.Allowed(
                tenantId: "tenant-b",
                principalId: "user-a",
                permissions: ["read_metadata"])),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.Decision.TerminalLayer.ShouldBe(AuthorizationLayer.EventStoreClaimTransform);
        result.Decision.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.ClaimTransformDenied);
        result.EvaluatedLayers.ShouldBe([AuthorizationLayer.JwtValidation, AuthorizationLayer.EventStoreClaimTransform]);
        tenantStore.GetCalls.ShouldBe(0);
        folderEvidence.Requests.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ClientControlledTenantMismatchShouldDenyBeforeTenantProjectionOrFolderEvidence()
    {
        RecordingTenantAccessProjectionStore tenantStore = TenantStore("tenant-a", "user-a");
        RecordingFolderPermissionEvidenceProvider folderEvidence = new(FolderPermissionEvidenceResult.Allowed("folder_watermark_v1"));
        LayeredFolderAuthorizationService service = CreateService(
            tenantStore,
            folderEvidence,
            new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1")),
            new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "service_invocation_v1")));

        LayeredFolderAuthorizationResult result = await service.AuthorizeAsync(
            Context(clientTenantValues: new Dictionary<string, string?>
            {
                ["route_tenant_id"] = "tenant-a",
                ["header_tenant_id"] = "tenant-secret-victim",
            }),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.Decision.TerminalLayer.ShouldBe(AuthorizationLayer.EventStoreClaimTransform);
        result.Decision.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.ClaimTransformDenied);
        tenantStore.GetCalls.ShouldBe(0);
        folderEvidence.Requests.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData(TenantAccessOutcome.DisabledTenant, "tenant_access_denied")]
    [InlineData(TenantAccessOutcome.StaleProjection, "tenant_projection_stale")]
    [InlineData(TenantAccessOutcome.UnavailableProjection, "tenant_projection_unavailable")]
    [InlineData(TenantAccessOutcome.MalformedEvidence, "authorization_evidence_malformed")]
    [InlineData(TenantAccessOutcome.ReplayConflict, "authorization_evidence_malformed")]
    [InlineData(TenantAccessOutcome.UnknownTenant, "safe_not_found")]
    public async Task TenantAccessFailureShouldShortCircuitBeforeFolderEvidence(
        TenantAccessOutcome outcome,
        string expectedOutcomeCode)
    {
        RecordingTenantAccessProjectionStore tenantStore = outcome switch
        {
            TenantAccessOutcome.UnavailableProjection => new RecordingTenantAccessProjectionStore(throwOnGet: true),
            TenantAccessOutcome.UnknownTenant => new RecordingTenantAccessProjectionStore(),
            _ => TenantStoreForOutcome(outcome),
        };
        RecordingFolderPermissionEvidenceProvider folderEvidence = new(FolderPermissionEvidenceResult.Allowed("folder_watermark_v1"));
        LayeredFolderAuthorizationService service = CreateService(
            tenantStore,
            folderEvidence,
            new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1")),
            new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "service_invocation_v1")));

        LayeredFolderAuthorizationResult result = await service.AuthorizeAsync(Context(), TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.Decision.TerminalLayer.ShouldBe(AuthorizationLayer.TenantAccessFreshness);
        result.Decision.OutcomeCode.ShouldBe(expectedOutcomeCode);
        folderEvidence.Requests.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData(FolderPermissionEvidenceStatus.Denied, "folder_acl_denied", false)]
    [InlineData(FolderPermissionEvidenceStatus.NotFoundSafe, "safe_not_found", false)]
    [InlineData(FolderPermissionEvidenceStatus.Stale, "folder_acl_stale", false)]
    [InlineData(FolderPermissionEvidenceStatus.Unavailable, "folder_acl_unavailable", true)]
    [InlineData(FolderPermissionEvidenceStatus.Malformed, "authorization_evidence_malformed", false)]
    public async Task FolderPermissionFailureShouldShortCircuitBeforeValidatorAndDapr(
        FolderPermissionEvidenceStatus status,
        string expectedOutcomeCode,
        bool expectedRetryable)
    {
        RecordingFolderPermissionEvidenceProvider folderEvidence = new(FolderPermissionEvidenceResult.FromStatus(status, "folder_watermark_v1"));
        RecordingEventStoreAuthorizationValidator validator = new(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1"));
        RecordingDaprPolicyEvidenceProvider dapr = new(DaprPolicyEvidenceResult.Allowed("folders", "service_invocation_v1"));
        LayeredFolderAuthorizationService service = CreateService(TenantStore("tenant-a", "user-a"), folderEvidence, validator, dapr);

        LayeredFolderAuthorizationResult result = await service.AuthorizeAsync(Context(), TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.Decision.TerminalLayer.ShouldBe(AuthorizationLayer.FolderAcl);
        result.Decision.OutcomeCode.ShouldBe(expectedOutcomeCode);
        result.Decision.Retryable.ShouldBe(expectedRetryable);
        validator.Requests.Count.ShouldBe(0);
        dapr.Requests.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ValidatorDenialShouldShortCircuitBeforeDaprPolicy()
    {
        RecordingDaprPolicyEvidenceProvider dapr = new(DaprPolicyEvidenceResult.Allowed("folders", "service_invocation_v1"));
        LayeredFolderAuthorizationService service = CreateService(
            TenantStore("tenant-a", "user-a"),
            new RecordingFolderPermissionEvidenceProvider(FolderPermissionEvidenceResult.Allowed("folder_watermark_v1")),
            new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Denied()),
            dapr);

        LayeredFolderAuthorizationResult result = await service.AuthorizeAsync(Context(), TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.Decision.TerminalLayer.ShouldBe(AuthorizationLayer.EventStoreValidator);
        result.Decision.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied);
        dapr.Requests.Count.ShouldBe(0);
    }

    [Fact]
    public async Task RequiredDaprEvidenceShouldFailClosedWhenUnavailable()
    {
        LayeredFolderAuthorizationService service = CreateService(
            TenantStore("tenant-a", "user-a"),
            new RecordingFolderPermissionEvidenceProvider(FolderPermissionEvidenceResult.Allowed("folder_watermark_v1")),
            new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1")),
            new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Unavailable("dapr_policy_unavailable")));

        LayeredFolderAuthorizationResult result = await service.AuthorizeAsync(
            Context(operationPolicy: LayeredFolderOperationPolicy.Mutation(requiresDaprPolicyEvidence: true)),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.Decision.TerminalLayer.ShouldBe(AuthorizationLayer.DaprDenyByDefaultPolicy);
        result.Decision.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.DaprPolicyDenied);
        result.Decision.Retryable.ShouldBeTrue();
    }

    [Fact]
    public async Task BoundedDiagnosticReadShouldCarryStaleFreshnessWithoutFailingOpenForStrictReads()
    {
        RecordingFolderPermissionEvidenceProvider folderEvidence = new(FolderPermissionEvidenceResult.Allowed(
            freshnessWatermark: "folder_stale_watermark_v1",
            freshnessClass: "bounded_stale"));
        LayeredFolderAuthorizationService service = CreateService(
            TenantStore("tenant-a", "user-a", lastEventTimestamp: Now.AddMinutes(-2)),
            folderEvidence,
            new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1")),
            new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "service_invocation_v1")));

        LayeredFolderAuthorizationResult result = await service.AuthorizeAsync(
            Context(operationPolicy: LayeredFolderOperationPolicy.BoundedDiagnosticRead()),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeTrue();
        result.Decision.FreshnessClass.ShouldBe("bounded_stale");
        result.AllowedContext.ShouldNotBeNull();
        result.AllowedContext.FreshnessWatermark.ShouldBe("folder_stale_watermark_v1");
    }

    [Fact]
    public async Task DecisionsShouldNotBeReusedAcrossTenantsPrincipalsTasksActionsOrFreshnessWatermarks()
    {
        RecordingFolderPermissionEvidenceProvider folderEvidence = new(FolderPermissionEvidenceResult.Allowed("folder_watermark_v1"));
        LayeredFolderAuthorizationService service = CreateService(
            TenantStore("tenant-a", "user-a", "tenant-b", "user-b"),
            folderEvidence,
            new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1")),
            new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "service_invocation_v1")));

        LayeredFolderAuthorizationResult first = await service.AuthorizeAsync(Context(), TestContext.Current.CancellationToken);
        LayeredFolderAuthorizationResult second = await service.AuthorizeAsync(
            Context(
                authoritativeTenantId: "tenant-b",
                principalId: "user-b",
                actorSafeIdentifier: "actor-user-b",
                actionToken: "query_status",
                taskId: "task-b",
                claimTransformEvidence: EventStoreClaimTransformEvidence.Allowed("tenant-b", "user-b", ["query_status"])),
            TestContext.Current.CancellationToken);

        first.IsAllowed.ShouldBeTrue();
        second.IsAllowed.ShouldBeTrue();
        folderEvidence.Requests.Count.ShouldBe(2);
        folderEvidence.Requests[0].ManagedTenantId.ShouldBe("tenant-a");
        folderEvidence.Requests[1].ManagedTenantId.ShouldBe("tenant-b");
        folderEvidence.Requests[0].ActionToken.ShouldBe("read_metadata");
        folderEvidence.Requests[1].ActionToken.ShouldBe("query_status");
        folderEvidence.Requests[0].TaskId.ShouldBe("task-a");
        folderEvidence.Requests[1].TaskId.ShouldBe("task-b");
    }

    [Fact]
    public async Task WildcardPermissionTokenShouldNotEscalateClaimTransformEvidence()
    {
        RecordingTenantAccessProjectionStore tenantStore = TenantStore("tenant-a", "user-a");
        RecordingFolderPermissionEvidenceProvider folderEvidence = new(FolderPermissionEvidenceResult.Allowed("folder_watermark_v1"));
        LayeredFolderAuthorizationService service = CreateService(
            tenantStore,
            folderEvidence,
            new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1")),
            new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "service_invocation_v1")));

        LayeredFolderAuthorizationResult result = await service.AuthorizeAsync(
            Context(claimTransformEvidence: EventStoreClaimTransformEvidence.Allowed(
                tenantId: "tenant-a",
                principalId: "user-a",
                permissions: ["*", "folders:*", "commands:*"])),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.Decision.TerminalLayer.ShouldBe(AuthorizationLayer.EventStoreClaimTransform);
        result.Decision.OutcomeCode.ShouldBe(LayeredAuthorizationOutcomeCodes.ClaimTransformDenied);
    }

    [Fact]
    public async Task SameFolderIdAcrossTenantsShouldProduceIsolatedAuthorizationDecisions()
    {
        RecordingTenantAccessProjectionStore tenantStore = TenantStore("tenant-a", "user-a", "tenant-b", "user-b");
        RecordingFolderPermissionEvidenceProvider folderEvidence = new(FolderPermissionEvidenceResult.Allowed("folder_watermark_v1"));
        LayeredFolderAuthorizationService service = CreateService(
            tenantStore,
            folderEvidence,
            new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1")),
            new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "service_invocation_v1")));

        LayeredFolderAuthorizationResult tenantA = await service.AuthorizeAsync(
            Context(operationScope: "folder-shared-id"),
            TestContext.Current.CancellationToken);
        LayeredFolderAuthorizationResult tenantB = await service.AuthorizeAsync(
            Context(
                authoritativeTenantId: "tenant-b",
                principalId: "user-b",
                actorSafeIdentifier: "actor-user-b",
                operationScope: "folder-shared-id",
                taskId: "task-b",
                claimTransformEvidence: EventStoreClaimTransformEvidence.Allowed("tenant-b", "user-b", ["read_metadata"])),
            TestContext.Current.CancellationToken);

        tenantA.IsAllowed.ShouldBeTrue();
        tenantB.IsAllowed.ShouldBeTrue();
        folderEvidence.Requests.Count.ShouldBe(2);
        folderEvidence.Requests[0].ManagedTenantId.ShouldBe("tenant-a");
        folderEvidence.Requests[1].ManagedTenantId.ShouldBe("tenant-b");
        folderEvidence.Requests[0].OperationScope.ShouldBe("folder-shared-id");
        folderEvidence.Requests[1].OperationScope.ShouldBe("folder-shared-id");
        tenantA.AllowedContext.ShouldNotBeNull();
        tenantB.AllowedContext.ShouldNotBeNull();
        tenantA.AllowedContext.AuthoritativeTenantId.ShouldNotBe(tenantB.AllowedContext.AuthoritativeTenantId);
    }

    [Fact]
    public async Task NonexistentAndUnauthorizedFolderShouldProduceIndistinguishableProblemBodies()
    {
        RecordingTenantAccessProjectionStore tenantStore = TenantStore("tenant-a", "user-a");
        LayeredFolderAuthorizationService notFoundService = CreateService(
            tenantStore,
            new RecordingFolderPermissionEvidenceProvider(FolderPermissionEvidenceResult.FromStatus(
                FolderPermissionEvidenceStatus.NotFoundSafe, null)),
            new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1")),
            new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "service_invocation_v1")));
        LayeredFolderAuthorizationService deniedService = CreateService(
            tenantStore,
            new RecordingFolderPermissionEvidenceProvider(FolderPermissionEvidenceResult.FromStatus(
                FolderPermissionEvidenceStatus.Denied, "folder_watermark_denied")),
            new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Allowed("validator_watermark_v1")),
            new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Allowed("folders", "service_invocation_v1")));

        LayeredFolderAuthorizationResult notFound = await notFoundService.AuthorizeAsync(Context(), TestContext.Current.CancellationToken);
        LayeredFolderAuthorizationResult denied = await deniedService.AuthorizeAsync(Context(), TestContext.Current.CancellationToken);

        notFound.IsAllowed.ShouldBeFalse();
        denied.IsAllowed.ShouldBeFalse();
        notFound.Decision.TerminalLayer.ShouldBe(denied.Decision.TerminalLayer);
        // The HTTP-facing mapper collapses both to 404 not_found_to_caller; here we assert the
        // pre-mapping snapshot does not encode caller-visible distinctions beyond the outcome code.
        notFound.Decision.ActorSafeIdentifier.ShouldBe(denied.Decision.ActorSafeIdentifier);
        notFound.Decision.OperationPolicyClass.ShouldBe(denied.Decision.OperationPolicyClass);
    }

    [Fact]
    public async Task DeniedDecisionSnapshotShouldRemainMetadataOnly()
    {
        string[] forbiddenValues =
        [
            "tenant-secret-victim",
            "folder-secret-victim",
            "Bearer eyJ.secret",
            "user@example.invalid",
            "repo-secret-name",
            "validator raw exception text",
        ];
        LayeredFolderAuthorizationService service = CreateService(
            TenantStore("tenant-a", "user-a"),
            new RecordingFolderPermissionEvidenceProvider(FolderPermissionEvidenceResult.FromStatus(
                FolderPermissionEvidenceStatus.Denied,
                "folder_watermark_v1")),
            new RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult.Denied()),
            new RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult.Denied("folders")));

        LayeredFolderAuthorizationResult result = await service.AuthorizeAsync(
            Context(
                operationScope: "folder-secret-victim",
                actorSafeIdentifier: "actor-safe-user-a",
                clientTenantValues: new Dictionary<string, string?> { ["header_tenant"] = "tenant-a" },
                clientPrincipalValues: new Dictionary<string, string?> { ["body_email"] = "user@example.invalid" }),
            TestContext.Current.CancellationToken);

        string json = JsonSerializer.Serialize(result);
        foreach (string forbidden in forbiddenValues)
        {
            json.ShouldNotContain(forbidden, Case.Sensitive);
        }

        result.Decision.CorrelationId.ShouldBe("corr-a");
        result.Decision.TaskId.ShouldBe("task-a");
        result.Decision.ActorSafeIdentifier.ShouldBe("actor-safe-user-a");
    }

    private static LayeredFolderAuthorizationService CreateService(
        IFolderTenantAccessProjectionStore tenantStore,
        IFolderPermissionEvidenceProvider folderEvidence,
        IEventStoreAuthorizationValidator validator,
        IDaprPolicyEvidenceProvider dapr)
        => new(
            new TenantAccessAuthorizer(
                tenantStore,
                new FixedUtcClock(Now),
                new TenantAccessOptions
                {
                    MutationFreshnessBudget = TimeSpan.FromMinutes(1),
                    DiagnosticStalenessBudget = TimeSpan.FromMinutes(5),
                }),
            folderEvidence,
            validator,
            dapr,
            new FixedUtcClock(Now));

    private static LayeredFolderAuthorizationContext Context(
        string? authoritativeTenantId = "tenant-a",
        string principalId = "user-a",
        string actorSafeIdentifier = "actor-user-a",
        string actionToken = "read_metadata",
        string? operationScope = "folder-a",
        string? taskId = "task-a",
        LayeredFolderOperationPolicy? operationPolicy = null,
        EventStoreClaimTransformEvidence? claimTransformEvidence = null,
        IReadOnlyDictionary<string, string?>? clientTenantValues = null,
        IReadOnlyDictionary<string, string?>? clientPrincipalValues = null)
        => new(
            AuthoritativeTenantId: authoritativeTenantId,
            PrincipalId: principalId,
            ActorSafeIdentifier: actorSafeIdentifier,
            ActionToken: actionToken,
            OperationPolicy: operationPolicy ?? LayeredFolderOperationPolicy.Mutation(),
            ClaimTransformEvidence: claimTransformEvidence ?? EventStoreClaimTransformEvidence.Allowed(
                tenantId: authoritativeTenantId,
                principalId: principalId,
                permissions: [actionToken]),
            OperationScope: operationScope,
            CorrelationId: "corr-a",
            TaskId: taskId,
            ClientControlledTenantValues: clientTenantValues,
            ClientControlledPrincipalValues: clientPrincipalValues);

    private static RecordingTenantAccessProjectionStore TenantStore(params string[] tenantPrincipalPairs)
    {
        RecordingTenantAccessProjectionStore store = new();
        for (int i = 0; i < tenantPrincipalPairs.Length; i += 2)
        {
            store.Save(Projection(tenantPrincipalPairs[i], tenantPrincipalPairs[i + 1], Now.AddSeconds(-30), enabled: true));
        }

        return store;
    }

    private static RecordingTenantAccessProjectionStore TenantStore(
        string tenantId,
        string principalId,
        DateTimeOffset? lastEventTimestamp = null)
    {
        RecordingTenantAccessProjectionStore store = new();
        store.Save(Projection(tenantId, principalId, lastEventTimestamp ?? Now.AddSeconds(-30), enabled: true));
        return store;
    }

    private static RecordingTenantAccessProjectionStore TenantStoreForOutcome(TenantAccessOutcome outcome)
    {
        RecordingTenantAccessProjectionStore store = new();
        FolderTenantAccessProjection projection = outcome switch
        {
            TenantAccessOutcome.DisabledTenant => Projection("tenant-a", "user-a", Now.AddSeconds(-30), enabled: false),
            TenantAccessOutcome.StaleProjection => Projection("tenant-a", "user-a", Now.AddMinutes(-2), enabled: true),
            TenantAccessOutcome.MalformedEvidence => Projection("tenant-a", "user-a", null, enabled: true, malformed: true),
            TenantAccessOutcome.ReplayConflict => Projection("tenant-a", "user-a", Now.AddSeconds(-30), enabled: true, replayConflict: true),
            _ => Projection("tenant-a", "user-a", Now.AddSeconds(-30), enabled: true),
        };
        store.Save(projection);
        return store;
    }

    private static FolderTenantAccessProjection Projection(
        string tenantId,
        string principalId,
        DateTimeOffset? lastEventTimestamp,
        bool enabled,
        bool malformed = false,
        bool replayConflict = false)
        => new()
        {
            TenantId = tenantId,
            Enabled = enabled,
            MalformedEvidence = malformed,
            ReplayConflict = replayConflict,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
            {
                [principalId] = new(principalId, "Member"),
            },
            Watermark = 7,
            ProjectionWatermark = $"{tenantId}:7",
            LastEventTimestamp = lastEventTimestamp,
        };

    private sealed class RecordingTenantAccessProjectionStore(bool throwOnGet = false) : IFolderTenantAccessProjectionStore
    {
        private readonly Dictionary<string, FolderTenantAccessProjection> _projections = new(StringComparer.Ordinal);

        public int GetCalls { get; private set; }

        public Task<FolderTenantAccessProjection?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            GetCalls++;
            if (throwOnGet)
            {
                throw new InvalidOperationException("tenant projection unavailable");
            }

            return Task.FromResult(_projections.TryGetValue(tenantId, out FolderTenantAccessProjection? projection)
                ? projection
                : null);
        }

        public void Save(FolderTenantAccessProjection projection) => _projections[projection.TenantId] = projection;

        public Task SaveAsync(FolderTenantAccessProjection projection, CancellationToken cancellationToken = default)
        {
            Save(projection);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingFolderPermissionEvidenceProvider(FolderPermissionEvidenceResult result) : IFolderPermissionEvidenceProvider
    {
        public List<FolderPermissionEvidenceRequest> Requests { get; } = [];

        public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
            FolderPermissionEvidenceRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingEventStoreAuthorizationValidator(EventStoreAuthorizationValidationResult result) : IEventStoreAuthorizationValidator
    {
        public List<EventStoreAuthorizationValidationRequest> Requests { get; } = [];

        public Task<EventStoreAuthorizationValidationResult> ValidateAsync(
            EventStoreAuthorizationValidationRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingDaprPolicyEvidenceProvider(DaprPolicyEvidenceResult result) : IDaprPolicyEvidenceProvider
    {
        public List<DaprPolicyEvidenceRequest> Requests { get; } = [];

        public Task<DaprPolicyEvidenceResult> GetEvidenceAsync(
            DaprPolicyEvidenceRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(result);
        }
    }
}
