using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Contracts;
using Hexalith.Folders.Server;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Exercises callback input handling and authorization through the actual production pipeline.</summary>
public sealed class FoldersCallbackBoundaryTests
{
    private const string Issuer = "https://identity.example.test/realms/folders";
    private const string ChannelToken = "callback-boundary-sidecar-token";

    /// <summary>Requires both sidecar credentials and an assertion granting the selected callback operation.</summary>
    [Theory]
    [InlineData("/process", EventStoreWorkloadOperations.DomainServiceProcess)]
    [InlineData("/project", EventStoreWorkloadOperations.DomainServiceProject)]
    [InlineData("/query", EventStoreWorkloadOperations.DomainServiceQuery)]
    [InlineData("/admin/operational-index-metadata", EventStoreWorkloadOperations.DomainServiceMetadata)]
    public async Task ProductionCallbacksRejectMissingInvalidAndWrongOperationCredentials(string route, string operation)
    {
        using RSA rsa = RSA.Create(2048);
        RsaSecurityKey key = new(rsa) { KeyId = "callback-boundary" };
        await using WebApplication app = BuildApp(key);
        int admitted = 0;
        // Terminate after the real authentication/authorization middleware so no callback
        // persistence or payload binding can turn a denied request into a false positive.
        app.Run(context =>
        {
            context.GetEndpoint().ShouldBeOfType<RouteEndpoint>().RoutePattern.RawText.ShouldBe(route);
            context.User.Identity!.IsAuthenticated.ShouldBeTrue();
            context.User.HasClaim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, operation).ShouldBeTrue();
            admitted++;
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        using RSA wrongRsa = RSA.Create(2048);
        foreach (string credential in new[] { "absent", "channel-only", "assertion-only", "wrong-channel", "wrong-operation", "wrong-signature", "wrong-caller" })
        {
            string granted = credential == "wrong-operation"
                ? operation == EventStoreWorkloadOperations.DomainServiceQuery
                    ? EventStoreWorkloadOperations.DomainServiceProcess : EventStoreWorkloadOperations.DomainServiceQuery
                : operation;
            using HttpRequestMessage request = CreateRequest(route, new { }, key, granted);
            if (credential is "absent" or "channel-only") request.Headers.Remove(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName);
            if (credential is "absent" or "assertion-only") request.Headers.Remove(DaprAppChannelToken.HeaderName);
            if (credential == "wrong-channel")
            {
                request.Headers.Remove(DaprAppChannelToken.HeaderName);
                request.Headers.Add(DaprAppChannelToken.HeaderName, "wrong-sidecar-token");
            }
            if (credential == "wrong-signature")
            {
                request.Headers.Remove(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName);
                request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName,
                    CreateAssertion(new RsaSecurityKey(wrongRsa) { KeyId = key.KeyId }, operation));
            }
            if (credential == "wrong-caller")
            {
                request.Headers.Remove(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName);
                request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName,
                    CreateAssertion(key, operation, caller: "unauthorized-workload"));
            }
            using HttpResponseMessage denied = await client.SendAsync(request, TestContext.Current.CancellationToken);
            denied.StatusCode.ShouldBe(credential == "wrong-operation" ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized, credential);
            admitted.ShouldBe(0, credential);
        }
        using HttpRequestMessage accepted = CreateRequest(route, new { }, key, operation);
        using HttpResponseMessage response = await client.SendAsync(accepted, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        admitted.ShouldBe(1);
    }

