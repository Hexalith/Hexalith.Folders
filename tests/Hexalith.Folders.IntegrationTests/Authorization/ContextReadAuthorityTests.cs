using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.FileContext;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Testing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NSubstitute;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.IntegrationTests.Authorization;

public sealed class ContextReadAuthorityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private const string Folder = "folder_0000000001";
    private const string Workspace = "workspace_000001";

    [Theory]
    [InlineData("tree")]
    [InlineData("metadata")]
    [InlineData("search")]
    [InlineData("glob")]
    [InlineData("range-read")]
    public async Task EveryContextReadRequiresWriteAtBothPublicBoundaries(string operation)
    {
        foreach (bool candidate in new[] { false, true })
        foreach (bool write in new[] { false, true })
        {
            IFolderPermissionEvidenceProvider evidence = Substitute.For<IFolderPermissionEvidenceProvider>();
            evidence.GetEvidenceAsync(Arg.Any<FolderPermissionEvidenceRequest>(), Arg.Any<CancellationToken>())
                .Returns(call => call.Arg<FolderPermissionEvidenceRequest>().ActionToken == "mutate_files" && !write
                    ? FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Denied, "acl:1")
                    : FolderPermissionEvidenceResult.Allowed("acl:1"));
            IWorkspaceFileContextSource source = Source();
            IWorkspaceFileSensitivityClassifier sensitivity = Sensitivity();
            await using WebApplication app = BuildApp(evidence, source, sensitivity);
            await app.StartAsync(TestContext.Current.CancellationToken);
            using HttpRequestMessage request = Request(operation, candidate);
            using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(write ? HttpStatusCode.OK : HttpStatusCode.NotFound,
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            if (!write)
            {
                await source.DidNotReceive().QueryAsync(Arg.Any<WorkspaceFileContextSourceRequest>(), Arg.Any<CancellationToken>());
                await sensitivity.DidNotReceive().ClassifyAsync(Arg.Any<PathMetadata>(), Arg.Any<CancellationToken>());
                if (candidate) await AssertCanonicalAsync(response, "tenant_access_denied", "resource_unavailable");
            }
            else
            {
                await source.Received(1).QueryAsync(Arg.Is<WorkspaceFileContextSourceRequest>(value =>
                    value.ActionToken == "mutate_files" && value.ManagedTenantId == "tenant-a" && value.FolderId == Folder),
                    Arg.Any<CancellationToken>());
            }
        }
    }

    [Theory]
    [InlineData(FolderPermissionEvidenceStatus.Stale)]
    [InlineData(FolderPermissionEvidenceStatus.Unavailable)]
    [InlineData(FolderPermissionEvidenceStatus.Malformed)]
    [InlineData(FolderPermissionEvidenceStatus.Allowed)]
    public async Task ChangedUnusableEvidenceStopsTheInnerReadBeforeAnyObservation(FolderPermissionEvidenceStatus status)
    {
        int calls = 0;
        IFolderPermissionEvidenceProvider evidence = Substitute.For<IFolderPermissionEvidenceProvider>();
        evidence.GetEvidenceAsync(Arg.Any<FolderPermissionEvidenceRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++calls == 1 ? FolderPermissionEvidenceResult.Allowed("acl:1")
                : FolderPermissionEvidenceResult.FromStatus(status, null));
        IWorkspaceFileContextSource source = Source();
        IWorkspaceFileSensitivityClassifier sensitivity = Sensitivity();
        await using WebApplication app = BuildApp(evidence, source, sensitivity);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(Request("metadata", true), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        await AssertCanonicalAsync(response, "read_model_unavailable", "projection_unavailable");
        calls.ShouldBe(2);
        await source.DidNotReceive().QueryAsync(Arg.Any<WorkspaceFileContextSourceRequest>(), Arg.Any<CancellationToken>());
        await sensitivity.DidNotReceive().ClassifyAsync(Arg.Any<PathMetadata>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(FolderPermissionEvidenceStatus.Allowed)]
    [InlineData(FolderPermissionEvidenceStatus.Denied)]
    [InlineData(FolderPermissionEvidenceStatus.Stale)]
    [InlineData(FolderPermissionEvidenceStatus.Unavailable)]
    public async Task DelegatorIsRecheckedWithTheActorBeforeProtectedContext(FolderPermissionEvidenceStatus status)
    {
        Dictionary<string, int> calls = new(StringComparer.Ordinal);
        IFolderPermissionEvidenceProvider evidence = Substitute.For<IFolderPermissionEvidenceProvider>();
        evidence.GetEvidenceAsync(Arg.Any<FolderPermissionEvidenceRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                FolderPermissionEvidenceRequest value = call.Arg<FolderPermissionEvidenceRequest>();
                value.ActionToken.ShouldBe("mutate_files");
                value.OperationScope.ShouldBe(Folder);
                calls[value.PrincipalId] = calls.GetValueOrDefault(value.PrincipalId) + 1;
                return value.PrincipalId == "delegator-a" && calls[value.PrincipalId] > 1
                    ? FolderPermissionEvidenceResult.FromStatus(status, "acl:2")
                    : FolderPermissionEvidenceResult.Allowed("acl:1");
            });
        IWorkspaceFileContextSource source = Source();
        IWorkspaceFileSensitivityClassifier sensitivity = Sensitivity();
        await using WebApplication app = BuildApp(evidence, source, sensitivity, delegated: true);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(Request("metadata", true), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(status switch
        {
            FolderPermissionEvidenceStatus.Allowed => HttpStatusCode.OK,
            FolderPermissionEvidenceStatus.Denied => HttpStatusCode.NotFound,
            _ => HttpStatusCode.ServiceUnavailable,
        });
        calls["actor-a"].ShouldBe(status == FolderPermissionEvidenceStatus.Allowed ? 4 : 2);
        calls["delegator-a"].ShouldBe(status == FolderPermissionEvidenceStatus.Allowed ? 4 : 2);
        if (status != FolderPermissionEvidenceStatus.Allowed)
        {
            await source.DidNotReceive().QueryAsync(Arg.Any<WorkspaceFileContextSourceRequest>(), Arg.Any<CancellationToken>());
            await sensitivity.DidNotReceive().ClassifyAsync(Arg.Any<PathMetadata>(), Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SensitivityAwaitCannotReuseEarlierActorOrDelegatorAuthority(bool delegated)
    {
        bool revoked = false;
        IFolderPermissionEvidenceProvider evidence = Substitute.For<IFolderPermissionEvidenceProvider>();
        evidence.GetEvidenceAsync(Arg.Any<FolderPermissionEvidenceRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => revoked && call.Arg<FolderPermissionEvidenceRequest>().PrincipalId == (delegated ? "delegator-a" : "actor-a")
                ? FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Denied, "acl:2")
                : FolderPermissionEvidenceResult.Allowed("acl:1"));
        IWorkspaceFileContextSource source = Source();
        IWorkspaceFileSensitivityClassifier sensitivity = Sensitivity();
        sensitivity.ClassifyAsync(Arg.Any<PathMetadata>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            revoked = true;
            return WorkspacePathSensitivityResult.Allowed();
        });
        await using WebApplication app = BuildApp(evidence, source, sensitivity, delegated);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(Request("metadata", true), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await source.DidNotReceive().QueryAsync(Arg.Any<WorkspaceFileContextSourceRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(FolderPermissionEvidenceStatus.Denied)]
    [InlineData(FolderPermissionEvidenceStatus.Stale)]
    [InlineData(FolderPermissionEvidenceStatus.Unavailable)]
    public async Task LegacyContextCannotBypassDelegatingPrincipal(FolderPermissionEvidenceStatus status)
    {
        IFolderPermissionEvidenceProvider evidence = Substitute.For<IFolderPermissionEvidenceProvider>();
        evidence.GetEvidenceAsync(Arg.Any<FolderPermissionEvidenceRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<FolderPermissionEvidenceRequest>().PrincipalId == "delegator-a"
                ? FolderPermissionEvidenceResult.FromStatus(status, "acl:1") : FolderPermissionEvidenceResult.Allowed("acl:1"));
        IWorkspaceFileContextSource source = Source();
        IWorkspaceFileSensitivityClassifier sensitivity = Sensitivity();
        await using WebApplication app = BuildApp(evidence, source, sensitivity, delegated: true);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(Request("metadata", false), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(status == FolderPermissionEvidenceStatus.Denied ? HttpStatusCode.NotFound : HttpStatusCode.ServiceUnavailable);
        await source.DidNotReceive().QueryAsync(Arg.Any<WorkspaceFileContextSourceRequest>(), Arg.Any<CancellationToken>());
        await sensitivity.DidNotReceive().ClassifyAsync(Arg.Any<PathMetadata>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AmbientDelegationCannotAuthorizeAnotherFolderOrTask(bool folderMismatch)
    {
        IFolderPermissionEvidenceProvider evidence = Substitute.For<IFolderPermissionEvidenceProvider>();
        IWorkspaceFileContextSource source = Source();
        IWorkspaceFileSensitivityClassifier sensitivity = Sensitivity();
        await using WebApplication app = BuildApp(evidence, source, sensitivity);
        using IServiceScope scope = app.Services.CreateScope();
        PreauthorizedRequestContext.Begin(new PreauthorizedRequestState("tenant-a", "actor-a",
            folderMismatch ? "another_folder01" : Folder, "acl:1", "tenant-a", "mutate_files", "mutate_files", "delegator-a")
        {
            TaskId = folderMismatch ? "task_00000000001" : "another_task0001",
        });
        try
        {
            WorkspaceFileContextQueryResult result = await scope.ServiceProvider.GetRequiredService<WorkspaceFileContextQueryHandler>()
                .HandleAsync(new(WorkspaceFileContextQueryKind.Metadata, Folder, Workspace, "tenant-a", "actor-a",
                    EventStoreClaimTransformEvidence.Allowed("tenant-a", "actor-a", ["mutate_files"]), null,
                    "task_00000000001", null, null, [new("docs/a.txt", "a.txt", "content_allowed", "NFC")]),
                    TestContext.Current.CancellationToken);
            result.Code.ShouldBe(WorkspaceFileContextResultCode.NotFoundSafe);
            await evidence.DidNotReceive().GetEvidenceAsync(Arg.Any<FolderPermissionEvidenceRequest>(), Arg.Any<CancellationToken>());
            await source.DidNotReceive().QueryAsync(Arg.Any<WorkspaceFileContextSourceRequest>(), Arg.Any<CancellationToken>());
            await sensitivity.DidNotReceive().ClassifyAsync(Arg.Any<PathMetadata>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            PreauthorizedRequestContext.End();
        }
    }

    [Theory]
    [InlineData("stale", false)]
    [InlineData("stale", true)]
    [InlineData("unavailable", false)]
    [InlineData("incomplete", false)]
    [InlineData("negative", false)]
    public async Task TenantAuthorityIsFreshBeforeFolderOrContextObservation(string state, bool disabled)
    {
        IFolderPermissionEvidenceProvider evidence = Substitute.For<IFolderPermissionEvidenceProvider>();
        IWorkspaceFileContextSource source = Source();
        IWorkspaceFileSensitivityClassifier sensitivity = Sensitivity();
        IFolderTenantAccessProjectionStore tenants = Substitute.For<IFolderTenantAccessProjectionStore>();
        if (state == "unavailable") tenants.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<FolderTenantAccessProjection?>(new IOException("unavailable")));
        else tenants.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new FolderTenantAccessProjection
        {
            TenantId = "tenant-a", Enabled = !disabled, Watermark = 1, ProjectionWatermark = "tenant:1",
            LastEventTimestamp = state == "stale" ? Now.AddDays(-1) : Now,
            MalformedEvidence = state == "incomplete",
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal),
        });
        await using WebApplication app = BuildApp(evidence, source, sensitivity, tenantStore: tenants);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(Request("metadata", true), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(state == "negative" ? HttpStatusCode.NotFound : HttpStatusCode.ServiceUnavailable);
        await evidence.DidNotReceive().GetEvidenceAsync(Arg.Any<FolderPermissionEvidenceRequest>(), Arg.Any<CancellationToken>());
        await source.DidNotReceive().QueryAsync(Arg.Any<WorkspaceFileContextSourceRequest>(), Arg.Any<CancellationToken>());
        await sensitivity.DidNotReceive().ClassifyAsync(Arg.Any<PathMetadata>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ProductionValidatorChecksTheDistinctSignedDelegator(bool candidate, bool changedDelegator)
    {
        IFolderPermissionEvidenceProvider evidence = Substitute.For<IFolderPermissionEvidenceProvider>();
        evidence.GetEvidenceAsync(Arg.Any<FolderPermissionEvidenceRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (changedDelegator && call.Arg<FolderPermissionEvidenceRequest>().PrincipalId == "delegator-a")
            {
                var identity = (ClaimsIdentity)new HttpContextAccessor().HttpContext!.User.Identity!;
                foreach (Claim claim in identity.FindAll("eventstore:delegator").ToArray()) identity.RemoveClaim(claim);
                identity.AddClaim(new("eventstore:delegator", "another-delegator"));
            }
            return FolderPermissionEvidenceResult.Allowed("acl:1");
        });
        IWorkspaceFileContextSource source = Source();
        IWorkspaceFileSensitivityClassifier sensitivity = Sensitivity();
        await using WebApplication app = BuildApp(evidence, source, sensitivity, delegated: true, productionValidator: true);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(Request("metadata", candidate), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(changedDelegator ? HttpStatusCode.NotFound : HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        if (changedDelegator)
        {
            await source.DidNotReceive().QueryAsync(Arg.Any<WorkspaceFileContextSourceRequest>(), Arg.Any<CancellationToken>());
            await sensitivity.DidNotReceive().ClassifyAsync(Arg.Any<PathMetadata>(), Arg.Any<CancellationToken>());
            if (candidate) await AssertCanonicalAsync(response, "tenant_access_denied", "resource_unavailable");
        }
    }

    [Theory]
    [InlineData("unavailable")]
    [InlineData("throw")]
    [InlineData("stale-allowed")]
    [InlineData("malformed-allowed")]
    [InlineData("malformed-dapr")]
    [InlineData("stale-dapr-allowed")]
    [InlineData("malformed-dapr-allowed")]
    [InlineData("stateless-fresh")]
    public async Task InnerEvidenceAvailabilitySurvivesTheHttpBoundary(string state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (bool candidate in new[] { false, true })
        {
            int validations = 0, policies = 0;
            IEventStoreAuthorizationValidator validator = Substitute.For<IEventStoreAuthorizationValidator>();
            validator.ValidateAsync(Arg.Any<EventStoreAuthorizationValidationRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
            {
                if (candidate && ++validations == 1) return EventStoreAuthorizationValidationResult.Allowed(null);
                return state switch
                {
                    "throw" => throw new IOException("synthetic validator outage"),
                    "unavailable" => EventStoreAuthorizationValidationResult.Unavailable(),
                    "stale-allowed" => EventStoreAuthorizationValidationResult.Allowed(null) with { FreshnessClass = "stale" },
                    "malformed-allowed" => EventStoreAuthorizationValidationResult.Allowed(null) with { FreshnessClass = "malformed" },
                    _ => EventStoreAuthorizationValidationResult.Allowed(null),
                };
            });
            IDaprPolicyEvidenceProvider dapr = Substitute.For<IDaprPolicyEvidenceProvider>();
            dapr.GetEvidenceAsync(Arg.Any<DaprPolicyEvidenceRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
            {
                if (candidate && ++policies == 1) return DaprPolicyEvidenceResult.Allowed("folders", null);
                return state switch
                {
                    "malformed-dapr" => DaprPolicyEvidenceResult.Malformed(),
                    "stale-dapr-allowed" => DaprPolicyEvidenceResult.Allowed("folders", null) with { FreshnessClass = "stale" },
                    "malformed-dapr-allowed" => DaprPolicyEvidenceResult.Allowed("folders", null) with { FreshnessClass = "malformed" },
                    _ => DaprPolicyEvidenceResult.Allowed("folders", null),
                };
            });
            IFolderPermissionEvidenceProvider evidence = Substitute.For<IFolderPermissionEvidenceProvider>();
            evidence.GetEvidenceAsync(Arg.Any<FolderPermissionEvidenceRequest>(), Arg.Any<CancellationToken>())
                .Returns(FolderPermissionEvidenceResult.Allowed("acl:1"));
            IWorkspaceFileContextSource source = Source();
            IWorkspaceFileSensitivityClassifier sensitivity = Sensitivity();
            await using WebApplication app = BuildApp(evidence, source, sensitivity, validatorOverride: validator, daprOverride: dapr);
            await app.StartAsync(TestContext.Current.CancellationToken);
            using HttpResponseMessage response = await app.GetTestClient().SendAsync(Request("metadata", candidate), TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(state == "stateless-fresh" ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
            if (state != "stateless-fresh")
            {
                if (candidate) await AssertCanonicalAsync(response, "read_model_unavailable", "projection_unavailable");
                else
                {
                    using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                    problem.RootElement.GetProperty("retryable").GetBoolean().ShouldBeTrue();
                    problem.RootElement.GetProperty("details").GetProperty("visibility").GetString().ShouldBe("metadata_only");
                }
                await source.DidNotReceive().QueryAsync(Arg.Any<WorkspaceFileContextSourceRequest>(), Arg.Any<CancellationToken>());
                await sensitivity.DidNotReceive().ClassifyAsync(Arg.Any<PathMetadata>(), Arg.Any<CancellationToken>());
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingWatermarkCannotTurnFreshNegativeTenantEvidenceIntoDefinitiveDenial(bool disabled)
    {
        foreach (bool candidate in new[] { false, true })
        {
            int reads = 0;
            IFolderTenantAccessProjectionStore tenants = Substitute.For<IFolderTenantAccessProjectionStore>();
            tenants.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ =>
            {
                bool outer = candidate && ++reads == 1;
                return new FolderTenantAccessProjection
                {
                    TenantId = "tenant-a", Enabled = outer || !disabled, Watermark = 1,
                    ProjectionWatermark = outer ? "tenant:1" : string.Empty, LastEventTimestamp = Now,
                    Principals = outer ? new(StringComparer.Ordinal) { ["actor-a"] = new("actor-a", "Member") } : new(StringComparer.Ordinal),
                };
            });
            IFolderPermissionEvidenceProvider evidence = Substitute.For<IFolderPermissionEvidenceProvider>();
            evidence.GetEvidenceAsync(Arg.Any<FolderPermissionEvidenceRequest>(), Arg.Any<CancellationToken>()).Returns(FolderPermissionEvidenceResult.Allowed("acl:1"));
            IWorkspaceFileContextSource source = Source(); IWorkspaceFileSensitivityClassifier sensitivity = Sensitivity();
            await using WebApplication app = BuildApp(evidence, source, sensitivity, tenantStore: tenants);
            await app.StartAsync(TestContext.Current.CancellationToken);
            using HttpResponseMessage response = await app.GetTestClient().SendAsync(Request("metadata", candidate), TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            await source.DidNotReceive().QueryAsync(Arg.Any<WorkspaceFileContextSourceRequest>(), Arg.Any<CancellationToken>());
            await sensitivity.DidNotReceive().ClassifyAsync(Arg.Any<PathMetadata>(), Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData(WorkspaceFileSensitivityDecision.Allowed)]
    [InlineData(WorkspaceFileSensitivityDecision.Redacted)]
    [InlineData(WorkspaceFileSensitivityDecision.Unavailable)]
    public async Task EachClassificationRechecksRevocationBeforeItsResultOrTheNextPath(WorkspaceFileSensitivityDecision decision)
    {
        bool revoked = false;
        IFolderPermissionEvidenceProvider evidence = Substitute.For<IFolderPermissionEvidenceProvider>();
        evidence.GetEvidenceAsync(Arg.Any<FolderPermissionEvidenceRequest>(), Arg.Any<CancellationToken>()).Returns(_ => revoked
            ? FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Denied, "acl:2") : FolderPermissionEvidenceResult.Allowed("acl:1"));
        IWorkspaceFileContextSource source = Source(); IWorkspaceFileSensitivityClassifier sensitivity = Sensitivity();
        sensitivity.ClassifyAsync(Arg.Any<PathMetadata>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            revoked = true;
            return decision switch
            {
                WorkspaceFileSensitivityDecision.Redacted => WorkspacePathSensitivityResult.Redacted(),
                WorkspaceFileSensitivityDecision.Unavailable => WorkspacePathSensitivityResult.Unavailable(),
                _ => WorkspacePathSensitivityResult.Allowed(),
            };
        });
        await using WebApplication app = BuildApp(evidence, source, sensitivity);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(Request("metadata", true, pathCount: 2), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await AssertCanonicalAsync(response, "tenant_access_denied", "resource_unavailable");
        await sensitivity.Received(1).ClassifyAsync(Arg.Any<PathMetadata>(), Arg.Any<CancellationToken>());
        await source.DidNotReceive().QueryAsync(Arg.Any<WorkspaceFileContextSourceRequest>(), Arg.Any<CancellationToken>());
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldNotContain("docs/"); body.ShouldNotContain("contentBytes");
    }

    [Theory]
    [InlineData("conflicting")]
    [InlineData("missing-permission")]
    [InlineData("missing-delegator")]
    [InlineData("blank")]
    [InlineData("self")]
    public async Task LegacyRealClaimAccessorRejectsMalformedDelegationBeforeObservation(string state)
    {
        IFolderPermissionEvidenceProvider evidence = Substitute.For<IFolderPermissionEvidenceProvider>();
        evidence.GetEvidenceAsync(Arg.Any<FolderPermissionEvidenceRequest>(), Arg.Any<CancellationToken>()).Returns(FolderPermissionEvidenceResult.Allowed("acl:1"));
        IWorkspaceFileContextSource source = Source(); IWorkspaceFileSensitivityClassifier sensitivity = Sensitivity();
        await using WebApplication app = BuildApp(evidence, source, sensitivity, delegated: true, productionValidator: true, alterClaims: claims =>
        {
            if (state == "conflicting") claims.Add(new("eventstore:delegator", "another-delegator"));
            else if (state == "missing-permission") claims.RemoveAll(claim => claim.Type == "eventstore:delegator-permission");
            else if (state == "missing-delegator") claims.RemoveAll(claim => claim.Type == "eventstore:delegator");
            else
            {
                claims.RemoveAll(claim => claim.Type == "eventstore:delegator");
                claims.Add(new("eventstore:delegator", state == "self" ? "actor-a" : " "));
            }
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(Request("metadata", false), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        await source.DidNotReceive().QueryAsync(Arg.Any<WorkspaceFileContextSourceRequest>(), Arg.Any<CancellationToken>());
        await sensitivity.DidNotReceive().ClassifyAsync(Arg.Any<PathMetadata>(), Arg.Any<CancellationToken>());
    }

    private static WebApplication BuildApp(IFolderPermissionEvidenceProvider evidence, IWorkspaceFileContextSource source,
        IWorkspaceFileSensitivityClassifier sensitivity, bool delegated = false, IFolderTenantAccessProjectionStore? tenantStore = null,
        bool productionValidator = false, IEventStoreAuthorizationValidator? validatorOverride = null,
        IDaprPolicyEvidenceProvider? daprOverride = null, Action<List<Claim>>? alterClaims = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Services.AddFoldersServerTestDefaults();
        builder.Services.AddFoldersServer();
        builder.Services.AddInMemoryFolderRepository();
        IUtcClock clock = Substitute.For<IUtcClock>();
        clock.UtcNow.Returns(Now);
        Replace(builder, clock); Replace(builder, evidence); Replace(builder, source); Replace(builder, sensitivity);
        IEventStoreAuthorizationValidator validator = Substitute.For<IEventStoreAuthorizationValidator>();
        validator.ValidateAsync(Arg.Any<EventStoreAuthorizationValidationRequest>(), Arg.Any<CancellationToken>())
            .Returns(EventStoreAuthorizationValidationResult.Allowed("validator:1"));
        if (productionValidator)
        {
            builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
            builder.Services.AddSingleton<IEventStoreAuthorizationValidator, VerifiedReceiptEventStoreAuthorizationValidator>();
        }
        else Replace(builder, validatorOverride ?? validator);
        IDaprPolicyEvidenceProvider dapr = Substitute.For<IDaprPolicyEvidenceProvider>();
        dapr.GetEvidenceAsync(Arg.Any<DaprPolicyEvidenceRequest>(), Arg.Any<CancellationToken>())
            .Returns(DaprPolicyEvidenceResult.Allowed("folders", "policy:1"));
        Replace(builder, daprOverride ?? dapr);
        InMemoryFolderTenantAccessProjectionStore tenants = new();
        tenants.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = "tenant-a", Enabled = true, Watermark = 1, ProjectionWatermark = "tenant:1", LastEventTimestamp = Now,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
            {
                ["actor-a"] = new("actor-a", "Member"), ["delegator-a"] = new("delegator-a", "Member"),
            },
        }).GetAwaiter().GetResult();
        Replace<IFolderTenantAccessProjectionStore>(builder, tenantStore ?? tenants);
        WebApplication app = builder.Build();
        app.Use(async (http, next) =>
        {
            List<Claim> claims = [new("eventstore:tenant", "tenant-a"), new("sub", "actor-a"), new(ClaimTypes.NameIdentifier, "actor-a"),
                new("eventstore:permission", "mutate_files")];
            if (delegated) claims.AddRange([new("eventstore:delegator", "delegator-a"), new("eventstore:delegator-permission", "mutate_files")]);
            alterClaims?.Invoke(claims);
            http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "context-test"));
            await next(http).ConfigureAwait(true);
        });
        app.UsePd10V2CandidateCompatibilitySeam();
        app.UseRouting();
        app.MapFoldersServerEndpoints();
        return app;
    }

    private static void Replace<T>(WebApplicationBuilder builder, T value) where T : class
    {
        builder.Services.RemoveAll<T>();
        builder.Services.AddSingleton(value);
    }

    private static IWorkspaceFileSensitivityClassifier Sensitivity()
    {
        IWorkspaceFileSensitivityClassifier value = Substitute.For<IWorkspaceFileSensitivityClassifier>();
        value.ClassifyAsync(Arg.Any<PathMetadata>(), Arg.Any<CancellationToken>()).Returns(WorkspacePathSensitivityResult.Allowed());
        return value;
    }

    private static IWorkspaceFileContextSource Source()
    {
        IWorkspaceFileContextSource value = Substitute.For<IWorkspaceFileContextSource>();
        value.QueryAsync(Arg.Any<WorkspaceFileContextSourceRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            WorkspaceFileContextSourceRequest request = call.Arg<WorkspaceFileContextSourceRequest>();
            return new WorkspaceFileContextSourceResult(WorkspaceFileContextSourceStatus.Available, [],
                request.Kind == WorkspaceFileContextQueryKind.Range ? new PathMetadata("docs/a.txt", "a.txt", "content_allowed", "NFC") : null,
                request.Kind == WorkspaceFileContextQueryKind.Range ? new WorkspaceFileContextRange(0, 1, 1, false) : null,
                request.Kind == WorkspaceFileContextQueryKind.Range ? "YQ==" : null, null,
                new("metadata", 10, 0, 1, 0, false, "not_truncated"),
                new FolderLifecycleFreshness("snapshot_per_task", Now, "source_00000001", false, "authoritative_content"));
        });
        return value;
    }

    private static HttpRequestMessage Request(string operation, bool candidate, int pathCount = 1)
    {
        string path = $"/api/{(candidate ? "v2" : "v1")}/folders/{Folder}/workspaces/{Workspace}/context/{operation}";
        HttpRequestMessage request = new(operation == "tree" ? HttpMethod.Get : HttpMethod.Post, path);
        object[] paths = Enumerable.Range(0, pathCount).Select(index => (object)new { normalizedPath = $"docs/a{index}.txt", displayName = $"a{index}.txt", pathPolicyClass = "tenant_sensitive_document", unicodeNormalization = "NFC" }).ToArray();
        if (operation != "tree")
        {
            object body = operation switch
            {
                "metadata" => new { requestSchemaVersion = candidate ? "v2" : "v1", paths },
                "search" => new { requestSchemaVersion = candidate ? "v2" : "v1", queryFamily = "search", queryText = "needle", requestedPaths = paths, limit = 10 },
                "glob" => new { requestSchemaVersion = candidate ? "v2" : "v1", queryFamily = "glob", globPattern = "*.txt", requestedPaths = paths, limit = 10 },
                _ => new { requestSchemaVersion = candidate ? "v2" : "v1", path = paths[0], startOffset = 0, endOffset = 1 },
            };
            request.Content = JsonContent.Create(body);
        }
        request.Headers.Add("X-Hexalith-Task-Id", "task_00000000001");
        request.Headers.Add("X-Hexalith-Freshness", "snapshot_per_task");
        return request;
    }

    private static async Task AssertCanonicalAsync(HttpResponseMessage response, string category, string code)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
        problem.RootElement.GetProperty("category").GetString().ShouldBe(category);
        problem.RootElement.GetProperty("code").GetString().ShouldBe(code);
    }
}
