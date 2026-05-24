using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Hexalith.Folders.Providers.Forgejo;

internal sealed class ForgejoHttpApiClient : IForgejoApiClient
{
    private readonly HttpClient _client;
    private readonly Uri _authorizedBaseUri;

    public ForgejoHttpApiClient(HttpClient client, Uri authorizedBaseUri)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _authorizedBaseUri = authorizedBaseUri ?? throw new ArgumentNullException(nameof(authorizedBaseUri));
    }

    public async Task<ForgejoReadinessResult> GetReadinessAsync(
        ForgejoReadinessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        HttpResponseMessage response;
        try
        {
            response = await _client.GetAsync(
                new Uri(_authorizedBaseUri, "api/v1/version"),
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ForgejoReadinessResult.Failure(ForgejoApiFailureCondition.ServerUnavailable);
        }
        catch (HttpRequestException)
        {
            return ForgejoReadinessResult.Failure(ForgejoApiFailureCondition.UnexpectedTransportFailure);
        }

        using (response)
        {
            if (IsRedirect(response.StatusCode))
            {
                return response.Headers.Location is { } location
                    && location.IsAbsoluteUri
                    && !ForgejoAuthorizedBaseUrl.IsSameOrigin(_authorizedBaseUri, location)
                        ? ForgejoReadinessResult.Failure(ForgejoApiFailureCondition.RedirectCrossOrigin)
                        : ForgejoReadinessResult.Failure(ForgejoApiFailureCondition.ServerUnavailable);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return ForgejoReadinessResult.Failure(ForgejoApiFailureCondition.AuthenticationRequired);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return ForgejoReadinessResult.Failure(ForgejoApiFailureCondition.PermissionInsufficient);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return ForgejoReadinessResult.Failure(ForgejoApiFailureCondition.NotFoundOrHidden);
            }

            if ((int)response.StatusCode == 429)
            {
                return ForgejoReadinessResult.Failure(ForgejoApiFailureCondition.RateLimit, response.Headers.RetryAfter?.Delta);
            }

            if ((int)response.StatusCode >= 500)
            {
                return ForgejoReadinessResult.Failure(ForgejoApiFailureCondition.ServerUnavailable);
            }

            if (!response.IsSuccessStatusCode)
            {
                return ForgejoReadinessResult.Failure(ForgejoApiFailureCondition.ValidationFailure);
            }

            if (!IsJson(response.Content.Headers.ContentType))
            {
                return ForgejoReadinessResult.Failure(ForgejoApiFailureCondition.MalformedResponse);
            }

            string? productVersion;
            try
            {
                using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                productVersion = document.RootElement.TryGetProperty("version", out JsonElement version)
                    ? version.GetString()
                    : null;
            }
            catch (JsonException)
            {
                return ForgejoReadinessResult.Failure(ForgejoApiFailureCondition.MalformedResponse);
            }

            if (string.IsNullOrWhiteSpace(productVersion)
                || !ForgejoSupportedVersionCatalog.TryFind(productVersion, out ForgejoSupportedVersionEntry? supportedVersion))
            {
                return ForgejoReadinessResult.Failure(ForgejoApiFailureCondition.VersionIncompatible);
            }

            return ForgejoReadinessResult.Success(
                new ForgejoVersionEvidence(
                    productVersion,
                    supportedVersion.Version,
                    ForgejoProviderConstants.ApiSurfaceVersion,
                    supportedVersion.ExpectedApiCompatibilityPosture,
                    supportedVersion.ExpectedApiCompatibilityPosture),
                new ForgejoPermissionEvidence(
                    SupportsRepositoryCreation: true,
                    SupportsRepositoryBinding: true,
                    SupportsBranchRefInspection: true,
                    SupportsFileMutation: true,
                    SupportsCommit: true,
                    SupportsStatus: true,
                    SupportsMetadata: true,
                    SupportsPagination: true,
                    SupportsContentsApi: true,
                    RequiredScopePosture: "repository_contents_status_scope"),
                new ForgejoRateLimitEvidence(
                    "bounded",
                    Retryable: true,
                    response.Headers.RetryAfter?.Delta,
                    "forgejo_headers_metadata_only"));
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.Moved
            or HttpStatusCode.Redirect
            or HttpStatusCode.RedirectMethod
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;

    private static bool IsJson(MediaTypeHeaderValue? contentType)
        => contentType is not null
            && (string.Equals(contentType.MediaType, "application/json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(contentType.MediaType, "text/json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(contentType.MediaType, "application/problem+json", StringComparison.OrdinalIgnoreCase));
}
