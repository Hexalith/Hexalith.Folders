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

    public async Task<GitHubFileChangeSetResult> StageFileChangesAsync(
        GitHubFileChangeSetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested
            || !TryGitSha(request.ExpectedHeadSha, out _)
            || request.Changes is null
            || request.Changes.Count == 0)
        {
            return GitHubFileChangeSetResult.Failure(
                cancellationToken.IsCancellationRequested
                    ? GitHubApiFailureCondition.CancellationBeforeDispatch
                    : GitHubApiFailureCondition.ValidationFailure);
        }

        bool mutationDispatched = false;
        try
        {
            Reference head = await _client.Git.Reference.Get(
                request.Target.Owner,
                request.Target.RepositoryName,
                $"heads/{request.Target.SelectedRef}").ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return GitHubFileChangeSetResult.Failure(GitHubApiFailureCondition.CancellationBeforeDispatch);
            }

            if (!TryCommitReference(head, out string observedHead))
            {
                return GitHubFileChangeSetResult.Failure(GitHubApiFailureCondition.MalformedResponse);
            }

            if (!string.Equals(observedHead, request.ExpectedHeadSha, StringComparison.Ordinal))
            {
                return GitHubFileChangeSetResult.Failure(GitHubApiFailureCondition.RefMoved);
            }

            Commit baseCommit = await _client.Git.Commit.Get(
                request.Target.Owner,
                request.Target.RepositoryName,
                request.ExpectedHeadSha).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return GitHubFileChangeSetResult.Failure(GitHubApiFailureCondition.CancellationBeforeDispatch);
            }

            if (!TryGitSha(baseCommit.Sha, out string baseCommitSha)
                || !string.Equals(baseCommitSha, request.ExpectedHeadSha, StringComparison.Ordinal)
                || !TryGitSha(baseCommit.Tree?.Sha, out string baseTreeSha))
            {
                return GitHubFileChangeSetResult.Failure(GitHubApiFailureCondition.MalformedResponse);
            }

            TreeResponse baseTree = await _client.Git.Tree.GetRecursive(
                request.Target.Owner,
                request.Target.RepositoryName,
                baseTreeSha).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return GitHubFileChangeSetResult.Failure(GitHubApiFailureCondition.CancellationBeforeDispatch);
            }

            if (baseTree.Truncated
                || !TryGitSha(baseTree.Sha, out string observedBaseTreeSha)
                || !string.Equals(observedBaseTreeSha, baseTreeSha, StringComparison.Ordinal)
                || baseTree.Tree is null)
            {
                return GitHubFileChangeSetResult.Failure(GitHubApiFailureCondition.MalformedResponse);
            }

            Dictionary<string, TreeItem> baseEntries = new(StringComparer.Ordinal);
            foreach (TreeItem entry in baseTree.Tree)
            {
                if (string.IsNullOrWhiteSpace(entry.Path) || !baseEntries.TryAdd(entry.Path, entry))
                {
                    return GitHubFileChangeSetResult.Failure(GitHubApiFailureCondition.MalformedResponse);
                }
            }

            NewTree tree = new() { BaseTree = baseTreeSha };
            HashSet<string> requestedPaths = new(StringComparer.Ordinal);
            foreach (ProviderGitResolvedFileChange change in request.Changes)
            {
                if (change is null
                    || string.IsNullOrWhiteSpace(change.Path)
                    || !requestedPaths.Add(change.Path))
                {
                    return GitHubFileChangeSetResult.Failure(GitHubApiFailureCondition.ValidationFailure);
                }

                baseEntries.TryGetValue(change.Path, out TreeItem? existing);
                switch (change.Kind)
                {
                    case ProviderFileChangeKind.Add when existing is null:
                    case ProviderFileChangeKind.Change when TryExistingBlob(existing, out _):
                    {
                        if (change.Content is null)
                        {
                            return GitHubFileChangeSetResult.Failure(GitHubApiFailureCondition.ValidationFailure);
                        }

                        if (cancellationToken.IsCancellationRequested)
                        {
                            return GitHubFileChangeSetResult.Failure(
                                mutationDispatched
                                    ? GitHubApiFailureCondition.AmbiguousMutationResponse
                                    : GitHubApiFailureCondition.CancellationBeforeDispatch);
                        }

                        mutationDispatched = true;
                        BlobReference blob = await _client.Git.Blob.Create(
                            request.Target.Owner,
                            request.Target.RepositoryName,
                            new NewBlob
                            {
                                Content = Convert.ToBase64String(change.Content),
                                Encoding = EncodingType.Base64,
                            }).ConfigureAwait(false);
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return GitHubFileChangeSetResult.Failure(GitHubApiFailureCondition.AmbiguousMutationResponse);
                        }

                        if (!TryGitSha(blob.Sha, out string blobSha))
                        {
                            return GitHubFileChangeSetResult.Failure(GitHubApiFailureCondition.AmbiguousMutationResponse);
                        }

                        tree.Tree.Add(new NewTreeItem
                        {
                            Path = change.Path,
                            Mode = existing?.Mode ?? "100644",
                            Type = TreeType.Blob,
                            Sha = blobSha,
                        });
                        break;
                    }

                    case ProviderFileChangeKind.Remove when TryExistingBlob(existing, out string existingMode):
                    {
                        if (change.Content is not null)
                        {
                            return GitHubFileChangeSetResult.Failure(GitHubApiFailureCondition.ValidationFailure);
                        }

                        tree.Tree.Add(new NewTreeItem
                        {
                            Path = change.Path,
                            Mode = existingMode,
                            Type = TreeType.Blob,
                            Sha = null!,
                        });
                        break;
                    }

                    case ProviderFileChangeKind.Add:
                    case ProviderFileChangeKind.Change:
                    case ProviderFileChangeKind.Remove:
                    default:
                        return GitHubFileChangeSetResult.Failure(GitHubApiFailureCondition.ValidationFailure);
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return GitHubFileChangeSetResult.Failure(
                    mutationDispatched
                        ? GitHubApiFailureCondition.AmbiguousMutationResponse
                        : GitHubApiFailureCondition.CancellationBeforeDispatch);
            }

            mutationDispatched = true;
            Dictionary<string, object?> treeBody = new(StringComparer.Ordinal)
            {
                ["base_tree"] = tree.BaseTree,
                ["tree"] = tree.Tree.Select(static item => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = item.Path,
                    ["mode"] = item.Mode,
                    ["type"] = "blob",
                    ["sha"] = item.Sha,
                }).ToArray(),
            };
            IApiResponse<TreeResponse> stagedResponse = await _client.Connection.Post<TreeResponse>(
                ApiUrls.Tree(request.Target.Owner, request.Target.RepositoryName),
                treeBody,
                "application/vnd.github+json",
                "application/json",
                parameters: null!,
                cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return GitHubFileChangeSetResult.Failure(GitHubApiFailureCondition.AmbiguousMutationResponse);
            }

            TreeResponse staged = stagedResponse.Body;
            return TryGitSha(staged.Sha, out string stagedTreeSha)
                ? GitHubFileChangeSetResult.Success(stagedTreeSha)
                : GitHubFileChangeSetResult.Failure(GitHubApiFailureCondition.AmbiguousMutationResponse);
        }
        catch (Exception exception)
        {
            (GitHubApiFailureCondition Condition, TimeSpan? RetryAfter) failure =
                MapGitDataFailure(exception, mutationDispatched, refUpdateDispatched: false, observationOnly: false);
            return GitHubFileChangeSetResult.Failure(failure.Condition, failure.RetryAfter);
        }
    }

    public async Task<GitHubCommitResult> CommitAsync(
        GitHubCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested
            || !TryGitSha(request.ExpectedHeadSha, out _)
            || !TryGitSha(request.StagedTreeSha, out _))
        {
            return GitHubCommitResult.Failure(
                cancellationToken.IsCancellationRequested
                    ? GitHubApiFailureCondition.CancellationBeforeDispatch
                    : GitHubApiFailureCondition.ValidationFailure);
        }

        bool mutationDispatched = false;
        bool refUpdateDispatched = false;
        string? createdCommitSha = null;
        try
        {
            Reference head = await _client.Git.Reference.Get(
                request.Target.Owner,
                request.Target.RepositoryName,
                $"heads/{request.Target.SelectedRef}").ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return GitHubCommitResult.Failure(GitHubApiFailureCondition.CancellationBeforeDispatch);
            }

            if (!TryCommitReference(head, out string observedHead))
            {
                return GitHubCommitResult.Failure(GitHubApiFailureCondition.MalformedResponse);
            }

            if (!string.Equals(observedHead, request.ExpectedHeadSha, StringComparison.Ordinal))
            {
                return GitHubCommitResult.Failure(GitHubApiFailureCondition.RefMoved);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return GitHubCommitResult.Failure(GitHubApiFailureCondition.CancellationBeforeDispatch);
            }

            mutationDispatched = true;
            Commit created = await _client.Git.Commit.Create(
                request.Target.Owner,
                request.Target.RepositoryName,
                new NewCommit(request.CommitMessage, request.StagedTreeSha, request.ExpectedHeadSha)).ConfigureAwait(false);
            if (TryGitSha(created.Sha, out string candidateCommitSha))
            {
                createdCommitSha = candidateCommitSha;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return GitHubCommitResult.Failure(
                    GitHubApiFailureCondition.AmbiguousMutationResponse,
                    commitSha: createdCommitSha);
            }

            if (createdCommitSha is null
                || !TryGitSha(created.Tree?.Sha, out string createdTreeSha)
                || !string.Equals(createdTreeSha, request.StagedTreeSha, StringComparison.Ordinal)
                || created.Parents is null
                || created.Parents.Count != 1
                || !TryGitSha(created.Parents[0].Sha, out string createdParentSha)
                || !string.Equals(createdParentSha, request.ExpectedHeadSha, StringComparison.Ordinal))
            {
                return GitHubCommitResult.Failure(
                    GitHubApiFailureCondition.AmbiguousMutationResponse,
                    commitSha: createdCommitSha);
            }

            refUpdateDispatched = true;
            Reference updated = await _client.Git.Reference.Update(
                request.Target.Owner,
                request.Target.RepositoryName,
                $"heads/{request.Target.SelectedRef}",
                new ReferenceUpdate(createdCommitSha, force: false)).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return GitHubCommitResult.Failure(
                    GitHubApiFailureCondition.AmbiguousMutationResponse,
                    commitSha: createdCommitSha);
            }

            return TryCommitReference(updated, out string updatedHead)
                && string.Equals(updatedHead, createdCommitSha, StringComparison.Ordinal)
                    ? GitHubCommitResult.Success(createdCommitSha)
                    : GitHubCommitResult.Failure(
                        GitHubApiFailureCondition.AmbiguousMutationResponse,
                        commitSha: createdCommitSha);
        }
        catch (Exception exception)
        {
            (GitHubApiFailureCondition Condition, TimeSpan? RetryAfter) failure =
                MapGitDataFailure(exception, mutationDispatched, refUpdateDispatched, observationOnly: false);
            return GitHubCommitResult.Failure(failure.Condition, failure.RetryAfter, createdCommitSha);
        }
    }

    public async Task<GitHubMutationStatusResult> GetMutationStatusAsync(
        GitHubMutationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested
            || !TryGitSha(request.ExpectedHeadSha, out _)
            || !TryGitSha(request.ExpectedCommitSha, out _)
            || string.Equals(request.ExpectedHeadSha, request.ExpectedCommitSha, StringComparison.Ordinal))
        {
            return GitHubMutationStatusResult.Unavailable(
                cancellationToken.IsCancellationRequested
                    ? GitHubApiFailureCondition.CancellationBeforeDispatch
                    : GitHubApiFailureCondition.ValidationFailure);
        }

        try
        {
            Reference head = await _client.Git.Reference.Get(
                request.Target.Owner,
                request.Target.RepositoryName,
                $"heads/{request.Target.SelectedRef}").ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return GitHubMutationStatusResult.Unavailable(GitHubApiFailureCondition.TimeoutDuringObservation);
            }

            if (!TryCommitReference(head, out string observedHead))
            {
                return GitHubMutationStatusResult.Unavailable(GitHubApiFailureCondition.MalformedResponse);
            }

            if (string.Equals(observedHead, request.ExpectedCommitSha, StringComparison.Ordinal))
            {
                return GitHubMutationStatusResult.Available(GitHubMutationStatusDisposition.Confirmed);
            }

            return string.Equals(observedHead, request.ExpectedHeadSha, StringComparison.Ordinal)
                ? GitHubMutationStatusResult.Available(GitHubMutationStatusDisposition.NotApplied)
                : GitHubMutationStatusResult.Available(GitHubMutationStatusDisposition.Conflicting);
        }
        catch (Exception exception)
        {
            (GitHubApiFailureCondition Condition, TimeSpan? RetryAfter) failure =
                MapGitDataFailure(exception, mutationDispatched: false, refUpdateDispatched: false, observationOnly: true);
            return GitHubMutationStatusResult.Unavailable(failure.Condition, failure.RetryAfter);
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

    private static bool TryGitSha(string? value, out string sha)
    {
        sha = value ?? string.Empty;
        return value is { Length: 40 }
            && value.All(static character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f');
    }

    private static bool TryCommitReference(Reference reference, out string sha)
    {
        sha = string.Empty;
        return reference.Object is not null
            && string.Equals(reference.Object.Type.StringValue, "commit", StringComparison.Ordinal)
            && TryGitSha(reference.Object.Sha, out sha);
    }

    private static bool TryExistingBlob(TreeItem? entry, out string mode)
    {
        mode = entry?.Mode ?? string.Empty;
        return entry is not null
            && string.Equals(entry.Type.StringValue, "blob", StringComparison.Ordinal)
            && mode is "100644" or "100755" or "120000"
            && TryGitSha(entry.Sha, out _);
    }

    private static (GitHubApiFailureCondition Condition, TimeSpan? RetryAfter) MapGitDataFailure(
        Exception exception,
        bool mutationDispatched,
        bool refUpdateDispatched,
        bool observationOnly)
    {
        if (exception is SecondaryRateLimitExceededException secondaryRateLimit)
        {
            return (GitHubApiFailureCondition.SecondaryRateLimit, RetryAfter(secondaryRateLimit.HttpResponse));
        }

        if (exception is RateLimitExceededException primaryRateLimit)
        {
            return (GitHubApiFailureCondition.PrimaryRateLimit, BoundedRetryAfter(primaryRateLimit.GetRetryAfterTimeSpan()));
        }

        if (exception is AuthorizationException)
        {
            return (GitHubApiFailureCondition.AuthenticationRequired, null);
        }

        if (exception is ForbiddenException)
        {
            return (refUpdateDispatched
                ? GitHubApiFailureCondition.BranchProtectionConflict
                : GitHubApiFailureCondition.PermissionInsufficient, null);
        }

        if (exception is NotFoundException)
        {
            return (GitHubApiFailureCondition.NotFoundOrHidden, null);
        }

        if (exception is ApiValidationException)
        {
            return (refUpdateDispatched
                ? GitHubApiFailureCondition.RefUpdateConflict
                : GitHubApiFailureCondition.ValidationFailure, null);
        }

        if (exception is ApiException apiException)
        {
            return MapGitDataApiFailure(apiException, mutationDispatched, refUpdateDispatched);
        }

        if (exception is OperationCanceledException)
        {
            return (mutationDispatched
                ? GitHubApiFailureCondition.TimeoutDuringMutation
                : observationOnly
                    ? GitHubApiFailureCondition.TimeoutDuringObservation
                    : GitHubApiFailureCondition.CancellationBeforeDispatch, null);
        }

        if (exception is HttpRequestException)
        {
            return (mutationDispatched
                ? GitHubApiFailureCondition.UnexpectedTransportFailure
                : GitHubApiFailureCondition.TimeoutDuringObservation, null);
        }

        if (IsMalformedJsonException(exception))
        {
            return (mutationDispatched
                ? GitHubApiFailureCondition.AmbiguousMutationResponse
                : GitHubApiFailureCondition.MalformedResponse, null);
        }

        return (mutationDispatched
            ? GitHubApiFailureCondition.AmbiguousMutationResponse
            : GitHubApiFailureCondition.UnexpectedTransportFailure, null);
    }

    private static (GitHubApiFailureCondition Condition, TimeSpan? RetryAfter) MapGitDataApiFailure(
        ApiException exception,
        bool mutationDispatched,
        bool refUpdateDispatched)
        => exception.StatusCode switch
        {
            HttpStatusCode.BadRequest => (GitHubApiFailureCondition.ValidationFailure, null),
            HttpStatusCode.Unauthorized => (GitHubApiFailureCondition.AuthenticationRequired, null),
            HttpStatusCode.Forbidden when IsSecondaryRateLimit(exception.HttpResponse) =>
                (GitHubApiFailureCondition.SecondaryRateLimit, RetryAfter(exception.HttpResponse)),
            HttpStatusCode.Forbidden => (refUpdateDispatched
                ? GitHubApiFailureCondition.BranchProtectionConflict
                : GitHubApiFailureCondition.PermissionInsufficient, null),
            HttpStatusCode.NotFound => (GitHubApiFailureCondition.NotFoundOrHidden, null),
            HttpStatusCode.Conflict => (refUpdateDispatched
                ? GitHubApiFailureCondition.RefUpdateConflict
                : GitHubApiFailureCondition.RepositoryConflict, null),
            HttpStatusCode.UnprocessableEntity => (refUpdateDispatched
                ? GitHubApiFailureCondition.RefUpdateConflict
                : GitHubApiFailureCondition.ValidationFailure, null),
            HttpStatusCode.TooManyRequests => (IsSecondaryRateLimit(exception.HttpResponse)
                ? GitHubApiFailureCondition.SecondaryRateLimit
                : GitHubApiFailureCondition.PrimaryRateLimit, RetryAfter(exception.HttpResponse)),
            >= HttpStatusCode.InternalServerError => (mutationDispatched
                ? GitHubApiFailureCondition.AmbiguousMutationResponse
                : GitHubApiFailureCondition.ServerUnavailable, null),
            >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices => (mutationDispatched
                ? GitHubApiFailureCondition.AmbiguousMutationResponse
                : GitHubApiFailureCondition.MalformedResponse, null),
            _ => (mutationDispatched
                ? GitHubApiFailureCondition.UnexpectedTransportFailure
                : GitHubApiFailureCondition.ServerUnavailable, null),
        };

    // Octokit 14.0.0 surfaces an unparseable response body as Octokit.SerializationException
    // from its bundled SimpleJson deserializer, whose type name contains no "Json". Matching
    // the type explicitly keeps a malformed body classified as MalformedResponse instead of
    // falling through to UnexpectedTransportFailure, which would read as retryable noise.
    private static bool IsMalformedJsonException(Exception exception)
        => exception is System.Runtime.Serialization.SerializationException
            || exception.GetType().Name.Contains("Json", StringComparison.Ordinal);
}
