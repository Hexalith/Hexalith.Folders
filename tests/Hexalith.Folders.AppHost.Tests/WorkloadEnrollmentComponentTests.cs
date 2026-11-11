using System.Security.Cryptography;
using System.Text.Json.Nodes;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

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

    private static async Task<Dictionary<string, object>> EnvironmentAsync(IDistributedApplicationTestingBuilder builder, ProjectResource resource)
    {
        Dictionary<string, object> values = new(StringComparer.Ordinal);
        foreach (EnvironmentCallbackAnnotation annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await annotation.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, resource, values, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        return values;
    }
}