    /// <summary>Preserves input error categories, success payloads, and the shared safe-denial response.</summary>
    [Theory]
    [InlineData(QueryAdapterFailureReason.InvalidEnvelope, HttpStatusCode.BadRequest)]
    [InlineData(QueryAdapterFailureReason.InvalidCursor, HttpStatusCode.BadRequest)]
    [InlineData(QueryAdapterFailureReason.InvalidPage, HttpStatusCode.BadRequest)]
    [InlineData(QueryAdapterFailureReason.ActorException, HttpStatusCode.NotFound)]
    [InlineData(QueryAdapterFailureReason.ActorNotFoundInfrastructure, HttpStatusCode.NotFound)]
    [InlineData(QueryAdapterFailureReason.SerializationFailure, HttpStatusCode.NotFound)]
    [InlineData(QueryAdapterFailureReason.MissingPayload, HttpStatusCode.NotFound)]
    [InlineData(QueryAdapterFailureReason.Forbidden, HttpStatusCode.NotFound)]
    [InlineData(QueryAdapterFailureReason.MissingProjectionState, HttpStatusCode.NotFound)]
    [InlineData("safe-denial", HttpStatusCode.NotFound)]
    [InlineData("internal diagnostic tenant-secret folder-secret", HttpStatusCode.NotFound)]
    [InlineData("unknown-handler", HttpStatusCode.NotFound)]
    [InlineData("unverified-caller", HttpStatusCode.NotFound)]
    [InlineData("allowed", HttpStatusCode.OK)]
    [InlineData("stale", HttpStatusCode.OK)]
    [InlineData("unavailable", HttpStatusCode.OK)]
    public async Task QueryCallbackMapsInputErrorsAndSanitizesOtherFailures(string scenario, HttpStatusCode expected)
    {
        QueryResult supplied = expected == HttpStatusCode.OK
            ? QueryResult.FromPayload(JsonSerializer.SerializeToElement(new FolderOwnerReadResult(scenario switch
            {
                "stale" => FolderOwnerReadOutcome.Stale,
                "unavailable" => FolderOwnerReadOutcome.Unavailable,
                _ => FolderOwnerReadOutcome.Allowed,
            }), JsonSerializerOptions.Web), FolderOwnerReadQuery.QueryType,
                new QueryResponseMetadata(IsStale: scenario == "stale", ProjectionVersion: "public-version"))
            : QueryResult.Failure(scenario);
        if (expected == HttpStatusCode.NotFound)
        {
            supplied = supplied with
            {
                PayloadBytes = "{\"protected\":\"private-payload\"}"u8.ToArray(),
                ProjectionType = "private-projection",
                Metadata = new QueryResponseMetadata(ETag: "private-etag", ProjectionVersion: "private-version"),
            };
        }
        CallbackQueryResultHandler handler = new(supplied);
        using RSA rsa = RSA.Create(2048);
        RsaSecurityKey key = new(rsa) { KeyId = "callback-boundary" };
        await using WebApplication app = BuildApp(key, handler);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        QueryEnvelope query = new("tenant-a", "folders", "folder-a", handler.QueryType, "{}"u8.ToArray(), "correlation", "actor");
        if (scenario == "unknown-handler") query = query with { QueryType = "not-registered" };
        if (scenario == "unverified-caller") query = query with { CallerAuthority = new("delegated-owner-query", [], ["read_metadata"]) };
        using HttpRequestMessage request = CreateRequest("/query", query, key, EventStoreWorkloadOperations.DomainServiceQuery);
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(expected);
        QueryResult result = (await response.Content.ReadFromJsonAsync<QueryResult>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        handler.Calls.ShouldBe(scenario is "unknown-handler" or "unverified-caller" ? 0 : 1);
        if (expected == HttpStatusCode.OK)
        {
            result.Success.ShouldBeTrue();
            result.PayloadBytes.ShouldBe(supplied.PayloadBytes);
            result.ProjectionType.ShouldBe(supplied.ProjectionType);
            result.Metadata.ShouldBe(supplied.Metadata);
            result.ErrorMessage.ShouldBeNull();
        }
        else
        {
            result.ShouldBe(QueryResult.Failure(expected == HttpStatusCode.BadRequest ? scenario : "safe-denial"));
        }
    }

    /// <summary>Uses the SDK-owned production metadata route to enforce capability version negotiation.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public async Task ProductionMetadataRejectsUnsupportedSourceGuardVersions(int? version)
    {
        using RSA rsa = RSA.Create(2048);
        RsaSecurityKey key = new(rsa) { KeyId = "callback-boundary" };
        await using WebApplication app = BuildApp(key);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        foreach (bool bound in new[] { false, true })
        {
            AdminOperationalIndexMetadata.Request payload = new(["folders"])
            {
                AppId = bound ? "folders" : null,
                ServiceVersion = bound ? "v1" : null,
                ProjectionSourceGuardVersion = version,
            };
            using HttpRequestMessage request = CreateRequest("/admin/operational-index-metadata", payload, key,
                EventStoreWorkloadOperations.DomainServiceMetadata);
            using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(version is null or 1 ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
            if (version is not null and not 1)
            {
                (await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken))
                    .ShouldBe(ProjectionDispatchReasonCodes.UnsupportedCapability);
            }
            else
            {
                var metadata = (await response.Content.ReadFromJsonAsync<AdminOperationalIndexMetadata.Response>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
                var domain = metadata.Domains.ShouldHaveSingleItem();
                domain.Domain.ShouldBe("folders");
                if (bound)
                {
                    metadata.AppId.ShouldBe("folders");
                    metadata.ServiceVersion.ShouldBe("v1");
                    metadata.CatalogFingerprint.ShouldNotBeNullOrWhiteSpace();
                }
                // This host has no owner-proof enrollment and must not advertise a guard.
                (domain.SourceGuardCapabilities is null || domain.SourceGuardCapabilities.Count == 0).ShouldBeTrue();
            }
        }
    }

    private static WebApplication BuildApp(RsaSecurityKey key, IDomainQueryHandler? handler = null)
    {
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder();
        OpenIdConnectConfiguration oidc = new() { Issuer = Issuer };
        oidc.SigningKeys.Add(new RsaSecurityKey(key.Rsa.ExportParameters(false)) { KeyId = key.KeyId });
        builder.Configuration[DaprAppChannelToken.ConfigurationKey] = ChannelToken;
        builder.Configuration["Authentication:JwtBearer:AllowedAlgorithms:0"] = SecurityAlgorithms.RsaSha256;
        builder.Services.PostConfigure<JwtBearerOptions>(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
            options => options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(oidc));
        if (handler is not null) builder.Services.AddSingleton(handler);
        WebApplication app = builder.Build();
        app.UseFoldersServerPipeline();
        return app;
    }

    private static HttpRequestMessage CreateRequest(string route, object payload, RsaSecurityKey key, string operation)
    {
        HttpRequestMessage request = new(HttpMethod.Post, route) { Content = JsonContent.Create(payload) };
        request.Headers.Add(DaprAppChannelToken.HeaderName, ChannelToken);
        request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, CreateAssertion(key, operation));
        return request;
    }

    private static string CreateAssertion(RsaSecurityKey key, string operation, string caller = "eventstore")
    {
        DateTime issued = DateTime.UtcNow;
        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = "folders",
            IssuedAt = issued,
            NotBefore = issued,
            Expires = issued.AddMinutes(2),
            Claims = new Dictionary<string, object>
            {
                [EventStoreWorkloadAuthenticationDefaults.CallerClaimType] = caller,
                [EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = operation,
            },
            SigningCredentials = new(key, SecurityAlgorithms.RsaSha256),
        });
    }
}
