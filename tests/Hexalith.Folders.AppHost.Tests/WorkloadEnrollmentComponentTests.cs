using System.Security.Cryptography;
using System.Text.Json.Nodes;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.Platform.Composition;

using Microsoft.Extensions.Configuration;

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
            builder.AddProject<Projects.Hexalith_Folders_Workers>(name).WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions { AppId = name }));
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
        foreach (string fault in new[] { "expiry", "child", "directory" })
        {
            JsonObject changed = JsonNode.Parse(receiptBytes)!.AsObject();
            if (fault == "expiry") changed["deadline"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1;
            if (fault == "child") changed["childPid"] = Environment.ProcessId + 1;
            if (fault == "directory") changed["directory"] = Path.GetTempPath();
            try
            {
                await File.WriteAllTextAsync(receiptPath, changed.ToJsonString(), TestContext.Current.CancellationToken).ConfigureAwait(true);
                await Should.ThrowAsync<InvalidOperationException>(() => PlatformRunOwnedWorkloadEnrollment.AdoptAsync(builder, tenant, TestContext.Current.CancellationToken));
            }
            finally { await File.WriteAllBytesAsync(receiptPath, receiptBytes, TestContext.Current.CancellationToken).ConfigureAwait(true); }
        }
        await using PlatformRunOwnedWorkloadEnrollment? enrollment = await PlatformRunOwnedWorkloadEnrollment.AdoptAsync(builder, tenant, TestContext.Current.CancellationToken).ConfigureAwait(true);
        enrollment.ShouldNotBeNull();
        string manifestPath = builder.Configuration[PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestPath"]!;
        byte[] manifestBytes = await File.ReadAllBytesAsync(manifestPath, TestContext.Current.CancellationToken).ConfigureAwait(true);
        JsonObject original = JsonNode.Parse(manifestBytes)!.AsObject();
        JsonArray clients = original["clients"]!.AsArray();
        clients.Count.ShouldBe(8);
        string Secret(string name) => clients.Single(client => client!["resource"]!.GetValue<string>() == name)!["secretFile"]!.GetValue<string>();
        Secret("folders").ShouldBe(Secret("folders-replica"));
        Secret("folders-workers").ShouldBe(Secret("folders-workers-replica"));
        new[] { Secret("folders-workers"), Secret("folders-workers-proof-a"), Secret("folders-workers-proof-b") }.Distinct().Count().ShouldBe(3);
        foreach (string fault in new[] { "probe", "signer", "tls", "permissions", "expiry", "cohort", "tenant" })
        {
            JsonObject changed = JsonNode.Parse(manifestBytes)!.AsObject();
            if (fault == "probe") changed["clients"]![0]!["probes"]![0]!["revision"] = 2;
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
            (values["Authentication__BoundStreamRead__PrivateKey"] is string actualProof && actualProof == proofs[resource.Name]).ShouldBeTrue("Independent RSA proof remains unchanged.");
        }
        Console.WriteLine("RUN_OWNED_CONSUMER_CONTROLS resources=8 workloads=6 proofCallers=2 controls=missing-trust,foreign-receipt,expiry,child,directory,probe,signer,tls,permissions,cohort,tenant");
    }

    private static async Task<Dictionary<string, object>> EnvironmentAsync(IDistributedApplicationTestingBuilder builder, ProjectResource resource)
    {
        Dictionary<string, object> values = new(StringComparer.Ordinal);
        foreach (EnvironmentCallbackAnnotation annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await annotation.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, resource, values, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        return values;
    }
}
