using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authorization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class Pd10V2ReadKeyPrecedenceTests
{
    [Fact]
    public async Task EveryGeneratedReadRejectsAKeyBeforeAuthorizationOrSourceAccess()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
        });
        builder.WebHost.UseTestServer();

        await using WebApplication app = builder.Build();
        app.UsePd10V2CandidateCompatibilitySeam();
        int handlerCalls = 0;
        app.MapFallback(() =>
        {
            handlerCalls++;
            return Results.Ok();
        });
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        Pd10ProtectedOperationDescriptor[] reads = Pd10ProtectedOperationCatalog.Descriptors
            .Where(descriptor => Pd10V2CandidateCompatibilitySeam.IsGeneratedReadOperation(descriptor.OperationId))
            .ToArray();
        reads.Length.ShouldBe(35);

        foreach (Pd10ProtectedOperationDescriptor descriptor in reads)
        {
            string path = Regex.Replace(descriptor.CandidateRoute, @"\{[^/{}]+\}", "scope_0000000001");
            using HttpRequestMessage request = new(new HttpMethod(descriptor.Method), path);
            request.Headers.Add("Idempotency-Key", "key_a");
            using HttpResponseMessage response = await app.GetTestClient()
                .SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, descriptor.OperationId);
            using JsonDocument problem = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            problem.RootElement.GetProperty("category").GetString().ShouldBe("validation_error");
            problem.RootElement.GetProperty("code").GetString().ShouldBe("idempotency_key_not_allowed");
        }

        handlerCalls.ShouldBe(0);
    }
}
