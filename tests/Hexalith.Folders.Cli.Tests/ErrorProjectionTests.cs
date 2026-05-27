using System;

using Hexalith.Folders.Cli.Errors;
using Hexalith.Folders.Client.Generated;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Cli.Tests;

/// <summary>
/// Validates the canonical category→exit-code projection for every member of the SDK
/// <see cref="CanonicalErrorCategory"/> enum. Expected values are restated independently from the parity
/// oracle's <c>outcome_mapping</c> (Story 5.4 proves the map against that oracle).
/// </summary>
/// <remarks>
/// Retained as defense-in-depth (Story 5.4 AC #8): this hand-typed table cross-checks <see cref="ErrorProjection"/>
/// from a source <b>other</b> than the oracle, so an erroneous oracle edit is caught here while an erroneous
/// projection edit is caught by the oracle-driven <see cref="ParityOracleConformanceTests"/>. Keep both.
/// </remarks>
public sealed class ErrorProjectionTests
{
    [Theory]
    [InlineData(CanonicalErrorCategory.Success, 0)]
    [InlineData(CanonicalErrorCategory.Authentication_failure, 65)]
    [InlineData(CanonicalErrorCategory.Client_configuration_error, 64)]
    [InlineData(CanonicalErrorCategory.Credential_missing, 65)]
    [InlineData(CanonicalErrorCategory.Credential_reference_invalid, 65)]
    [InlineData(CanonicalErrorCategory.Tenant_access_denied, 66)]
    [InlineData(CanonicalErrorCategory.Cross_tenant_access_denied, 66)]
    [InlineData(CanonicalErrorCategory.Folder_acl_denied, 66)]
    [InlineData(CanonicalErrorCategory.Audit_access_denied, 66)]
    [InlineData(CanonicalErrorCategory.Workspace_locked, 67)]
    [InlineData(CanonicalErrorCategory.Lock_conflict, 67)]
    [InlineData(CanonicalErrorCategory.Lock_expired, 67)]
    [InlineData(CanonicalErrorCategory.Lock_not_owned, 67)]
    [InlineData(CanonicalErrorCategory.Stale_workspace, 67)]
    [InlineData(CanonicalErrorCategory.Idempotency_conflict, 68)]
    [InlineData(CanonicalErrorCategory.Validation_error, 69)]
    [InlineData(CanonicalErrorCategory.Input_limit_exceeded, 69)]
    [InlineData(CanonicalErrorCategory.Path_validation_failed, 69)]
    [InlineData(CanonicalErrorCategory.Branch_ref_policy_invalid, 69)]
    [InlineData(CanonicalErrorCategory.Response_limit_exceeded, 69)]
    [InlineData(CanonicalErrorCategory.Provider_failure_known, 70)]
    [InlineData(CanonicalErrorCategory.Provider_unavailable, 70)]
    [InlineData(CanonicalErrorCategory.Provider_rate_limited, 70)]
    [InlineData(CanonicalErrorCategory.Provider_readiness_failed, 70)]
    [InlineData(CanonicalErrorCategory.Provider_permission_insufficient, 70)]
    [InlineData(CanonicalErrorCategory.Repository_binding_unavailable, 70)]
    [InlineData(CanonicalErrorCategory.Repository_conflict, 70)]
    [InlineData(CanonicalErrorCategory.Duplicate_binding, 70)]
    [InlineData(CanonicalErrorCategory.Unsupported_provider_capability, 70)]
    [InlineData(CanonicalErrorCategory.Failed_operation, 70)]
    [InlineData(CanonicalErrorCategory.Commit_failed, 70)]
    [InlineData(CanonicalErrorCategory.File_operation_failed, 70)]
    [InlineData(CanonicalErrorCategory.Unknown_provider_outcome, 71)]
    [InlineData(CanonicalErrorCategory.Reconciliation_required, 72)]
    [InlineData(CanonicalErrorCategory.Read_model_unavailable, 72)]
    [InlineData(CanonicalErrorCategory.Projection_stale, 72)]
    [InlineData(CanonicalErrorCategory.Projection_unavailable, 72)]
    [InlineData(CanonicalErrorCategory.Workspace_not_ready, 72)]
    [InlineData(CanonicalErrorCategory.Workspace_preparation_failed, 72)]
    [InlineData(CanonicalErrorCategory.Dirty_workspace, 72)]
    [InlineData(CanonicalErrorCategory.Not_found, 73)]
    [InlineData(CanonicalErrorCategory.Authorization_revocation_detected, 73)]
    [InlineData(CanonicalErrorCategory.State_transition_invalid, 74)]
    [InlineData(CanonicalErrorCategory.Redacted, 75)]
    [InlineData(CanonicalErrorCategory.Query_timeout, 1)]
    [InlineData(CanonicalErrorCategory.Internal_error, 1)]
    public void ProjectsCategoryToCanonicalExitCode(CanonicalErrorCategory category, int expectedExitCode)
        => ErrorProjection.Project(category).ShouldBe(expectedExitCode);

    [Fact]
    public void CategoryAbsentFromOracleFallsThroughToInternalError()
        // range_unsatisfiable is an SDK enum member but has no oracle outcome_mapping row → 1 (drift signal).
        => ErrorProjection.Project(CanonicalErrorCategory.Range_unsatisfiable).ShouldBe(1);

    [Fact]
    public void EveryEnumMemberHasACoveredProjection()
    {
        foreach (CanonicalErrorCategory category in Enum.GetValues<CanonicalErrorCategory>())
        {
            int exit = ErrorProjection.Project(category);
            exit.ShouldBeOneOf(0, 1, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75);
        }
    }
}
