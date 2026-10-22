using System.Security.Claims;
using Hexalith.EventStore.DomainService.Content;

namespace Hexalith.Folders.Server.Tests;

internal sealed class WorkspaceContentExpiringStore(IAuthoritativeContentStore inner, WorkspaceContentProductionClock clock) : IAuthoritativeContentStore
{
    internal bool Prepared { get; private set; }
    public Task<ContentReceipt> UploadAsync(ClaimsPrincipal principal, ContentUploadRequest request, Stream bytes, CancellationToken cancellationToken = default) => inner.UploadAsync(principal, request, bytes, cancellationToken);
    public Task<ContentReceipt> VerifyUploadAsync(ClaimsPrincipal principal, ContentUploadVerification request, CancellationToken cancellationToken = default) => inner.VerifyUploadAsync(principal, request, cancellationToken);
    public Task<ContentReadResult> InspectUploadAsync(ClaimsPrincipal principal, ContentUploadVerification request, CancellationToken cancellationToken = default) => inner.InspectUploadAsync(principal, request, cancellationToken);
    public async Task PrepareAsync(ClaimsPrincipal principal, ContentPublicationRequest request, CancellationToken cancellationToken = default) { await inner.PrepareAsync(principal, request, cancellationToken).ConfigureAwait(false); Prepared = true; }
    public Task<ContentPublicationOutcome> RecoverAsync(ClaimsPrincipal principal, ContentScope scope, string commandId, CancellationToken cancellationToken = default) => inner.RecoverAsync(principal, scope, commandId, cancellationToken);
    public Task<ContentReadResult> ReadAsync(ClaimsPrincipal principal, ContentReadRequest request, CancellationToken cancellationToken = default) => inner.ReadAsync(principal, request, cancellationToken);
    public async Task<IReadOnlyList<ContentBinding>> ListAsync(ClaimsPrincipal principal, ContentScope scope, CancellationToken cancellationToken = default) { var result = await inner.ListAsync(principal, scope, cancellationToken).ConfigureAwait(false); if (Prepared) clock.Now = clock.Now.AddHours(2); return result; }
    public Task<IReadOnlyList<ContentUploadStatus>> ListUploadsAsync(ClaimsPrincipal principal, ContentScope scope, CancellationToken cancellationToken = default) => inner.ListUploadsAsync(principal, scope, cancellationToken);
    public Task SetHoldAsync(ClaimsPrincipal principal, ContentScope scope, string receiptId, bool legalHold, CancellationToken cancellationToken = default) => inner.SetHoldAsync(principal, scope, receiptId, legalHold, cancellationToken);
    public Task AbandonUploadAsync(ClaimsPrincipal principal, ContentScope scope, string receiptId, CancellationToken cancellationToken = default) => inner.AbandonUploadAsync(principal, scope, receiptId, cancellationToken);
    public Task<bool> ApplyDispositionAsync(ClaimsPrincipal principal, ContentScope scope, string receiptId, CancellationToken cancellationToken = default) => inner.ApplyDispositionAsync(principal, scope, receiptId, cancellationToken);
}
