using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoApiClientRequest(
    string ProductHeader,
    Uri BaseUri,
    string ApiSurfaceVersion,
    ProviderCredentialMode CredentialMode,
    string ProviderBindingRef,
    string CorrelationId);
