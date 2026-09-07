using System.Net;
using System.Text;
using System.Text.Json;
using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

internal sealed partial class ForgejoHttpApiClient
{
    private const int MaximumOperationChangeCount = 100;
    private const int MaximumOperationFileBytes = 1024 * 1024;
    private const long MaximumOperationAggregateBytes = 10L * 1024 * 1024;
    private const int MaximumOperationPathCharacters = 500;
    private const int MaximumOperationBranchCharacters = 100;
    private static readonly SemaphoreSlim NativeOperationGate = new(1, 1);
    private static readonly TimeSpan OperationResponseTimeout = TimeSpan.FromSeconds(30);

    public async Task<ForgejoFileMutationResult> StageFileChangesAsync(
        ForgejoFileMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.CancellationBeforeDispatch);
        }

        if (!IsSupportedOperationRequest(request.Target, request.SupportedSnapshotVersion)
            || !TryValidateOperationChanges(request.Changes))
        {
            return ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.ValidationFailure);
        }

        if (!IsSha1Operation(request.Target, request.Changes))
        {
            return ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.ObjectFormatUnsupported);
        }

        if (_authorizationHeader is null)
        {
            return ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.AuthenticationRequired);
        }

        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(EffectiveOperationTimeout());
        ForgejoNativeOperationPermit? nativePermit = null;
        try
        {
            await NativeOperationGate.WaitAsync(deadline.Token).ConfigureAwait(false);
            nativePermit = new ForgejoNativeOperationPermit(NativeOperationGate);
            if (!await request.ValidateReservationAsync(deadline.Token).ConfigureAwait(false))
            {
                return ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.ReservationInvalidated);
            }

            ForgejoApiFailureCondition? versionFailure = await RecheckVersionAsync(
                request.SupportedSnapshotVersion,
                deadline.Token).ConfigureAwait(false);
            if (versionFailure is not null)
            {
                return ForgejoFileMutationResult.Failure(versionFailure.Value);
            }

            ForgejoSmartHttpGitTransport transport = new(
                _client,
                _authorizedBaseUri,
                _authorizationHeader,
                _certificateCheck,
                _beforeReceivePackDispatch,
                _transportTestHooks);
            ForgejoFileMutationResult result = await transport.StageAsync(
                request,
                deadline.Token,
                nativePermit).ConfigureAwait(false);
            return result.FailureCondition == ForgejoApiFailureCondition.CancellationBeforeDispatch
                && !cancellationToken.IsCancellationRequested
                && deadline.IsCancellationRequested
                    ? ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.OperationTimedOut)
                    : result;
        }
        catch (OperationCanceledException)
        {
            return ForgejoFileMutationResult.Failure(
                cancellationToken.IsCancellationRequested
                    ? ForgejoApiFailureCondition.CancellationBeforeDispatch
                    : ForgejoApiFailureCondition.OperationTimedOut);
        }
        catch (Exception)
        {
            return ForgejoFileMutationResult.Failure(ForgejoApiFailureCondition.ServerUnavailable);
        }
        finally
        {
            nativePermit?.ReleaseByCaller();
        }
    }

    public async Task<ForgejoCommitResult> CommitAsync(
        ForgejoCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return ForgejoCommitResult.Failure(ForgejoApiFailureCondition.CancellationBeforeDispatch);
        }

        if (!IsSupportedOperationRequest(request.Target, request.SupportedSnapshotVersion)
            || !TryValidateOperationChanges(request.Changes)
            || !TryNormalizeCommitMessage(request.CommitMessage, out string commitMessage))
        {
            return ForgejoCommitResult.Failure(ForgejoApiFailureCondition.ValidationFailure);
        }

        if (!IsSha1Operation(request.Target, request.Changes))
        {
            return ForgejoCommitResult.Failure(ForgejoApiFailureCondition.ObjectFormatUnsupported);
        }

        if (!ProviderGitOperationResolvedTarget.IsGitObjectId(request.StagedTreeSha)
            || request.StagedTreeSha.Length != request.Target.ExpectedHeadSha.Length
            || _authorizationHeader is null)
        {
            return ForgejoCommitResult.Failure(ForgejoApiFailureCondition.ValidationFailure);
        }

        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(EffectiveOperationTimeout());
        ForgejoNativeOperationPermit? nativePermit = null;
        string? dispatchedCommitSha = null;
        try
        {
            await NativeOperationGate.WaitAsync(deadline.Token).ConfigureAwait(false);
            nativePermit = new ForgejoNativeOperationPermit(NativeOperationGate);
            if (!await request.ValidateReservationAsync(deadline.Token).ConfigureAwait(false))
            {
                return ForgejoCommitResult.Failure(ForgejoApiFailureCondition.ReservationInvalidated);
            }

            ForgejoApiFailureCondition? versionFailure = await RecheckVersionAsync(
                request.SupportedSnapshotVersion,
                deadline.Token).ConfigureAwait(false);
            if (versionFailure is not null)
            {
                return ForgejoCommitResult.Failure(versionFailure.Value);
            }

            ForgejoSmartHttpGitTransport transport = new(
                _client,
                _authorizedBaseUri,
                _authorizationHeader,
                _certificateCheck,
                _beforeReceivePackDispatch,
                _transportTestHooks);
            ForgejoCommitResult result = await transport.CommitAsync(
                request,
                deadline.Token,
                nativePermit).ConfigureAwait(false);
            if (!result.IsSuccess
                && result.FailureCondition == ForgejoApiFailureCondition.CancellationBeforeDispatch
                && !cancellationToken.IsCancellationRequested
                && deadline.IsCancellationRequested)
            {
                return ForgejoCommitResult.Failure(
                    ForgejoApiFailureCondition.OperationTimedOut,
                    observedCommitSha: result.ObservedCommitSha);
            }

            if (!result.IsSuccess)
            {
                if (result.FailureCondition == ForgejoApiFailureCondition.RemoteRejected)
                {
                    (ForgejoApiFailureCondition? RefFailure, ForgejoRefObservation? Observation, TimeSpan? RetryAfter) rejectedRef =
                        await ObserveExactRefAsync(request.Target, deadline.Token).ConfigureAwait(false);
                    if (rejectedRef.RefFailure is null
                        && !string.Equals(
                            rejectedRef.Observation!.ObjectSha,
                            request.Target.ExpectedHeadSha,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return ForgejoCommitResult.Failure(
                            ForgejoApiFailureCondition.RefHeadConflict,
                            observedCommitSha: result.ObservedCommitSha);
                    }
                }

                return result;
            }

            dispatchedCommitSha = result.CommitSha;
            (ForgejoApiFailureCondition? RefFailure, ForgejoRefObservation? Observation, TimeSpan? RetryAfter) confirmation =
                await ObserveExactRefAsync(request.Target, deadline.Token).ConfigureAwait(false);
            return confirmation.RefFailure is null
                && string.Equals(confirmation.Observation!.FullRef, request.Target.FullRef, StringComparison.Ordinal)
                && string.Equals(confirmation.Observation.ObjectType, "commit", StringComparison.Ordinal)
                && string.Equals(confirmation.Observation.ObjectSha, result.CommitSha, StringComparison.OrdinalIgnoreCase)
                    ? result
                    : ForgejoCommitResult.Failure(
                        ForgejoApiFailureCondition.AmbiguousMutationResponse,
                        confirmation.RetryAfter,
                        result.CommitSha);
        }
        catch (OperationCanceledException)
        {
            return ForgejoCommitResult.Failure(
                dispatchedCommitSha is not null
                    ? ForgejoApiFailureCondition.AmbiguousMutationResponse
                    : cancellationToken.IsCancellationRequested
                    ? ForgejoApiFailureCondition.CancellationBeforeDispatch
                    : ForgejoApiFailureCondition.OperationTimedOut,
                observedCommitSha: dispatchedCommitSha);
        }
        catch (Exception)
        {
            return ForgejoCommitResult.Failure(
                dispatchedCommitSha is null
                    ? ForgejoApiFailureCondition.ServerUnavailable
                    : ForgejoApiFailureCondition.AmbiguousMutationResponse,
                observedCommitSha: dispatchedCommitSha);
        }
        finally
        {
            nativePermit?.ReleaseByCaller();
        }
    }

    private TimeSpan EffectiveOperationTimeout()
        => _transportTestHooks?.OperationTimeout is { } timeout && timeout > TimeSpan.Zero
            ? timeout
            : OperationResponseTimeout;

    public async Task<ForgejoOperationStatusResult> GetOperationStatusAsync(
        ForgejoOperationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return ForgejoOperationStatusResult.Failure(ForgejoApiFailureCondition.ObservationCancelled);
        }

        if (!IsSupportedOperationRequest(request.Target, request.SupportedSnapshotVersion)
            || !TryValidateOperationChanges(request.Changes)
            || !TryNormalizeCommitMessage(request.CommitMessage, out string commitMessage)
            || request.IntendedCommitSha is not null && !ProviderGitOperationResolvedTarget.IsGitObjectId(request.IntendedCommitSha))
        {
            return ForgejoOperationStatusResult.Failure(ForgejoApiFailureCondition.ValidationFailure);
        }

        if (!IsSha1Operation(request.Target, request.Changes)
            || request.IntendedCommitSha is { Length: not 40 })
        {
            return ForgejoOperationStatusResult.Failure(ForgejoApiFailureCondition.ObjectFormatUnsupported);
        }

        try
        {
            ForgejoApiFailureCondition? versionFailure = await RecheckVersionAsync(
                request.SupportedSnapshotVersion,
                cancellationToken).ConfigureAwait(false);
            if (versionFailure is not null)
            {
                return ForgejoOperationStatusResult.Failure(versionFailure.Value);
            }

            (ForgejoApiFailureCondition? RefFailure, ForgejoRefObservation? Observation, TimeSpan? RetryAfter) reference =
                await ObserveExactRefAsync(request.Target, cancellationToken).ConfigureAwait(false);
            if (reference.RefFailure is not null)
            {
                if (reference.RefFailure != ForgejoApiFailureCondition.MissingBranchOrPath)
                {
                    return ForgejoOperationStatusResult.Failure(reference.RefFailure.Value, reference.RetryAfter);
                }

                // A ref 404 alone cannot distinguish a deleted ref from a concealed or missing
                // repository. Only a second, bounded authorized repository observation may turn
                // that absence into terminal conflicting evidence.
                return await IsRepositoryVisibleAsync(request.Target, cancellationToken).ConfigureAwait(false)
                    ? ForgejoOperationStatusResult.Conflicting(null, request.Target.FullRef, null)
                    : ForgejoOperationStatusResult.Failure(ForgejoApiFailureCondition.NotFoundOrHidden);
            }

            ForgejoRefObservation observed = reference.Observation!;
            if (request.IntendedCommitSha is not null
                && string.Equals(observed.ObjectSha, request.IntendedCommitSha, StringComparison.OrdinalIgnoreCase))
            {
                return ForgejoOperationStatusResult.Observed(
                    ProviderOperationStatusKind.Confirmed,
                    observed.ObjectSha,
                    observed.FullRef,
                    observed.ObjectType);
            }

            if (string.Equals(observed.ObjectSha, request.Target.ExpectedHeadSha, StringComparison.OrdinalIgnoreCase))
            {
                return ForgejoOperationStatusResult.Observed(
                    ProviderOperationStatusKind.NotApplied,
                    observed.ObjectSha,
                    observed.FullRef,
                    observed.ObjectType);
            }

            return ForgejoOperationStatusResult.Conflicting(
                observed.ObjectSha,
                observed.FullRef,
                observed.ObjectType);
        }
        catch (OperationCanceledException)
        {
            return ForgejoOperationStatusResult.Failure(ForgejoApiFailureCondition.ObservationCancelled);
        }
        catch (Exception)
        {
            return ForgejoOperationStatusResult.Failure(ForgejoApiFailureCondition.ServerUnavailable);
        }
    }

    private async Task<ForgejoApiFailureCondition?> RecheckVersionAsync(
        string supportedSnapshotVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await SendObservationAsync(ApiUri("version"), cancellationToken).ConfigureAwait(false);
            ForgejoApiFailureCondition? responseFailure = MapObservationResponse(response, ForgejoApiFailureCondition.NotFoundOrHidden);
            if (responseFailure is not null)
            {
                return responseFailure;
            }

            using JsonDocument? document = await ReadJsonDocumentAsync(response, cancellationToken).ConfigureAwait(false);
            if (document is null
                || document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("version", out JsonElement version)
                || version.ValueKind != JsonValueKind.String
                || !ForgejoSupportedVersionCatalog.TryFind(version.GetString()!, out ForgejoSupportedVersionEntry supported)
                || !string.Equals(supported.Version, supportedSnapshotVersion, StringComparison.Ordinal))
            {
                return ForgejoApiFailureCondition.VersionIncompatible;
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ForgejoApiFailureCondition.ServerUnavailable;
        }
    }

    private async Task<(ForgejoApiFailureCondition? RefFailure, ForgejoRefObservation? Observation, TimeSpan? RetryAfter)> ObserveExactRefAsync(
        ProviderGitOperationResolvedTarget target,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendObservationAsync(
            ApiUri($"repos/{Escape(target.Owner)}/{Escape(target.RepositoryName)}/git/refs/{Escape(target.RefName)}"),
            cancellationToken).ConfigureAwait(false);
        ForgejoApiFailureCondition? responseFailure = MapObservationResponse(response, ForgejoApiFailureCondition.MissingBranchOrPath);
        if (responseFailure is not null)
        {
            return (responseFailure, null, RetryAfter(response));
        }

        using JsonDocument? document = await ReadJsonDocumentAsync(response, cancellationToken).ConfigureAwait(false);
        if (!TryReadExactReference(document, target.FullRef, out ForgejoRefObservation? observation))
        {
            return (ForgejoApiFailureCondition.MalformedResponse, null, null);
        }

        return (null, observation, null);
    }

    private async Task<bool> IsRepositoryVisibleAsync(
        ProviderGitOperationResolvedTarget target,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendObservationAsync(
            ApiUri($"repos/{Escape(target.Owner)}/{Escape(target.RepositoryName)}"),
            cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return false;
        }

        using JsonDocument? document = await ReadJsonDocumentAsync(response, cancellationToken).ConfigureAwait(false);
        return document is not null
            && document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("id", out JsonElement id)
            && id.ValueKind == JsonValueKind.Number
            && id.TryGetInt64(out long numericId)
            && numericId > 0
            && TryReadString(document.RootElement, "name", out string? repositoryName)
            && string.Equals(repositoryName, target.RepositoryName, StringComparison.Ordinal);
    }

    private static bool TryReadExactReference(
        JsonDocument? document,
        string expectedFullRef,
        out ForgejoRefObservation? observation)
    {
        observation = null;
        if (document is null || document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        JsonElement[] entries = document.RootElement.EnumerateArray().ToArray();
        if (entries.Length != 1
            || entries[0].ValueKind != JsonValueKind.Object
            || !TryReadString(entries[0], "ref", out string? fullRef)
            || !string.Equals(fullRef, expectedFullRef, StringComparison.Ordinal)
            || !entries[0].TryGetProperty("object", out JsonElement gitObject)
            || gitObject.ValueKind != JsonValueKind.Object
            || !TryReadString(gitObject, "type", out string? objectType)
            || !string.Equals(objectType, "commit", StringComparison.Ordinal)
            || !TryReadString(gitObject, "sha", out string? objectSha)
            || !ProviderGitOperationResolvedTarget.IsGitObjectId(objectSha))
        {
            return false;
        }

        observation = new ForgejoRefObservation(fullRef!, objectType!, objectSha!);
        return true;
    }

    private static bool TryReadString(JsonElement element, string propertyName, out string? value)
    {
        value = element.TryGetProperty(propertyName, out JsonElement property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryValidateOperationChanges(IReadOnlyList<ProviderResolvedFileChange>? changes)
    {
        if (changes is null || changes.Count is < 1 or > MaximumOperationChangeCount)
        {
            return false;
        }

        HashSet<string> paths = new(StringComparer.Ordinal);
        long aggregateBytes = 0;
        for (int index = 0; index < changes.Count; index++)
        {
            ProviderResolvedFileChange? change = changes[index];
            if (change is null
                || change.Sequence != index
                || !Enum.IsDefined(change.Kind)
                || change.ContentType != ProviderFileContentType.RegularFile
                || !IsSafeOperationPath(change.Path)
                || !paths.Add(change.Path)
                || paths.Any(path => !string.Equals(path, change.Path, StringComparison.Ordinal)
                    && (path.StartsWith(change.Path + "/", StringComparison.Ordinal)
                        || change.Path.StartsWith(path + "/", StringComparison.Ordinal)))
                || change.Content.Length > MaximumOperationFileBytes
                || aggregateBytes > MaximumOperationAggregateBytes - change.Content.Length
                || change.Kind == ProviderFileChangeKind.Add && change.SourceObjectId is not null
                || change.Kind is ProviderFileChangeKind.Change or ProviderFileChangeKind.Remove
                    && !ProviderGitOperationResolvedTarget.IsGitObjectId(change.SourceObjectId)
                || change.Kind == ProviderFileChangeKind.Remove && !change.Content.IsEmpty)
            {
                return false;
            }

            aggregateBytes += change.Content.Length;
        }

        return true;
    }

    private static bool IsSupportedOperationRequest(
        ProviderGitOperationResolvedTarget? target,
        string supportedSnapshotVersion)
        => target is not null
            && target.TryValidate(out _)
            && target.RefName["heads/".Length..].Length <= MaximumOperationBranchCharacters
            && ForgejoSupportedVersionCatalog.IsSupported(supportedSnapshotVersion);

    private static bool IsSha1Operation(
        ProviderGitOperationResolvedTarget target,
        IReadOnlyList<ProviderResolvedFileChange> changes)
        => target.ExpectedHeadSha.Length == 40
            && changes.All(static change => change.SourceObjectId is null || change.SourceObjectId.Length == 40);

    private static bool TryNormalizeCommitMessage(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 65536
            || !ProviderGitOperationResolvedTarget.IsCanonicalUnicode(value)
            || value.Contains('\0', StringComparison.Ordinal))
        {
            return false;
        }

        normalized = value.Trim();
        return normalized.Length > 0 && string.Equals(value, normalized, StringComparison.Ordinal);
    }

    private static bool IsSafeOperationPath(string? path)
        => path is { Length: > 0 and <= MaximumOperationPathCharacters }
            && ProviderGitOperationResolvedTarget.IsCanonicalUnicode(path)
            && path[0] != '/'
            && !path.EndsWith("/", StringComparison.Ordinal)
            && !path.Contains("\\", StringComparison.Ordinal)
            && !path.Any(char.IsControl)
            && !path.Split('/').Any(static segment => segment is "" or "." or "..");

}
