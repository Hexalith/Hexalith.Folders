using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Commands.Models;

/// <summary>The closed AuditMetadataKey vocabulary from the v2 Contract Spine.</summary>
[Description("The AuditMetadataKey vocabulary.")]
[JsonConverter(typeof(JsonStringEnumConverter<AuditMetadataKey>))]
public enum AuditMetadataKey
{
    /// <summary>The tenant_id value.</summary>
    [JsonStringEnumMemberName("tenant_id"), Description("The tenant_id value.")]
    TenantId,

    /// <summary>The folder_id value.</summary>
    [JsonStringEnumMemberName("folder_id"), Description("The folder_id value.")]
    FolderId,

    /// <summary>The acl_entry_id value.</summary>
    [JsonStringEnumMemberName("acl_entry_id"), Description("The acl_entry_id value.")]
    AclEntryId,

    /// <summary>The subject_class value.</summary>
    [JsonStringEnumMemberName("subject_class"), Description("The subject_class value.")]
    SubjectClass,

    /// <summary>The permission_count value.</summary>
    [JsonStringEnumMemberName("permission_count"), Description("The permission_count value.")]
    PermissionCount,

    /// <summary>The external_repository_ref value.</summary>
    [JsonStringEnumMemberName("external_repository_ref"), Description("The external_repository_ref value.")]
    ExternalRepositoryRef,

    /// <summary>The provider_id value.</summary>
    [JsonStringEnumMemberName("provider_id"), Description("The provider_id value.")]
    ProviderId,

    /// <summary>The provider_binding_ref value.</summary>
    [JsonStringEnumMemberName("provider_binding_ref"), Description("The provider_binding_ref value.")]
    ProviderBindingRef,

    /// <summary>The capability_profile_ref value.</summary>
    [JsonStringEnumMemberName("capability_profile_ref"), Description("The capability_profile_ref value.")]
    CapabilityProfileRef,

    /// <summary>The repository_binding_id value.</summary>
    [JsonStringEnumMemberName("repository_binding_id"), Description("The repository_binding_id value.")]
    RepositoryBindingId,

    /// <summary>The branch_ref_policy_ref value.</summary>
    [JsonStringEnumMemberName("branch_ref_policy_ref"), Description("The branch_ref_policy_ref value.")]
    BranchRefPolicyRef,

    /// <summary>The workspace_id value.</summary>
    [JsonStringEnumMemberName("workspace_id"), Description("The workspace_id value.")]
    WorkspaceId,

    /// <summary>The lock_id value.</summary>
    [JsonStringEnumMemberName("lock_id"), Description("The lock_id value.")]
    LockId,

    /// <summary>The task_id value.</summary>
    [JsonStringEnumMemberName("task_id"), Description("The task_id value.")]
    TaskId,

    /// <summary>The operation_id value.</summary>
    [JsonStringEnumMemberName("operation_id"), Description("The operation_id value.")]
    OperationId,

    /// <summary>The correlation_id value.</summary>
    [JsonStringEnumMemberName("correlation_id"), Description("The correlation_id value.")]
    CorrelationId,

    /// <summary>The readiness_status value.</summary>
    [JsonStringEnumMemberName("readiness_status"), Description("The readiness_status value.")]
    ReadinessStatus,

    /// <summary>The lifecycle_state value.</summary>
    [JsonStringEnumMemberName("lifecycle_state"), Description("The lifecycle_state value.")]
    LifecycleState,

    /// <summary>The authorization_outcome value.</summary>
    [JsonStringEnumMemberName("authorization_outcome"), Description("The authorization_outcome value.")]
    AuthorizationOutcome,

    /// <summary>The workspace_state value.</summary>
    [JsonStringEnumMemberName("workspace_state"), Description("The workspace_state value.")]
    WorkspaceState,

    /// <summary>The lock_status value.</summary>
    [JsonStringEnumMemberName("lock_status"), Description("The lock_status value.")]
    LockStatus,

