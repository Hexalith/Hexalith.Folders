using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Testing.Providers;

public sealed class RecordingProviderCapabilityAuthorizer : IProviderCapabilityAuthorizer
{
    private readonly ProviderCapabilityAuthorizationResult _result;

    private RecordingProviderCapabilityAuthorizer(ProviderCapabilityAuthorizationResult result)
    {
        _result = result;
    }

    public int Calls { get; private set; }

    public static RecordingProviderCapabilityAuthorizer Allowed(string fingerprint)
        => new(ProviderCapabilityAuthorizationResult.Allowed(new ProviderAuthorizationEvidenceSnapshot(
            fingerprint,
            DateTimeOffset.Parse("2026-05-24T06:00:00+00:00"),
            "fresh")));

    public static RecordingProviderCapabilityAuthorizer Denied()
        => new(ProviderCapabilityAuthorizationResult.Denied(
            ProviderFailureCategory.ProviderPermissionInsufficient,
            "safe_denial"));

    public Task<ProviderCapabilityAuthorizationResult> AuthorizeAsync(
        ProviderCapabilityDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        return Task.FromResult(_result);
    }
}
