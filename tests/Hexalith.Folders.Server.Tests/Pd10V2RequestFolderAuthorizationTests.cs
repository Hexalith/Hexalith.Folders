using System.Net;
using System.Text;
using System.Text.Json;

using Hexalith.Folders;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Server.Authorization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class Pd10V2RequestFolderAuthorizationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepositoryBackedCreationRequiresBodyFolderAuthorityBeforeHistoricalDispatch(bool unknownLength)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddFoldersLayeredAuthorization();
        builder.Services.AddSingleton<IPd10AuthorizationAuditSink, LoggingPd10AuthorizationAuditSink>();
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

        LayeredFolderAuthorizationResult tenantAuthorization = await app.Services
            .GetRequiredService<LayeredFolderAuthorizationService>()
            .AuthorizeTenantScopedAsync(
                new LayeredFolderAuthorizationContext(
                    "tenant-a", "user-a", "user-a", "manage_folder_access",
                    LayeredFolderOperationPolicy.Mutation(), identity.GetEvidence("manage_folder_access"),
                    OperationScope: null, CorrelationId: null, TaskId: null,
                    ClientControlledTenantValues: new Dictionary<string, string?>(),
                    ClientControlledPrincipalValues: new Dictionary<string, string?>()),
                TestContext.Current.CancellationToken).ConfigureAwait(true);
        tenantAuthorization.IsAllowed.ShouldBeTrue();

        int handlerCalls = 0;
        app.UsePd10V2CandidateCompatibilitySeam();
        app.MapPost("/api/v1/folders/repository-backed", () =>
        {
            handlerCalls++;
            return Results.Accepted();
        });
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        byte[] body = Encoding.UTF8.GetBytes("""{"folderId":"folder_0000000001"}""");
        using HttpContent content = unknownLength
            ? new StreamContent(new Pd10NonSeekableReadStream(body))
            : new StringContent(Encoding.UTF8.GetString(body), Encoding.UTF8, "application/json");
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        if (unknownLength)
        {
            content.Headers.ContentLength.ShouldBeNull();
        }

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v2/folders/repository-backed")
        {
            Content = content,
        };
        request.Headers.Add("Idempotency-Key", "same-key");
        using HttpResponseMessage response = await app.GetTestClient()
            .SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using JsonDocument problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
        problem.RootElement.GetProperty("category").GetString().ShouldBe("tenant_access_denied");
        handlerCalls.ShouldBe(0);
    }
}
