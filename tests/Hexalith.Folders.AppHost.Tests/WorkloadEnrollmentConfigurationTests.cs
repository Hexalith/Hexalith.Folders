using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Hexalith.Platform.Composition;

using Microsoft.Extensions.Configuration;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Verifies the supported consumer cannot infer enrollment from a capability option or partial owner references.</summary>
public sealed class WorkloadEnrollmentConfigurationTests
{
    /// <summary>Even locally pinned bytes must pass schema, duplicate-field, current-validity and complete-cohort checks.</summary>
    /// <param name="fault">The isolated artifact failure.</param>
    [Theory]
    [InlineData("digest")]
    [InlineData("duplicate")]
    [InlineData("expired")]
    [InlineData("cohort")]
    public async Task InvalidOwnerArtifactRefusesBeforeAnyIssuerExchange(string fault)
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_Folders_AppHost>(
            ["--EnableKeycloak=true", "--KeycloakPersistent=false", "--Folders:WorkloadEnrollment:DeferToCohort=true"], TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using var scope = builder.ConfigureAwait(true);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string json = JsonSerializer.Serialize(new
        {
            schema = "hexalith.workload-enrollment/v1",
            authority = "https://unreachable.example.invalid/realms/hexalith",
            issuer = "https://unreachable.example.invalid/realms/hexalith",
            tokenEndpoint = "https://unreachable.example.invalid/realms/hexalith/protocol/openid-connect/token",
            notBefore = now - 3600,
            expiresAt = fault == "expired" ? now - 1 : now + 3600,
            signingKeyId = "untrusted-component",
            signingPublicKeySha256 = new string('A', 64),
            receivers = Array.Empty<string>(),
            clients = Array.Empty<object>(),
        });
        if (fault == "duplicate") json = json.Replace("\"schema\":", "\"schema\":\"duplicate\",\"schema\":", StringComparison.Ordinal);
        string owned = Path.Combine(Path.GetTempPath(), "workload-enrollment-test-" + Path.GetRandomFileName());
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await File.WriteAllBytesAsync(owned, bytes, TestContext.Current.CancellationToken).ConfigureAwait(true);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(owned, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestPath"] = owned,
                [PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestSha256"] = fault == "digest" ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(bytes)),
            });
            (await Should.ThrowAsync<InvalidOperationException>(() => PlatformWorkloadEnrollment.ConfigureAsync(builder, TestContext.Current.CancellationToken)))
                .Message.ShouldStartWith("Protected workload enrollment verification refused.");
        }
        finally { File.Delete(owned); }
    }

    /// <summary>Without protected owner references every caller remains disabled, even if an inherited option was true.</summary>
    [Fact]
    public async Task MissingProtectedEnrollmentDisablesEveryActualCaller()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_Folders_AppHost>(
            ["--EnableKeycloak=true", "--KeycloakPersistent=false", "--Folders:WorkloadEnrollment:DeferToCohort=true"], TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using var scope = builder.ConfigureAwait(true);
        foreach (ProjectResource resource in builder.Resources.OfType<ProjectResource>())
            builder.CreateResourceBuilder(resource).WithEnvironment("Authentication__WorkloadIssuer__EnableResourceBindingRequests", "true");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestPath"] = null,
            [PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestSha256"] = null,
        });
        (await PlatformWorkloadEnrollment.ConfigureAsync(builder, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();
        foreach (ProjectResource resource in builder.Resources.OfType<ProjectResource>().Where(resource => resource.Name is "eventstore" or "tenants" or "folders" or "folders-workers"))
        {
            Dictionary<string, object> environment = new(StringComparer.Ordinal);
            foreach (EnvironmentCallbackAnnotation annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
                await annotation.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, resource, environment, TestContext.Current.CancellationToken)).ConfigureAwait(true);
            environment["Authentication__WorkloadIssuer__EnableResourceBindingRequests"].ShouldBe("false");
            environment.ShouldNotContainKey("Authentication__WorkloadJwtBearer__Authority");
        }
    }

    /// <summary>A capability option plus an unverified artifact reference never enables the profile.</summary>
    [Fact]
    public async Task PartialOwnerReferencesRefuseBeforeCatalogCapture()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_Folders_AppHost>(
            ["--EnableKeycloak=true", "--KeycloakPersistent=false", "--Folders:WorkloadEnrollment:DeferToCohort=true"], TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using var scope = builder.ConfigureAwait(true);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestPath"] = "/unapproved-owner-reference",
            [PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestSha256"] = null,
        });
        InvalidOperationException refusal = await Should.ThrowAsync<InvalidOperationException>(() =>
            PlatformWorkloadEnrollment.ConfigureAsync(builder, TestContext.Current.CancellationToken));
        refusal.Message.ShouldStartWith("Protected workload enrollment verification refused.");
        refusal.Message.ShouldNotContain("/unapproved-owner-reference");
    }
}
