using Microsoft.Extensions.Options;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Aggregates.Organization;

/// <summary>
/// Authorizes and applies the organization <see cref="ConfigureProviderBinding"/> command. The provider
/// binding is org-scoped: the owning organization is resolved from authorization evidence (the route only
/// carries the provider-binding reference). See story 8.1 DD4.
/// </summary>
public sealed class ConfigureProviderBindingService(
    LayeredFolderAuthorizationService authorizationService,
    IOrganizationProviderBindingRepository repository,
    TimeProvider? timeProvider = null,
    IOptions<FoldersProviderBindingOptions>? bindingOptions = null)
{
    /// <summary>Action token authorizing provider-binding configuration.</summary>
    public const string ActionToken = "configure_provider_binding";

    private readonly LayeredFolderAuthorizationService _authorizationService =
        authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    private readonly IOrganizationProviderBindingRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// Authorizes and applies the provider-binding configuration request.
    /// </summary>
    /// <param name="request">The configuration request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The accepted result with events, or a typed rejection.</returns>
    public async Task<OrganizationProviderBindingResult> ConfigureAsync(
        ConfigureProviderBindingServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyDictionary<string, string?> clientTenantValues = ClientTenantValues.WithPayloadTenant(
            request.ClientControlledTenantValues,
            request.PayloadTenantId);

        LayeredFolderAuthorizationContext authorizationContext = new(
            request.AuthoritativeTenantId,
            request.PrincipalId,
            ActorSafeIdentifier: request.PrincipalId,
            ActionToken,
            LayeredFolderOperationPolicy.Mutation(),
            request.ClaimTransformEvidence,
            OperationScope: FolderStreamName.ReservedOrganizationPermissionScope,
            request.CorrelationId,
            request.TaskId,
            clientTenantValues,
            request.ClientControlledPrincipalValues);
        LayeredFolderAuthorizationResult authorization = await _authorizationService.AuthorizeAsync(
            authorizationContext,
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAllowed || authorization.AllowedContext is null)
        {
            return OrganizationProviderBindingResult.Rejected(
                MapAuthorization(authorization.Decision.OutcomeCode),
                request.AuthoritativeTenantId,
                organizationId: null,
                request.CorrelationId,
                request.TaskId,
                request.IdempotencyKey);
        }

        LayeredFolderAuthorizationAllowedContext allowed = authorization.AllowedContext;
        if (string.IsNullOrWhiteSpace(allowed.OrganizationId))
        {
            return OrganizationProviderBindingResult.Rejected(
                OrganizationProviderBindingResultCode.MalformedEvidence,
                allowed.AuthoritativeTenantId,
                organizationId: null,
                request.CorrelationId,
                request.TaskId,
                request.IdempotencyKey);
        }

        ProviderBindingProfile? profile = null;
        if (!string.IsNullOrWhiteSpace(request.CapabilityProfileRef)
            && (bindingOptions?.Value.Profiles.TryGetValue(request.CapabilityProfileRef, out profile) != true
                || profile is null || profile.ManagedTenantId != allowed.AuthoritativeTenantId
                || profile.ProviderKind != request.ProviderKind))
            return OrganizationProviderBindingResult.Rejected(OrganizationProviderBindingResultCode.InvalidPolicy,
                allowed.AuthoritativeTenantId, allowed.OrganizationId, request.CorrelationId,
                request.TaskId, request.IdempotencyKey);

        ConfigureProviderBinding command = new(
            allowed.AuthoritativeTenantId,
            allowed.OrganizationId,
            OrganizationAclPrincipalKind.User,
            allowed.ActorSafeIdentifier,
            request.ProviderBindingRef,
            request.ProviderKind,
            request.CredentialReferenceId,
            profile is null ? OrganizationProviderBindingPolicy.Empty : new OrganizationProviderBindingPolicy(
                profile.NamingPolicy.PolicyRef, new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProviderBindingProfileFingerprint.ProfileRefKey] = request.CapabilityProfileRef!,
                    [ProviderBindingProfileFingerprint.FingerprintKey] = ProviderBindingProfileFingerprint.Compute(request.CapabilityProfileRef!, profile),
                }),
            profile is null ? OrganizationProviderBindingPolicy.Empty : new OrganizationProviderBindingPolicy(
                profile.BranchPolicy.PolicyRef, new Dictionary<string, string>(StringComparer.Ordinal)),
            request.CorrelationId,
            request.TaskId,
            request.IdempotencyKey,
            _timeProvider.GetUtcNow(),
            request.PayloadTenantId);

        OrganizationProviderBindingCommandValidationResult validation = OrganizationProviderBindingCommandValidator.Validate(command);
        if (!validation.IsAccepted)
        {
            return OrganizationProviderBindingResult.Rejected(command, validation.Code);
        }

        OrganizationStreamName streamName = _repository.CreateStreamName(command.ManagedTenantId, command.ProviderBindingRef);
        OrganizationState state = _repository.Load(streamName);

        OrganizationProviderBindingResult result = OrganizationAggregate.Handle(state, command);
        if (result.Events.Count == 0)
        {
            return result;
        }

        LayeredFolderAuthorizationResult finalAuthorization = await _authorizationService
            .ReauthorizeMutationAsync(authorizationContext, includeFolderAcl: true, cancellationToken)
            .ConfigureAwait(false);
        if (!finalAuthorization.IsAllowed)
        {
            return OrganizationProviderBindingResult.Rejected(command, MapAuthorization(finalAuthorization.Decision.OutcomeCode));
        }

        OrganizationAclAppendOutcome outcome = _repository.AppendIfFingerprintAbsent(
            streamName,
            command.IdempotencyKey,
            validation.IdempotencyFingerprint,
            result.Events);

        return outcome switch
        {
            OrganizationAclAppendOutcome.Appended => result,
            OrganizationAclAppendOutcome.FingerprintMatched => OrganizationProviderBindingResult.Rejected(command, OrganizationProviderBindingResultCode.AlreadyApplied),
            OrganizationAclAppendOutcome.FingerprintConflict => OrganizationProviderBindingResult.Rejected(command, OrganizationProviderBindingResultCode.IdempotencyConflict),
            _ => OrganizationProviderBindingResult.Rejected(command, OrganizationProviderBindingResultCode.MalformedEvidence),
        };
    }

    private static OrganizationProviderBindingResultCode MapAuthorization(string outcomeCode)
        => outcomeCode switch
        {
            LayeredAuthorizationOutcomeCodes.AuthenticationDenied => OrganizationProviderBindingResultCode.MissingAuthoritativeTenant,
            LayeredAuthorizationOutcomeCodes.ClaimTransformDenied => OrganizationProviderBindingResultCode.TenantAccessDenied,
            LayeredAuthorizationOutcomeCodes.TenantProjectionStale or LayeredAuthorizationOutcomeCodes.FolderAclStale => OrganizationProviderBindingResultCode.StaleProjection,
            LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable or LayeredAuthorizationOutcomeCodes.FolderAclUnavailable => OrganizationProviderBindingResultCode.UnavailableProjection,
            LayeredAuthorizationOutcomeCodes.FolderAclDenied or LayeredAuthorizationOutcomeCodes.SafeNotFound => OrganizationProviderBindingResultCode.MissingPermission,
            LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied or LayeredAuthorizationOutcomeCodes.TenantAccessDenied => OrganizationProviderBindingResultCode.TenantAccessDenied,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied => OrganizationProviderBindingResultCode.UnavailableProjection,
            LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed => OrganizationProviderBindingResultCode.MalformedEvidence,
            _ => OrganizationProviderBindingResultCode.TenantAccessDenied,
        };
}
