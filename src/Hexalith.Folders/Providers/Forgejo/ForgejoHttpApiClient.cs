using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

internal sealed class ForgejoHttpApiClient : IForgejoApiClient
{
    private const int MaximumJsonResponseBytes = 256 * 1024;
    private static readonly TimeSpan ResponseBodyTimeout = TimeSpan.FromSeconds(30);
    private readonly HttpClient _client;
    private readonly Uri _authorizedBaseUri;

    public ForgejoHttpApiClient(HttpClient client, Uri authorizedBaseUri)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _authorizedBaseUri = authorizedBaseUri ?? throw new ArgumentNullException(nameof(authorizedBaseUri));
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
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
                ApiUri("version"),
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
            ForgejoApiFailureCondition? responseFailure = MapObservationResponse(
                response,
                ForgejoApiFailureCondition.NotFoundOrHidden);
            if (responseFailure is not null)
            {
                return ForgejoReadinessResult.Failure(responseFailure.Value, RetryAfter(response));
            }

            try
            {
                using JsonDocument? document = await ReadJsonDocumentAsync(response, cancellationToken).ConfigureAwait(false);
                string? productVersion = document is not null
                    && document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("version", out JsonElement version)
                    && version.ValueKind == JsonValueKind.String
                        ? version.GetString()
                        : null;
                if (string.IsNullOrWhiteSpace(productVersion))
                {
                    return ForgejoReadinessResult.Failure(ForgejoApiFailureCondition.MalformedResponse);
                }

                if (!ForgejoSupportedVersionCatalog.TryFind(productVersion, out ForgejoSupportedVersionEntry? supportedVersion))
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
                        RetryAfter(response),
                        "forgejo_headers_metadata_only"));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return ForgejoReadinessResult.Failure(ForgejoApiFailureCondition.ServerUnavailable);
            }
            catch (JsonException)
            {
                return ForgejoReadinessResult.Failure(ForgejoApiFailureCondition.MalformedResponse);
            }
            catch (IOException)
            {
                return ForgejoReadinessResult.Failure(ForgejoApiFailureCondition.UnexpectedTransportFailure);
            }
        }
    }

    public async Task<ForgejoRepositoryCreationResult> CreateRepositoryAsync(
        ForgejoRepositoryCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return ForgejoRepositoryCreationResult.Failure(ForgejoApiFailureCondition.CancellationBeforeDispatch);
        }

        if (!IsSupportedRequest(request.ApiSurfaceVersion, request.SupportedSnapshotVersion)
            || request.Target is null
            || !request.Target.TryValidate(out _)
            || !HasValidExpectedCanonicalRepositoryId(request.Target)
            || !TryMapPrivateVisibility(request.Target.Visibility, out bool isPrivate))
        {
            return ForgejoRepositoryCreationResult.Failure(ForgejoApiFailureCondition.ValidationFailure);
        }

        Dictionary<string, object?> body = new(StringComparer.Ordinal)
        {
            ["auto_init"] = false,
            ["name"] = request.Target.RepositoryName,
            ["private"] = isPrivate,
        };

        using HttpRequestMessage httpRequest = CreateJsonRequest(
            HttpMethod.Post,
            ApiUri($"orgs/{Escape(request.Target.Owner)}/repos"),
            body);
        bool mutationDispatched = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            mutationDispatched = true;
            using HttpResponseMessage response = await _client.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.BadRequest
                or HttpStatusCode.Conflict
                or HttpStatusCode.UnprocessableEntity)
            {
                return await ReconcileExistingRepositoryAsync(request, cancellationToken).ConfigureAwait(false);
            }

            if (response.StatusCode != HttpStatusCode.Created)
            {
                return ForgejoRepositoryCreationResult.Failure(
                    MapMutationResponse(response),
                    RetryAfter(response));
            }

            using JsonDocument? document = await ReadJsonDocumentAsync(response, cancellationToken).ConfigureAwait(false);
            return TryReadRepositoryIdentity(document, request.Target, requirePolicyEvidence: false, out string? canonicalRepositoryId, out _)
                ? ForgejoRepositoryCreationResult.Success(canonicalRepositoryId: canonicalRepositoryId)
                : ForgejoRepositoryCreationResult.Failure(ForgejoApiFailureCondition.AmbiguousMutationResponse);
        }
        catch (OperationCanceledException) when (!mutationDispatched)
        {
            return ForgejoRepositoryCreationResult.Failure(ForgejoApiFailureCondition.CancellationBeforeDispatch);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ForgejoRepositoryCreationResult.Failure(ForgejoApiFailureCondition.CancellationDuringMutation);
        }
        catch (OperationCanceledException)
        {
            return ForgejoRepositoryCreationResult.Failure(ForgejoApiFailureCondition.TimeoutDuringMutation);
        }
        catch (HttpRequestException)
        {
            return ForgejoRepositoryCreationResult.Failure(ForgejoApiFailureCondition.UnexpectedTransportFailure);
        }
        catch (JsonException)
        {
            return ForgejoRepositoryCreationResult.Failure(ForgejoApiFailureCondition.AmbiguousMutationResponse);
        }
        catch (IOException)
        {
            return ForgejoRepositoryCreationResult.Failure(ForgejoApiFailureCondition.UnexpectedTransportFailure);
        }
    }

    public async Task<ForgejoRepositoryBindingResult> ValidateRepositoryBindingAsync(
        ForgejoRepositoryBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return ForgejoRepositoryBindingResult.Failure(ForgejoApiFailureCondition.CancellationBeforeDispatch);
        }

        if (!IsSupportedRequest(request.ApiSurfaceVersion, request.SupportedSnapshotVersion)
            || request.Target is null
            || !request.Target.TryValidate(out _)
            || !HasValidExpectedCanonicalRepositoryId(request.Target))
        {
            return ForgejoRepositoryBindingResult.Failure(ForgejoApiFailureCondition.ValidationFailure);
        }

        if (request.Target.SelectedRefKind != ProviderRepositoryRefKind.Branch)
        {
            return ForgejoRepositoryBindingResult.Failure(ForgejoApiFailureCondition.UnsupportedRefOperation);
        }

        try
        {
            using HttpResponseMessage repositoryResponse = await SendObservationAsync(
                ApiUri($"repos/{Escape(request.Target.Owner)}/{Escape(request.Target.RepositoryName)}"),
                cancellationToken).ConfigureAwait(false);
            ForgejoApiFailureCondition? repositoryFailure = MapObservationResponse(
                repositoryResponse,
                ForgejoApiFailureCondition.NotFoundOrHidden);
            if (repositoryFailure is not null)
            {
                return ForgejoRepositoryBindingResult.Failure(repositoryFailure.Value, RetryAfter(repositoryResponse));
            }

            using JsonDocument? repositoryDocument = await ReadJsonDocumentAsync(repositoryResponse, cancellationToken).ConfigureAwait(false);
            if (!TryReadRepositoryIdentity(
                repositoryDocument,
                request.Target,
                requirePolicyEvidence: true,
                out string? canonicalRepositoryId,
                out ForgejoApiFailureCondition policyFailure))
            {
                return ForgejoRepositoryBindingResult.Failure(policyFailure);
            }

            using HttpResponseMessage branchResponse = await SendObservationAsync(
                ApiUri($"repos/{Escape(request.Target.Owner)}/{Escape(request.Target.RepositoryName)}/branches/{Escape(request.Target.SelectedRef)}"),
                cancellationToken).ConfigureAwait(false);
            ForgejoApiFailureCondition? branchFailure = MapObservationResponse(
                branchResponse,
                ForgejoApiFailureCondition.MissingBranchOrPath);
            if (branchFailure is not null)
            {
                return ForgejoRepositoryBindingResult.Failure(branchFailure.Value, RetryAfter(branchResponse));
            }

            using JsonDocument? branchDocument = await ReadJsonDocumentAsync(branchResponse, cancellationToken).ConfigureAwait(false);
            if (!TryReadBranch(
                branchDocument,
                request.Target.SelectedRef,
                request.Target.RequireProtectedRef,
                out string? protectionName,
                out ForgejoApiFailureCondition branchValidationFailure))
            {
                return ForgejoRepositoryBindingResult.Failure(branchValidationFailure);
            }

            if (request.Target.RequireProtectedRef)
            {
                using HttpResponseMessage protectionResponse = await SendObservationAsync(
                    ApiUri($"repos/{Escape(request.Target.Owner)}/{Escape(request.Target.RepositoryName)}/branch_protections/{Escape(protectionName!)}"),
                    cancellationToken).ConfigureAwait(false);
                ForgejoApiFailureCondition? protectionFailure = MapObservationResponse(
                    protectionResponse,
                    ForgejoApiFailureCondition.BranchProtectionConflict);
                if (protectionFailure is not null)
                {
                    return ForgejoRepositoryBindingResult.Failure(protectionFailure.Value, RetryAfter(protectionResponse));
                }

                using JsonDocument? protectionDocument = await ReadJsonDocumentAsync(protectionResponse, cancellationToken).ConfigureAwait(false);
                if (!TryReadExactString(protectionDocument, "rule_name", protectionName!))
                {
                    return ForgejoRepositoryBindingResult.Failure(ForgejoApiFailureCondition.BranchProtectionConflict);
                }
            }

            bool equivalentExisting = request.Target.EquivalentExistingAuthorized
                && request.Target.ExpectedCanonicalRepositoryId is not null
                && string.Equals(
                    request.Target.ExpectedCanonicalRepositoryId,
                    canonicalRepositoryId,
                    StringComparison.Ordinal);
            return ForgejoRepositoryBindingResult.Success(equivalentExisting, canonicalRepositoryId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ForgejoRepositoryBindingResult.Failure(ForgejoApiFailureCondition.ObservationCancelled);
        }
        catch (OperationCanceledException)
        {
            return ForgejoRepositoryBindingResult.Failure(ForgejoApiFailureCondition.ServerUnavailable);
        }
        catch (HttpRequestException)
        {
            return ForgejoRepositoryBindingResult.Failure(ForgejoApiFailureCondition.ServerUnavailable);
        }
        catch (JsonException)
        {
            return ForgejoRepositoryBindingResult.Failure(ForgejoApiFailureCondition.MalformedResponse);
        }
        catch (IOException)
        {
            return ForgejoRepositoryBindingResult.Failure(ForgejoApiFailureCondition.ServerUnavailable);
        }
    }

    private async Task<ForgejoRepositoryCreationResult> ReconcileExistingRepositoryAsync(
        ForgejoRepositoryCreationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await SendObservationAsync(
                ApiUri($"repos/{Escape(request.Target.Owner)}/{Escape(request.Target.RepositoryName)}"),
                cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return ForgejoRepositoryCreationResult.Failure(ForgejoApiFailureCondition.RepositoryConflict);
            }

            using JsonDocument? document = await ReadJsonDocumentAsync(response, cancellationToken).ConfigureAwait(false);
            if (!request.Target.EquivalentExistingAuthorized
                || string.IsNullOrWhiteSpace(request.Target.ExpectedCanonicalRepositoryId)
                || !TryReadRepositoryIdentity(document, request.Target, requirePolicyEvidence: false, out string? canonicalRepositoryId, out _)
                || !string.Equals(
                    request.Target.ExpectedCanonicalRepositoryId,
                    canonicalRepositoryId,
                    StringComparison.Ordinal))
            {
                return ForgejoRepositoryCreationResult.Failure(ForgejoApiFailureCondition.RepositoryConflict);
            }

            return ForgejoRepositoryCreationResult.Success(equivalentExisting: true, canonicalRepositoryId);
        }
        catch (OperationCanceledException)
        {
            return ForgejoRepositoryCreationResult.Failure(ForgejoApiFailureCondition.RepositoryConflict);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return ForgejoRepositoryCreationResult.Failure(ForgejoApiFailureCondition.RepositoryConflict);
        }
    }

    private async Task<HttpResponseMessage> SendObservationAsync(Uri uri, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        return await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
    }

    private static HttpRequestMessage CreateJsonRequest(
        HttpMethod method,
        Uri uri,
        IReadOnlyDictionary<string, object?> body)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(body);
        ByteArrayContent content = new(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
        {
            CharSet = Encoding.UTF8.WebName,
        };
        return new HttpRequestMessage(method, uri) { Content = content };
    }

    private static async Task<JsonDocument?> ReadJsonDocumentAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!IsJson(response.Content.Headers.ContentType)
            || response.Content.Headers.ContentLength is > MaximumJsonResponseBytes)
        {
            return null;
        }

        using CancellationTokenSource responseBodyCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        responseBodyCancellation.CancelAfter(ResponseBodyTimeout);
        CancellationToken responseBodyToken = responseBodyCancellation.Token;
        using Stream stream = await response.Content.ReadAsStreamAsync(responseBodyToken).ConfigureAwait(false);
        using MemoryStream buffer = new();
        byte[] chunk = new byte[8192];
        while (true)
        {
            int read = await stream.ReadAsync(chunk.AsMemory(), responseBodyToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaximumJsonResponseBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), responseBodyToken).ConfigureAwait(false);
        }

        if (buffer.Length == 0)
        {
            return null;
        }

        buffer.Position = 0;
        return await JsonDocument.ParseAsync(buffer, cancellationToken: responseBodyToken).ConfigureAwait(false);
    }

    private static bool TryReadRepositoryIdentity(
        JsonDocument? document,
        ProviderRepositoryResolvedTarget target,
        bool requirePolicyEvidence,
        out string? canonicalRepositoryId,
        out ForgejoApiFailureCondition failure)
    {
        canonicalRepositoryId = null;
        failure = ForgejoApiFailureCondition.MalformedResponse;
        if (document is null
            || document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("id", out JsonElement id)
            || id.ValueKind != JsonValueKind.Number
            || !id.TryGetInt64(out long numericId)
            || numericId <= 0)
        {
            return false;
        }

        canonicalRepositoryId = numericId.ToString(CultureInfo.InvariantCulture);
        if (!TryReadExactString(document, "name", target.RepositoryName))
        {
            failure = ForgejoApiFailureCondition.RepositoryConflict;
            return false;
        }

        if (!TryReadVisibility(document.RootElement, target.Visibility, out bool visibilityMatches))
        {
            return false;
        }

        if (!visibilityMatches)
        {
            failure = ForgejoApiFailureCondition.RepositoryConflict;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(target.ExpectedCanonicalRepositoryId)
            && !string.Equals(target.ExpectedCanonicalRepositoryId, canonicalRepositoryId, StringComparison.Ordinal))
        {
            failure = ForgejoApiFailureCondition.RepositoryConflict;
            return false;
        }

        if (!requirePolicyEvidence)
        {
            return true;
        }

        if (!TryReadExactString(document, "default_branch", target.DefaultBranch))
        {
            failure = ForgejoApiFailureCondition.DefaultBranchConflict;
            return false;
        }

        if (!target.RequireContentsPermission && !target.RequireAdministrationPermission)
        {
            return true;
        }

        if (!document.RootElement.TryGetProperty("permissions", out JsonElement permissions)
            || permissions.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (target.RequireContentsPermission
            && (!permissions.TryGetProperty("pull", out JsonElement pull)
                || pull.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || !pull.GetBoolean()))
        {
            failure = ForgejoApiFailureCondition.ContentsPermissionInsufficient;
            return false;
        }

        if (target.RequireAdministrationPermission
            && (!permissions.TryGetProperty("admin", out JsonElement admin)
                || admin.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || !admin.GetBoolean()))
        {
            failure = ForgejoApiFailureCondition.AdministrationPermissionInsufficient;
            return false;
        }

        return true;
    }

    private static bool TryReadBranch(
        JsonDocument? document,
        string expectedBranch,
        bool requireProtection,
        out string? protectionName,
        out ForgejoApiFailureCondition failure)
    {
        protectionName = null;
        failure = ForgejoApiFailureCondition.MalformedResponse;
        if (document is null
            || document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("protected", out JsonElement isProtected)
            || isProtected.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        if (!TryReadExactString(document, "name", expectedBranch))
        {
            failure = ForgejoApiFailureCondition.MissingBranchOrPath;
            return false;
        }

        if (!requireProtection)
        {
            return true;
        }

        if (!isProtected.GetBoolean()
            || !document.RootElement.TryGetProperty("effective_branch_protection_name", out JsonElement name)
            || name.ValueKind != JsonValueKind.String
            || !IsSafeProtectionName(name.GetString()))
        {
            failure = ForgejoApiFailureCondition.BranchProtectionConflict;
            return false;
        }

        protectionName = name.GetString();
        return true;
    }

    private static bool TryReadVisibility(
        JsonElement repository,
        ProviderRepositoryVisibility expected,
        out bool matches)
    {
        matches = false;
        if (!repository.TryGetProperty("private", out JsonElement isPrivate)
            || isPrivate.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !repository.TryGetProperty("internal", out JsonElement isInternal)
            || isInternal.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        matches = expected switch
        {
            ProviderRepositoryVisibility.Public => !isPrivate.GetBoolean() && !isInternal.GetBoolean(),
            ProviderRepositoryVisibility.Private => isPrivate.GetBoolean() && !isInternal.GetBoolean(),
            ProviderRepositoryVisibility.Internal => !isPrivate.GetBoolean() && isInternal.GetBoolean(),
            _ => false,
        };
        return Enum.IsDefined(expected);
    }

    private static bool TryReadExactString(JsonDocument? document, string propertyName, string expected)
        => document is not null
            && document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty(propertyName, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && string.Equals(value.GetString(), expected, StringComparison.Ordinal);

    private static bool TryMapPrivateVisibility(ProviderRepositoryVisibility visibility, out bool isPrivate)
    {
        isPrivate = visibility == ProviderRepositoryVisibility.Private;
        return visibility is ProviderRepositoryVisibility.Public or ProviderRepositoryVisibility.Private;
    }

    private static bool HasValidExpectedCanonicalRepositoryId(ProviderRepositoryResolvedTarget target)
        => target.ExpectedCanonicalRepositoryId is null
            || TryNormalizeCanonicalRepositoryId(target.ExpectedCanonicalRepositoryId, out _);

    private static bool TryNormalizeCanonicalRepositoryId(string value, out string canonicalRepositoryId)
    {
        canonicalRepositoryId = string.Empty;
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long numericId)
            || numericId <= 0)
        {
            return false;
        }

        canonicalRepositoryId = numericId.ToString(CultureInfo.InvariantCulture);
        return string.Equals(value, canonicalRepositoryId, StringComparison.Ordinal);
    }

    private static bool IsSafeProtectionName(string? value)
        => value is { Length: > 0 and <= 256 }
            && ProviderGitOperationResolvedTarget.IsCanonicalUnicode(value)
            && !value.Contains("..", StringComparison.Ordinal)
            && !value.Any(char.IsControl);

    private static bool IsSupportedRequest(string apiSurfaceVersion, string snapshotVersion)
        => string.Equals(apiSurfaceVersion, ForgejoProviderConstants.ApiSurfaceVersion, StringComparison.Ordinal)
            && ForgejoSupportedVersionCatalog.IsSupported(snapshotVersion);

    private ForgejoApiFailureCondition? MapObservationResponse(
        HttpResponseMessage response,
        ForgejoApiFailureCondition notFound)
    {
        if (response.StatusCode == HttpStatusCode.OK)
        {
            return null;
        }

        if (response.IsSuccessStatusCode)
        {
            return ForgejoApiFailureCondition.MalformedResponse;
        }

        if (IsRedirect(response.StatusCode))
        {
            return IsCrossOriginRedirect(response)
                ? ForgejoApiFailureCondition.RedirectCrossOrigin
                : ForgejoApiFailureCondition.ServerUnavailable;
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => ForgejoApiFailureCondition.AuthenticationRequired,
            HttpStatusCode.Forbidden => ForgejoApiFailureCondition.PermissionInsufficient,
            HttpStatusCode.NotFound => notFound,
            HttpStatusCode.Conflict => ForgejoApiFailureCondition.RepositoryConflict,
            HttpStatusCode.UnprocessableEntity => ForgejoApiFailureCondition.ValidationFailure,
            _ when (int)response.StatusCode == 429 => ForgejoApiFailureCondition.RateLimit,
            _ when (int)response.StatusCode >= 500 => ForgejoApiFailureCondition.ServerUnavailable,
            _ => ForgejoApiFailureCondition.ValidationFailure,
        };
    }

    private ForgejoApiFailureCondition MapMutationResponse(HttpResponseMessage response)
    {
        if (IsRedirect(response.StatusCode))
        {
            return IsCrossOriginRedirect(response)
                ? ForgejoApiFailureCondition.RedirectCrossOrigin
                : ForgejoApiFailureCondition.AmbiguousMutationResponse;
        }

        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest => ForgejoApiFailureCondition.ValidationFailure,
            HttpStatusCode.Unauthorized => ForgejoApiFailureCondition.AuthenticationRequired,
            HttpStatusCode.Forbidden => ForgejoApiFailureCondition.PermissionInsufficient,
            HttpStatusCode.NotFound => ForgejoApiFailureCondition.NotFoundOrHidden,
            _ when (int)response.StatusCode is 429 or >= 500 => ForgejoApiFailureCondition.AmbiguousMutationResponse,
            _ => ForgejoApiFailureCondition.AmbiguousMutationResponse,
        };
    }

    private bool IsCrossOriginRedirect(HttpResponseMessage response)
        => response.Headers.Location is { IsAbsoluteUri: true } location
            && !ForgejoAuthorizedBaseUrl.IsSameOrigin(_authorizedBaseUri, location);

    private Uri ApiUri(string relativePath)
        => new(_authorizedBaseUri, $"api/v1/{relativePath}");

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        TimeSpan? delta = response.Headers.RetryAfter?.Delta;
        if (delta is { } boundedDelta)
        {
            return BoundedRetryAfter(boundedDelta);
        }

        DateTimeOffset? date = response.Headers.RetryAfter?.Date;
        return date is null
            ? null
            : BoundedRetryAfter(date.Value - DateTimeOffset.UtcNow);
    }

    private static TimeSpan? BoundedRetryAfter(TimeSpan value)
        => value <= TimeSpan.Zero
            ? TimeSpan.Zero
            : value > TimeSpan.FromHours(24)
                ? TimeSpan.FromHours(24)
                : value;

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
