using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Platform.Composition;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Explicit component-only consumer qualification; generated fixture authority is never owner enrollment evidence.</summary>
public sealed class WorkloadEnrollmentComponentTests
{
    /// <summary>The complete fixture manifest activates only the affected cohort after live checks; partial or invalid evidence refuses.</summary>
    /// <param name="fault">The isolated live or inventory fault.</param>
    [Theory(Explicit = true)]
    [InlineData("none")]
    [InlineData("probe")]
    [InlineData("tls")]
    [InlineData("signer")]
    [InlineData("permissions")]
    public async Task ActualIssuerVerifiesCompleteConsumerCohortBeforeActivation(string fault)
    {
        string source = Environment.GetEnvironmentVariable("HEXALITH_WORKLOAD_CONSUMER_COMPONENT_MANIFEST")
            ?? throw new InvalidOperationException("Run through Platform verify_component.py --apphost-assembly <Debug assembly>.");
        JsonObject manifest = JsonNode.Parse(await File.ReadAllBytesAsync(source, TestContext.Current.CancellationToken).ConfigureAwait(true))!.AsObject();
        if (fault == "probe") manifest["clients"]![0]!["probes"]![0]!["revision"] = 2;
        if (fault == "signer") manifest["signingPublicKeySha256"] = new string('0', 64);
        if (fault == "permissions") manifest["clients"]![0]!["probes"]!.AsArray().RemoveAt(0);
        if (fault == "tls")
        {
            foreach (string field in new[] { "authority", "issuer", "tokenEndpoint" })
                manifest[field] = manifest[field]!.GetValue<string>().Replace("localhost", "127.0.0.1", StringComparison.Ordinal);
        }
        string owned = Path.Combine(Path.GetTempPath(), "workload-consumer-component-" + Path.GetRandomFileName());
        try
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(manifest.ToJsonString());
            await File.WriteAllBytesAsync(owned, bytes, TestContext.Current.CancellationToken).ConfigureAwait(true);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(owned, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_Folders_AppHost>(
                ["--EnableKeycloak=true", "--KeycloakPersistent=false", "--Folders:WorkloadEnrollment:DeferToCohort=true"], TestContext.Current.CancellationToken).ConfigureAwait(true);
            await using var scope = builder.ConfigureAwait(true);
            var before = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
            foreach (ProjectResource resource in builder.Resources.OfType<ProjectResource>()) before[resource.Name] = await EnvironmentAsync(builder, resource);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestPath"] = owned,
                [PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestSha256"] = Convert.ToHexString(SHA256.HashData(bytes)),
            });
            if (fault == "none")
                (await PlatformWorkloadEnrollment.ConfigureAsync(builder, TestContext.Current.CancellationToken, "tenant-a").ConfigureAwait(true)).ShouldBeTrue();
            else
                await Should.ThrowAsync<InvalidOperationException>(() => PlatformWorkloadEnrollment.ConfigureAsync(builder, TestContext.Current.CancellationToken, "tenant-a"));
            foreach (ProjectResource resource in builder.Resources.OfType<ProjectResource>())
            {
                Dictionary<string, object> after = await EnvironmentAsync(builder, resource);
                foreach (var human in before[resource.Name].Where(pair => pair.Key.StartsWith("Authentication__JwtBearer", StringComparison.Ordinal)))
                    after[human.Key].ShouldBe(human.Value);
                if (resource.Name is "eventstore" or "tenants" or "folders" or "folders-workers")
                {
                    after["Authentication__WorkloadIssuer__EnableResourceBindingRequests"].ShouldBe(fault == "none" ? "true" : "false");
                    if (fault == "none")
                    {
                        after["Authentication__WorkloadJwtBearer__Audience"].ShouldBe(resource.Name == "eventstore" ? "eventstore" : PlatformWorkloadEnrollmentInventory.Workload(resource));
                        after["Authentication__WorkloadJwtBearer__Issuer"].ShouldBe(manifest["issuer"]!.GetValue<string>());
                        after["Authentication__WorkloadJwtBearer__RequireHttpsMetadata"].ShouldBe("true");
                        after["Authentication__WorkloadIssuer__ClientSecret"].ShouldBeOfType<ParameterResource>().Secret.ShouldBeTrue();
                    }
                }
                else
                    after.Keys.Where(key => key.StartsWith("Authentication__WorkloadJwtBearer", StringComparison.Ordinal))
                        .ShouldBe(before[resource.Name].Keys.Where(key => key.StartsWith("Authentication__WorkloadJwtBearer", StringComparison.Ordinal)));
            }
        }
        finally { File.Delete(owned); }
    }

    /// <summary>Real startup-imported enrollment verifies the final replicas and independent proof callers.</summary>
    [Fact(Explicit = true)]
    public async Task RunOwnedAuthorityVerifiesFinalCohortAndAllRefusals()
    {
        if (Environment.GetEnvironmentVariable("HEXALITH_RUN_OWNED_WORKLOAD_COMPONENT") != "true")
            throw new InvalidOperationException("Run Platform verify_component.py --run-owned --apphost-assembly <Debug assembly>.");
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_Folders_AppHost>(
            ["--EnableKeycloak=true", "--KeycloakPersistent=false", "--Folders:WorkloadEnrollment:DeferToCohort=true"], TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using var scope = builder.ConfigureAwait(true);
        foreach (string name in new[] { "folders-workers-replica", "folders-workers-proof-a", "folders-workers-proof-b" })
            builder.AddProject<Projects.Hexalith_Folders_Workers>(name).WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions { AppId = name == "folders-workers-replica" ? "folders-workers" : name }));
        builder.AddProject<Projects.Hexalith_Folders_Server>("folders-replica").WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions { AppId = "folders" }));
        string suffix = "-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        var proofs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (ProjectResource resource in PlatformWorkloadEnrollmentInventory.Cohort(builder))
        {
            var sidecar = resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
            DaprSidecarOptionsAnnotation previous = sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single();
            sidecar.Annotations.Remove(previous);
            sidecar.Annotations.Add(new DaprSidecarOptionsAnnotation(previous.Options with { AppId = previous.Options.AppId + suffix }));
            using RSA proof = RSA.Create(2048);
            proofs[resource.Name] = Convert.ToBase64String(proof.ExportPkcs8PrivateKey());
            builder.CreateResourceBuilder(resource).WithEnvironment("Authentication__BoundStreamRead__PrivateKey", proofs[resource.Name]);
        }
        const string tenant = "selected-component-tenant";
        string receiptPath = Environment.GetEnvironmentVariable(PlatformRunOwnedWorkloadEnrollment.ReceiptEnvironmentVariable)!;
        string trust = Environment.GetEnvironmentVariable("SSL_CERT_FILE")!;
        string? capability = Environment.GetEnvironmentVariable(PlatformRunOwnedWorkloadEnrollment.CapabilityEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable("SSL_CERT_FILE", null);
            (await Should.ThrowAsync<InvalidOperationException>(() => PlatformRunOwnedWorkloadEnrollment.AdoptAsync(builder, tenant, TestContext.Current.CancellationToken)))
                .Message.ShouldContain("missing-prelaunch-trust");
        }
        finally { Environment.SetEnvironmentVariable("SSL_CERT_FILE", trust); }
        try
        {
            Environment.SetEnvironmentVariable(PlatformRunOwnedWorkloadEnrollment.CapabilityEnvironmentVariable, new string('0', 64));
            (await Should.ThrowAsync<InvalidOperationException>(() => PlatformRunOwnedWorkloadEnrollment.AdoptAsync(builder, tenant, TestContext.Current.CancellationToken)))
                .Message.ShouldContain("foreign-launcher-receipt");
        }
        finally { Environment.SetEnvironmentVariable(PlatformRunOwnedWorkloadEnrollment.CapabilityEnvironmentVariable, capability); }
        byte[] receiptBytes = await File.ReadAllBytesAsync(receiptPath, TestContext.Current.CancellationToken).ConfigureAwait(true);
        foreach ((string fault, string code) in new[] { ("expiry", "fixed-owner-deadline-expired-or-invalid"), ("child", "owner-child-process-binding"), ("directory", "foreign-launcher-receipt") })
        {
            JsonObject changed = JsonNode.Parse(receiptBytes)!.AsObject();
            if (fault == "expiry") changed["deadline"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1;
            if (fault == "child") changed["childPid"] = Environment.ProcessId + 1;
            if (fault == "directory") changed["directory"] = Path.GetTempPath();
            try
            {
                await File.WriteAllTextAsync(receiptPath, changed.ToJsonString(), TestContext.Current.CancellationToken).ConfigureAwait(true);
                (await Should.ThrowAsync<InvalidOperationException>(() => PlatformRunOwnedWorkloadEnrollment.AdoptAsync(builder, tenant, TestContext.Current.CancellationToken)))
                    .Message.ShouldContain(code);
                File.Exists(Path.Combine(Path.GetDirectoryName(receiptPath)!, "request.json")).ShouldBeFalse("A refused receipt publishes no request.");
            }
            finally { await File.WriteAllBytesAsync(receiptPath, receiptBytes, TestContext.Current.CancellationToken).ConfigureAwait(true); }
        }
        PlatformRunOwnedWorkloadEnrollment? enrollment = await PlatformRunOwnedWorkloadEnrollment.AdoptAsync(builder, tenant, TestContext.Current.CancellationToken).ConfigureAwait(true);
        enrollment.ShouldNotBeNull();
        await using System.Runtime.CompilerServices.ConfiguredAsyncDisposable enrollmentCleanup = enrollment.ConfigureAwait(true);
        string manifestPath = builder.Configuration[PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestPath"]!;
        byte[] manifestBytes = await File.ReadAllBytesAsync(manifestPath, TestContext.Current.CancellationToken).ConfigureAwait(true);
        JsonObject original = JsonNode.Parse(manifestBytes)!.AsObject();
        JsonArray clients = original["clients"]!.AsArray();
        clients.Count.ShouldBe(8);
        clients.Sum(client => client!["probes"]!.AsArray().Count).ShouldBe(19);
        clients.Select(client => client!["workload"]!.GetValue<string>()).Distinct().Count().ShouldBe(6);
        string Workload(string name) => clients.Single(client => client!["resource"]!.GetValue<string>() == name)!["workload"]!.GetValue<string>();
        Workload("folders-workers-replica").ShouldBe(Workload("folders-workers"));
        string Secret(string name) => clients.Single(client => client!["resource"]!.GetValue<string>() == name)!["secretFile"]!.GetValue<string>();
        Secret("folders").ShouldBe(Secret("folders-replica"));
        Secret("folders-workers").ShouldBe(Secret("folders-workers-replica"));
        new[] { Secret("folders-workers"), Secret("folders-workers-proof-a"), Secret("folders-workers-proof-b") }.Distinct().Count().ShouldBe(3);
        foreach (string fault in new[] { "probe", "signer", "tls", "permissions", "expiry", "cohort", "tenant", "physical-audience" })
        {
            JsonObject changed = JsonNode.Parse(manifestBytes)!.AsObject();
            if (fault == "probe") changed["clients"]![0]!["probes"]![0]!["revision"] = 2;
            if (fault == "physical-audience")
                foreach (JsonNode? client in changed["clients"]!.AsArray().Where(client => client!["resource"]!.GetValue<string>() != "eventstore"))
                    foreach (JsonNode? probe in client!["probes"]!.AsArray()) probe!["audience"] = Workload("eventstore");
            if (fault == "signer") changed["signingPublicKeySha256"] = new string('0', 64);
            if (fault == "permissions") changed["clients"]![0]!["probes"]!.AsArray().RemoveAt(0);
            if (fault == "expiry") changed["expiresAt"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1;
            if (fault == "cohort") changed["receivers"]!.AsArray().RemoveAt(0);
            if (fault == "tls")
                foreach (string field in new[] { "authority", "issuer", "tokenEndpoint" })
                    changed[field] = changed[field]!.GetValue<string>().Replace("localhost", "127.0.0.1", StringComparison.Ordinal);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(changed.ToJsonString());
            await File.WriteAllBytesAsync(manifestPath, bytes, TestContext.Current.CancellationToken).ConfigureAwait(true);
            builder.Configuration[PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestSha256"] = Convert.ToHexString(SHA256.HashData(bytes));
            await Should.ThrowAsync<InvalidOperationException>(() => PlatformWorkloadEnrollment.ConfigureAsync(builder, TestContext.Current.CancellationToken, fault == "tenant" ? "foreign-tenant" : tenant));
            foreach (ProjectResource resource in PlatformWorkloadEnrollmentInventory.Cohort(builder))
                (await EnvironmentAsync(builder, resource))["Authentication__WorkloadIssuer__EnableResourceBindingRequests"].ShouldBe("false");
        }
        await File.WriteAllBytesAsync(manifestPath, manifestBytes, TestContext.Current.CancellationToken).ConfigureAwait(true);
        builder.Configuration[PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestSha256"] = Convert.ToHexString(SHA256.HashData(manifestBytes));
        (await PlatformWorkloadEnrollment.ConfigureAsync(builder, TestContext.Current.CancellationToken, tenant).ConfigureAwait(true)).ShouldBeTrue();
        foreach (ProjectResource resource in PlatformWorkloadEnrollmentInventory.Cohort(builder))
        {
            Dictionary<string, object> values = await EnvironmentAsync(builder, resource);
            values["SSL_CERT_FILE"].ShouldBe(trust);
            values["Authentication__WorkloadIssuer__EnableResourceBindingRequests"].ShouldBe("true");
            values["Authentication__WorkloadJwtBearer__Audience"].ShouldBe(resource.Name == "eventstore" ? "eventstore" : Workload(resource.Name));
            (values["Authentication__BoundStreamRead__PrivateKey"] is string actualProof && actualProof == proofs[resource.Name]).ShouldBeTrue("Independent RSA proof remains unchanged.");
        }
        using JsonDocument sdkManifest = JsonDocument.Parse(manifestBytes);
        foreach (string name in new[] { "tenants", "folders", "folders-workers-proof-a" })
        {
            ProjectResource resource = PlatformWorkloadEnrollmentInventory.Cohort(builder).Single(resource => resource.Name == name);
            JsonElement client = sdkManifest.RootElement.GetProperty("clients").EnumerateArray().Single(client => client.GetProperty("resource").GetString() == name);
            await VerifyNativeSdkAsync(await EnvironmentAsync(builder, resource), client, sdkManifest.RootElement, Workload("eventstore"));
        }
        Console.WriteLine("RUN_OWNED_CONSUMER_CONTROLS resources=8 workloads=6 proofCallers=2 controls=missing-trust,foreign-receipt,expiry,child,directory,probe,signer,tls,permissions,cohort,tenant,physical-audience,sdk-physical-audience,sdk-altered-bindings assertions=real-sdk-tenants-command-and-source,real-sdk-folders-bound-source,physical-dapr-target,independent-rsa-proof,no-transport-on-refusal");
    }

    private static async Task VerifyNativeSdkAsync(Dictionary<string, object> environment, JsonElement client, JsonElement manifest, string physicalGateway)
    {
        var configuration = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach ((string name, object value) in environment.Where(pair => pair.Key.StartsWith("Authentication__Workload", StringComparison.Ordinal)
            || pair.Key == "Authentication__BoundStreamRead__PrivateKey"))
            configuration[name.Replace("__", ":", StringComparison.Ordinal)] = value is IValueProvider parameter
                ? await parameter.GetValueAsync(TestContext.Current.CancellationToken).ConfigureAwait(true) : value.ToString();
        configuration["DAPR_HTTP_ENDPOINT"] = "http://localhost:3500";
        configuration["EventStore:Gateway:AppId"] = physicalGateway;
        string authority = manifest.GetProperty("authority").GetString()!;
        using HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false });
        using JsonDocument metadata = JsonDocument.Parse(await http.GetStringAsync(authority + "/.well-known/openid-configuration", TestContext.Current.CancellationToken).ConfigureAwait(true));
        using JsonDocument jwks = JsonDocument.Parse(await http.GetStringAsync(metadata.RootElement.GetProperty("jwks_uri").GetString(), TestContext.Current.CancellationToken).ConfigureAwait(true));
        string kid = manifest.GetProperty("signingKeyId").GetString()!;
        JsonElement key = jwks.RootElement.GetProperty("keys").EnumerateArray().Single(key => key.GetProperty("kid").GetString() == kid);
        static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
        using RSA signer = RSA.Create();
        signer.ImportParameters(new RSAParameters { Modulus = Decode(key.GetProperty("n").GetString()!), Exponent = Decode(key.GetProperty("e").GetString()!) });
        (Convert.ToHexString(SHA256.HashData(signer.ExportSubjectPublicKeyInfo())) == manifest.GetProperty("signingPublicKeySha256").GetString())
            .ShouldBeTrue("Fresh SDK assertions use the independently pinned signer.");
        void Verify(string token, JsonElement probe)
            => typeof(PlatformWorkloadEnrollment).GetMethod("VerifyToken", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [token, signer, kid, authority, client.GetProperty("workload").GetString()!, probe, manifest.GetProperty("expiresAt").GetInt64()]);
        WebApplicationBuilder host = WebApplication.CreateBuilder();
        host.Logging.ClearProviders();
        host.Configuration.AddInMemoryCollection(configuration);
        host.Services.AddEventStoreWorkloadAssertionIssuer();
        WebApplication app = host.Build();
        await using var appCleanup = app.ConfigureAwait(true);
        IWorkloadAssertionIssuer sdk = app.Services.GetRequiredService<IWorkloadAssertionIssuer>();
        sdk.CanBindResources.ShouldBeTrue();
        foreach (JsonElement probe in client.GetProperty("probes").EnumerateArray())
        {
            string audience = probe.GetProperty("audience").GetString()!, operation = probe.GetProperty("operation").GetString()!;
            audience.ShouldBe("eventstore");
            audience.ShouldNotBe(physicalGateway);
            Dictionary<string, string> bindings = probe.GetProperty("bindings").EnumerateObject()
                .ToDictionary(binding => binding.Name, binding => binding.Value.GetString()!, StringComparer.Ordinal);
            string? token = await sdk.IssueAsync(new(audience, operation, bindings), TestContext.Current.CancellationToken).ConfigureAwait(true);
            if (token is null) throw new InvalidOperationException("Native SDK refused canonical enrolled assertion.");
            Verify(token, probe);
            (await sdk.IssueAsync(new(physicalGateway, operation, bindings), TestContext.Current.CancellationToken).ConfigureAwait(true) is null)
                .ShouldBeTrue("A physical gateway audience has no enrolled grant.");
            Dictionary<string, string> altered = new(bindings, StringComparer.Ordinal) { ["eventstore:bound-tenant"] = "foreign-tenant" };
            (await sdk.IssueAsync(new(audience, operation, altered), TestContext.Current.CancellationToken).ConfigureAwait(true) is null)
                .ShouldBeTrue("Altered bindings refuse without fallback.");
            if (bindings.Count == 0) continue;
            using RSA proof = RSA.Create();
            proof.ImportPkcs8PrivateKey(Convert.FromBase64String(configuration["Authentication:BoundStreamRead:PrivateKey"]!), out _);
            foreach (string fault in new[] { "none", "physical-audience", "tenant", "domain" })
            {
                WebApplicationBuilder sourceHost = WebApplication.CreateBuilder();
                sourceHost.Logging.ClearProviders();
                sourceHost.Configuration.AddInMemoryCollection(configuration);
                using var transport = new RecordingDurabilityHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                sourceHost.Services.AddEventStoreGatewayNativeSourceMetadataReader("folders", fault == "physical-audience" ? physicalGateway : "eventstore")
                    .ConfigurePrimaryHttpMessageHandler(() => transport);
                WebApplication sourceApp = sourceHost.Build();
                await using var sourceCleanup = sourceApp.ConfigureAwait(true);
                INativeSourceMetadataReader source = sourceApp.Services.GetRequiredService<INativeSourceMetadataReader>();
                var identity = new AggregateIdentity(fault == "tenant" ? "foreign-tenant" : bindings["eventstore:bound-tenant"], fault == "domain" ? "other" : "folders", "component-source");
                (await source.ReadAsync(identity, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeNull();
                transport.Requests.Count.ShouldBe(fault == "none" ? 1 : 0);
                if (fault != "none") continue;
                var sent = transport.Requests.Single();
                sent.Headers["dapr-app-id"].ShouldBe(physicalGateway);
                new Uri(sent.Uri).AbsolutePath.ShouldBe("/api/v1/streams/read/workload");
                Verify(sent.Headers[EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName], probe);
                StreamReadRequest request = JsonSerializer.Deserialize<StreamReadRequest>(sent.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                BoundStreamReadProof.Verify(sent.Headers[BoundStreamReadProof.HeaderName], request, client.GetProperty("workload").GetString()!,
                    Convert.ToBase64String(proof.ExportSubjectPublicKeyInfo()), DateTimeOffset.UtcNow).ShouldBeTrue();
            }
        }
    }

    private static async Task<Dictionary<string, object>> EnvironmentAsync(IDistributedApplicationTestingBuilder builder, ProjectResource resource)
    {
        Dictionary<string, object> values = new(StringComparer.Ordinal);
        foreach (EnvironmentCallbackAnnotation annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await annotation.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, resource, values, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        return values;
    }
}
