using System.Net;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.Testing.Http;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Explicit real-Keycloak component controls, driven by Platform's owned verifier; never actual enrollment evidence.</summary>
[Collection("Source head boundary")]
public sealed class EnrolledWorkloadSourceBoundaryTests
{
    /// <summary>Real issuer/SDK refusals precede source transport; an exact assertion still needs its independent RSA source proof.</summary>
    /// <param name="fault">The isolated request, response or proof fault.</param>
    [Theory(Explicit = true)]
    [InlineData("none")]
    [InlineData("credential")]
    [InlineData("caller")]
    [InlineData("tenant")]
    [InlineData("audience")]
    [InlineData("signature")]
    [InlineData("scope")]
    [InlineData("expired")]
    [InlineData("malformed")]
    [InlineData("missing-proof")]
    [InlineData("cancelled")]
    public async Task ActualIssuerAndIndependentSdkGuardEverySourceRequest(string fault)
    {
        string componentFile = Environment.GetEnvironmentVariable("HEXALITH_WORKLOAD_COMPONENT_FILE")
            ?? throw new InvalidOperationException("Run this explicit component suite through Platform infrastructure/keycloak-workload-binding/verify_component.py --sdk-assembly <Debug assembly>.");
        using JsonDocument component = JsonDocument.Parse(await File.ReadAllBytesAsync(componentFile, TestContext.Current.CancellationToken));
        JsonElement settings = component.RootElement;
        string authority = settings.GetProperty("issuer").GetString()!;
        using RSA proof = RSA.Create(2048);
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Authority"] = "https://human.example.invalid/realms/hexalith",
            ["Authentication:JwtBearer:Issuer"] = "https://human.example.invalid/realms/hexalith",
            ["Authentication:JwtBearer:Audience"] = "human-client",
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "RS256",
            ["Authentication:WorkloadJwtBearer:Authority"] = authority,
            ["Authentication:WorkloadJwtBearer:Issuer"] = authority,
            ["Authentication:WorkloadJwtBearer:Audience"] = "eventstore",
            ["Authentication:WorkloadJwtBearer:AllowedAlgorithms:0"] = "RS256",
            ["Authentication:WorkloadJwtBearer:RequireHttpsMetadata"] = "true",
            ["Authentication:WorkloadIssuer:Workload"] = fault == "caller" ? "other" : "folders-component",
            ["Authentication:WorkloadIssuer:ClientId"] = "folders-component",
            ["Authentication:WorkloadIssuer:ClientSecret"] = fault == "credential" ? "incorrect-component-credential" : settings.GetProperty("secret").GetString(),
            ["Authentication:WorkloadIssuer:TokenEndpoint"] = authority + "/protocol/openid-connect/token",
            ["Authentication:WorkloadIssuer:EnableResourceBindingRequests"] = "true",
            ["Authentication:BoundStreamRead:PrivateKey"] = fault == "missing-proof" ? null : Convert.ToBase64String(proof.ExportPkcs8PrivateKey()),
            ["DAPR_HTTP_ENDPOINT"] = "http://localhost:3500",
            ["EventStore:Gateway:AppId"] = "eventstore-component",
        });
        using var transport = new EnrolledWorkloadIssuerTransport(settings.GetProperty("certificatePath").GetString()!, fault,
            settings.GetProperty("expiredTokenFile").GetString()!);
        using var sourceTransport = new MockHttpMessageHandler(request =>
        {
            request.Headers.TryGetValues(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, out IEnumerable<string>? assertions).ShouldBeTrue();
            JwtWorkloadAssertionIssuer.GetTokenScopeFailure(assertions!.Single(), "eventstore", "eventstore:stream-read").ShouldBeNull();
            request.Headers.TryGetValues(BoundStreamReadProof.HeaderName, out IEnumerable<string>? proofs).ShouldBeTrue();
            StreamReadRequest actual = JsonSerializer.Deserialize<StreamReadRequest>(request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            BoundStreamReadProof.Verify(proofs!.Single(), actual, "folders-component", Convert.ToBase64String(proof.ExportSubjectPublicKeyInfo()), DateTimeOffset.UtcNow).ShouldBeTrue();
            // Unavailable source is never fabricated into an authoritative zero observation.
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });
        builder.Services.AddEventStoreGatewayNativeSourceMetadataReader("folders", fault == "audience" ? "other" : "eventstore")
            .ConfigurePrimaryHttpMessageHandler(() => sourceTransport);
        builder.Services.AddHttpClient(JwtWorkloadAssertionIssuer.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => transport);
        await using ServiceProvider provider = builder.Services.BuildServiceProvider();
        IWorkloadAssertionIssuer issuer = provider.GetRequiredService<IWorkloadAssertionIssuer>();
        issuer.CanBindResources.ShouldBeTrue();
        if (fault == "none")
        {
            foreach (Assembly assembly in new[] { typeof(JwtWorkloadAssertionIssuer).Assembly, typeof(EventStoreGatewayClient).Assembly,
                typeof(INativeSourceMetadataReader).Assembly, typeof(AggregateIdentity).Assembly }.Distinct())
            {
                string name = assembly.GetName().Name!;
                string sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../references/Hexalith.EventStore/src",
                    name, "bin/Debug/net11.0", name + ".dll"));
                byte[] loaded = await File.ReadAllBytesAsync(assembly.Location, TestContext.Current.CancellationToken);
                string hash = Convert.ToHexString(SHA256.HashData(loaded));
                hash.ShouldBe(Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(sourcePath, TestContext.Current.CancellationToken))));
                using var pe = new PEReader(new MemoryStream(loaded));
                MetadataReader metadata = pe.GetMetadataReader();
                Guid mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
                mvid.ShouldBe(assembly.ManifestModule.ModuleVersionId);
                Console.WriteLine("COMPONENT_RUNTIME_BINDING " + JsonSerializer.Serialize(new
                { name, version = assembly.GetName().Version!.ToString(), sha256 = hash, mvid }));
            }
        }
        INativeSourceMetadataReader source = provider.GetRequiredService<INativeSourceMetadataReader>();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        if (fault == "cancelled") cancellation.Cancel();
        var identity = new AggregateIdentity(fault == "tenant" ? "tenant-other" : "tenant-a", "folders", "aggregate-a");
        if (fault == "cancelled")
            await Should.ThrowAsync<OperationCanceledException>(() => source.ReadAsync(identity, cancellation.Token));
        else
            (await source.ReadAsync(identity, cancellation.Token)).ShouldBeNull();
        sourceTransport.CallCount.ShouldBe(fault == "none" ? 1 : 0);
    }
}
