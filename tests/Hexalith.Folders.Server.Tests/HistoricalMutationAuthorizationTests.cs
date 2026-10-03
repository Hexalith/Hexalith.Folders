using System.Net;
using System.Text;
using System.Text.Json;

using Hexalith.Folders;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class HistoricalMutationAuthorizationTests
{
    [Fact]
    public async Task DirectV1MutationRequiresCurrentAuthorityBeforeTheGatewayHandler()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddFoldersLayeredAuthorization();
        builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
        builder.Services.AddSingleton<IEventStoreAuthorizationValidator, HistoricalMutationAllowingValidator>();
        builder.Services.RemoveAll<IDaprPolicyEvidenceProvider>();
        builder.Services.AddSingleton<IDaprPolicyEvidenceProvider, HistoricalMutationAllowingDaprPolicy>();
        HistoricalMutationStaticIdentity identity = new();
        builder.Services.AddSingleton<ITenantContextAccessor>(identity);
        builder.Services.AddSingleton<IEventStoreClaimTransformEvidenceAccessor>(identity);

        await using WebApplication app = builder.Build();
        InMemoryFolderTenantAccessProjectionStore tenants = (InMemoryFolderTenantAccessProjectionStore)app.Services
            .GetRequiredService<IFolderTenantAccessProjectionStore>();
        await tenants.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = "tenant-a",
            Enabled = true,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
            {
                ["user-a"] = new("user-a", "Member"),
            },
            Watermark = 1,
            ProjectionWatermark = "tenant-a:1",
            LastEventTimestamp = DateTimeOffset.UtcNow.AddMinutes(-1),
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);

        int handlerCalls = 0;
        int allowedCalls = 0;
        int repositoryHandlerCalls = 0;
        app.UseHistoricalMutationAuthorization();
        app.MapPost("/api/v1/folders/{folderId}/archive", () =>
        {
            handlerCalls++;
            return Results.Accepted();
        });
        app.MapPost("/api/v1/folders", () =>
        {
            allowedCalls++;
            return Results.Accepted();
        });
        app.MapPost("/api/v1/folders/repository-backed", () =>
        {
            repositoryHandlerCalls++;
            return Results.Accepted();
        });
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/folders/folder-a/archive");
        request.Headers.Add("Idempotency-Key", "same-key");
        request.Headers.Add("X-Correlation-Id", "correlation-a");
        request.Headers.Add("X-Hexalith-Task-Id", "task-a");
        using HttpResponseMessage response = await app.GetTestClient()
            .SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using JsonDocument problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
        problem.RootElement.GetProperty("category").GetString().ShouldBe("not_found_to_caller");
        handlerCalls.ShouldBe(0);

        using HttpRequestMessage allowed = new(HttpMethod.Post, "/api/v1/folders");
        using HttpResponseMessage accepted = await app.GetTestClient()
            .SendAsync(allowed, TestContext.Current.CancellationToken).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        allowedCalls.ShouldBe(1);

        using HttpRequestMessage repository = new(HttpMethod.Post, "/api/v1/folders/repository-backed")
        {
            Content = new StringContent("""{"folderId":"folder-b"}""", Encoding.UTF8, "application/json"),
        };
        repository.Headers.Add("Idempotency-Key", "same-key");
        using HttpResponseMessage repositoryDenied = await app.GetTestClient()
            .SendAsync(repository, TestContext.Current.CancellationToken).ConfigureAwait(true);
        repositoryDenied.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        repositoryHandlerCalls.ShouldBe(0);
    }
}