    /// <summary>The lease_status value.</summary>
    [JsonStringEnumMemberName("lease_status"), Description("The lease_status value.")]
    LeaseStatus,

    /// <summary>The lock_ownership_proof value.</summary>
    [JsonStringEnumMemberName("lock_ownership_proof"), Description("The lock_ownership_proof value.")]
    LockOwnershipProof,

    /// <summary>The retryability value.</summary>
    [JsonStringEnumMemberName("retryability"), Description("The retryability value.")]
    Retryability,

    /// <summary>The evidence_timestamp value.</summary>
    [JsonStringEnumMemberName("evidence_timestamp"), Description("The evidence_timestamp value.")]
    EvidenceTimestamp,

    /// <summary>The path_policy_class value.</summary>
    [JsonStringEnumMemberName("path_policy_class"), Description("The path_policy_class value.")]
    PathPolicyClass,

    /// <summary>The content_hash_reference value.</summary>
    [JsonStringEnumMemberName("content_hash_reference"), Description("The content_hash_reference value.")]
    ContentHashReference,

    /// <summary>The changed_path_metadata_digest value.</summary>
    [JsonStringEnumMemberName("changed_path_metadata_digest"), Description("The changed_path_metadata_digest value.")]
    ChangedPathMetadataDigest,

    /// <summary>The author_metadata_reference value.</summary>
    [JsonStringEnumMemberName("author_metadata_reference"), Description("The author_metadata_reference value.")]
    AuthorMetadataReference,

    /// <summary>The commit_message_classification value.</summary>
    [JsonStringEnumMemberName("commit_message_classification"), Description("The commit_message_classification value.")]
    CommitMessageClassification,

    /// <summary>The provider_correlation_reference value.</summary>
    [JsonStringEnumMemberName("provider_correlation_reference"), Description("The provider_correlation_reference value.")]
    ProviderCorrelationReference,

    /// <summary>The reconciliation_id value.</summary>
    [JsonStringEnumMemberName("reconciliation_id"), Description("The reconciliation_id value.")]
    ReconciliationId,

    /// <summary>The byte_count value.</summary>
    [JsonStringEnumMemberName("byte_count"), Description("The byte_count value.")]
    ByteCount,

    /// <summary>The query_family value.</summary>
    [JsonStringEnumMemberName("query_family"), Description("The query_family value.")]
    QueryFamily,

    /// <summary>The result_count value.</summary>
    [JsonStringEnumMemberName("result_count"), Description("The result_count value.")]
    ResultCount,

    /// <summary>The configured_limit value.</summary>
    [JsonStringEnumMemberName("configured_limit"), Description("The configured_limit value.")]
    ConfiguredLimit,

    /// <summary>The actual_count value.</summary>
    [JsonStringEnumMemberName("actual_count"), Description("The actual_count value.")]
    ActualCount,

    /// <summary>The actual_bytes value.</summary>
    [JsonStringEnumMemberName("actual_bytes"), Description("The actual_bytes value.")]
    ActualBytes,

    /// <summary>The elapsed_milliseconds value.</summary>
    [JsonStringEnumMemberName("elapsed_milliseconds"), Description("The elapsed_milliseconds value.")]
    ElapsedMilliseconds,

    /// <summary>The result value.</summary>
    [JsonStringEnumMemberName("result"), Description("The result value.")]
    Result,

    /// <summary>The truncation value.</summary>
    [JsonStringEnumMemberName("truncation"), Description("The truncation value.")]
    Truncation,

    /// <summary>The projection_watermark value.</summary>
    [JsonStringEnumMemberName("projection_watermark"), Description("The projection_watermark value.")]
    ProjectionWatermark,

    /// <summary>The retention_class value.</summary>
    [JsonStringEnumMemberName("retention_class"), Description("The retention_class value.")]
    RetentionClass,

    /// <summary>The sensitive_metadata_tier value.</summary>
    [JsonStringEnumMemberName("sensitive_metadata_tier"), Description("The sensitive_metadata_tier value.")]
    SensitiveMetadataTier,

}
