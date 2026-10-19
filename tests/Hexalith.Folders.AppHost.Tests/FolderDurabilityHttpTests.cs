using System.Net;
using System.Net.Http.Json;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

public sealed class FolderDurabilityHttpTests
{
    [Fact]
    public async Task TransientAdmissionAndThrottleRetriesPreserveExactRequestIdentity()
    {
        using RecordingDurabilityHttpHandler handler = new(attempt => attempt switch
        {
            1 => RecordingDurabilityHttpHandler.Problem(HttpStatusCode.ServiceUnavailable, "idempotency_admission_unavailable", "service_unavailable", true),
            2 => RecordingDurabilityHttpHandler.Problem(HttpStatusCode.ServiceUnavailable, "folder_acl_unavailable", "read_model_unavailable", true),
            3 => new(HttpStatusCode.TooManyRequests),
            _ => new(HttpStatusCode.Accepted),
        });
        using HttpClient client = new(handler) { BaseAddress = new("https://fixture.invalid") };
        using HttpRequestMessage request = new(HttpMethod.Put, "/mutation") { Content = JsonContent.Create(new { value = "original" }) };
        request.Headers.Add("Idempotency-Key", "same-key");
        request.Headers.Add("X-Correlation-Id", "same-correlation");
        request.Headers.Add("X-Hexalith-Task-Id", "same-task");
        using HttpResponseMessage result = await FolderDurabilityHttp.SendAsync(client, request, false, TestContext.Current.CancellationToken);
        result.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        handler.Requests.Count.ShouldBe(4);
        foreach (var retained in handler.Requests)
        {
            retained.Method.ShouldBe("PUT");
            retained.Uri.ShouldBe("https://fixture.invalid/mutation");
            retained.Body.ShouldBe(handler.Requests[0].Body);
            retained.Headers.ShouldBe(handler.Requests[0].Headers);
        }
    }

    [Theory]
    [InlineData(503, "unknown_provider_outcome", "unknown_provider_outcome", true)]
    [InlineData(503, "provider_unavailable", "provider_unavailable", true)]
    [InlineData(503, "idempotency_admission_unavailable", "service_unavailable", false)]
    [InlineData(503, "folder_acl_stale", "read_model_unavailable", false)]
    [InlineData(403, "authorization_denied", "authorization_denied", false)]
    [InlineData(409, "idempotency_conflict", "idempotency_conflict", false)]
    [InlineData(500, "internal_error", "internal_error", true)]
    public async Task BusinessAndPermanentFailuresRemainVisible(int status, string code, string category, bool retryable)
    {
        using RecordingDurabilityHttpHandler handler = new(_ => RecordingDurabilityHttpHandler.Problem((HttpStatusCode)status, code, category, retryable));
        using HttpClient client = new(handler) { BaseAddress = new("https://fixture.invalid") };
        using HttpRequestMessage request = new(HttpMethod.Post, "/mutation") { Content = JsonContent.Create(new { value = "original" }) };
        using HttpResponseMessage result = await FolderDurabilityHttp.SendAsync(client, request, false, TestContext.Current.CancellationToken);
        result.StatusCode.ShouldBe((HttpStatusCode)status);
        handler.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ReadRetriesHonorCallerCancellationWithoutClaimingSuccess()
    {
        using RecordingDurabilityHttpHandler handler = new(_ => new(HttpStatusCode.ServiceUnavailable));
        using HttpClient client = new(handler) { BaseAddress = new("https://fixture.invalid") };
        using HttpRequestMessage request = new(HttpMethod.Get, "/status");
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMilliseconds(250));
        await Should.ThrowAsync<OperationCanceledException>(() => FolderDurabilityHttp.SendAsync(client, request, true, deadline.Token));
        handler.Requests.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ReadinessWaitsForFreshAclAndOperationalRowsAtTheRetainedHead()
    {
        using RecordingDurabilityHttpHandler handler = new(attempt => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                folderId = "folder-a",
                permissions = new[] { "write" },
                authorizationOutcome = "allowed",
                freshness = new { stale = attempt == 1, projectionWatermark = attempt <= 2 ? "8" : "9" },
            }),
        });
        using HttpClient client = new(handler) { BaseAddress = new("https://fixture.invalid") };
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/effective-permissions");
        int observations = 0;
        await FolderDurabilityHttp.WaitForCurrentFolderAsync(client, request, "folder-a", "workspace lock",
            _ => Task.FromResult((9L, ++observations < 4 ? 8L : 9L)), TestContext.Current.CancellationToken);
        handler.Requests.Count.ShouldBe(4);
        handler.Requests.ShouldAllBe(retained => retained.Method == "GET");
    }

    [Fact]
    public async Task FreshDeniedReadinessStopsBeforeAnyPositiveMutation()
    {
        using RecordingDurabilityHttpHandler handler = new(_ => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                folderId = "folder-a",
                permissions = Array.Empty<string>(),
                authorizationOutcome = "denied_safe",
                freshness = new { stale = false, projectionWatermark = "9" },
            }),
        });
        using HttpClient client = new(handler) { BaseAddress = new("https://fixture.invalid") };
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/folders/folder-a/effective-permissions");
        _ = await Should.ThrowAsync<InvalidOperationException>(() => FolderDurabilityHttp.WaitForCurrentFolderAsync(
            client, request, "folder-a", "workspace lock", _ => Task.FromResult((9L, 9L)), TestContext.Current.CancellationToken));
        handler.Requests.ShouldHaveSingleItem();
    }
}
