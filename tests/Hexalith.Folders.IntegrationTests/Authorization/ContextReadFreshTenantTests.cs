using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.FileContext;

using NSubstitute;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.IntegrationTests.Authorization;

public sealed class ContextReadFreshTenantTests
{
    [Theory]
    [InlineData("stale")]
    [InlineData("stale-disabled")]
    [InlineData("unavailable")]
    [InlineData("missing-watermark")]
    [InlineData("malformed")]
    [InlineData("fresh-negative")]
    public async Task EveryContextFamilyRechecksTenantBeforeClassifierOrSource(string state)
    {
        ArgumentNullException.ThrowIfNull(state);
        DateTimeOffset now = new(2026, 10, 25, 7, 0, 0, TimeSpan.Zero);
        IUtcClock clock = Substitute.For<IUtcClock>();
        clock.UtcNow.Returns(now);
        IFolderTenantAccessProjectionStore tenants = Substitute.For<IFolderTenantAccessProjectionStore>();
        FolderTenantAccessProjection projection = new()
        {
            TenantId = "tenant-a",
            Enabled = state != "stale-disabled",
            Watermark = 1,
            ProjectionWatermark = state == "missing-watermark" ? string.Empty : "tenant:current",
            LastEventTimestamp = state.StartsWith("stale", StringComparison.Ordinal) ? now.AddHours(-1) : now,
            MalformedEvidence = state == "malformed",
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
            {
                [state == "fresh-negative" ? "someone-else" : "actor-a"] = new(state == "fresh-negative" ? "someone-else" : "actor-a", "Member"),
            },
        };
        tenants.GetAsync("tenant-a", Arg.Any<CancellationToken>()).Returns(_ => state == "unavailable"
            ? throw new InvalidOperationException("Synthetic authority outage") : projection);
        IFolderPermissionEvidenceProvider permissions = Substitute.For<IFolderPermissionEvidenceProvider>();
        IWorkspaceFileContextSource source = Substitute.For<IWorkspaceFileContextSource>();
        IWorkspaceFileSensitivityClassifier classifier = Substitute.For<IWorkspaceFileSensitivityClassifier>();
        LayeredFolderAuthorizationService authorization = new(new TenantAccessAuthorizer(tenants, clock, new()), permissions,
            Substitute.For<IEventStoreAuthorizationValidator>(), Substitute.For<IDaprPolicyEvidenceProvider>(), clock);
        WorkspaceFileContextQueryHandler handler = new(authorization, source, classifier, clock);
        foreach (WorkspaceFileContextQueryKind kind in Enum.GetValues<WorkspaceFileContextQueryKind>())
        {
            WorkspaceFileContextQueryResult result = await handler.HandleAsync(new(kind, "folder-a", "workspace-a", "tenant-a", "actor-a",
                EventStoreClaimTransformEvidence.Allowed("tenant-a", "actor-a", ["mutate_files"]), "correlation-a", "task-a", null, null),
                TestContext.Current.CancellationToken);
            result.Code.ShouldBe(state == "fresh-negative" ? WorkspaceFileContextResultCode.AuthorizationDenied
                : WorkspaceFileContextResultCode.ReadModelUnavailable);
        }
        await tenants.Received(5).GetAsync("tenant-a", Arg.Any<CancellationToken>());
        await permissions.DidNotReceive().GetEvidenceAsync(Arg.Any<FolderPermissionEvidenceRequest>(), Arg.Any<CancellationToken>());
        await classifier.DidNotReceive().ClassifyAsync(Arg.Any<Hexalith.Folders.Aggregates.Folder.PathMetadata>(), Arg.Any<CancellationToken>());
        await source.DidNotReceive().QueryAsync(Arg.Any<WorkspaceFileContextSourceRequest>(), Arg.Any<CancellationToken>());
    }
}
