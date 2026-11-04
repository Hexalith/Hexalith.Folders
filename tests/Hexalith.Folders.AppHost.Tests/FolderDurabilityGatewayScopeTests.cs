using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Tenants.Contracts.Identity;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Checks actual fixture request construction against supported explicit scopes and native proof APIs.</summary>
public sealed class FolderDurabilityGatewayScopeTests
{
    /// <summary>Tenant commands use their original body/bearer while platform commands retain their namespace.</summary>
    [Theory]
    [InlineData("tenant-a")]
    [InlineData(TenantPlatformScopes.Tenants)]
    [InlineData(TenantPlatformScopes.GlobalAdministrators)]
    public async Task HumanCommandPreservesExactBodyIdentityAndTracingWithoutAnAdditionalTenantHeader(string scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var command = new SubmitCommandRequest("original-message", scope, "folders", "original-aggregate", "OriginalCommand",
            JsonSerializer.SerializeToElement(new { original = "payload" }), CorrelationId: "original-correlation",
            Extensions: new Dictionary<string, string> { ["taskId"] = "original-task" }, IdempotencyKey: "original-key");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands") { Content = JsonContent.Create(command) };
        string bearer = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        request.Headers.Add("X-Correlation-Id", "original-correlation");
        request.Headers.Add("Idempotency-Key", "original-key");
        byte[] before = await request.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

        FolderDurabilityGatewayScope.ApplyHumanCommand(request, scope);

        request.Headers.Contains("X-Hexalith-Tenant").ShouldBeFalse();
        if (scope.StartsWith("platform:", StringComparison.Ordinal)) request.Headers.GetValues("X-Hexalith-Scope").ShouldBe(new[] { scope });
        else request.Headers.Contains("X-Hexalith-Scope").ShouldBeFalse();
        request.Headers.Authorization.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe(bearer);
        request.Headers.GetValues("X-Correlation-Id").ShouldBe(new[] { "original-correlation" });
        request.Headers.GetValues("Idempotency-Key").ShouldBe(new[] { "original-key" });
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.OriginalString.ShouldBe("/api/v1/commands");
        (await request.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).ShouldBe(before);
        var after = (await request.Content.ReadFromJsonAsync<SubmitCommandRequest>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        after.Tenant.ShouldBe(scope);
        after.MessageId.ShouldBe("original-message");
        after.CorrelationId.ShouldBe("original-correlation");
        after.Extensions!["taskId"].ShouldBe("original-task");
        after.IdempotencyKey.ShouldBe("original-key");
    }

    /// <summary>Omitting the redundant header does not admit reserved, unknown or noncanonical command scopes.</summary>
    [Theory]
    [InlineData("system")]
    [InlineData("System")]
    [InlineData("platform:folders")]
    [InlineData("")]
    [InlineData("tenant_a")]
    [InlineData("Tenant-A")]
    public void InvalidHumanCommandScopeStillRefusesBeforeAddingHeaders(string scope)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands");
        Should.Throw<InvalidOperationException>(() => FolderDurabilityGatewayScope.ApplyHumanCommand(request, scope));
        request.Headers.ShouldBeEmpty();
    }

