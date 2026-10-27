using System.Security.Claims;
using Hexalith.EventStore.DomainService.Content;
namespace Hexalith.Folders.NativeContent.Tests;

internal sealed class NativeContentSeedAuthority : IContentAuthority
{
    public Task AuthorizeAsync(ClaimsPrincipal principal, ContentScope scope, ContentOperation operation, object resource, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task ValidateUploadAsync(ClaimsPrincipal principal, ContentUploadRequest request, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task ValidatePreparationAsync(ClaimsPrincipal principal, ContentPublicationRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<ContentPublicationOutcome> GetPublicationOutcomeAsync(ContentPublicationRequest request, CancellationToken cancellationToken) => Task.FromResult(ContentPublicationOutcome.Accepted);
    public Task<bool> TryAuthorizeErasureAsync(ContentScope scope, ContentReceipt receipt, long generation, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<ContentDisposition> GetDispositionAsync(ContentScope scope, ContentReceipt receipt, CancellationToken cancellationToken) => Task.FromResult(new ContentDisposition(0, false, true, true, false));
}
