using System.Security.Claims;
using Hexalith.EventStore.DomainService.Content;
namespace Hexalith.Folders.NativeContent.Tests;

internal sealed class NativeContentDiagnosticStore(IAuthoritativeContentStore inner, NativeContentFixture fixture) : IAuthoritativeContentStore
{
    private async Task<T> Track<T>(Func<Task<T>> action)
    { try { return await action().ConfigureAwait(true); } catch (Exception error) { fixture.ContentFailure = error.ToString(); throw; } }
    private async Task Track(Func<Task> action)
    { try { await action().ConfigureAwait(true); } catch (Exception error) { fixture.ContentFailure = error.ToString(); throw; } }
    public Task<ContentReceipt> UploadAsync(ClaimsPrincipal p, ContentUploadRequest r, Stream b, CancellationToken t = default) => Track(() => inner.UploadAsync(p, r, b, t));
    public Task<ContentReceipt> VerifyUploadAsync(ClaimsPrincipal p, ContentUploadVerification r, CancellationToken t = default) => Track(() => inner.VerifyUploadAsync(p, r, t));
    public Task<ContentReadResult> InspectUploadAsync(ClaimsPrincipal p, ContentUploadVerification r, CancellationToken t = default) => Track(() => inner.InspectUploadAsync(p, r, t));
    public Task PrepareAsync(ClaimsPrincipal p, ContentPublicationRequest r, CancellationToken t = default) => Track(() => inner.PrepareAsync(p, r, t));
    public Task<ContentPublicationOutcome> RecoverAsync(ClaimsPrincipal p, ContentScope s, string c, CancellationToken t = default) => Track(() => inner.RecoverAsync(p, s, c, t));
    public Task<ContentReadResult> ReadAsync(ClaimsPrincipal p, ContentReadRequest r, CancellationToken t = default) => Track(() => inner.ReadAsync(p, r, t));
    public Task<IReadOnlyList<ContentBinding>> ListAsync(ClaimsPrincipal p, ContentScope s, CancellationToken t = default) => Track(() => inner.ListAsync(p, s, t));
    public Task<bool> TrySealTaskForCompletionAsync(ClaimsPrincipal p, ContentScope s, string task, long fence, CancellationToken t = default) => Track(() => inner.TrySealTaskForCompletionAsync(p, s, task, fence, t));
    public Task<bool> TryQuiesceTaskLeaseAsync(ClaimsPrincipal p, ContentScope s, string task, long fence, CancellationToken t = default) => Track(() => inner.TryQuiesceTaskLeaseAsync(p, s, task, fence, t));
    public Task<IReadOnlyList<ContentUploadStatus>> ListUploadsAsync(ClaimsPrincipal p, ContentScope s, CancellationToken t = default) => Track(() => inner.ListUploadsAsync(p, s, t));
    public Task SetHoldAsync(ClaimsPrincipal p, ContentScope s, string r, bool hold, CancellationToken t = default) => Track(() => inner.SetHoldAsync(p, s, r, hold, t));
    public Task AbandonUploadAsync(ClaimsPrincipal p, ContentScope s, string r, CancellationToken t = default) => Track(() => inner.AbandonUploadAsync(p, s, r, t));
    public Task<bool> ApplyDispositionAsync(ClaimsPrincipal p, ContentScope s, string r, CancellationToken t = default) => Track(() => inner.ApplyDispositionAsync(p, s, r, t));
}
