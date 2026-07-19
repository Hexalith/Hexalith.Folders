using System.Net;
using Hexalith.Folders.Providers.Abstractions;
using Octokit;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed class OctokitGitHubApiClient : IGitHubApiClient
{
    private readonly GitHubClient _client;

    public OctokitGitHubApiClient(GitHubClient client)
        => _client = client ?? throw new ArgumentNullException(nameof(client));

    public Task<GitHubReadinessResult> GetReadinessAsync(
        GitHubReadinessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // The live Octokit readiness probe is intentionally deferred to the provider
        // contract / live-nightly drift path (AC 12). Fail loudly here so the
        // unimplemented seam cannot masquerade as a runtime transport failure that
        // would otherwise be mapped to unknown_provider_outcome / reconciliation.
        throw new NotImplementedException(
            "Live GitHub readiness probing is deferred to the provider contract/live-nightly path; "
            + "supply an IGitHubApiClient seam for offline scenarios.");
    }

    public async Task<GitHubRepositoryCreationResult> CreateRepositoryAsync(
        GitHubRepositoryCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.CancellationBeforeDispatch);
        }

        NewRepository repository = new(request.Target.RepositoryName)
        {
            AutoInit = false,
            Visibility = request.Target.Visibility switch
            {
                ProviderRepositoryVisibility.Private => RepositoryVisibility.Private,
                ProviderRepositoryVisibility.Internal => RepositoryVisibility.Internal,
                ProviderRepositoryVisibility.Public => RepositoryVisibility.Public,
                _ => throw new ArgumentOutOfRangeException(nameof(request), "The resolved repository visibility is invalid."),
            },
        };

        try
        {
            Repository created = await _client.Repository.Create(request.Target.Owner, repository).ConfigureAwait(false);
            return TryCanonicalRepositoryId(created, out string? canonicalRepositoryId)
                ? GitHubRepositoryCreationResult.Success(canonicalRepositoryId: canonicalRepositoryId)
                : GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.AmbiguousMutationResponse);
        }
        catch (RepositoryExistsException)
        {
            return await ReconcileExistingRepositoryAsync(request).ConfigureAwait(false);
        }
        catch (RateLimitExceededException exception)
        {
            return GitHubRepositoryCreationResult.Failure(
                GitHubApiFailureCondition.PrimaryRateLimit,
                BoundedRetryAfter(exception.GetRetryAfterTimeSpan()));
        }
        catch (SecondaryRateLimitExceededException exception)
        {
            return GitHubRepositoryCreationResult.Failure(
                GitHubApiFailureCondition.SecondaryRateLimit,
                RetryAfter(exception.HttpResponse));
        }
        catch (AuthorizationException)
        {
            return GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.AuthenticationRequired);
        }
        catch (ForbiddenException)
        {
            return GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.PermissionInsufficient);
        }
        catch (NotFoundException)
        {
            return GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.NotFoundOrHidden);
        }
        catch (ApiValidationException)
        {
            return GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.ValidationFailure);
        }
        catch (ApiException exception)
        {
            return MapCreationApiFailure(exception);
        }
        catch (Exception exception) when (IsMalformedJsonException(exception))
        {
            return GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.AmbiguousMutationResponse);
        }
        catch (OperationCanceledException)
        {
            return GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.TimeoutDuringMutation);
        }
        catch (HttpRequestException)
        {
            return GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.UnexpectedTransportFailure);
        }
        catch (Exception)
        {
            return GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.AmbiguousMutationResponse);
        }
    }

    public async Task<GitHubRepositoryBindingResult> ValidateRepositoryBindingAsync(
        GitHubRepositoryBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.CancellationBeforeDispatch);
        }

        Repository repository;
        try
        {
            repository = await _client.Repository.Get(
                request.Target.Owner,
                request.Target.RepositoryName).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return MapBindingObservationFailure(exception, GitHubApiFailureCondition.NotFoundOrHidden);
        }

        if (!TryCanonicalRepositoryId(repository, out string? canonicalRepositoryId))
        {
            return GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.MalformedResponse);
        }

        if (!string.IsNullOrWhiteSpace(request.Target.ExpectedCanonicalRepositoryId)
            && !string.Equals(
                request.Target.ExpectedCanonicalRepositoryId,
                canonicalRepositoryId,
                StringComparison.Ordinal))
        {
            return GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.RepositoryConflict);
        }

        if (!string.Equals(repository.DefaultBranch, request.Target.DefaultBranch, StringComparison.Ordinal))
        {
            return GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.DefaultBranchConflict);
        }

        RepositoryPermissions? permissions = repository.Permissions;
        if (request.Target.RequireContentsPermission && (permissions is null || !permissions.Pull))
        {
            return GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.ContentsPermissionInsufficient);
        }

        if (request.Target.SelectedRefKind != ProviderRepositoryRefKind.Branch)
        {
            return GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.UnsupportedRefOperation);
        }

        Branch branch;
        try
        {
            branch = await _client.Repository.Branch.Get(
                request.Target.Owner,
                request.Target.RepositoryName,
                request.Target.SelectedRef).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return MapBindingObservationFailure(exception, GitHubApiFailureCondition.MissingBranchOrRef);
        }

        if (!string.Equals(branch.Name, request.Target.SelectedRef, StringComparison.Ordinal))
        {
            return GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.MissingBranchOrRef);
        }

        if (request.Target.RequireAdministrationPermission && (permissions is null || !permissions.Admin))
        {
            return GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.AdministrationPermissionInsufficient);
        }

        if (request.Target.RequireProtectedRef)
        {
            if (!branch.Protected)
            {
                return GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.BranchProtectionConflict);
            }

            try
            {
                _ = await _client.Repository.Branch.GetBranchProtection(
                    request.Target.Owner,
                    request.Target.RepositoryName,
                    request.Target.SelectedRef).ConfigureAwait(false);
            }
            catch (AuthorizationException)
            {
                return GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.AdministrationPermissionInsufficient);
            }
            catch (ForbiddenException)
            {
                return GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.AdministrationPermissionInsufficient);
            }
            catch (NotFoundException)
            {
                return GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.BranchProtectionConflict);
            }
            catch (Exception exception)
            {
                return MapBindingObservationFailure(exception, GitHubApiFailureCondition.BranchProtectionConflict);
            }
        }

        bool equivalentExisting = request.Target.EquivalentExistingAuthorized
            && !string.IsNullOrWhiteSpace(request.Target.ExpectedCanonicalRepositoryId)
            && string.Equals(
                request.Target.ExpectedCanonicalRepositoryId,
                canonicalRepositoryId,
                StringComparison.Ordinal);
        return GitHubRepositoryBindingResult.Success(equivalentExisting, canonicalRepositoryId);
    }

    private async Task<GitHubRepositoryCreationResult> ReconcileExistingRepositoryAsync(
        GitHubRepositoryCreationRequest request)
    {
        try
        {
            Repository existing = await _client.Repository.Get(
                request.Target.Owner,
                request.Target.RepositoryName).ConfigureAwait(false);
            if (!TryCanonicalRepositoryId(existing, out string? canonicalRepositoryId))
            {
                return GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.MalformedResponse);
            }

            return request.Target.EquivalentExistingAuthorized
                && !string.IsNullOrWhiteSpace(request.Target.ExpectedCanonicalRepositoryId)
                && string.Equals(
                    request.Target.ExpectedCanonicalRepositoryId,
                    canonicalRepositoryId,
                    StringComparison.Ordinal)
                ? GitHubRepositoryCreationResult.Success(
                    equivalentExisting: true,
                    canonicalRepositoryId: canonicalRepositoryId)
                : GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.RepositoryConflict);
        }
        catch (RateLimitExceededException exception)
        {
            return GitHubRepositoryCreationResult.Failure(
                GitHubApiFailureCondition.PrimaryRateLimit,
                BoundedRetryAfter(exception.GetRetryAfterTimeSpan()));
        }
        catch (SecondaryRateLimitExceededException exception)
        {
            return GitHubRepositoryCreationResult.Failure(
                GitHubApiFailureCondition.SecondaryRateLimit,
                RetryAfter(exception.HttpResponse));
        }
        catch (AuthorizationException)
        {
            return GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.AuthenticationRequired);
        }
        catch (ForbiddenException)
        {
            return GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.PermissionInsufficient);
        }
        catch (NotFoundException)
        {
            return GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.NotFoundOrHidden);
        }
        catch (ApiException exception)
        {
            return MapCreationApiFailure(exception);
        }
        catch (Exception exception) when (IsMalformedJsonException(exception))
        {
            return GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.MalformedResponse);
        }
        catch (OperationCanceledException)
        {
            return GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.TimeoutDuringMutation);
        }
        catch (HttpRequestException)
        {
            return GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.UnexpectedTransportFailure);
        }
        catch (Exception)
        {
            return GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.UnexpectedTransportFailure);
        }
    }

    private static GitHubRepositoryCreationResult MapCreationApiFailure(ApiException exception)
        => exception.StatusCode switch
        {
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity =>
                GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.ValidationFailure),
            HttpStatusCode.Unauthorized =>
                GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.AuthenticationRequired),
            HttpStatusCode.Forbidden =>
                GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.PermissionInsufficient),
            HttpStatusCode.NotFound =>
                GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.NotFoundOrHidden),
            HttpStatusCode.Conflict =>
                GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.RepositoryConflict),
            HttpStatusCode.TooManyRequests => MapCreationRateLimit(exception.HttpResponse),
            >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices =>
                GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.AmbiguousMutationResponse),
            >= HttpStatusCode.InternalServerError =>
                GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.ServerUnavailable),
            _ => GitHubRepositoryCreationResult.Failure(GitHubApiFailureCondition.UnexpectedTransportFailure),
        };

    private static GitHubRepositoryBindingResult MapBindingObservationFailure(
        Exception exception,
        GitHubApiFailureCondition notFoundCondition)
        => exception switch
        {
            RateLimitExceededException rateLimit => GitHubRepositoryBindingResult.Failure(
                GitHubApiFailureCondition.PrimaryRateLimit,
                BoundedRetryAfter(rateLimit.GetRetryAfterTimeSpan())),
            SecondaryRateLimitExceededException secondaryRateLimit =>
                GitHubRepositoryBindingResult.Failure(
                    GitHubApiFailureCondition.SecondaryRateLimit,
                    RetryAfter(secondaryRateLimit.HttpResponse)),
            AuthorizationException =>
                GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.AuthenticationRequired),
            ForbiddenException =>
                GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.PermissionInsufficient),
            NotFoundException => GitHubRepositoryBindingResult.Failure(notFoundCondition),
            ApiValidationException =>
                GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.ValidationFailure),
            ApiException apiException => MapBindingApiFailure(apiException, notFoundCondition),
            OperationCanceledException =>
                GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.TimeoutDuringObservation),
            HttpRequestException =>
                GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.TimeoutDuringObservation),
            _ when IsMalformedJsonException(exception) =>
                GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.MalformedResponse),
            _ => GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.UnexpectedTransportFailure),
        };

    private static GitHubRepositoryBindingResult MapBindingApiFailure(
        ApiException exception,
        GitHubApiFailureCondition notFoundCondition)
        => exception.StatusCode switch
        {
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity =>
                GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.ValidationFailure),
            HttpStatusCode.Unauthorized =>
                GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.AuthenticationRequired),
            HttpStatusCode.Forbidden =>
                GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.PermissionInsufficient),
            HttpStatusCode.NotFound => GitHubRepositoryBindingResult.Failure(notFoundCondition),
            HttpStatusCode.Conflict =>
                GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.RepositoryConflict),
            HttpStatusCode.TooManyRequests => MapBindingRateLimit(exception.HttpResponse),
            >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices =>
                GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.MalformedResponse),
            >= HttpStatusCode.InternalServerError =>
                GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.ServerUnavailable),
            _ => GitHubRepositoryBindingResult.Failure(GitHubApiFailureCondition.UnexpectedTransportFailure),
        };

    private static GitHubRepositoryCreationResult MapCreationRateLimit(IResponse response)
        => GitHubRepositoryCreationResult.Failure(
            IsSecondaryRateLimit(response)
                ? GitHubApiFailureCondition.SecondaryRateLimit
                : GitHubApiFailureCondition.PrimaryRateLimit,
            RetryAfter(response));

    private static GitHubRepositoryBindingResult MapBindingRateLimit(IResponse response)
        => GitHubRepositoryBindingResult.Failure(
            IsSecondaryRateLimit(response)
                ? GitHubApiFailureCondition.SecondaryRateLimit
                : GitHubApiFailureCondition.PrimaryRateLimit,
            RetryAfter(response));

    private static bool IsSecondaryRateLimit(IResponse response)
        => response.Headers.ContainsKey("Retry-After")
            && (!response.Headers.TryGetValue("X-RateLimit-Remaining", out string? remaining)
                || !string.Equals(remaining, "0", StringComparison.Ordinal));

    private static TimeSpan? RetryAfter(IResponse response)
    {
        if (response.Headers.TryGetValue("Retry-After", out string? value)
            && int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int seconds))
        {
            return BoundedRetryAfter(TimeSpan.FromSeconds(seconds));
        }

        return null;
    }

    private static TimeSpan? BoundedRetryAfter(TimeSpan retryAfter)
        => retryAfter <= TimeSpan.Zero
            ? null
            : retryAfter > TimeSpan.FromHours(24)
                ? TimeSpan.FromHours(24)
                : retryAfter;

    private static bool TryCanonicalRepositoryId(Repository repository, out string? canonicalRepositoryId)
    {
        canonicalRepositoryId = repository.Id > 0
            ? repository.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;
        return canonicalRepositoryId is not null;
    }

    private static bool IsMalformedJsonException(Exception exception)
        => exception.GetType().Name.Contains("Json", StringComparison.Ordinal);
}
