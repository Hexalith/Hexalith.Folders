using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Convenience;
using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Sample;

/// <summary>
/// Metadata-only, synthetic inputs for one run of the canonical folder lifecycle. None of these values is a
/// secret, credential, raw file content, or local path; the identifiers are opaque references.
/// </summary>
public sealed record FolderLifecycleInputs
{
    /// <summary>Gets the caller-provided task identity (required; never SDK-generated).</summary>
    public required string TaskId { get; init; }

    /// <summary>Gets an optional explicit correlation ID; when blank, the SDK generates a ULID.</summary>
    public string? CorrelationId { get; init; }

    public string FolderId { get; init; } = "folder_01HZY7Z6N7J4Q2X8Y9V0FLD001";

    public string WorkspaceId { get; init; } = "workspace_01HZY7Z6N7J4Q2X8Y9V0WKS001";

    public string ProviderBindingRef { get; init; } = "provider_binding_01HZY7Z6N7J4Q2X8Y9V0PBR001";

    public string RepositoryBindingId { get; init; } = "repository_binding_01HZY7Z6N7J4Q2X8Y9V0RBI001";

    public string FileOperationId { get; init; } = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
}

/// <summary>
/// Drives the canonical Epic 5 "golden flow" through the typed <see cref="IClient"/> and the SDK convenience
/// helpers: configure provider binding → validate readiness → create a repository-backed folder → prepare
/// workspace → lock → add a file via the upload helper → commit → query status → inspect audit.
/// </summary>
/// <remarks>
/// Ordering is contractual: workspace preparation requires a ready repository-backed folder (never a plain
/// <c>CreateFolder</c> result); the lock precedes the file mutation; commit follows staged changes. The sample
/// surfaces returned states truthfully (including replay and provider-outcome states) and emits metadata-only
/// log lines.
/// </remarks>
public sealed class FolderLifecycleSample(IClient client, Action<string>? log = null, ICorrelationIdProvider? correlationIdProvider = null)
{
    private readonly IClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly Action<string> _log = log ?? Console.WriteLine;
    private readonly ICorrelationIdProvider? _correlationIdProvider = correlationIdProvider;

    /// <summary>
    /// Runs the canonical lifecycle end-to-end. Intended to execute against a running AppHost; the steps are
    /// thin wrappers over generated operations plus the convenience helpers.
    /// </summary>
    /// <param name="inputs">Synthetic, metadata-only lifecycle inputs.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task RunAsync(FolderLifecycleInputs inputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        // Sourcing (Adapter Parity Contract): correlation is explicit → provider → SDK ULID; task is caller-only.
        string correlationId = CorrelationAndTaskId.ResolveCorrelationId(inputs.CorrelationId, _correlationIdProvider);
        string taskId = CorrelationAndTaskId.ResolveTaskId(inputs.TaskId);
        _log($"correlation resolved (length={correlationId.Length}); task id provided by caller.");

        // 1) Configure provider binding.
        ConfigureProviderBindingRequest providerBinding = new()
        {
            RequestSchemaVersion = ConfigureProviderBindingRequestRequestSchemaVersion.V1,
            ProviderFamilyRef = "provider_family_github",
            CapabilityProfileRef = "capability_profile_default",
            NonSecretCredentialReference = "credential_reference_01HZY7Z6N7J4Q2X8Y9V0CRD001",
        };
        AcceptedCommand bindingAck = await _client.ConfigureProviderBindingAsync(
            inputs.ProviderBindingRef,
            providerBinding.ComputeIdempotencyHash(inputs.ProviderBindingRef),
            correlationId,
            taskId,
            providerBinding,
            cancellationToken).ConfigureAwait(false);
        _log($"provider binding configured: status={bindingAck.Status}, replay={bindingAck.IdempotentReplay}");

        // 2) Validate provider readiness (non-mutating; no idempotency key).
        ValidateProviderReadinessRequest readinessRequest = new()
        {
            ProviderBindingRef = inputs.ProviderBindingRef,
            RequestedCapability = ProviderCapabilityName.Repository_creation,
        };
        ProviderReadinessConsumer readiness = await _client.ValidateProviderReadinessAsync(
            correlationId,
            x_Hexalith_Freshness: null,
            readinessRequest,
            cancellationToken).ConfigureAwait(false);
        _log($"provider readiness validated: status={readiness.Status}.");

        // 3) Create a repository-backed folder (preparation requires a ready repository-backed folder).
        CreateRepositoryBackedFolderRequest createRequest = new()
        {
            RequestSchemaVersion = CreateRepositoryBackedFolderRequestRequestSchemaVersion.V1,
            FolderId = inputs.FolderId,
            ProviderBindingRef = inputs.ProviderBindingRef,
            RepositoryProfileRef = "repository_profile_01HZY7Z6N7J4Q2X8Y9V0RPF001",
            FolderMetadata = new FolderMetadata
            {
                DisplayName = "Sample Folder",
                MetadataClass = SensitiveMetadataTier.Public_metadata,
            },
            BranchRefPolicy = new BranchRefPolicyRequest
            {
                RequestSchemaVersion = BranchRefPolicyRequestRequestSchemaVersion.V1,
                RepositoryBindingId = inputs.RepositoryBindingId,
                PolicyRef = "branch_ref_default",
                DefaultRef = "branch_ref_main",
                AllowedRefPatterns = ["branch_ref_main"],
                ProtectedRefPatterns = ["branch_ref_release"],
            },
        };
        AcceptedCommand folderAck = await _client.CreateRepositoryBackedFolderAsync(
            createRequest.ComputeIdempotencyHash(),
            correlationId,
            taskId,
            createRequest,
            cancellationToken).ConfigureAwait(false);
        _log($"repository-backed folder created: status={folderAck.Status}, replay={folderAck.IdempotentReplay}");

