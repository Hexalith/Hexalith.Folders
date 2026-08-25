using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hexalith.Folders.Providers.Abstractions;
using Octokit;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed class OctokitGitHubApiClient : IGitHubApiClient
{
    private readonly GitHubClient _client;
    private readonly Func<HttpMessageHandler> _operationHandlerFactory;
    private readonly string _accessToken;
    private readonly string _productHeader;
    private readonly string _apiVersion;

    public OctokitGitHubApiClient(
        GitHubClient client,
        Func<HttpMessageHandler> operationHandlerFactory,
        string accessToken,
        string productHeader,
        string apiVersion)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _operationHandlerFactory = operationHandlerFactory ?? throw new ArgumentNullException(nameof(operationHandlerFactory));
        _accessToken = !string.IsNullOrWhiteSpace(accessToken)
            ? accessToken
            : throw new ArgumentException("The GitHub access token is required.", nameof(accessToken));
        _productHeader = !string.IsNullOrWhiteSpace(productHeader)
            ? productHeader
            : throw new ArgumentException("The GitHub product header is required.", nameof(productHeader));
        _apiVersion = !string.IsNullOrWhiteSpace(apiVersion)
            ? apiVersion
            : throw new ArgumentException("The GitHub API version is required.", nameof(apiVersion));
    }

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

    public async Task<GitHubFileMutationResult> StageFileChangesAsync(
        GitHubFileMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return GitHubFileMutationResult.Failure(GitHubApiFailureCondition.CancellationBeforeDispatch);
        }

        if (request.Target is null || !request.Target.TryValidate(out _))
        {
            return GitHubFileMutationResult.Failure(GitHubApiFailureCondition.ValidationFailure);
        }

        if (!TryValidateFileChanges(request.Changes, out GitHubApiFailureCondition validationFailure))
        {
            return GitHubFileMutationResult.Failure(validationFailure);
        }

        bool mutationDispatched = false;
        using HttpClient httpClient = CreateOperationHttpClient();
        try
        {
            using HttpResponseMessage refResponse = await SendAsync(
                httpClient,
                HttpMethod.Get,
                OperationUri(request.Target, $"git/refs/{request.Target.RefName}"),
                body: null,
                cancellationToken).ConfigureAwait(false);
            if (!refResponse.IsSuccessStatusCode)
            {
                return GitHubFileMutationResult.Failure(MapRawResponse(
                    refResponse,
                    mutationDispatched: false,
                    GitHubApiFailureCondition.RefHeadConflict), RetryAfter(refResponse));
            }

            using JsonDocument? refDocument = await ReadJsonDocumentAsync(refResponse, cancellationToken).ConfigureAwait(false);
            if (!TryReadReference(refDocument, request.Target, out string? currentHeadSha))
            {
                return GitHubFileMutationResult.Failure(GitHubApiFailureCondition.MalformedResponse);
            }

            if (!string.Equals(currentHeadSha, request.Target.ExpectedHeadSha, StringComparison.OrdinalIgnoreCase))
            {
                return GitHubFileMutationResult.Failure(GitHubApiFailureCondition.RefHeadConflict);
            }

            using HttpResponseMessage commitResponse = await SendAsync(
                httpClient,
                HttpMethod.Get,
                OperationUri(request.Target, $"git/commits/{request.Target.ExpectedHeadSha}"),
                body: null,
                cancellationToken).ConfigureAwait(false);
            if (!commitResponse.IsSuccessStatusCode)
            {
                return GitHubFileMutationResult.Failure(MapRawResponse(
                    commitResponse,
                    mutationDispatched: false,
                    GitHubApiFailureCondition.RefHeadConflict), RetryAfter(commitResponse));
            }

            using JsonDocument? commitDocument = await ReadJsonDocumentAsync(commitResponse, cancellationToken).ConfigureAwait(false);
            if (!TryReadCommitIdentity(
                commitDocument,
                request.Target.ExpectedHeadSha,
                expectedTreeSha: null,
                expectedParentSha: null,
                out _,
                out string? baseTreeSha))
            {
                return GitHubFileMutationResult.Failure(GitHubApiFailureCondition.MalformedResponse);
            }

            using HttpResponseMessage baseTreeResponse = await SendAsync(
                httpClient,
                HttpMethod.Get,
                OperationUri(request.Target, $"git/trees/{baseTreeSha}?recursive=1"),
                body: null,
                cancellationToken).ConfigureAwait(false);
            if (!baseTreeResponse.IsSuccessStatusCode)
            {
                return GitHubFileMutationResult.Failure(MapRawResponse(
                    baseTreeResponse,
                    mutationDispatched: false,
                    GitHubApiFailureCondition.ContentPolicyViolation), RetryAfter(baseTreeResponse));
            }

            using JsonDocument? baseTreeDocument = await ReadJsonDocumentAsync(baseTreeResponse, cancellationToken).ConfigureAwait(false);
            if (!TryReadBaseTree(
                baseTreeDocument,
                baseTreeSha!,
                out HashSet<string>? existingPaths,
                out HashSet<string>? existingBlobPaths)
                || !ValidatePathExistence(request.Changes, existingPaths, existingBlobPaths))
            {
                return GitHubFileMutationResult.Failure(GitHubApiFailureCondition.ContentPolicyViolation);
            }

            List<Dictionary<string, object?>> treeEntries = new(request.Changes.Count);

            foreach (ProviderResolvedFileChange change in request.Changes)
            {
                string? blobSha = null;
                if (change.Kind is ProviderFileChangeKind.Add or ProviderFileChangeKind.Change)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return GitHubFileMutationResult.Failure(
                            mutationDispatched
                                ? GitHubApiFailureCondition.TimeoutDuringMutation
                                : GitHubApiFailureCondition.CancellationBeforeDispatch);
                    }

                    Dictionary<string, object?> blobBody = new(StringComparer.Ordinal)
                    {
                        ["content"] = Convert.ToBase64String(change.Content.Span),
                        ["encoding"] = "base64",
                    };
                    mutationDispatched = true;
                    using HttpResponseMessage blobResponse = await SendAsync(
                        httpClient,
                        HttpMethod.Post,
                        OperationUri(request.Target, "git/blobs"),
                        blobBody,
                        cancellationToken).ConfigureAwait(false);
                    if (!blobResponse.IsSuccessStatusCode)
                    {
                        return GitHubFileMutationResult.Failure(MapRawResponse(
                            blobResponse,
                            mutationDispatched: true,
                            GitHubApiFailureCondition.ContentPolicyViolation), RetryAfter(blobResponse));
                    }

                    using JsonDocument? blobDocument = await ReadJsonDocumentAsync(blobResponse, cancellationToken).ConfigureAwait(false);
                    blobSha = TryReadString(blobDocument, "sha");
                    if (!ProviderGitOperationResolvedTarget.IsGitObjectId(blobSha))
                    {
                        return GitHubFileMutationResult.Failure(GitHubApiFailureCondition.AmbiguousMutationResponse);
                    }
                }

                treeEntries.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = change.Path,
                    ["mode"] = "100644",
                    ["type"] = "blob",
                    ["sha"] = blobSha,
                });
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return GitHubFileMutationResult.Failure(
                    mutationDispatched
                        ? GitHubApiFailureCondition.TimeoutDuringMutation
                        : GitHubApiFailureCondition.CancellationBeforeDispatch);
            }

            Dictionary<string, object?> treeBody = new(StringComparer.Ordinal)
            {
                ["base_tree"] = baseTreeSha,
                ["tree"] = treeEntries,
            };
            mutationDispatched = true;
            using HttpResponseMessage treeResponse = await SendAsync(
                httpClient,
                HttpMethod.Post,
                OperationUri(request.Target, "git/trees"),
                treeBody,
                cancellationToken).ConfigureAwait(false);
            if (!treeResponse.IsSuccessStatusCode)
            {
                return GitHubFileMutationResult.Failure(MapRawResponse(
                    treeResponse,
                    mutationDispatched: true,
                    GitHubApiFailureCondition.ContentPolicyViolation), RetryAfter(treeResponse));
            }

            using JsonDocument? treeDocument = await ReadJsonDocumentAsync(treeResponse, cancellationToken).ConfigureAwait(false);
            string? treeSha = TryReadString(treeDocument, "sha");
            return ProviderGitOperationResolvedTarget.IsGitObjectId(treeSha)
                ? GitHubFileMutationResult.Success(treeSha!)
                : GitHubFileMutationResult.Failure(GitHubApiFailureCondition.AmbiguousMutationResponse);
        }
        catch (Exception exception)
        {
            return MapFileMutationFailure(exception, mutationDispatched);
        }
    }

    public async Task<GitHubCommitResult> CommitAsync(
        GitHubCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return GitHubCommitResult.Failure(GitHubApiFailureCondition.CancellationBeforeDispatch);
        }

        if (request.Target is null
            || !request.Target.TryValidate(out _)
            || !ProviderGitOperationResolvedTarget.IsGitObjectId(request.TreeSha)
            || string.IsNullOrWhiteSpace(request.CommitMessage)
            || request.CommitMessage.Length > 65536
            || request.CommitMessage.Contains('\0', StringComparison.Ordinal))
        {
            return GitHubCommitResult.Failure(GitHubApiFailureCondition.ValidationFailure);
        }

        bool mutationDispatched = false;
        bool refUpdateDispatched = false;
        string? createdCommitSha = null;
        using HttpClient httpClient = CreateOperationHttpClient();
        try
        {
            using HttpResponseMessage refResponse = await SendAsync(
                httpClient,
                HttpMethod.Get,
                OperationUri(request.Target, $"git/refs/{request.Target.RefName}"),
                body: null,
                cancellationToken).ConfigureAwait(false);
            if (!refResponse.IsSuccessStatusCode)
            {
                return GitHubCommitResult.Failure(MapRawResponse(
                    refResponse,
                    mutationDispatched: false,
                    GitHubApiFailureCondition.RefHeadConflict), RetryAfter(refResponse));
            }

            using JsonDocument? refDocument = await ReadJsonDocumentAsync(refResponse, cancellationToken).ConfigureAwait(false);
            if (!TryReadReference(refDocument, request.Target, out string? currentHeadSha))
            {
                return GitHubCommitResult.Failure(GitHubApiFailureCondition.MalformedResponse);
            }

            if (!string.Equals(currentHeadSha, request.Target.ExpectedHeadSha, StringComparison.OrdinalIgnoreCase))
            {
                return GitHubCommitResult.Failure(GitHubApiFailureCondition.RefHeadConflict);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return GitHubCommitResult.Failure(GitHubApiFailureCondition.CancellationBeforeDispatch);
            }

            Dictionary<string, object?> commitBody = new(StringComparer.Ordinal)
            {
                ["message"] = request.CommitMessage,
                ["tree"] = request.TreeSha,
                ["parents"] = new[] { request.Target.ExpectedHeadSha },
            };
            mutationDispatched = true;
            using HttpResponseMessage commitResponse = await SendAsync(
                httpClient,
                HttpMethod.Post,
                OperationUri(request.Target, "git/commits"),
                commitBody,
                cancellationToken).ConfigureAwait(false);
            if (!commitResponse.IsSuccessStatusCode)
            {
                return GitHubCommitResult.Failure(MapRawResponse(
                    commitResponse,
                    mutationDispatched: true,
                    GitHubApiFailureCondition.ValidationFailure), RetryAfter(commitResponse));
            }

            using JsonDocument? commitDocument = await ReadJsonDocumentAsync(commitResponse, cancellationToken).ConfigureAwait(false);
            createdCommitSha = TryReadString(commitDocument, "sha");
            if (!ProviderGitOperationResolvedTarget.IsGitObjectId(createdCommitSha)
                || !TryReadCommitIdentity(
                    commitDocument,
                    expectedCommitSha: null,
                    request.TreeSha,
                    request.Target.ExpectedHeadSha,
                    out _,
                    out _))
            {
                return GitHubCommitResult.Failure(
                    GitHubApiFailureCondition.AmbiguousMutationResponse,
                    createdCommitSha: ProviderGitOperationResolvedTarget.IsGitObjectId(createdCommitSha) ? createdCommitSha : null);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return GitHubCommitResult.Failure(GitHubApiFailureCondition.TimeoutDuringMutation);
            }

            Dictionary<string, object?> updateBody = new(StringComparer.Ordinal)
            {
                ["sha"] = createdCommitSha,
                ["force"] = false,
            };
            refUpdateDispatched = true;
            using HttpResponseMessage updateResponse = await SendAsync(
                httpClient,
                HttpMethod.Patch,
                OperationUri(request.Target, $"git/refs/{request.Target.RefName}"),
                updateBody,
                cancellationToken).ConfigureAwait(false);
            if (!updateResponse.IsSuccessStatusCode)
            {
                GitHubApiFailureCondition condition = MapRawResponse(
                    updateResponse,
                    mutationDispatched: true,
                    GitHubApiFailureCondition.BranchProtectionConflict);
                return GitHubCommitResult.Failure(condition, RetryAfter(updateResponse), createdCommitSha);
            }

            using JsonDocument? updateDocument = await ReadJsonDocumentAsync(updateResponse, cancellationToken).ConfigureAwait(false);
            return TryReadReference(updateDocument, request.Target, out string? updatedSha)
                && string.Equals(updatedSha, createdCommitSha, StringComparison.OrdinalIgnoreCase)
                    ? GitHubCommitResult.Success(createdCommitSha!)
                    : GitHubCommitResult.Failure(GitHubApiFailureCondition.AmbiguousMutationResponse, createdCommitSha: createdCommitSha);
        }
        catch (Exception exception)
        {
            GitHubCommitResult mapped = MapCommitFailure(exception, mutationDispatched, refUpdateDispatched);
            return mapped.IsSuccess || createdCommitSha is null
                ? mapped
                : mapped with { CreatedCommitSha = createdCommitSha };
        }
    }

    public async Task<GitHubOperationStatusResult> GetOperationStatusAsync(
        GitHubOperationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return GitHubOperationStatusResult.Failure(GitHubApiFailureCondition.CancellationBeforeDispatch);
        }

        if (request.Target is null
            || !request.Target.TryValidate(out _)
            || !ProviderGitOperationResolvedTarget.IsGitObjectId(request.IntendedCommitSha))
        {
            return GitHubOperationStatusResult.Failure(GitHubApiFailureCondition.ValidationFailure);
        }

        using HttpClient httpClient = CreateOperationHttpClient();
        try
        {
            using HttpResponseMessage response = await SendAsync(
                httpClient,
                HttpMethod.Get,
                OperationUri(request.Target, $"git/refs/{request.Target.RefName}"),
                body: null,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return GitHubOperationStatusResult.Failure(MapRawResponse(
                    response,
                    mutationDispatched: false,
                    GitHubApiFailureCondition.ValidationFailure), RetryAfter(response));
            }

            using JsonDocument? document = await ReadJsonDocumentAsync(response, cancellationToken).ConfigureAwait(false);
            if (!TryReadReference(document, request.Target, out string? observedSha))
            {
                return GitHubOperationStatusResult.Failure(GitHubApiFailureCondition.MalformedResponse);
            }

            ProviderOperationStatusKind status = string.Equals(
                observedSha,
                request.IntendedCommitSha,
                StringComparison.OrdinalIgnoreCase)
                    ? ProviderOperationStatusKind.Confirmed
                    : string.Equals(
                        observedSha,
                        request.Target.ExpectedHeadSha,
                        StringComparison.OrdinalIgnoreCase)
                            ? ProviderOperationStatusKind.NotApplied
                            : ProviderOperationStatusKind.Conflicting;
            return GitHubOperationStatusResult.Observed(status, observedSha!);
        }
        catch (Exception exception)
        {
            return MapStatusObservationFailure(exception);
        }
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

    private HttpClient CreateOperationHttpClient()
    {
        HttpClient client = new(_operationHandlerFactory(), disposeHandler: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(_productHeader);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", _apiVersion);
        return client;
    }

    private static Uri OperationUri(ProviderGitOperationResolvedTarget target, string suffix)
    {
        string owner = Uri.EscapeDataString(target.Owner);
        string repository = Uri.EscapeDataString(target.RepositoryName);
        return new Uri($"https://api.github.com/repos/{owner}/{repository}/{suffix}", UriKind.Absolute);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        Uri uri,
        object? body,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using HttpRequestMessage request = new(method, uri);
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<JsonDocument?> ReadJsonDocumentAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        const int maximumResponseBytes = 1024 * 1024;
        if (response.Content.Headers.ContentLength is > maximumResponseBytes)
        {
            return null;
        }

        try
        {
            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using MemoryStream buffer = new();
            byte[] chunk = new byte[8192];
            int total = 0;
            while (true)
            {
                int read = await stream.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total += read;
                if (total > maximumResponseBytes)
                {
                    return null;
                }

                buffer.Write(chunk, 0, read);
            }

            buffer.Position = 0;
            return await JsonDocument.ParseAsync(buffer, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryReadReference(
        JsonDocument? document,
        ProviderGitOperationResolvedTarget target,
        out string? sha)
    {
        sha = null;
        if (document is null
            || !document.RootElement.TryGetProperty("ref", out JsonElement refElement)
            || refElement.ValueKind != JsonValueKind.String
            || !string.Equals(refElement.GetString(), $"refs/{target.RefName}", StringComparison.Ordinal)
            || !document.RootElement.TryGetProperty("object", out JsonElement objectElement)
            || !objectElement.TryGetProperty("sha", out JsonElement shaElement)
            || shaElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        sha = shaElement.GetString();
        return ProviderGitOperationResolvedTarget.IsGitObjectId(sha);
    }

    private static bool TryReadCommitIdentity(
        JsonDocument? document,
        string? expectedCommitSha,
        string? expectedTreeSha,
        string? expectedParentSha,
        out string? commitSha,
        out string? treeSha)
    {
        commitSha = TryReadString(document, "sha");
        treeSha = null;
        if (!ProviderGitOperationResolvedTarget.IsGitObjectId(commitSha)
            || (expectedCommitSha is not null
                && !string.Equals(commitSha, expectedCommitSha, StringComparison.OrdinalIgnoreCase))
            || document is null
            || !document.RootElement.TryGetProperty("tree", out JsonElement treeElement)
            || !treeElement.TryGetProperty("sha", out JsonElement treeShaElement)
            || treeShaElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        treeSha = treeShaElement.GetString();
        if (!ProviderGitOperationResolvedTarget.IsGitObjectId(treeSha)
            || (expectedTreeSha is not null
                && !string.Equals(treeSha, expectedTreeSha, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (expectedParentSha is null)
        {
            return true;
        }

        return document.RootElement.TryGetProperty("parents", out JsonElement parents)
            && parents.ValueKind == JsonValueKind.Array
            && parents.GetArrayLength() == 1
            && parents[0].TryGetProperty("sha", out JsonElement parentSha)
            && parentSha.ValueKind == JsonValueKind.String
            && string.Equals(parentSha.GetString(), expectedParentSha, StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryReadString(JsonDocument? document, string propertyName)
        => document is not null
            && document.RootElement.TryGetProperty(propertyName, out JsonElement element)
            && element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : null;

    private static bool TryReadBaseTree(
        JsonDocument? document,
        string expectedTreeSha,
        out HashSet<string> paths,
        out HashSet<string> blobPaths)
    {
        paths = new HashSet<string>(StringComparer.Ordinal);
        blobPaths = new HashSet<string>(StringComparer.Ordinal);
        if (document is null
            || !string.Equals(TryReadString(document, "sha"), expectedTreeSha, StringComparison.OrdinalIgnoreCase)
            || (document.RootElement.TryGetProperty("truncated", out JsonElement truncated)
                && (truncated.ValueKind is not (JsonValueKind.True or JsonValueKind.False) || truncated.GetBoolean()))
            || !document.RootElement.TryGetProperty("tree", out JsonElement tree)
            || tree.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (JsonElement entry in tree.EnumerateArray())
        {
            if (entry.TryGetProperty("path", out JsonElement pathElement)
                && pathElement.ValueKind == JsonValueKind.String)
            {
                string? path = pathElement.GetString();
                if (path is null || !paths.Add(path))
                {
                    return false;
                }

                if (entry.TryGetProperty("type", out JsonElement typeElement)
                    && typeElement.ValueKind == JsonValueKind.String
                    && string.Equals(typeElement.GetString(), "blob", StringComparison.Ordinal))
                {
                    blobPaths.Add(path);
                }
            }
        }

        return true;
    }

    private static bool ValidatePathExistence(
        IReadOnlyList<ProviderResolvedFileChange> changes,
        HashSet<string> existingPaths,
        HashSet<string> existingBlobPaths)
        => changes.All(change => change.Kind switch
        {
            ProviderFileChangeKind.Add => !existingPaths.Contains(change.Path),
            ProviderFileChangeKind.Change or ProviderFileChangeKind.Remove => existingBlobPaths.Contains(change.Path),
            _ => false,
        });

    private static GitHubApiFailureCondition MapRawResponse(
        HttpResponseMessage response,
        bool mutationDispatched,
        GitHubApiFailureCondition validationCondition)
        => response.StatusCode switch
        {
            HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity => validationCondition,
            HttpStatusCode.Unauthorized => GitHubApiFailureCondition.AuthenticationRequired,
            HttpStatusCode.Forbidden => IsSecondaryRateLimit(response)
                ? GitHubApiFailureCondition.SecondaryRateLimit
                : HasExhaustedPrimaryQuota(response)
                    ? GitHubApiFailureCondition.PrimaryRateLimit
                    : GitHubApiFailureCondition.PermissionInsufficient,
            HttpStatusCode.NotFound => GitHubApiFailureCondition.NotFoundOrHidden,
            HttpStatusCode.TooManyRequests => IsSecondaryRateLimit(response)
                ? GitHubApiFailureCondition.SecondaryRateLimit
                : GitHubApiFailureCondition.PrimaryRateLimit,
            >= HttpStatusCode.InternalServerError => mutationDispatched
                ? GitHubApiFailureCondition.TimeoutDuringMutation
                : GitHubApiFailureCondition.ServerUnavailable,
            _ => mutationDispatched
                ? GitHubApiFailureCondition.UnexpectedTransportFailure
                : GitHubApiFailureCondition.TimeoutDuringObservation,
        };

    private static GitHubFileMutationResult MapFileMutationFailure(Exception exception, bool mutationDispatched)
    {
        if (exception is RateLimitExceededException primaryRateLimit)
        {
            return GitHubFileMutationResult.Failure(
                GitHubApiFailureCondition.PrimaryRateLimit,
                BoundedRetryAfter(primaryRateLimit.GetRetryAfterTimeSpan()));
        }

        if (exception is SecondaryRateLimitExceededException secondaryRateLimit)
        {
            return GitHubFileMutationResult.Failure(
                GitHubApiFailureCondition.SecondaryRateLimit,
                RetryAfter(secondaryRateLimit.HttpResponse));
        }

        if (exception is ApiException apiException)
        {
            GitHubApiFailureCondition condition = MapApiCondition(
                apiException,
                mutationDispatched,
                conflictCondition: GitHubApiFailureCondition.RefHeadConflict);
            return GitHubFileMutationResult.Failure(condition, RateLimitRetryAfter(apiException));
        }

        return GitHubFileMutationResult.Failure(MapTransportCondition(exception, mutationDispatched));
    }

    private static GitHubCommitResult MapCommitFailure(
        Exception exception,
        bool mutationDispatched,
        bool refUpdateDispatched)
    {
        if (exception is RateLimitExceededException primaryRateLimit)
        {
            return GitHubCommitResult.Failure(
                GitHubApiFailureCondition.PrimaryRateLimit,
                BoundedRetryAfter(primaryRateLimit.GetRetryAfterTimeSpan()));
        }

        if (exception is SecondaryRateLimitExceededException secondaryRateLimit)
        {
            return GitHubCommitResult.Failure(
                GitHubApiFailureCondition.SecondaryRateLimit,
                RetryAfter(secondaryRateLimit.HttpResponse));
        }

        if (exception is ApiException apiException)
        {
            GitHubApiFailureCondition condition = MapApiCondition(
                apiException,
                mutationDispatched,
                refUpdateDispatched
                    ? GitHubApiFailureCondition.BranchProtectionConflict
                    : GitHubApiFailureCondition.RefHeadConflict);
            return GitHubCommitResult.Failure(condition, RateLimitRetryAfter(apiException));
        }

        return GitHubCommitResult.Failure(MapTransportCondition(exception, mutationDispatched));
    }

    private static GitHubOperationStatusResult MapStatusObservationFailure(Exception exception)
    {
        if (exception is RateLimitExceededException primaryRateLimit)
        {
            return GitHubOperationStatusResult.Failure(
                GitHubApiFailureCondition.PrimaryRateLimit,
                BoundedRetryAfter(primaryRateLimit.GetRetryAfterTimeSpan()));
        }

        if (exception is SecondaryRateLimitExceededException secondaryRateLimit)
        {
            return GitHubOperationStatusResult.Failure(
                GitHubApiFailureCondition.SecondaryRateLimit,
                RetryAfter(secondaryRateLimit.HttpResponse));
        }

        if (exception is ApiException apiException)
        {
            return GitHubOperationStatusResult.Failure(
                MapApiCondition(apiException, mutationDispatched: false, GitHubApiFailureCondition.RefHeadConflict),
                RateLimitRetryAfter(apiException));
        }

        return GitHubOperationStatusResult.Failure(MapTransportCondition(exception, mutationDispatched: false));
    }

    private static GitHubApiFailureCondition MapApiCondition(
        ApiException exception,
        bool mutationDispatched,
        GitHubApiFailureCondition conflictCondition)
        => exception.StatusCode switch
        {
            HttpStatusCode.BadRequest => GitHubApiFailureCondition.ValidationFailure,
            HttpStatusCode.Unauthorized => GitHubApiFailureCondition.AuthenticationRequired,
            HttpStatusCode.Forbidden => IsSecondaryRateLimit(exception.HttpResponse)
                ? GitHubApiFailureCondition.SecondaryRateLimit
                : exception.HttpResponse.Headers.TryGetValue("X-RateLimit-Remaining", out string? remaining)
                    && string.Equals(remaining, "0", StringComparison.Ordinal)
                        ? GitHubApiFailureCondition.PrimaryRateLimit
                        : GitHubApiFailureCondition.PermissionInsufficient,
            HttpStatusCode.NotFound => GitHubApiFailureCondition.NotFoundOrHidden,
            HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity => conflictCondition,
            HttpStatusCode.TooManyRequests => IsSecondaryRateLimit(exception.HttpResponse)
                ? GitHubApiFailureCondition.SecondaryRateLimit
                : GitHubApiFailureCondition.PrimaryRateLimit,
            >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices => mutationDispatched
                ? GitHubApiFailureCondition.AmbiguousMutationResponse
                : GitHubApiFailureCondition.MalformedResponse,
            >= HttpStatusCode.InternalServerError => mutationDispatched
                ? GitHubApiFailureCondition.TimeoutDuringMutation
                : GitHubApiFailureCondition.ServerUnavailable,
            _ => mutationDispatched
                ? GitHubApiFailureCondition.UnexpectedTransportFailure
                : GitHubApiFailureCondition.TimeoutDuringObservation,
        };

    private static GitHubApiFailureCondition MapTransportCondition(Exception exception, bool mutationDispatched)
        => exception switch
        {
            AuthorizationException => GitHubApiFailureCondition.AuthenticationRequired,
            ForbiddenException => GitHubApiFailureCondition.PermissionInsufficient,
            NotFoundException => GitHubApiFailureCondition.NotFoundOrHidden,
            ApiValidationException => mutationDispatched
                ? GitHubApiFailureCondition.RefHeadConflict
                : GitHubApiFailureCondition.ValidationFailure,
            OperationCanceledException => mutationDispatched
                ? GitHubApiFailureCondition.TimeoutDuringMutation
                : GitHubApiFailureCondition.TimeoutDuringObservation,
            HttpRequestException => mutationDispatched
                ? GitHubApiFailureCondition.UnexpectedTransportFailure
                : GitHubApiFailureCondition.TimeoutDuringObservation,
            _ when IsMalformedJsonException(exception) => mutationDispatched
                ? GitHubApiFailureCondition.AmbiguousMutationResponse
                : GitHubApiFailureCondition.MalformedResponse,
            _ => mutationDispatched
                ? GitHubApiFailureCondition.AmbiguousMutationResponse
                : GitHubApiFailureCondition.UnexpectedTransportFailure,
        };

    private static TimeSpan? RateLimitRetryAfter(ApiException exception)
        => exception.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
            ? RetryAfter(exception.HttpResponse)
            : null;

    private static bool TryReferenceSha(Reference reference, out string? sha)
    {
        sha = reference.Object?.Sha;
        return ProviderGitOperationResolvedTarget.IsGitObjectId(sha);
    }

    private static bool TryValidateFileChanges(
        IReadOnlyList<ProviderResolvedFileChange>? changes,
        out GitHubApiFailureCondition failureCondition)
    {
        failureCondition = GitHubApiFailureCondition.ContentPolicyViolation;
        if (changes is null || changes.Count == 0)
        {
            return false;
        }

        HashSet<string> paths = new(StringComparer.Ordinal);
        for (int index = 0; index < changes.Count; index++)
        {
            ProviderResolvedFileChange change = changes[index];
            if (change is null || !IsSafeGitPath(change.Path) || !paths.Add(change.Path))
            {
                failureCondition = GitHubApiFailureCondition.PathPolicyViolation;
                return false;
            }

            if (change.Sequence != index
                || !Enum.IsDefined(change.Kind)
                || change.ContentType != ProviderFileContentType.RegularFile
                || (change.Kind == ProviderFileChangeKind.Remove && !change.Content.IsEmpty))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSafeGitPath(string? path)
        => !string.IsNullOrWhiteSpace(path)
            && path.Length <= 4096
            && path[0] != '/'
            && !path.EndsWith("/", StringComparison.Ordinal)
            && !path.Contains("\\", StringComparison.Ordinal)
            && !path.Any(char.IsControl)
            && !path.Split('/').Any(static segment => segment is "" or "." or "..");

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

    private static bool IsSecondaryRateLimit(HttpResponseMessage response)
        => response.Headers.TryGetValues("X-RateLimit-Remaining", out IEnumerable<string>? remaining)
            && remaining.Any(static value => long.TryParse(
                value,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out long quota) && quota > 0)
            || (response.Headers.Contains("Retry-After") && !HasExhaustedPrimaryQuota(response));

    private static bool HasExhaustedPrimaryQuota(HttpResponseMessage response)
        => response.Headers.TryGetValues("X-RateLimit-Remaining", out IEnumerable<string>? remaining)
            && remaining.Contains("0", StringComparer.Ordinal);

    private static TimeSpan? RetryAfter(IResponse response)
    {
        if (response.Headers.TryGetValue("Retry-After", out string? value)
            && int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int seconds))
        {
            return BoundedRetryAfter(TimeSpan.FromSeconds(seconds));
        }

        return null;
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return BoundedRetryAfter(delta);
        }

        if (response.Headers.TryGetValues("Retry-After", out IEnumerable<string>? values)
            && values.FirstOrDefault() is { } value)
        {
            if (int.TryParse(
                value,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out int seconds))
            {
                return BoundedRetryAfter(TimeSpan.FromSeconds(seconds));
            }

            if (DateTimeOffset.TryParse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out DateTimeOffset retryAt))
            {
                return BoundedRetryAfter(retryAt - DateTimeOffset.UtcNow);
            }
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
        => exception is System.Runtime.Serialization.SerializationException
            || exception.GetType().Name.Contains("Json", StringComparison.Ordinal);
}
