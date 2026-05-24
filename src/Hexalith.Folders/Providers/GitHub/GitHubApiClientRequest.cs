using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubApiClientRequest(
    string ProductHeader,
    string ApiVersion,
    ProviderCredentialMode CredentialMode,
    string ProviderBindingRef,
    string CorrelationId);

