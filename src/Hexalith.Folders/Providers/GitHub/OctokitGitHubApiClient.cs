using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hexalith.Folders.Providers.Abstractions;
using Octokit;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed class OctokitGitHubApiClient : IGitHubApiClient
{
    private const int MaximumChangeCount = 100;
    private const int MaximumFileBytes = 1024 * 1024;
    private const long MaximumAggregateContentBytes = 10L * 1024 * 1024;
    private const int MaximumTreeRequests = 64;
    private const int MaximumTreeEntries = 256;
    private const int MaximumTreeDepth = 32;
    private const int MaximumTreeResponseBytes = 7 * 1024 * 1024;
    private static readonly TimeSpan MaximumTreeElapsed = TimeSpan.FromSeconds(5);
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
        if (!TryCreateOperationHttpClient(out HttpClient? operationHttpClient))
        {
            return GitHubFileMutationResult.Failure(GitHubApiFailureCondition.ServerUnavailable);
        }

        using HttpClient httpClient = operationHttpClient;
        try
        {
            using HttpResponseMessage refResponse = await SendAsync(
                httpClient,
                HttpMethod.Get,
                RefUri(request.Target),
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
                GitObjectUri(request.Target, "commits", request.Target.ExpectedHeadSha),
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
                expectedMessage: null,
                out _,
                out string? baseTreeSha))
            {
                return GitHubFileMutationResult.Failure(GitHubApiFailureCondition.MalformedResponse);
            }

            using HttpResponseMessage baseTreeResponse = await SendAsync(
                httpClient,
                HttpMethod.Get,
                TreeUri(request.Target, baseTreeSha!, recursive: true),
                body: null,
                cancellationToken).ConfigureAwait(false);
            if (!baseTreeResponse.IsSuccessStatusCode)
            {
                return GitHubFileMutationResult.Failure(MapRawResponse(
                    baseTreeResponse,
                    mutationDispatched: false,
                    GitHubApiFailureCondition.ContentPolicyViolation), RetryAfter(baseTreeResponse));
            }

            using JsonDocument? baseTreeDocument = await ReadJsonDocumentAsync(baseTreeResponse, cancellationToken, MaximumTreeResponseBytes).ConfigureAwait(false);
            Dictionary<string, GitHubTreeEntry>? baseEntries = await ReadTouchedTreeEntriesAsync(
                httpClient,
                request.Target,
                baseTreeSha!,
                baseTreeDocument,
                request.Changes.Select(static change => change.Path).ToArray(),
                cancellationToken).ConfigureAwait(false);
            if (baseEntries is null || !ValidatePathExistence(request.Changes, baseEntries))
            {
                return GitHubFileMutationResult.Failure(GitHubApiFailureCondition.ContentPolicyViolation);
            }

            if (!await request.ValidateReservationAsync(cancellationToken).ConfigureAwait(false))
            {
                return GitHubFileMutationResult.Failure(GitHubApiFailureCondition.ReservationInvalidated);
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
            if (!ProviderGitOperationResolvedTarget.IsGitObjectId(treeSha))
            {
                return GitHubFileMutationResult.Failure(GitHubApiFailureCondition.AmbiguousMutationResponse);
            }

            using HttpResponseMessage resultingTreeResponse = await SendAsync(
                httpClient,
                HttpMethod.Get,
                TreeUri(request.Target, treeSha!, recursive: true),
                body: null,
                cancellationToken).ConfigureAwait(false);
            if (!resultingTreeResponse.IsSuccessStatusCode)
            {
                return GitHubFileMutationResult.Failure(GitHubApiFailureCondition.AmbiguousMutationResponse);
            }

            using JsonDocument? resultingTreeDocument = await ReadJsonDocumentAsync(
                resultingTreeResponse,
                cancellationToken,
                MaximumTreeResponseBytes).ConfigureAwait(false);
            Dictionary<string, GitHubTreeEntry>? resultingEntries = await ReadTouchedTreeEntriesAsync(
                httpClient,
                request.Target,
                treeSha!,
                resultingTreeDocument,
                request.Changes.Select(static change => change.Path).ToArray(),
                cancellationToken).ConfigureAwait(false);
            return resultingEntries is not null && ValidateResultingTree(request.Changes, treeEntries, resultingEntries)
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
        if (!TryCreateOperationHttpClient(out HttpClient? operationHttpClient))
        {
            return GitHubCommitResult.Failure(GitHubApiFailureCondition.ServerUnavailable);
        }

        using HttpClient httpClient = operationHttpClient;
        try
        {
            using HttpResponseMessage refResponse = await SendAsync(
                httpClient,
                HttpMethod.Get,
                RefUri(request.Target),
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

            if (!await request.ValidateReservationAsync(cancellationToken).ConfigureAwait(false))
            {
                return GitHubCommitResult.Failure(GitHubApiFailureCondition.ReservationInvalidated);
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
            if (!ProviderGitOperationResolvedTarget.IsGitObjectId(createdCommitSha))
            {
                return GitHubCommitResult.Failure(
                    GitHubApiFailureCondition.AmbiguousMutationResponse,
                    createdCommitSha: ProviderGitOperationResolvedTarget.IsGitObjectId(createdCommitSha) ? createdCommitSha : null);
            }

            using HttpResponseMessage commitReadBackResponse = await SendAsync(
                httpClient,
                HttpMethod.Get,
                GitObjectUri(request.Target, "commits", createdCommitSha!),
                body: null,
                cancellationToken).ConfigureAwait(false);
            if (!commitReadBackResponse.IsSuccessStatusCode)
            {
                return GitHubCommitResult.Failure(GitHubApiFailureCondition.AmbiguousMutationResponse, createdCommitSha: createdCommitSha);
            }

            using JsonDocument? commitReadBackDocument = await ReadJsonDocumentAsync(commitReadBackResponse, cancellationToken).ConfigureAwait(false);
            if (!TryReadCommitIdentity(
                commitReadBackDocument,
                createdCommitSha,
                request.TreeSha,
                request.Target.ExpectedHeadSha,
                request.CommitMessage,
                out _,
                out _))
            {
                return GitHubCommitResult.Failure(GitHubApiFailureCondition.AmbiguousMutationResponse, createdCommitSha: createdCommitSha);
            }

            if (!await request.RecordCreatedCommitAsync(createdCommitSha!).ConfigureAwait(false))
            {
                return GitHubCommitResult.Failure(GitHubApiFailureCondition.OutcomeRecordingFailed, createdCommitSha: createdCommitSha);
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
                RefUri(request.Target),
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

            using HttpResponseMessage confirmationResponse = await SendAsync(
                httpClient,
                HttpMethod.Get,
                RefUri(request.Target),
                body: null,
                cancellationToken).ConfigureAwait(false);
            if (!confirmationResponse.IsSuccessStatusCode)
            {
                return GitHubCommitResult.Failure(GitHubApiFailureCondition.TimeoutDuringMutation, createdCommitSha: createdCommitSha);
            }

            using JsonDocument? confirmationDocument = await ReadJsonDocumentAsync(confirmationResponse, cancellationToken).ConfigureAwait(false);
            return TryReadReference(confirmationDocument, request.Target, out string? updatedSha)
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

        if (!TryCreateOperationHttpClient(out HttpClient? operationHttpClient))
        {
            return GitHubOperationStatusResult.Failure(GitHubApiFailureCondition.ServerUnavailable);
        }

        using HttpClient httpClient = operationHttpClient;
        try
        {
            using HttpResponseMessage response = await SendAsync(
                httpClient,
                HttpMethod.Get,
                RefUri(request.Target),
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
            if (!TryReadReferenceEvidence(document, out string? observedFullRef, out string? observedObjectType, out string? observedSha))
            {
                return GitHubOperationStatusResult.Failure(GitHubApiFailureCondition.MalformedResponse);
            }

            if (!string.Equals(observedFullRef, request.Target.FullRef, StringComparison.Ordinal)
                || !string.Equals(observedObjectType, "commit", StringComparison.Ordinal)
                || !ProviderGitOperationResolvedTarget.IsGitObjectId(observedSha))
            {
                return GitHubOperationStatusResult.Conflicting(observedSha, observedFullRef, observedObjectType);
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
            return GitHubOperationStatusResult.Observed(status, observedSha!, observedFullRef!, observedObjectType!);
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

    private bool TryCreateOperationHttpClient([NotNullWhen(true)] out HttpClient? client)
    {
        try
        {
            client = CreateOperationHttpClient();
            return true;
        }
        catch (Exception)
        {
            client = null;
            return false;
        }
    }

    private static Uri OperationUri(ProviderGitOperationResolvedTarget target, string suffix)
    {
        string owner = Uri.EscapeDataString(target.Owner);
        string repository = Uri.EscapeDataString(target.RepositoryName);
        return new Uri($"https://api.github.com/repos/{owner}/{repository}/{suffix}", UriKind.Absolute);
    }

    private static Uri RefUri(ProviderGitOperationResolvedTarget target)
        => OperationUri(target, $"git/refs/{Uri.EscapeDataString(target.RefName)}");

    private static Uri GitObjectUri(ProviderGitOperationResolvedTarget target, string objectKind, string objectId)
        => OperationUri(target, $"git/{objectKind}/{Uri.EscapeDataString(objectId)}");

    private static Uri TreeUri(ProviderGitOperationResolvedTarget target, string treeSha, bool recursive)
        => OperationUri(
            target,
            recursive
                ? $"git/trees/{Uri.EscapeDataString(treeSha)}?recursive=1"
                : $"git/trees/{Uri.EscapeDataString(treeSha)}");

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
        CancellationToken cancellationToken,
        int maximumResponseBytes = 1024 * 1024)
    {
        if (response.Content.Headers.ContentLength is { } contentLength && contentLength > maximumResponseBytes)
        {
            throw new GitHubResponseLimitExceededException();
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
                    throw new GitHubResponseLimitExceededException();
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
            || !objectElement.TryGetProperty("type", out JsonElement typeElement)
            || typeElement.ValueKind != JsonValueKind.String
            || !string.Equals(typeElement.GetString(), "commit", StringComparison.Ordinal)
            || !objectElement.TryGetProperty("sha", out JsonElement shaElement)
            || shaElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        sha = shaElement.GetString();
        return ProviderGitOperationResolvedTarget.IsGitObjectId(sha);
    }

    private static bool TryReadReferenceEvidence(
        JsonDocument? document,
        out string? fullRef,
        out string? objectType,
        out string? sha)
    {
        fullRef = null;
        objectType = null;
        sha = null;
        if (document is null
            || !document.RootElement.TryGetProperty("ref", out JsonElement refElement)
            || refElement.ValueKind != JsonValueKind.String
            || !document.RootElement.TryGetProperty("object", out JsonElement objectElement)
            || !objectElement.TryGetProperty("type", out JsonElement typeElement)
            || typeElement.ValueKind != JsonValueKind.String
            || !objectElement.TryGetProperty("sha", out JsonElement shaElement)
            || shaElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        fullRef = refElement.GetString();
        objectType = typeElement.GetString();
        sha = shaElement.GetString();
        return !string.IsNullOrWhiteSpace(fullRef)
            && !string.IsNullOrWhiteSpace(objectType)
            && !string.IsNullOrWhiteSpace(sha);
    }

    private static bool TryReadCommitIdentity(
        JsonDocument? document,
        string? expectedCommitSha,
        string? expectedTreeSha,
        string? expectedParentSha,
        string? expectedMessage,
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
            return expectedMessage is null || HasExpectedMessage(document, expectedMessage);
        }

        return (expectedMessage is null || HasExpectedMessage(document, expectedMessage))
            && document.RootElement.TryGetProperty("parents", out JsonElement parents)
            && parents.ValueKind == JsonValueKind.Array
            && parents.GetArrayLength() == 1
            && parents[0].TryGetProperty("sha", out JsonElement parentSha)
            && parentSha.ValueKind == JsonValueKind.String
            && string.Equals(parentSha.GetString(), expectedParentSha, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasExpectedMessage(JsonDocument document, string expectedMessage)
        => document.RootElement.TryGetProperty("message", out JsonElement message)
            && message.ValueKind == JsonValueKind.String
            && string.Equals(message.GetString(), expectedMessage, StringComparison.Ordinal);

    private static string? TryReadString(JsonDocument? document, string propertyName)
        => document is not null
            && document.RootElement.TryGetProperty(propertyName, out JsonElement element)
            && element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : null;

    private static async Task<Dictionary<string, GitHubTreeEntry>?> ReadTouchedTreeEntriesAsync(
        HttpClient httpClient,
        ProviderGitOperationResolvedTarget target,
        string expectedTreeSha,
        JsonDocument? recursiveDocument,
        IReadOnlyList<string> touchedPaths,
        CancellationToken cancellationToken)
    {
        if (!TryReadTree(recursiveDocument, expectedTreeSha, MaximumTreeEntries, out Dictionary<string, GitHubTreeEntry>? entries, out bool truncated))
        {
            return null;
        }

        if (!truncated)
        {
            return entries;
        }

        Dictionary<string, GitHubTreeEntry> result = new(StringComparer.Ordinal);
        Queue<(string Prefix, string TreeSha, int Depth)> pending = new();
        pending.Enqueue((string.Empty, expectedTreeSha, 0));
        int requests = 0;
        Stopwatch elapsed = Stopwatch.StartNew();
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(MaximumTreeElapsed);
        try
        {
            while (pending.Count > 0)
            {
                if (++requests > MaximumTreeRequests || elapsed.Elapsed > MaximumTreeElapsed)
                {
                    return null;
                }

                (string prefix, string treeSha, int depth) = pending.Dequeue();
                if (depth > MaximumTreeDepth)
                {
                    return null;
                }

                using HttpResponseMessage response = await SendAsync(
                    httpClient,
                    HttpMethod.Get,
                    TreeUri(target, treeSha, recursive: false),
                    body: null,
                    timeout.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                using JsonDocument? document = await ReadJsonDocumentAsync(
                    response,
                    timeout.Token,
                    MaximumTreeResponseBytes).ConfigureAwait(false);
                if (!TryReadTree(document, treeSha, MaximumTreeEntries, out Dictionary<string, GitHubTreeEntry>? localEntries, out bool localTruncated)
                    || localTruncated)
                {
                    return null;
                }

                foreach (GitHubTreeEntry localEntry in localEntries.Values)
                {
                    string fullPath = prefix.Length == 0 ? localEntry.Path : $"{prefix}/{localEntry.Path}";
                    GitHubTreeEntry fullEntry = localEntry with { Path = fullPath };
                    if (!result.TryAdd(fullPath, fullEntry))
                    {
                        return null;
                    }

                    if (string.Equals(localEntry.Type, "tree", StringComparison.Ordinal)
                        && touchedPaths.Any(path => path.StartsWith(fullPath + "/", StringComparison.Ordinal)))
                    {
                        pending.Enqueue((fullPath, localEntry.Sha, depth + 1));
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        return result;
    }

    private static bool TryReadTree(
        JsonDocument? document,
        string expectedTreeSha,
        int maximumEntries,
        out Dictionary<string, GitHubTreeEntry> entries,
        out bool truncated)
    {
        entries = new Dictionary<string, GitHubTreeEntry>(StringComparer.Ordinal);
        truncated = false;
        if (document is null
            || !string.Equals(TryReadString(document, "sha"), expectedTreeSha, StringComparison.OrdinalIgnoreCase)
            || !document.RootElement.TryGetProperty("truncated", out JsonElement truncatedElement)
            || truncatedElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !document.RootElement.TryGetProperty("tree", out JsonElement tree)
            || tree.ValueKind != JsonValueKind.Array
            || tree.GetArrayLength() > maximumEntries)
        {
            return false;
        }

        truncated = truncatedElement.GetBoolean();
        foreach (JsonElement entry in tree.EnumerateArray())
        {
            if (!TryReadTreeEntry(entry, out GitHubTreeEntry? parsed)
                || parsed is null
                || !entries.TryAdd(parsed.Path, parsed))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadTreeEntry(JsonElement entry, out GitHubTreeEntry? parsed)
    {
        parsed = null;
        if (!entry.TryGetProperty("path", out JsonElement pathElement)
            || pathElement.ValueKind != JsonValueKind.String
            || !entry.TryGetProperty("mode", out JsonElement modeElement)
            || modeElement.ValueKind != JsonValueKind.String
            || !entry.TryGetProperty("type", out JsonElement typeElement)
            || typeElement.ValueKind != JsonValueKind.String
            || !entry.TryGetProperty("sha", out JsonElement shaElement)
            || shaElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? path = pathElement.GetString();
        string? mode = modeElement.GetString();
        string? type = typeElement.GetString();
        string? sha = shaElement.GetString();
        if (!IsSafeGitPath(path)
            || mode is not { Length: > 0 and <= 8 }
            || type is not ("blob" or "tree" or "commit")
            || !ProviderGitOperationResolvedTarget.IsGitObjectId(sha))
        {
            return false;
        }

        parsed = new GitHubTreeEntry(path!, mode, type, sha!);
        return true;
    }

    private static bool ValidatePathExistence(
        IReadOnlyList<ProviderResolvedFileChange> changes,
        IReadOnlyDictionary<string, GitHubTreeEntry> existingEntries)
        => changes.All(change => change.Kind switch
        {
            ProviderFileChangeKind.Add => !existingEntries.ContainsKey(change.Path),
            ProviderFileChangeKind.Change or ProviderFileChangeKind.Remove => existingEntries.TryGetValue(change.Path, out GitHubTreeEntry? entry)
                && string.Equals(entry.Type, "blob", StringComparison.Ordinal)
                && string.Equals(entry.Mode, "100644", StringComparison.Ordinal),
            _ => false,
        });

    private static bool ValidateResultingTree(
        IReadOnlyList<ProviderResolvedFileChange> changes,
        IReadOnlyList<Dictionary<string, object?>> intendedEntries,
        IReadOnlyDictionary<string, GitHubTreeEntry> resultingEntries)
    {
        for (int index = 0; index < changes.Count; index++)
        {
            ProviderResolvedFileChange change = changes[index];
            if (change.Kind == ProviderFileChangeKind.Remove)
            {
                if (resultingEntries.ContainsKey(change.Path))
                {
                    return false;
                }

                continue;
            }

            string? expectedBlobSha = intendedEntries[index]["sha"] as string;
            if (!ProviderGitOperationResolvedTarget.IsGitObjectId(expectedBlobSha)
                || !resultingEntries.TryGetValue(change.Path, out GitHubTreeEntry? entry)
                || !string.Equals(entry.Type, "blob", StringComparison.Ordinal)
                || !string.Equals(entry.Mode, "100644", StringComparison.Ordinal)
                || !string.Equals(entry.Sha, expectedBlobSha, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static GitHubApiFailureCondition MapRawResponse(
        HttpResponseMessage response,
        bool mutationDispatched,
        GitHubApiFailureCondition validationCondition)
        => response.StatusCode switch
        {
            HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity => validationCondition,
            HttpStatusCode.Unauthorized => GitHubApiFailureCondition.AuthenticationRequired,
            HttpStatusCode.Forbidden => IsSecondaryRateLimit(response)
                ? mutationDispatched
                    ? GitHubApiFailureCondition.TimeoutDuringMutation
                    : GitHubApiFailureCondition.SecondaryRateLimit
                : HasExhaustedPrimaryQuota(response)
                    ? mutationDispatched
                        ? GitHubApiFailureCondition.TimeoutDuringMutation
                        : GitHubApiFailureCondition.PrimaryRateLimit
                    : GitHubApiFailureCondition.PermissionInsufficient,
            HttpStatusCode.NotFound => GitHubApiFailureCondition.NotFoundOrHidden,
            HttpStatusCode.TooManyRequests => IsSecondaryRateLimit(response)
                ? mutationDispatched
                    ? GitHubApiFailureCondition.TimeoutDuringMutation
                    : GitHubApiFailureCondition.SecondaryRateLimit
                : mutationDispatched
                    ? GitHubApiFailureCondition.TimeoutDuringMutation
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
        if (exception is GitHubResponseLimitExceededException)
        {
            return GitHubFileMutationResult.Failure(GitHubApiFailureCondition.ResponseLimitExceeded);
        }

        if (exception is RateLimitExceededException primaryRateLimit)
        {
            return GitHubFileMutationResult.Failure(
                mutationDispatched ? GitHubApiFailureCondition.TimeoutDuringMutation : GitHubApiFailureCondition.PrimaryRateLimit,
                BoundedRetryAfter(primaryRateLimit.GetRetryAfterTimeSpan()));
        }

        if (exception is SecondaryRateLimitExceededException secondaryRateLimit)
        {
            return GitHubFileMutationResult.Failure(
                mutationDispatched ? GitHubApiFailureCondition.TimeoutDuringMutation : GitHubApiFailureCondition.SecondaryRateLimit,
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
        if (exception is GitHubResponseLimitExceededException)
        {
            return GitHubCommitResult.Failure(GitHubApiFailureCondition.ResponseLimitExceeded);
        }

        if (exception is RateLimitExceededException primaryRateLimit)
        {
            return GitHubCommitResult.Failure(
                mutationDispatched ? GitHubApiFailureCondition.TimeoutDuringMutation : GitHubApiFailureCondition.PrimaryRateLimit,
                BoundedRetryAfter(primaryRateLimit.GetRetryAfterTimeSpan()));
        }

        if (exception is SecondaryRateLimitExceededException secondaryRateLimit)
        {
            return GitHubCommitResult.Failure(
                mutationDispatched ? GitHubApiFailureCondition.TimeoutDuringMutation : GitHubApiFailureCondition.SecondaryRateLimit,
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
        if (exception is GitHubResponseLimitExceededException)
        {
            return GitHubOperationStatusResult.Failure(GitHubApiFailureCondition.ResponseLimitExceeded);
        }

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
                ? mutationDispatched
                    ? GitHubApiFailureCondition.TimeoutDuringMutation
                    : GitHubApiFailureCondition.SecondaryRateLimit
                : exception.HttpResponse.Headers.TryGetValue("X-RateLimit-Remaining", out string? remaining)
                    && string.Equals(remaining, "0", StringComparison.Ordinal)
                        ? mutationDispatched
                            ? GitHubApiFailureCondition.TimeoutDuringMutation
                            : GitHubApiFailureCondition.PrimaryRateLimit
                        : GitHubApiFailureCondition.PermissionInsufficient,
            HttpStatusCode.NotFound => GitHubApiFailureCondition.NotFoundOrHidden,
            HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity => conflictCondition,
            HttpStatusCode.TooManyRequests => IsSecondaryRateLimit(exception.HttpResponse)
                ? mutationDispatched
                    ? GitHubApiFailureCondition.TimeoutDuringMutation
                    : GitHubApiFailureCondition.SecondaryRateLimit
                : mutationDispatched
                    ? GitHubApiFailureCondition.TimeoutDuringMutation
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
        if (changes is null || changes.Count is < 1 or > MaximumChangeCount)
        {
            return false;
        }

        HashSet<string> paths = new(StringComparer.Ordinal);
        long aggregateBytes = 0;
        for (int index = 0; index < changes.Count; index++)
        {
            ProviderResolvedFileChange change = changes[index];
            if (change is null
                || !IsSafeGitPath(change.Path)
                || !paths.Add(change.Path)
                || paths.Any(path => !string.Equals(path, change.Path, StringComparison.Ordinal)
                    && (path.StartsWith(change.Path + "/", StringComparison.Ordinal)
                        || change.Path.StartsWith(path + "/", StringComparison.Ordinal))))
            {
                failureCondition = GitHubApiFailureCondition.PathPolicyViolation;
                return false;
            }

            if (change.Sequence != index
                || !Enum.IsDefined(change.Kind)
                || change.ContentType != ProviderFileContentType.RegularFile
                || change.Content.Length > MaximumFileBytes
                || (change.Kind == ProviderFileChangeKind.Remove && !change.Content.IsEmpty))
            {
                return false;
            }

            if (aggregateBytes > MaximumAggregateContentBytes - change.Content.Length)
            {
                return false;
            }

            aggregateBytes += change.Content.Length;
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