        // 4) Prepare the workspace on the ready repository-backed folder.
        PrepareWorkspaceRequest prepareRequest = new()
        {
            RequestSchemaVersion = "v1",
            RepositoryBindingId = inputs.RepositoryBindingId,
            BranchRefPolicyRef = "branch_ref_default",
            WorkspacePolicyRef = "workspace_policy_default",
        };
        AcceptedCommand prepareAck = await _client.PrepareWorkspaceAsync(
            inputs.FolderId,
            inputs.WorkspaceId,
            prepareRequest.ComputeIdempotencyHash(inputs.FolderId, inputs.WorkspaceId, taskId),
            correlationId,
            taskId,
            prepareRequest,
            cancellationToken).ConfigureAwait(false);
        _log($"workspace prepared: status={prepareAck.Status}, replay={prepareAck.IdempotentReplay}");

        // 5) Lock the workspace (single active writer) before any file mutation.
        LockWorkspaceRequest lockRequest = new()
        {
            RequestSchemaVersion = "v1",
            LockIntent = LockWorkspaceRequestLockIntent.Exclusive_write,
            RequestedLeaseSeconds = 300,
        };
        AcceptedCommand lockAck = await _client.LockWorkspaceAsync(
            inputs.FolderId,
            inputs.WorkspaceId,
            lockRequest.ComputeIdempotencyHash(inputs.FolderId, inputs.WorkspaceId, taskId),
            correlationId,
            taskId,
            lockRequest,
            cancellationToken).ConfigureAwait(false);
        _log($"workspace locked: status={lockAck.Status}, replay={lockAck.IdempotentReplay}");

        // 6) Add a file via the upload convenience helper (inline transport selected for small content).
        FileUploadDescriptor descriptor = new()
        {
            FolderId = inputs.FolderId,
            WorkspaceId = inputs.WorkspaceId,
            OperationId = inputs.FileOperationId,
            MediaType = "text/markdown",
            PathMetadata = new PathMetadata
            {
                NormalizedPath = "docs/readme.md",
                DisplayName = "readme.md",
                PathPolicyClass = "metadata_only",
                UnicodeNormalization = PathMetadataUnicodeNormalization.NFC,
            },
        };
        byte[] content = Encoding.UTF8.GetBytes("# Sample\nSynthetic authorized request body for the SDK sample.\n");

        // The idempotency key is computed from the canonical request (the supported path); the SDK never
        // auto-generates it. UploadFileAsync re-selects the same inline transport deterministically.
        FileMutationRequest fileMutation = FileUpload.BuildInlineFileMutation(
            content, descriptor.MediaType, descriptor.PathMetadata, descriptor.OperationId);
        string fileIdempotencyKey = FileUpload.ComputeIdempotencyKey(fileMutation, inputs.WorkspaceId, taskId);

        AcceptedCommand fileAck = await _client.UploadFileAsync(
            descriptor, content, fileIdempotencyKey, correlationId, taskId, cancellationToken).ConfigureAwait(false);
        _log($"file added via upload helper: status={fileAck.Status}, replay={fileAck.IdempotentReplay}");

        // 7) Commit the staged changes.
        CommitWorkspaceRequest commitRequest = new()
        {
            RequestSchemaVersion = CommitWorkspaceRequestRequestSchemaVersion.V1,
            OperationId = "01ARZ3NDEKTSV4RRFFQ69G5COM",
            TaskId = taskId,
            BranchRefTarget = "branch_ref_main",
            ChangedPathMetadataDigest = "digest_01HZY7Z6N7J4Q2X8Y9V0DIG001",
            AuthorMetadataReference = "actorref_01HZY7Z6N7J4Q2X8Y9V0ACT001",
            CommitMessageClassification = "routine_update",
        };
        CommitWorkspaceAccepted commitAck = await _client.CommitWorkspaceAsync(
            inputs.FolderId,
            inputs.WorkspaceId,
            commitRequest.ComputeIdempotencyHash(inputs.WorkspaceId, taskId),
            correlationId,
            taskId,
            commitRequest,
            cancellationToken).ConfigureAwait(false);
        // Surface provider-outcome state truthfully (including unknown/reconciliation states).
        _log($"commit accepted: status={commitAck.Status}, providerOutcome={commitAck.ProviderOutcomeState}, replay={commitAck.IdempotentReplay}");

        // 8) Query status/context.
        WorkspaceStatus workspaceStatus = await _client.GetWorkspaceStatusAsync(
            inputs.FolderId, inputs.WorkspaceId, correlationId, x_Hexalith_Freshness: null, cancellationToken).ConfigureAwait(false);
        _log($"workspace status: state={workspaceStatus.CurrentState}");

        FolderLifecycleStatus folderStatus = await _client.GetFolderLifecycleStatusAsync(
            inputs.FolderId, correlationId, x_Hexalith_Freshness: null, cancellationToken).ConfigureAwait(false);
        _log($"folder lifecycle state={folderStatus.LifecycleState}, archived={folderStatus.Archived}");

        // 9) Inspect the audit trail (metadata-only).
        AuditTrailPage auditTrail = await _client.ListAuditTrailAsync(
            inputs.FolderId, correlationId, x_Hexalith_Freshness: null, cursor: null, limit: null, filter: null, cancellationToken).ConfigureAwait(false);
        _log($"audit trail page retrieved (truncated={auditTrail.Page?.IsTruncated ?? false}).");

        _log("canonical lifecycle complete.");
    }
}
