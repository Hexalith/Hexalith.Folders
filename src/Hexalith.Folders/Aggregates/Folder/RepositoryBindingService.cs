using System.Security.Cryptography;
using System.Text;

using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed class RepositoryBindingService(
    LayeredFolderAuthorizationService authorizationService,
    IRepositoryBindingReadinessValidator readinessValidator,
    IProviderReadinessBindingReader bindingReader,
    IProviderCapabilityResolver providerResolver,
    IFolderRepository repository,
    TimeProvider? timeProvider = null)
{
    public const string ActionToken = "bind_repository";

    private readonly LayeredFolderAuthorizationService _authorizationService =
        authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    private readonly IRepositoryBindingReadinessValidator _readinessValidator =
        readinessValidator ?? throw new ArgumentNullException(nameof(readinessValidator));
    private readonly IProviderReadinessBindingReader _bindingReader =
        bindingReader ?? throw new ArgumentNullException(nameof(bindingReader));
    private readonly IProviderCapabilityResolver _providerResolver =
        providerResolver ?? throw new ArgumentNullException(nameof(providerResolver));
    private readonly IFolderRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<FolderResult> BindAsync(
        BindRepositoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyDictionary<string, string?> clientTenantValues = WithPayloadTenant(
            request.ClientControlledTenantValues,
            request.PayloadTenantId);

        LayeredFolderAuthorizationResult authorization = await _authorizationService.AuthorizeAsync(
            new LayeredFolderAuthorizationContext(
                request.AuthoritativeTenantId,
                request.PrincipalId,
                ActorSafeIdentifier: request.PrincipalId,
                ActionToken,
                LayeredFolderOperationPolicy.Mutation(),
                request.ClaimTransformEvidence,
                OperationScope: request.FolderId,
                request.CorrelationId,
                request.TaskId,
                clientTenantValues,
                request.ClientControlledPrincipalValues),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAllowed || authorization.AllowedContext is null)
        {
            return FolderResult.Rejected(
                MapAuthorization(authorization.Decision.OutcomeCode),
                managedTenantId: null,
                organizationId: null,
                folderId: null,
                request.PrincipalId,
                request.CorrelationId,
                request.TaskId,
                request.IdempotencyKey);
        }

        LayeredFolderAuthorizationAllowedContext allowed = authorization.AllowedContext;
        if (string.IsNullOrWhiteSpace(allowed.OrganizationId))
        {
            return FolderResult.Rejected(
                FolderResultCode.MalformedEvidence,
                allowed.AuthoritativeTenantId,
                organizationId: null,
                request.FolderId,
                request.PrincipalId,
                request.CorrelationId,
                request.TaskId,
                request.IdempotencyKey);
        }

        string repositoryBindingId = FolderCommandValidator.DeriveRepositoryBindingId(
            allowed.AuthoritativeTenantId,
            request.FolderId,
            request.ProviderBindingRef,
            request.ExternalRepositoryRef,
            request.BranchRefPolicyRef);

        BindRepository command = new(
            allowed.AuthoritativeTenantId,
            allowed.OrganizationId,
            request.FolderId,
            request.RequestSchemaVersion,
            repositoryBindingId,
            request.ProviderBindingRef,
            request.ExternalRepositoryRef,
            request.BranchRefPolicyRef,
            request.CredentialScopeClass,
            allowed.ActorSafeIdentifier,
            request.CorrelationId,
            request.TaskId,
            request.IdempotencyKey,
            request.PayloadTenantId);

        FolderCommandValidationResult validation = FolderCommandValidator.Validate(command);
        if (!validation.IsAccepted)
        {
            return FolderResult.Rejected(command, validation.Code);
        }

        FolderStreamName streamName = _repository.CreateStreamName(command.ManagedTenantId, command.FolderId);
        FolderState state = _repository.Load(streamName);
        FolderResult aggregateResult = FolderAggregate.Handle(state, command, _timeProvider.GetUtcNow());
        if (aggregateResult.Events.Count == 0)
        {
            return aggregateResult;
        }

        FolderIdempotencyLookupResult lookup = _repository.TryGetIdempotencyFingerprint(
            streamName,
            command.IdempotencyKey,
            out string? priorFingerprint);

        if (lookup == FolderIdempotencyLookupResult.Found)
        {
            return string.Equals(priorFingerprint, validation.IdempotencyFingerprint, StringComparison.Ordinal)
                ? FolderResult.Rejected(command, FolderResultCode.IdempotentReplay)
                : FolderResult.Rejected(command, FolderResultCode.IdempotencyConflict);
        }

        if (lookup == FolderIdempotencyLookupResult.Unavailable)
        {
            return FolderResult.Rejected(command, FolderResultCode.IdempotencyUnavailable);
        }

        ProviderReadinessValidationResult readiness = await _readinessValidator.ValidateAsync(
            new ProviderReadinessValidationRequest(
                command.ManagedTenantId,
                request.PrincipalId,
                command.ProviderBindingRef,
                ProviderReadinessRequestedCapability.ExistingRepositoryBinding,
                command.CorrelationId,
                request.ClaimTransformEvidence,
                clientTenantValues),
            cancellationToken).ConfigureAwait(false);

        if (!IsReady(readiness))
        {
            return FolderResult.Rejected(command, MapReadiness(readiness));
        }

        OrganizationProviderBinding? binding = await _bindingReader.GetAsync(
            new ProviderReadinessBindingReadRequest(command.ManagedTenantId, command.ProviderBindingRef, command.CorrelationId),
            cancellationToken).ConfigureAwait(false);
        if (binding is null || !string.Equals(binding.OrganizationId, command.OrganizationId, StringComparison.Ordinal))
        {
            return FolderResult.Rejected(command, FolderResultCode.ProviderReadinessFailed);
        }

        IGitProvider? provider = await _providerResolver.ResolveAsync(
            binding.ProviderKind,
            binding.ProviderKind,
            cancellationToken).ConfigureAwait(false);
        if (provider is null)
        {
            return FolderResult.Rejected(command, FolderResultCode.UnsupportedProviderCapability);
        }

        ProviderRepositoryBindingResult providerResult = await provider.ValidateRepositoryBindingAsync(
            new ProviderRepositoryBindingRequest(
                command.ManagedTenantId,
                command.OrganizationId,
                command.ProviderBindingRef,
                binding.CredentialReferenceId,
                command.RepositoryBindingId,
                command.ExternalRepositoryRef,
                FolderCommandValidator.ExternalRepositoryRefFingerprint(command),
                command.BranchRefPolicyRef,
                binding.ProviderKind,
                binding.ProviderKind,
                BuildTargetEvidence(binding),
                CredentialModes(binding),
                new ProviderAuthorizationEvidenceSnapshot(
                    SafeHash($"{command.ManagedTenantId}|{command.ProviderBindingRef}|{request.PrincipalId}"),
                    _timeProvider.GetUtcNow(),
                    "fresh"),
                command.CorrelationId,
                command.IdempotencyKey),
            cancellationToken).ConfigureAwait(false);

        IReadOnlyList<IFolderEvent> events = BuildOutcomeEvents(
            command,
            validation.IdempotencyFingerprint!,
            aggregateResult.Events,
            providerResult,
            _timeProvider.GetUtcNow());
        FolderAppendOutcome outcome = _repository.AppendIfFingerprintAbsent(
            streamName,
            command.IdempotencyKey,
            validation.IdempotencyFingerprint!,
            events);

        if (outcome != FolderAppendOutcome.Appended)
        {
            return outcome switch
            {
                FolderAppendOutcome.FingerprintMatched => FolderResult.Rejected(command, FolderResultCode.IdempotentReplay),
                FolderAppendOutcome.FingerprintConflict => FolderResult.Rejected(command, FolderResultCode.IdempotencyConflict),
                FolderAppendOutcome.AppendConflict => ResolveAppendConflict(streamName, command),
                _ => FolderResult.Rejected(command, FolderResultCode.MalformedEvidence),
            };
        }

        if (providerResult.IsSuccess)
        {
            return new FolderResult(
                FolderResultCode.Accepted,
                command.ManagedTenantId,
                command.OrganizationId,
                command.FolderId,
                null,
                null,
                null,
                command.ActorPrincipalId,
                command.CorrelationId,
                command.TaskId,
                command.IdempotencyKey,
                events);
        }

        return FolderResult.Rejected(command, MapProvider(providerResult));
    }

    private FolderResult ResolveAppendConflict(FolderStreamName streamName, BindRepository command)
    {
        FolderState refreshed = _repository.Load(streamName);
        FolderResult refreshedResult = FolderAggregate.Handle(refreshed, command, _timeProvider.GetUtcNow());
        return refreshedResult.Events.Count == 0
            ? refreshedResult
            : FolderResult.Rejected(command, FolderResultCode.AppendConflict);
    }

    private static IReadOnlyList<IFolderEvent> BuildOutcomeEvents(
        BindRepository command,
        string fingerprint,
        IReadOnlyList<IFolderEvent> requestedEvents,
        ProviderRepositoryBindingResult providerResult,
        DateTimeOffset occurredAt)
    {
        List<IFolderEvent> events = [.. requestedEvents];
        if (providerResult.IsSuccess)
        {
            events.Add(new RepositoryBound(
                command.ManagedTenantId,
                command.OrganizationId,
                command.FolderId,
                command.RepositoryBindingId,
                command.ProviderBindingRef,
                command.CorrelationId,
                command.TaskId,
                command.IdempotencyKey,
                fingerprint,
                occurredAt));
            return events;
        }

        if (providerResult.FailureCategory is ProviderFailureCategory.UnknownProviderOutcome or ProviderFailureCategory.ReconciliationRequired)
        {
            events.Add(new ProviderOutcomeUnknown(
                command.ManagedTenantId,
                command.OrganizationId,
                command.FolderId,
                command.RepositoryBindingId,
                command.ProviderBindingRef,
                providerResult.FailureCategory == ProviderFailureCategory.ReconciliationRequired,
                providerResult.CategoryCode,
                command.CorrelationId,
                command.TaskId,
                command.IdempotencyKey,
                fingerprint,
                occurredAt));
            return events;
        }

        events.Add(new RepositoryBindingFailed(
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            command.RepositoryBindingId,
            command.ProviderBindingRef,
            providerResult.CategoryCode,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            fingerprint,
            occurredAt));
        return events;
    }

    private static IReadOnlyDictionary<string, string?> WithPayloadTenant(
        IReadOnlyDictionary<string, string?> values,
        string? payloadTenantId)
    {
        Dictionary<string, string?> merged = new(values, StringComparer.Ordinal);
        if (payloadTenantId is not null)
        {
            merged["payload_tenant_id"] = payloadTenantId;
        }

        return merged;
    }

    private static ProviderTargetEvidence BuildTargetEvidence(OrganizationProviderBinding binding)
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["operation_scope"] = ProviderOperationCatalog.RepositoryBinding,
            ["safe_target_fingerprint"] = SafeHash($"{binding.ManagedTenantId}|{binding.ProviderBindingRef}|repository-binding"),
        };

        if (string.Equals(binding.ProviderKind, "forgejo", StringComparison.Ordinal))
        {
            string baseUrl = TryGetSafeMetadata(binding, "authorized_base_url") ?? "https://forgejo.example.test";
            metadata["authorized_base_url"] = baseUrl;
        }

        return new ProviderTargetEvidence(
            binding.ProviderKind,
            TryGetSafeMetadata(binding, "provider_product_version")
                ?? TryGetSafeMetadata(binding, "product_version")
                ?? TryGetSafeMetadata(binding, "snapshot_version")
                ?? (string.Equals(binding.ProviderKind, "forgejo", StringComparison.Ordinal) ? "15.0.2" : "provider_binding_v1"),
            "provider_api_metadata_only",
            "provider_binding_v1",
            !string.Equals(binding.ConfiguredStatus, "configured", StringComparison.Ordinal),
            binding.OccurredAt,
            metadata);
    }

    private static string? TryGetSafeMetadata(OrganizationProviderBinding binding, string key)
    {
        if (binding.NamingPolicy.Metadata.TryGetValue(key, out string? naming) && IsSafeMetadataValue(key, naming))
        {
            return naming.Trim();
        }

        if (binding.BranchPolicy.Metadata.TryGetValue(key, out string? branch) && IsSafeMetadataValue(key, branch))
        {
            return branch.Trim();
        }

        return null;
    }

    private static bool IsSafeMetadataValue(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256)
        {
            return false;
        }

        return key == "authorized_base_url"
            ? Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
                && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                && string.IsNullOrEmpty(uri.UserInfo)
                && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment)
            : !value.Contains("://", StringComparison.Ordinal)
                && !value.Contains('@', StringComparison.Ordinal)
                && !value.Contains("secret", StringComparison.OrdinalIgnoreCase)
                && !value.Contains("token", StringComparison.OrdinalIgnoreCase)
                && !value.Contains("password", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ProviderCredentialMode> CredentialModes(OrganizationProviderBinding binding)
        => string.Equals(binding.ProviderKind, "forgejo", StringComparison.Ordinal)
            ? [ProviderCredentialMode.UserDelegatedReference]
            : [ProviderCredentialMode.AppInstallationReference];

    private static bool IsReady(ProviderReadinessValidationResult readiness)
        => readiness.Code == ProviderReadinessResultCode.Allowed
        && string.Equals(readiness.Status, "ready", StringComparison.Ordinal)
        && readiness.FailureCategory == ProviderFailureCategory.None;

    private static FolderResultCode MapAuthorization(string outcomeCode)
        => outcomeCode switch
        {
            LayeredAuthorizationOutcomeCodes.AuthenticationDenied => FolderResultCode.MissingAuthoritativeTenant,
            LayeredAuthorizationOutcomeCodes.ClaimTransformDenied => FolderResultCode.TenantAccessDenied,
            LayeredAuthorizationOutcomeCodes.TenantProjectionStale => FolderResultCode.StaleProjection,
            LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable => FolderResultCode.UnavailableProjection,
            LayeredAuthorizationOutcomeCodes.FolderAclDenied or LayeredAuthorizationOutcomeCodes.SafeNotFound => FolderResultCode.FolderAclDenied,
            LayeredAuthorizationOutcomeCodes.FolderAclStale => FolderResultCode.StaleProjection,
            LayeredAuthorizationOutcomeCodes.FolderAclUnavailable => FolderResultCode.UnavailableProjection,
            LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied => FolderResultCode.TenantAccessDenied,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied => FolderResultCode.PolicyEvidenceUnavailable,
            LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed => FolderResultCode.MalformedEvidence,
            _ => FolderResultCode.TenantAccessDenied,
        };

    private static FolderResultCode MapReadiness(ProviderReadinessValidationResult readiness)
    {
        if (readiness.Code is ProviderReadinessResultCode.ProjectionStale)
        {
            return FolderResultCode.StaleProjection;
        }

        if (readiness.Code is ProviderReadinessResultCode.ProjectionUnavailable or ProviderReadinessResultCode.ReadModelUnavailable)
        {
            return FolderResultCode.UnavailableProjection;
        }

        if (readiness.Code is ProviderReadinessResultCode.AuthenticationRequired or ProviderReadinessResultCode.AuthorizationDenied)
        {
            return FolderResultCode.TenantAccessDenied;
        }

        return readiness.FailureCategory switch
        {
            ProviderFailureCategory.UnsupportedProviderCapability => FolderResultCode.UnsupportedProviderCapability,
            ProviderFailureCategory.UnknownProviderOutcome => FolderResultCode.UnknownProviderOutcome,
            ProviderFailureCategory.ReconciliationRequired => FolderResultCode.ReconciliationRequired,
            ProviderFailureCategory.ProviderRateLimited => FolderResultCode.ProviderRateLimited,
            ProviderFailureCategory.ProviderUnavailable
                or ProviderFailureCategory.ProviderTransientFailure => FolderResultCode.ProviderUnavailable,
            ProviderFailureCategory.ProviderAuthenticationRequired
                or ProviderFailureCategory.ProviderPermissionInsufficient => FolderResultCode.ProviderPermissionInsufficient,
            ProviderFailureCategory.ProviderConflict => FolderResultCode.RepositoryConflict,
            _ => FolderResultCode.ProviderReadinessFailed,
        };
    }

    private static FolderResultCode MapProvider(ProviderRepositoryBindingResult result)
        => result.FailureCategory switch
        {
            ProviderFailureCategory.UnsupportedProviderCapability => FolderResultCode.UnsupportedProviderCapability,
            ProviderFailureCategory.UnknownProviderOutcome => FolderResultCode.UnknownProviderOutcome,
            ProviderFailureCategory.ReconciliationRequired => FolderResultCode.ReconciliationRequired,
            ProviderFailureCategory.ProviderRateLimited => FolderResultCode.ProviderRateLimited,
            ProviderFailureCategory.ProviderUnavailable
                or ProviderFailureCategory.ProviderTransientFailure => FolderResultCode.ProviderUnavailable,
            ProviderFailureCategory.ProviderAuthenticationRequired
                or ProviderFailureCategory.ProviderPermissionInsufficient => FolderResultCode.ProviderPermissionInsufficient,
            ProviderFailureCategory.ProviderConflict => FolderResultCode.RepositoryConflict,
            _ => FolderResultCode.ProviderReadinessFailed,
        };

    private static string SafeHash(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
