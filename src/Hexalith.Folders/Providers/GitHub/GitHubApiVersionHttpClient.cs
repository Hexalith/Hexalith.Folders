using Octokit;
using Octokit.Internal;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed class GitHubApiVersionHttpClient(IHttpClient inner, string apiVersion) : IHttpClient
{
    private readonly IHttpClient _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly string _apiVersion = !string.IsNullOrWhiteSpace(apiVersion)
        ? apiVersion
        : throw new ArgumentException("The GitHub API version is required.", nameof(apiVersion));

    public Task<IResponse> Send(
        IRequest request,
        CancellationToken cancellationToken,
        Func<object, object> preprocessResponseBody)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Headers["X-GitHub-Api-Version"] = _apiVersion;
        return _inner.Send(request, cancellationToken, preprocessResponseBody);
    }

    public void SetRequestTimeout(TimeSpan timeout)
        => _inner.SetRequestTimeout(timeout);

    public void Dispose()
        => _inner.Dispose();
}
