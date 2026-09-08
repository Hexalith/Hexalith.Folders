using Hexalith.Folders.Aggregates.Folder;

using Microsoft.AspNetCore.Http;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FolderCanonicalErrorMapperTests
{
    [Theory]
    [InlineData(FolderResultCode.ValidationFailed, "validation_error", StatusCodes.Status400BadRequest, false, "no_action")]
    [InlineData(FolderResultCode.TenantAccessDenied, "tenant_access_denied", StatusCodes.Status403Forbidden, false, "no_action")]
    [InlineData(FolderResultCode.FolderAclDenied, "folder_acl_denied", StatusCodes.Status403Forbidden, false, "no_action")]
    [InlineData(FolderResultCode.ProviderReadinessFailed, "provider_readiness_failed", StatusCodes.Status422UnprocessableEntity, false, "contact_operator")]
    [InlineData(FolderResultCode.ProviderUnavailable, "provider_unavailable", StatusCodes.Status503ServiceUnavailable, true, "retry")]
    [InlineData(FolderResultCode.ProviderRateLimited, "provider_rate_limited", StatusCodes.Status429TooManyRequests, true, "retry")]
    [InlineData(FolderResultCode.UnsupportedProviderCapability, "unsupported_provider_capability", StatusCodes.Status422UnprocessableEntity, false, "contact_operator")]
    [InlineData(FolderResultCode.RepositoryConflict, "repository_conflict", StatusCodes.Status409Conflict, false, "no_action")]
    [InlineData(FolderResultCode.LockConflict, "lock_conflict", StatusCodes.Status409Conflict, false, "retry_after_release")]
    [InlineData(FolderResultCode.LockNotOwned, "lock_not_owned", StatusCodes.Status409Conflict, false, "revise_request")]
    [InlineData(FolderResultCode.LockExpired, "lock_expired", StatusCodes.Status410Gone, true, "retry")]
    [InlineData(FolderResultCode.PathPolicyDenied, "path_policy_denied", StatusCodes.Status422UnprocessableEntity, false, "revise_request")]
    [InlineData(FolderResultCode.FileOperationFailed, "file_operation_failed", StatusCodes.Status422UnprocessableEntity, false, "no_action")]
    [InlineData(FolderResultCode.UnknownProviderOutcome, "unknown_provider_outcome", StatusCodes.Status503ServiceUnavailable, false, "wait_for_reconciliation")]
    [InlineData(FolderResultCode.ReconciliationRequired, "reconciliation_required", StatusCodes.Status409Conflict, false, "wait_for_reconciliation")]
    [InlineData(FolderResultCode.IdempotencyConflict, "idempotency_conflict", StatusCodes.Status409Conflict, false, "no_action")]
    [InlineData(FolderResultCode.IdempotencyKeyExpired, "idempotency_key_expired", StatusCodes.Status409Conflict, false, "refresh_state_then_submit_with_new_key")]
    [InlineData(FolderResultCode.StaleProjection, "projection_stale", StatusCodes.Status503ServiceUnavailable, true, "retry")]
    [InlineData(FolderResultCode.UnavailableProjection, "projection_unavailable", StatusCodes.Status503ServiceUnavailable, true, "retry")]
    [InlineData(FolderResultCode.StateTransitionInvalid, "state_transition_invalid", StatusCodes.Status422UnprocessableEntity, false, "revise_request")]
    public void FolderResultCodeShouldMapToCanonicalFailureSurface(
        FolderResultCode code,
        string category,
        int statusCode,
        bool retryable,
        string clientAction)
    {
        string actualCategory = FolderCanonicalErrorMapper.CategoryFor(code);

        actualCategory.ShouldBe(category);
        FolderCanonicalErrorMapper.StatusFor(actualCategory).ShouldBe(statusCode);
        FolderCanonicalErrorMapper.RetryableFor(actualCategory).ShouldBe(retryable);
        FolderCanonicalErrorMapper.ClientActionFor(actualCategory, retryable).ShouldBe(clientAction);

        (int mappedStatus, string mappedCategory, bool mappedRetryable, string mappedClientAction) =
            FolderCanonicalErrorMapper.ForDomain(code);
        mappedStatus.ShouldBe(statusCode);
        mappedCategory.ShouldBe(category);
        mappedRetryable.ShouldBe(retryable);
        mappedClientAction.ShouldBe(clientAction);
    }

    [Fact]
    public void IdempotencyAdmissionUnavailableShouldMapToRetryableServiceUnavailable()
    {
        FolderCanonicalErrorMapper.StatusFor("idempotency_admission_unavailable")
            .ShouldBe(StatusCodes.Status503ServiceUnavailable);
        FolderCanonicalErrorMapper.RetryableFor("idempotency_admission_unavailable").ShouldBeTrue();
        FolderCanonicalErrorMapper.ClientActionFor("idempotency_admission_unavailable", true)
            .ShouldBe("retry_later");
    }
}
