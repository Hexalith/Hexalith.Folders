using System.Security.Claims;
using Dapr.Client;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.Folders.Aggregates.Folder;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Isolates HTTP transport tests from domain authority, retaining actual SDK byte verification.</summary>
internal static class WorkspaceContentTransportTestServices
{
    internal static void Authenticate(WebApplication app) => app.Use(async (context, next) =>
    {
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-a")], "transport-test"));
        await next(context).ConfigureAwait(false);
    });
    internal static string Hash(byte[] bytes) => "hashref_" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
    internal static HttpContent Multipart(HttpContent metadata, byte[] bytes)
    {
        var multipart = new MultipartFormDataContent();
        multipart.Add(metadata, "metadata"); multipart.Add(new ByteArrayContent(bytes), "content");
        return multipart;
    }
    internal static void Add(WebApplicationBuilder builder)
    {
        builder.Configuration["Folders:WorkspaceFilePolicy:Version"] = "1.1.0";
        builder.Configuration["Folders:WorkspaceFilePolicy:ProtectionMode"] = "non_confidential";
        builder.Configuration["Folders:WorkspaceFilePolicy:Includes:0"] = "**";
        builder.Services.RemoveAll<DaprClient>();
        builder.Services.AddSingleton<DaprClient>(_ => new RecordingDaprClient { PersistTypedState = true });
        builder.Services.RemoveAll<IContentAuthority>();
        builder.Services.AddSingleton<IContentAuthority, TransportAuthority>();
        builder.Services.RemoveAll<IWorkspacePathPolicyEvidenceProvider>();
        builder.Services.AddSingleton<IWorkspacePathPolicyEvidenceProvider, TransportPathEvidence>();
    }

    private sealed class TransportPathEvidence : IWorkspacePathPolicyEvidenceProvider
    {
        public Task<WorkspacePathPolicyEvidenceResult> GetEvidenceAsync(WorkspacePathPolicyEvidenceRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new WorkspacePathPolicyEvidenceResult(WorkspacePathPolicyEvidenceDecision.NoEscape));
    }

    private sealed class TransportAuthority : IContentAuthority
    {
        public Task AuthorizeAsync(ClaimsPrincipal principal, ContentScope scope, ContentOperation operation, object resource, CancellationToken token) => Task.CompletedTask;
        public Task ValidateUploadAsync(ClaimsPrincipal principal, ContentUploadRequest request, ReadOnlyMemory<byte> bytes, CancellationToken token) => Task.CompletedTask;
        public Task ValidatePreparationAsync(ClaimsPrincipal principal, ContentPublicationRequest request, CancellationToken token) => Task.CompletedTask;
        public Task<ContentPublicationOutcome> GetPublicationOutcomeAsync(ContentPublicationRequest request, CancellationToken token) => Task.FromResult(ContentPublicationOutcome.Pending);
        public Task<ContentDisposition> GetDispositionAsync(ContentScope scope, ContentReceipt receipt, CancellationToken token) => Task.FromResult(new ContentDisposition(1, false, true, true, false));
        public Task<bool> TryAuthorizeErasureAsync(ContentScope scope, ContentReceipt receipt, long generation, CancellationToken token) => Task.FromResult(false);
    }
}
