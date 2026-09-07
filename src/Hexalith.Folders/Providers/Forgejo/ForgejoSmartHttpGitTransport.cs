using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Hexalith.Folders.Providers.Abstractions;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;

namespace Hexalith.Folders.Providers.Forgejo;

/// <summary>
/// Executes the Forgejo SHA-1 smart-HTTPS profile in isolated bare repositories.
/// </summary>
internal sealed class ForgejoSmartHttpGitTransport(
    HttpClient httpClient,
    Uri authorizedBaseUri,
    ForgejoAuthorizationHeader authorizationHeader,
    CertificateCheckHandler? certificateCheck = null,
    Action? beforeReceivePackDispatch = null,
    ForgejoSmartHttpGitTransportTestHooks? testHooks = null)
{
    private const long MaximumAdvertisementBytes = 1024 * 1024;
    private const long MaximumTemporaryDiskBytes = 64L * 1024 * 1024;
    private const long MaximumTransferBytes = 32L * 1024 * 1024;
    private static readonly TimeSpan NativeOperationDeadline = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets whether the pinned LibGit2Sharp 0.32.0 / libgit2 1.8.6 native profile loaded.
    /// </summary>
    public static bool IsPinnedNativeProfileAvailable()
    {
        try
        {
            return GlobalSettings.Version.InformationalVersion.Contains(
                "0.32.0+libgit2-5853918",
                StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Constructs and verifies a staged tree without creating a commit or moving a ref.
    /// </summary>
    public async Task<ForgejoFileMutationResult> StageAsync(
        ForgejoFileMutationRequest request,
        CancellationToken cancellationToken,
        ForgejoNativeOperationPermit? nativePermit = null)
    {
        try
        {
            (ForgejoApiFailureCondition? Failure, TimeSpan? RetryAfter) advertisement =
                await ValidateAdvertisementAsync(
                    request.Target,
                    "git-upload-pack",
                    requireReportStatus: false,
                    cancellationToken).ConfigureAwait(false);
            if (advertisement.Failure is not null)
            {
                return ForgejoFileMutationResult.Failure(
                    advertisement.Failure.Value,
                    advertisement.RetryAfter);
            }

            Task<ForgejoFileMutationResult> nativeTask = StartNativeTask(
                () => StageCore(request, cancellationToken),
                nativePermit);
            return await nativeTask.WaitAsync(EffectiveNativeOperationDeadline(), cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.OperationTimedOut);
        }
        catch (OperationCanceledException)
        {
            return ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.CancellationBeforeDispatch);
        }
        catch (DllNotFoundException)
        {
            return ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.NativeRuntimeUnavailable);
        }
        catch (BadImageFormatException)
        {
            return ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.NativeRuntimeUnavailable);
        }
        catch (Exception)
        {
            return ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.ServerUnavailable);
        }
    }

    /// <summary>
    /// Reconstructs the staged tree, creates one local commit, and pushes one exact ref update.
    /// </summary>
    public async Task<ForgejoCommitResult> CommitAsync(
        ForgejoCommitRequest request,
        CancellationToken cancellationToken,
        ForgejoNativeOperationPermit? nativePermit = null)
    {
        ForgejoCommitDispatchState dispatchState = new();
        try
        {
            (ForgejoApiFailureCondition? Failure, TimeSpan? RetryAfter) uploadAdvertisement =
                await ValidateAdvertisementAsync(
                    request.Target,
                    "git-upload-pack",
                    requireReportStatus: false,
                    cancellationToken).ConfigureAwait(false);
            if (uploadAdvertisement.Failure is not null)
            {
                return ForgejoCommitResult.Failure(
                    uploadAdvertisement.Failure.Value,
                    uploadAdvertisement.RetryAfter);
            }

            (ForgejoApiFailureCondition? Failure, TimeSpan? RetryAfter) receiveAdvertisement =
                await ValidateAdvertisementAsync(
                    request.Target,
                    "git-receive-pack",
                    requireReportStatus: true,
                    cancellationToken).ConfigureAwait(false);
            if (receiveAdvertisement.Failure is not null)
            {
                return ForgejoCommitResult.Failure(
                    receiveAdvertisement.Failure.Value,
                    receiveAdvertisement.RetryAfter);
            }

            Task<ForgejoCommitResult> nativeTask = StartNativeTask(
                () => CommitCore(request, dispatchState, cancellationToken),
                nativePermit);
            return await nativeTask.WaitAsync(EffectiveNativeOperationDeadline(), cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return InterruptedCommitResult(
                dispatchState,
                ForgejoApiFailureCondition.OperationTimedOut);
        }
        catch (OperationCanceledException)
        {
            return InterruptedCommitResult(
                dispatchState,
                ForgejoApiFailureCondition.CancellationBeforeDispatch);
        }
        catch (DllNotFoundException)
        {
            return InterruptedCommitResult(
                dispatchState,
                ForgejoApiFailureCondition.NativeRuntimeUnavailable);
        }
        catch (BadImageFormatException)
        {
            return InterruptedCommitResult(
                dispatchState,
                ForgejoApiFailureCondition.NativeRuntimeUnavailable);
        }
        catch (Exception)
        {
            return InterruptedCommitResult(
                dispatchState,
                ForgejoApiFailureCondition.ServerUnavailable);
        }
    }

    private Task<TResult> StartNativeTask<TResult>(
        Func<TResult> operation,
        ForgejoNativeOperationPermit? nativePermit)
    {
        nativePermit?.TransferToNativeTask();
        Task<TResult> nativeTask;
        try
        {
            nativeTask = Task.Run(operation, CancellationToken.None);
        }
        catch (Exception)
        {
            nativePermit?.ReleaseByNativeTask();
            throw;
        }

        _ = nativeTask.ContinueWith(
            completed =>
            {
                _ = completed.Exception;
                nativePermit?.ReleaseByNativeTask();
                try
                {
                    testHooks?.NativeOperationCompleted?.Invoke();
                }
                catch (Exception)
                {
                    // Test observers cannot affect production cleanup or permit release.
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return nativeTask;
    }

    private ForgejoFileMutationResult StageCore(
        ForgejoFileMutationRequest request,
        CancellationToken cancellationToken)
    {
        string path = TemporaryRepositoryPath();
        bool ownsTemporaryDirectory = false;
        ForgejoNativeOperationState? state = null;
        ForgejoFileMutationResult result;
        try
        {
            CreatePrivateTemporaryDirectory(path);
            ownsTemporaryDirectory = true;
            testHooks?.BeforeRepositoryOpen?.Invoke(path);
            state = CreateOperationState(path, cancellationToken);
            result = ExecuteStageCore(path, request, state);
        }
        catch (UserCancelledException)
        {
            result = ForgejoFileMutationResult.Failure(
                state?.FailureCondition is { } stateFailure and not ForgejoApiFailureCondition.None
                    ? stateFailure
                    : cancellationToken.IsCancellationRequested
                        ? ForgejoApiFailureCondition.CancellationBeforeDispatch
                        : ForgejoApiFailureCondition.OperationTimedOut);
        }
        catch (NonFastForwardException)
        {
            result = ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.RefHeadConflict);
        }
        catch (ForgejoAmbientConfigurationException)
        {
            result = ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.AmbientConfigurationUnsupported);
        }
        catch (LibGit2SharpException exception)
        {
            result = ForgejoFileMutationResult.Failure(ClassifyNativeFailure(exception, mutationDispatched: false));
        }
        catch (DllNotFoundException)
        {
            result = ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.NativeRuntimeUnavailable);
        }
        catch (BadImageFormatException)
        {
            result = ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.NativeRuntimeUnavailable);
        }
        catch (Exception)
        {
            result = ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.ServerUnavailable);
        }

        return !ownsTemporaryDirectory || TryDeleteTemporaryDirectory(path)
            ? result
            : ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.TemporaryRepositoryCleanupFailed);
    }

    private ForgejoFileMutationResult ExecuteStageCore(
        string path,
        ForgejoFileMutationRequest request,
        ForgejoNativeOperationState state)
    {
        using Repository repository = CreateRepository(path);
        testHooks?.BeforeFetch?.Invoke(path, state);
        if (!state.Check(0))
        {
            return ForgejoFileMutationResult.Failure(state.FailureCondition);
        }

        FetchExpectedHead(repository, request.Target, state);
        if (state.FailureCondition != ForgejoApiFailureCondition.None)
        {
            return ForgejoFileMutationResult.Failure(state.FailureCondition);
        }

        if (!TryCreateTree(repository, request.Target, request.Changes, state, out Tree? tree, out ForgejoApiFailureCondition failure))
        {
            return ForgejoFileMutationResult.Failure(failure);
        }

        return ForgejoFileMutationResult.Success(tree!.Id.Sha);
    }

    private ForgejoCommitResult CommitCore(
        ForgejoCommitRequest request,
        ForgejoCommitDispatchState dispatchState,
        CancellationToken cancellationToken)
    {
        string path = TemporaryRepositoryPath();
        bool ownsTemporaryDirectory = false;
        ForgejoCommitResult result;
        try
        {
            CreatePrivateTemporaryDirectory(path);
            ownsTemporaryDirectory = true;
            testHooks?.BeforeRepositoryOpen?.Invoke(path);
            result = ExecuteCommitCore(path, request, dispatchState, cancellationToken);
        }
        catch (DllNotFoundException)
        {
            result = InterruptedCommitResult(dispatchState, ForgejoApiFailureCondition.NativeRuntimeUnavailable);
        }
        catch (BadImageFormatException)
        {
            result = InterruptedCommitResult(dispatchState, ForgejoApiFailureCondition.NativeRuntimeUnavailable);
        }
        catch (ForgejoAmbientConfigurationException)
        {
            result = InterruptedCommitResult(dispatchState, ForgejoApiFailureCondition.AmbientConfigurationUnsupported);
        }
        catch (Exception)
        {
            result = InterruptedCommitResult(dispatchState, ForgejoApiFailureCondition.ServerUnavailable);
        }

        if (!ownsTemporaryDirectory || TryDeleteTemporaryDirectory(path))
        {
            return result;
        }

        return dispatchState.MutationDispatched
            ? ForgejoCommitResult.Failure(
                ForgejoApiFailureCondition.AmbiguousMutationResponse,
                observedCommitSha: dispatchState.CreatedCommitSha,
                mutationDispatched: true)
            : ForgejoCommitResult.Failure(
                ForgejoApiFailureCondition.TemporaryRepositoryCleanupFailed,
                observedCommitSha: dispatchState.CreatedCommitSha);
    }

    private ForgejoCommitResult ExecuteCommitCore(
        string path,
        ForgejoCommitRequest request,
        ForgejoCommitDispatchState dispatchState,
        CancellationToken cancellationToken)
    {
        bool negotiationStale = false;
        bool negotiationRejected = false;
        bool policyRejected = false;
        bool otherRejected = false;
        string? createdCommitSha = null;
        ForgejoNativeOperationState state = CreateOperationState(path, cancellationToken);
        try
        {
            using Repository repository = CreateRepository(path);
            testHooks?.BeforeFetch?.Invoke(path, state);
            if (!state.Check(0))
            {
                return ForgejoCommitResult.Failure(state.FailureCondition);
            }

            FetchExpectedHead(repository, request.Target, state);
            if (state.FailureCondition != ForgejoApiFailureCondition.None)
            {
                return ForgejoCommitResult.Failure(state.FailureCondition);
            }

            if (!TryCreateTree(repository, request.Target, request.Changes, state, out Tree? tree, out ForgejoApiFailureCondition failure))
            {
                return ForgejoCommitResult.Failure(failure);
            }

            if (!string.Equals(tree!.Id.Sha, request.StagedTreeSha, StringComparison.OrdinalIgnoreCase))
            {
                return ForgejoCommitResult.Failure(ForgejoApiFailureCondition.StatusEvidenceConflicting);
            }

            Commit? parent = repository.Lookup<Commit>(request.Target.ExpectedHeadSha);
            if (parent is null)
            {
                return ForgejoCommitResult.Failure(ForgejoApiFailureCondition.RefHeadConflict);
            }

            Signature signature = new(
                "Hexalith Folders",
                "folders@localhost.invalid",
                DateTimeOffset.UtcNow);
            Commit commit = repository.ObjectDatabase.CreateCommit(
                signature,
                signature,
                request.CommitMessage,
                tree,
                [parent],
                prettifyMessage: false);
            createdCommitSha = commit.Id.Sha;
            dispatchState.RecordCreatedCommit(commit.Id.Sha);
            if (!state.Check(0))
            {
                return ForgejoCommitResult.Failure(state.FailureCondition);
            }

            if (!request.RecordCreatedCommitAsync(commit.Id.Sha).AsTask().GetAwaiter().GetResult())
            {
                return ForgejoCommitResult.Failure(
                    ForgejoApiFailureCondition.OutcomeRecordingFailed,
                    observedCommitSha: commit.Id.Sha);
            }

            PushOptions options = new()
            {
                CertificateCheck = certificateCheck,
                CustomHeaders = [AuthorizationValue()],
                OnNegotiationCompletedBeforePush = updates =>
                {
                    PushUpdate[] materialized = updates.ToArray();
                    bool exactRef = materialized.Length == 1
                        && string.Equals(materialized[0].DestinationRefName, request.Target.FullRef, StringComparison.Ordinal);
                    negotiationStale = exactRef
                        && !string.Equals(
                            materialized[0].SourceObjectId?.Sha,
                            request.Target.ExpectedHeadSha,
                            StringComparison.OrdinalIgnoreCase);
                    bool exactUpdate = exactRef
                        && !negotiationStale
                        && string.Equals(
                            materialized[0].DestinationObjectId?.Sha,
                            commit.Id.Sha,
                            StringComparison.OrdinalIgnoreCase);
                    negotiationRejected = !exactUpdate;
                    if (!exactUpdate || !state.Check(0))
                    {
                        return false;
                    }

                    if (!dispatchState.TryClaimMutationDispatch())
                    {
                        return false;
                    }

                    beforeReceivePackDispatch?.Invoke();
                    testHooks?.ReceivePackDispatched?.Invoke();
                    return true;
                },
                OnPackBuilderProgress = (_, _, _) => state.Check(0),
                OnPushTransferProgress = (_, _, bytes) => state.Check(bytes),
                OnPushStatusError = error =>
                {
                    bool isPolicy = IsPolicyRejection(error.Message);
                    policyRejected |= isPolicy;
                    otherRejected |= !isPolicy;
                },
            };

            Remote remote = repository.Network.Remotes["origin"]
                ?? throw new InvalidOperationException();
            cancellationToken.ThrowIfCancellationRequested();
            repository.Network.Push(remote, commit.Id.Sha, request.Target.FullRef, options);
            if (state.FailureCondition != ForgejoApiFailureCondition.None)
            {
                return ForgejoCommitResult.Failure(
                    ForgejoApiFailureCondition.AmbiguousMutationResponse,
                    observedCommitSha: commit.Id.Sha,
                    mutationDispatched: true);
            }

            if (policyRejected)
            {
                return ForgejoCommitResult.Failure(
                    ForgejoApiFailureCondition.RemotePolicyRejected,
                    observedCommitSha: commit.Id.Sha,
                    mutationDispatched: true);
            }

            if (otherRejected)
            {
                return ForgejoCommitResult.Failure(
                    ForgejoApiFailureCondition.RemoteRejected,
                    observedCommitSha: commit.Id.Sha,
                    mutationDispatched: true);
            }

            return ForgejoCommitResult.Success(commit.Id.Sha);
        }
        catch (NonFastForwardException)
        {
            return ForgejoCommitResult.Failure(
                ForgejoApiFailureCondition.RefHeadConflict,
                observedCommitSha: createdCommitSha,
                mutationDispatched: dispatchState.MutationDispatched);
        }
        catch (UserCancelledException)
        {
            return ForgejoCommitResult.Failure(
                dispatchState.MutationDispatched
                    ? ForgejoApiFailureCondition.AmbiguousMutationResponse
                    : negotiationStale
                        ? ForgejoApiFailureCondition.RefHeadConflict
                        : negotiationRejected
                            ? ForgejoApiFailureCondition.RemoteRejected
                            : state.FailureCondition != ForgejoApiFailureCondition.None
                                ? state.FailureCondition
                                : cancellationToken.IsCancellationRequested
                                    ? ForgejoApiFailureCondition.CancellationBeforeDispatch
                                    : ForgejoApiFailureCondition.OperationTimedOut,
                observedCommitSha: createdCommitSha,
                mutationDispatched: dispatchState.MutationDispatched);
        }
        catch (LibGit2SharpException exception)
        {
            ForgejoApiFailureCondition condition = dispatchState.MutationDispatched
                ? ForgejoApiFailureCondition.AmbiguousMutationResponse
                : negotiationStale
                ? ForgejoApiFailureCondition.RefHeadConflict
                : policyRejected
                    ? ForgejoApiFailureCondition.RemotePolicyRejected
                    : negotiationRejected || otherRejected
                        ? ForgejoApiFailureCondition.RemoteRejected
                        : ClassifyNativeFailure(exception, dispatchState.MutationDispatched);
            return ForgejoCommitResult.Failure(
                condition,
                observedCommitSha: createdCommitSha,
                mutationDispatched: dispatchState.MutationDispatched);
        }
        catch (IOException)
        {
            return ForgejoCommitResult.Failure(
                dispatchState.MutationDispatched
                    ? ForgejoApiFailureCondition.AmbiguousMutationResponse
                    : ForgejoApiFailureCondition.ServerUnavailable,
                observedCommitSha: createdCommitSha,
                mutationDispatched: dispatchState.MutationDispatched);
        }
    }

    private ForgejoNativeOperationState CreateOperationState(string path, CancellationToken cancellationToken)
        => new(
            path,
            cancellationToken,
            EffectiveNativeOperationDeadline(),
            testHooks?.MaximumTransferBytes ?? MaximumTransferBytes,
            testHooks?.MaximumTemporaryDiskBytes ?? MaximumTemporaryDiskBytes);

    private TimeSpan EffectiveNativeOperationDeadline()
        => testHooks?.OperationTimeout is { } timeout && timeout > TimeSpan.Zero
            ? timeout
            : NativeOperationDeadline;

    private static ForgejoCommitResult InterruptedCommitResult(
        ForgejoCommitDispatchState dispatchState,
        ForgejoApiFailureCondition preDispatchCondition)
    {
        bool interruptedBeforeDispatch = dispatchState.TryRecordCallerInterruption();
        return ForgejoCommitResult.Failure(
            interruptedBeforeDispatch
                ? preDispatchCondition
                : ForgejoApiFailureCondition.AmbiguousMutationResponse,
            observedCommitSha: dispatchState.CreatedCommitSha,
            mutationDispatched: !interruptedBeforeDispatch);
    }

    private async Task<(ForgejoApiFailureCondition? Failure, TimeSpan? RetryAfter)> ValidateAdvertisementAsync(
        ProviderGitOperationResolvedTarget target,
        string service,
        bool requireReportStatus,
        CancellationToken cancellationToken)
    {
        try
        {
            UriBuilder builder = new(RemoteUrl(target))
            {
                Query = $"service={service}",
            };
            builder.Path = $"{builder.Path.TrimEnd('/')}/info/refs";

            using HttpRequestMessage request = new(HttpMethod.Get, builder.Uri);
            request.Headers.TryAddWithoutValidation("Git-Protocol", "version=0");
            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return (ForgejoApiFailureCondition.AuthenticationRequired, null);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return (ForgejoApiFailureCondition.PermissionInsufficient, null);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return (ForgejoApiFailureCondition.NotFoundOrHidden, null);
            }

            if ((int)response.StatusCode == 429)
            {
                return (ForgejoApiFailureCondition.RateLimit, RetryAfter(response));
            }

            if ((int)response.StatusCode is >= 300 and < 400)
            {
                return (ForgejoApiFailureCondition.RedirectCrossOrigin, null);
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return ((int)response.StatusCode >= 500
                    ? ForgejoApiFailureCondition.ServerUnavailable
                    : ForgejoApiFailureCondition.SmartHttpUnsupported, null);
            }

            string expectedMediaType = $"application/x-{service}-advertisement";
            if (!string.Equals(response.Content.Headers.ContentType?.MediaType, expectedMediaType, StringComparison.OrdinalIgnoreCase))
            {
                return (ForgejoApiFailureCondition.SmartHttpUnsupported, null);
            }

            if (response.Content.Headers.ContentLength is > MaximumAdvertisementBytes)
            {
                return (ForgejoApiFailureCondition.ResponseLimitExceeded, null);
            }

            byte[] bytes = await ReadBoundedAsync(response.Content, cancellationToken).ConfigureAwait(false);
            return TryValidateAdvertisement(bytes, service, target, requireReportStatus, out ForgejoApiFailureCondition failure)
                ? (null, null)
                : (failure, null);
        }
        catch (InvalidDataException)
        {
            return (ForgejoApiFailureCondition.ResponseLimitExceeded, null);
        }
        catch (OperationCanceledException)
        {
            return (cancellationToken.IsCancellationRequested
                ? ForgejoApiFailureCondition.CancellationBeforeDispatch
                : ForgejoApiFailureCondition.OperationTimedOut, null);
        }
        catch (HttpRequestException)
        {
            return (ForgejoApiFailureCondition.ServerUnavailable, null);
        }
        catch (IOException)
        {
            return (ForgejoApiFailureCondition.ServerUnavailable, null);
        }
        catch (Exception)
        {
            return (ForgejoApiFailureCondition.ServerUnavailable, null);
        }
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        TimeSpan? delay = response.Headers.RetryAfter?.Delta;
        if (delay is null && response.Headers.RetryAfter?.Date is { } date)
        {
            delay = date - DateTimeOffset.UtcNow;
        }

        return delay is null
            ? null
            : delay <= TimeSpan.Zero
                ? TimeSpan.Zero
                : delay > TimeSpan.FromHours(24)
                    ? TimeSpan.FromHours(24)
                    : delay;
    }

    internal static bool TryValidateAdvertisement(
        ReadOnlySpan<byte> bytes,
        string service,
        ProviderGitOperationResolvedTarget target,
        bool requireReportStatus,
        out ForgejoApiFailureCondition failure)
    {
        failure = ForgejoApiFailureCondition.SmartHttpUnsupported;
        int offset = 0;
        bool serviceHeaderSeen = false;
        bool serviceHeaderFlushSeen = false;
        bool firstAdvertisedRefSeen = false;
        bool targetRefSeen = false;
        bool terminalFlushSeen = false;
        HashSet<string> capabilities = new(StringComparer.Ordinal);
        while (offset < bytes.Length)
        {
            if (bytes.Length - offset < 4
                || !int.TryParse(Encoding.ASCII.GetString(bytes.Slice(offset, 4)), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int length))
            {
                return false;
            }

            offset += 4;
            if (length == 0)
            {
                if (serviceHeaderSeen && !serviceHeaderFlushSeen)
                {
                    serviceHeaderFlushSeen = true;
                    continue;
                }

                if (!serviceHeaderFlushSeen || !firstAdvertisedRefSeen || offset != bytes.Length)
                {
                    return false;
                }

                terminalFlushSeen = true;
                continue;
            }

            if (terminalFlushSeen)
            {
                return false;
            }

            int payloadLength = length - 4;
            if (payloadLength <= 0 || payloadLength > bytes.Length - offset)
            {
                return false;
            }

            string line = Encoding.UTF8.GetString(bytes.Slice(offset, payloadLength)).TrimEnd('\n');
            offset += payloadLength;
            if (!serviceHeaderSeen)
            {
                if (!string.Equals(line, $"# service={service}", StringComparison.Ordinal))
                {
                    return false;
                }

                serviceHeaderSeen = true;
                continue;
            }

            if (!serviceHeaderFlushSeen)
            {
                return false;
            }

            int nul = line.IndexOf('\0');
            string identity = nul >= 0 ? line[..nul] : line;
            string[] fields = identity.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 2 || !ProviderGitOperationResolvedTarget.IsGitObjectId(fields[0]) || fields[0].Length != 40)
            {
                failure = ForgejoApiFailureCondition.ObjectFormatUnsupported;
                return false;
            }

            if (!firstAdvertisedRefSeen)
            {
                if (nul < 0)
                {
                    return false;
                }

                foreach (string capability in line[(nul + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    capabilities.Add(capability);
                }
            }
            else if (nul >= 0)
            {
                return false;
            }

            firstAdvertisedRefSeen = true;

            if (string.Equals(fields[1], target.FullRef, StringComparison.Ordinal))
            {
                if (!string.Equals(fields[0], target.ExpectedHeadSha, StringComparison.OrdinalIgnoreCase))
                {
                    failure = ForgejoApiFailureCondition.RefHeadConflict;
                    return false;
                }

                targetRefSeen = true;
            }
        }

        if (capabilities.Any(static capability => capability.StartsWith("object-format=", StringComparison.Ordinal)
                && !string.Equals(capability, "object-format=sha1", StringComparison.Ordinal)))
        {
            failure = ForgejoApiFailureCondition.ObjectFormatUnsupported;
            return false;
        }

        if (!serviceHeaderFlushSeen || !terminalFlushSeen || !targetRefSeen)
        {
            failure = ForgejoApiFailureCondition.MissingBranchOrPath;
            return false;
        }

        if (requireReportStatus && !capabilities.Contains("report-status"))
        {
            return false;
        }

        return true;
    }

    internal static bool TryCreateTree(
        Repository repository,
        ProviderGitOperationResolvedTarget target,
        IReadOnlyList<ProviderResolvedFileChange> changes,
        ForgejoNativeOperationState state,
        out Tree? tree,
        out ForgejoApiFailureCondition failure)
    {
        tree = null;
        failure = ForgejoApiFailureCondition.ValidationFailure;
        Commit? parent = repository.Lookup<Commit>(target.ExpectedHeadSha);
        if (parent is null)
        {
            failure = ForgejoApiFailureCondition.RefHeadConflict;
            return false;
        }

        TreeDefinition definition = TreeDefinition.From(parent.Tree);
        foreach (ProviderResolvedFileChange change in changes)
        {
            TreeEntry? existing = parent.Tree[change.Path];
            if (change.Kind == ProviderFileChangeKind.Add)
            {
                if (existing is not null || HasNonTreeAncestor(parent.Tree, change.Path))
                {
                    return false;
                }
            }
            else if (existing is null
                || existing.TargetType != TreeEntryTargetType.Blob
                || existing.Mode != Mode.NonExecutableFile
                || !string.Equals(existing.Target.Id.Sha, change.SourceObjectId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (change.Kind == ProviderFileChangeKind.Remove)
            {
                definition.Remove(change.Path);
            }
            else
            {
                using MemoryStream stream = new(change.Content.ToArray(), writable: false);
                Blob blob = repository.ObjectDatabase.CreateBlob(stream, change.Content.Length);
                definition.Add(change.Path, blob, Mode.NonExecutableFile);
            }

            if (!state.Check(0))
            {
                failure = state.FailureCondition;
                return false;
            }
        }

        tree = repository.ObjectDatabase.CreateTree(definition);
        if (!state.Check(0))
        {
            failure = state.FailureCondition;
            tree = null;
            return false;
        }

        failure = ForgejoApiFailureCondition.None;
        return true;
    }

    private static bool HasNonTreeAncestor(Tree root, string path)
    {
        int separator = path.IndexOf('/');
        while (separator >= 0)
        {
            TreeEntry? ancestor = root[path[..separator]];
            if (ancestor is not null
                && (ancestor.TargetType != TreeEntryTargetType.Tree || ancestor.Mode != Mode.Directory))
            {
                return true;
            }

            separator = path.IndexOf('/', separator + 1);
        }

        return false;
    }

    private void FetchExpectedHead(
        Repository repository,
        ProviderGitOperationResolvedTarget target,
        ForgejoNativeOperationState state)
    {
        FetchOptions options = new()
        {
            CertificateCheck = certificateCheck,
            CustomHeaders = [AuthorizationValue()],
            Depth = 1,
            Prune = false,
            TagFetchMode = TagFetchMode.None,
            OnProgress = _ => state.Check(0),
            OnTransferProgress = progress => state.Check(progress.ReceivedBytes),
            OnUpdateTips = (_, _, _) => state.Check(0),
        };
        Remote remote = repository.Network.Remotes.Add("origin", RemoteUrl(target).AbsoluteUri);
        repository.Network.Fetch(
            remote.Name,
            [$"{target.FullRef}:refs/hexalith/expected"],
            options,
            logMessage: null);

        Reference? fetched = repository.Refs["refs/hexalith/expected"];
        if (fetched?.TargetIdentifier is null
            || !string.Equals(fetched.TargetIdentifier, target.ExpectedHeadSha, StringComparison.OrdinalIgnoreCase))
        {
            throw new NonFastForwardException("Expected head was not fetched.");
        }
    }

    private Repository CreateRepository(string path)
    {
        Repository.Init(path, isBare: true);
        Repository? repository = null;
        Configuration? effectiveConfiguration = null;
        try
        {
            repository = new Repository(path);
            testHooks?.RepositoryOpened?.Invoke(repository);
            effectiveConfiguration = testHooks?.EffectiveConfigurationFactory?.Invoke(path);
            if (testHooks?.AllowAmbientConfigurationForTests != true
                && HasForbiddenAmbientConfiguration(effectiveConfiguration ?? repository.Config))
            {
                throw new ForgejoAmbientConfigurationException();
            }

            repository.Config.Set("core.bare", true);
            repository.Config.Set("core.hooksPath", Path.Combine(path, "disabled-hooks"));
            repository.Config.Set("credential.helper", string.Empty);
            repository.Config.Set("http.followRedirects", false);
            repository.Config.Set("http.proxy", string.Empty);
            repository.Config.Set("protocol.version", 0);
            repository.Config.Set("filter.lfs.required", false);
            return repository;
        }
        catch
        {
            repository?.Dispose();
            throw;
        }
        finally
        {
            effectiveConfiguration?.Dispose();
        }
    }

    /// <summary>
    /// Determines whether effective configuration contains a non-repository scope.
    /// </summary>
    /// <param name="configuration">The effective repository configuration.</param>
    /// <returns><see langword="true"/> when ambient configuration is present.</returns>
    internal static bool HasForbiddenAmbientConfiguration(Configuration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.Any(static entry => entry.Level is ConfigurationLevel.ProgramData
            or ConfigurationLevel.System
            or ConfigurationLevel.Xdg
            or ConfigurationLevel.Global);
    }

    private string TemporaryRepositoryPath()
        => testHooks?.TemporaryRepositoryPathFactory?.Invoke()
            ?? Path.Combine(Path.GetTempPath(), $"hxf-forgejo-{Guid.NewGuid():N}");

    private static void CreatePrivateTemporaryDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            SecurityIdentifier identity = WindowsIdentity.GetCurrent().User
                ?? throw new UnauthorizedAccessException();
            DirectorySecurity security = new();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.SetOwner(identity);
            security.AddAccessRule(new FileSystemAccessRule(
                identity,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            security.CreateDirectory(path);
        }
        else
        {
            const uint UserOnlyMode = (uint)(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            if (CreateUnixDirectory(path, UserOnlyMode) != 0)
            {
                throw new IOException();
            }
        }
    }

    [DllImport("libc", EntryPoint = "mkdir", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern int CreateUnixDirectory(string path, uint mode);

    private bool TryDeleteTemporaryDirectory(string path)
    {
        bool observerSucceeded = true;
        try
        {
            testHooks?.CleanupAttempted?.Invoke();
        }
        catch (Exception)
        {
            observerSucceeded = false;
        }

        bool deleted;
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }

            deleted = !Directory.Exists(path);
        }
        catch (Exception)
        {
            deleted = false;
        }

        try
        {
            return observerSucceeded && (testHooks?.CleanupResult?.Invoke(deleted) ?? deleted);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static ForgejoApiFailureCondition ClassifyNativeFailure(
        LibGit2SharpException exception,
        bool mutationDispatched)
    {
        string message = exception.Message;
        if (message.Contains("401", StringComparison.OrdinalIgnoreCase)
            || message.Contains("authentication", StringComparison.OrdinalIgnoreCase))
        {
            return ForgejoApiFailureCondition.AuthenticationRequired;
        }

        if (message.Contains("403", StringComparison.OrdinalIgnoreCase)
            || message.Contains("permission", StringComparison.OrdinalIgnoreCase))
        {
            return ForgejoApiFailureCondition.PermissionInsufficient;
        }

        return mutationDispatched
            ? ForgejoApiFailureCondition.AmbiguousMutationResponse
            : ForgejoApiFailureCondition.ServerUnavailable;
    }

    private static bool IsPolicyRejection(string message)
        => message.Contains("protected", StringComparison.OrdinalIgnoreCase)
            || message.Contains("policy", StringComparison.OrdinalIgnoreCase)
            || message.Contains("hook declined", StringComparison.OrdinalIgnoreCase);

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        using Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using MemoryStream buffer = new();
        byte[] chunk = new byte[8192];
        while (true)
        {
            int read = await stream.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length > MaximumAdvertisementBytes - read)
            {
                throw new InvalidDataException();
            }

            buffer.Write(chunk, 0, read);
        }
    }

    private string AuthorizationValue() => $"Authorization: {authorizationHeader.Scheme} {authorizationHeader.Parameter}";

    /// <summary>
    /// Determines whether a resolved repository locator is one literal URI path segment.
    /// </summary>
    /// <param name="value">The resolved owner or repository value.</param>
    /// <returns><see langword="true"/> when URI resolution cannot interpret the value as path navigation.</returns>
    internal static bool IsSafeRepositoryPathSegment(string? value)
        => value is { Length: > 0 }
            && value is not "." and not ".."
            && !value.Contains('/', StringComparison.Ordinal)
            && !value.Contains('\\', StringComparison.Ordinal)
            && ProviderGitOperationResolvedTarget.IsCanonicalUnicode(value)
            && !value.Any(char.IsControl);

    private Uri RemoteUrl(ProviderGitOperationResolvedTarget target)
    {
        if (!IsSafeRepositoryPathSegment(target.Owner)
            || !IsSafeRepositoryPathSegment(target.RepositoryName))
        {
            throw new InvalidOperationException();
        }

        Uri remote = new(
            authorizedBaseUri,
            $"{Uri.EscapeDataString(target.Owner)}/{Uri.EscapeDataString(target.RepositoryName)}.git");
        string authorizedPath = authorizedBaseUri.AbsolutePath.EndsWith("/", StringComparison.Ordinal)
            ? authorizedBaseUri.AbsolutePath
            : authorizedBaseUri.AbsolutePath + "/";
        if (!ForgejoAuthorizedBaseUrl.IsSameOrigin(authorizedBaseUri, remote)
            || !remote.AbsolutePath.StartsWith(authorizedPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException();
        }

        return remote;
    }
}