    /// <summary>A caller-supplied duplicate scope is refused without being stripped or relabeled.</summary>
    [Theory]
    [InlineData("X-Hexalith-Tenant", "tenant-a")]
    [InlineData("X-Hexalith-Scope", TenantPlatformScopes.Tenants)]
    public void MixedHumanCommandHeadersRemainAnExplicitRefusal(string header, string value)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands");
        request.Headers.Add(header, value);
        Should.Throw<InvalidOperationException>(() => FolderDurabilityGatewayScope.ApplyHumanCommand(request, "tenant-a"));
        request.Headers.GetValues(header).ShouldBe(new[] { value });
        request.Headers.Count().ShouldBe(1);
    }

    [Theory]
    [InlineData(TenantPlatformScopes.Tenants)]
    [InlineData(TenantPlatformScopes.GlobalAdministrators)]
    [InlineData("tenant-a")]
    public void StatusNamesExactlyOneOriginalScopeWithoutChangingIdentity(string scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/commands/status/original-message");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "unchanged-human-credential");
        FolderDurabilityGatewayScope.Apply(request, scope);
        string header = scope.StartsWith("platform:", StringComparison.Ordinal) ? "X-Hexalith-Scope" : "X-Hexalith-Tenant";
        request.Headers.GetValues(header).ShouldBe(new[] { scope });
        request.Headers.Contains(header == "X-Hexalith-Scope" ? "X-Hexalith-Tenant" : "X-Hexalith-Scope").ShouldBeFalse();
        request.Headers.Authorization.Parameter.ShouldBe("unchanged-human-credential");
        request.RequestUri!.OriginalString.ShouldBe("/api/v1/commands/status/original-message");
        Should.Throw<InvalidOperationException>(() => FolderDurabilityGatewayScope.Apply(request, scope));
        request.Headers.GetValues(header).ShouldBe(new[] { scope });
    }

    [Theory]
    [InlineData("system")]
    [InlineData("System")]
    [InlineData("platform:folders")]
    [InlineData("")]
    [InlineData("tenant_a")]
    [InlineData("Tenant-A")]
    public void ReservedUnknownOrNoncanonicalScopesCannotBeMappedByTheFixture(string scope)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/commands/status/original-message");
        Should.Throw<InvalidOperationException>(() => FolderDurabilityGatewayScope.Apply(request, scope));
        request.Headers.ShouldBeEmpty();
    }

    [Fact]
    public void DirectAndBoundBootstrapControlsRetainTheSameOriginalOperationAndHumanTarget()
    {
        var command = FolderDurabilityGatewayScope.BootstrapCommand("original-message", "admin-user");
        command.MessageId.ShouldBe("original-message");
        command.Tenant.ShouldBe(TenantPlatformScopes.GlobalAdministrators);
        command.Domain.ShouldBe(TenantIdentity.GlobalAdministratorsDomain);
        command.AggregateId.ShouldBe(TenantIdentity.GlobalAdministratorsAggregateId);
        command.CommandType.ShouldBe("BootstrapGlobalAdmin");
        command.Payload.GetProperty("userId").GetString().ShouldBe("admin-user");
        command.Extensions.ShouldBeNull();
        command.IdempotencyKey.ShouldBeNull();
        foreach (string path in new[] { "/api/v1/commands", "/api/v1/commands/bound/workload" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            FolderDurabilityGatewayScope.Apply(request, command.Tenant);
            request.Headers.GetValues("X-Hexalith-Scope").ShouldBe(new[] { command.Tenant });
        }
    }

    [Fact]
    public async Task OriginalSourceUsesTheRealSelectedOwnerCoordinatesAndExactSdkProofScope()
    {
        using RSA key = RSA.Create(2048);
        string privateKey = Convert.ToBase64String(key.ExportPkcs8PrivateKey());
        string publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using DaprClient sidecar = new DaprClientBuilder().UseHttpEndpoint("http://localhost:3500").Build();
        using HttpRequestMessage request = FolderDurabilityGatewayScope.BootstrapSource(sidecar,
            "eventstore-isolated", "tenants-isolated", "unchanged-issued-assertion", privateKey, now);
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.ToString().ShouldBe("http://localhost:3500/v1.0/invoke/eventstore-isolated/method/api/v1/streams/read/workload");
        request.Headers.Authorization.ShouldBeNull();
        request.Headers.GetValues(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName).ShouldBe(new[] { "unchanged-issued-assertion" });
        request.Headers.GetValues("X-Hexalith-Scope").ShouldBe(new[] { TenantPlatformScopes.GlobalAdministrators });
        request.Headers.Contains("X-Hexalith-Tenant").ShouldBeFalse();
        StreamReadRequest source = (await request.Content!.ReadFromJsonAsync<StreamReadRequest>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        source.Tenant.ShouldBe(TenantPlatformScopes.GlobalAdministrators);
        source.Domain.ShouldBe("global-administrators");
        source.AggregateId.ShouldBe("global-administrators");
        source.RequireNativeProof.ShouldBeTrue();
        source.FromSequence.ShouldBe(0);
        source.ToSequence.ShouldBeNull();
        string proof = request.Headers.GetValues(BoundStreamReadProof.HeaderName).ShouldHaveSingleItem();
        BoundStreamReadProof.Verify(proof, source, "tenants-isolated", publicKey, now).ShouldBeTrue();
        BoundStreamReadProof.Verify(proof, source with { Tenant = "system" }, "tenants-isolated", publicKey, now).ShouldBeFalse();
        BoundStreamReadProof.Verify(proof, source, "tenants", publicKey, now).ShouldBeFalse();
        BoundStreamReadProof.Verify(proof, source, "tenants-isolated", publicKey, now.AddSeconds(30)).ShouldBeFalse();
    }
}
