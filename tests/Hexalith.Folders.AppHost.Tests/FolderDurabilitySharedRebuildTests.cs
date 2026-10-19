using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

public sealed class FolderDurabilitySharedRebuildTests
{
    [Fact]
    public async Task UncertainActionResumesWithExactBodySessionAndHeadersUntilCompleted()
    {
        using RecordingDurabilityHttpHandler handler = new(attempt => attempt switch
        {
            1 => new(HttpStatusCode.ServiceUnavailable),
            2 => Outcome(ProjectionDispatchStatus.Indeterminate, ProjectionDispatchReasonCodes.HandlerFailure),
            3 => Outcome(ProjectionDispatchStatus.Retryable, ProjectionDispatchReasonCodes.PartialRetry),
            _ => Outcome(ProjectionDispatchStatus.Completed, null),
        });
        using HttpClient client = new(handler) { BaseAddress = new("https://fixture.invalid") };
        using HttpRequestMessage request = Request();
        DomainSharedProjectionRebuildResponse result = await FolderDurabilitySharedRebuild.SendAsync(client,
            request, DomainSharedProjectionRebuildAction.Accumulate, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        handler.Requests.Count.ShouldBe(4);
        foreach (var retained in handler.Requests)
        {
            retained.Method.ShouldBe("POST");
            retained.Uri.ShouldBe("https://fixture.invalid/project/rebuild/shared/v1");
            retained.Body.ShouldBe(handler.Requests[0].Body);
            retained.Headers.ShouldBe(handler.Requests[0].Headers);
        }
    }

    [Theory]
    [InlineData(ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.HandlerFailure)]
    [InlineData(ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict)]
    [InlineData(ProjectionDispatchStatus.Indeterminate, ProjectionDispatchReasonCodes.DeliveryReconciliationRequired)]
    [InlineData(ProjectionDispatchStatus.Indeterminate, ProjectionDispatchReasonCodes.MalformedOutcome)]
    public async Task TerminalOrUnsupportedOutcomesReturnImmediately(ProjectionDispatchStatus status, string reason)
    {
        using RecordingDurabilityHttpHandler handler = new(_ => Outcome(status, reason));
        using HttpClient client = new(handler) { BaseAddress = new("https://fixture.invalid") };
        using HttpRequestMessage request = Request();
        DomainSharedProjectionRebuildResponse result = await FolderDurabilitySharedRebuild.SendAsync(client,
            request, DomainSharedProjectionRebuildAction.Accumulate, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(status);
        result.ReasonCode.ShouldBe(reason);
        handler.Requests.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(422)]
    public async Task NonretryableHttpFailureStopsTheAction(int status)
    {
        using RecordingDurabilityHttpHandler handler = new(_ => new((HttpStatusCode)status));
        using HttpClient client = new(handler) { BaseAddress = new("https://fixture.invalid") };
        using HttpRequestMessage request = Request();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => FolderDurabilitySharedRebuild.SendAsync(client,
            request, DomainSharedProjectionRebuildAction.Accumulate, TestContext.Current.CancellationToken));
        handler.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task UnresolvedActionDeadlineFailsWithLastOutcome()
    {
        using RecordingDurabilityHttpHandler handler = new(_ => Outcome(
            ProjectionDispatchStatus.Indeterminate, ProjectionDispatchReasonCodes.HandlerFailure));
        using HttpClient client = new(handler) { BaseAddress = new("https://fixture.invalid") };
        using HttpRequestMessage request = Request();
        TimeoutException failure = await Should.ThrowAsync<TimeoutException>(() => FolderDurabilitySharedRebuild.SendAsync(client,
            request, DomainSharedProjectionRebuildAction.Accumulate, TestContext.Current.CancellationToken,
            TimeSpan.FromMilliseconds(250)));

        failure.Message.ShouldContain("Accumulate");
        failure.Message.ShouldContain("Indeterminate");
        failure.Message.ShouldContain(ProjectionDispatchReasonCodes.HandlerFailure);
        handler.Requests.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CallerCancellationStopsWithoutClaimingCompletion()
    {
        using RecordingDurabilityHttpHandler handler = new(_ => Outcome(
            ProjectionDispatchStatus.Retryable, ProjectionDispatchReasonCodes.PartialRetry));
        using HttpClient client = new(handler) { BaseAddress = new("https://fixture.invalid") };
        using HttpRequestMessage request = Request();
        using CancellationTokenSource canceled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        canceled.CancelAfter(TimeSpan.FromMilliseconds(250));
        _ = await Should.ThrowAsync<OperationCanceledException>(() => FolderDurabilitySharedRebuild.SendAsync(client,
            request, DomainSharedProjectionRebuildAction.Accumulate, canceled.Token));
        handler.Requests.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task MalformedCompletionCannotAdvanceTheFixture()
    {
        using RecordingDurabilityHttpHandler handler = new(_ => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new DomainSharedProjectionRebuildResponse(99,
                DomainSharedProjectionRebuildPhase.Accumulating, ProjectionDispatchStatus.Completed, 1, "inventory", null, null)),
        });
        using HttpClient client = new(handler) { BaseAddress = new("https://fixture.invalid") };
        using HttpRequestMessage request = Request();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => FolderDurabilitySharedRebuild.SendAsync(client,
            request, DomainSharedProjectionRebuildAction.Accumulate, TestContext.Current.CancellationToken));
        handler.Requests.ShouldHaveSingleItem();
    }

    private static HttpRequestMessage Request()
    {
        DomainSharedProjectionRebuildIdentity identity = new("tenant-a", "folders", "operational-status", "stable-operation", "catalog");
        ProjectionEventDto source = new("FolderCreated", "{}"u8.ToArray(), "json", 1,
            DateTimeOffset.UnixEpoch, "original-correlation", "original-message", "original-user", 77)
        { MetadataVersion = 1 };
        HttpRequestMessage request = new(HttpMethod.Post, "/project/rebuild/shared/v1")
        {
            Content = JsonContent.Create(new DomainSharedProjectionRebuildRequest(1,
                DomainSharedProjectionRebuildAction.Accumulate, identity, AggregateOrdinal: 0, AggregateId: "folder-a", Events: [source])),
        };
        request.Headers.Add("X-Hexalith-Workload-Assertion", "same-workload");
        request.Headers.Add("dapr-api-token", "same-channel");
        return request;
    }

    private static HttpResponseMessage Outcome(ProjectionDispatchStatus status, string? reason)
        => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new DomainSharedProjectionRebuildResponse(1,
                DomainSharedProjectionRebuildPhase.Accumulating, status, 0, "inventory", null, reason)),
        };
}
