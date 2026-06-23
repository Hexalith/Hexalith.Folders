namespace Hexalith.Folders.Workers.SemanticIndexing;

public sealed record SemanticIndexingRequest
{
    public SemanticIndexingRequest(
        string managedTenantId,
        string organizationId,
        string folderId,
        string fileVersionId,
        string contentHash,
        SemanticIndexingSourceIdentity source,
        SemanticIndexingContentDescriptor content,
        SemanticIndexingPolicyOutcome policy,
        string correlationId,
        string taskId,
        string idempotencyKey,
        byte[]? contentBytes = null)
    {
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(managedTenantId);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(organizationId);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(folderId);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(fileVersionId);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(policy);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(correlationId);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(taskId);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(idempotencyKey);

        ManagedTenantId = managedTenantId;
        OrganizationId = organizationId;
        FolderId = folderId;
        FileVersionId = fileVersionId;
        ContentHash = contentHash;
        Source = source;
        Content = content;
        Policy = policy;
        CorrelationId = correlationId;
        TaskId = taskId;
        IdempotencyKey = idempotencyKey;
        ContentBytes = contentBytes;
    }

    public string ManagedTenantId { get; init; }

    public string OrganizationId { get; init; }

    public string FolderId { get; init; }

    public string FileVersionId { get; init; }

    public string ContentHash { get; init; }

    public SemanticIndexingSourceIdentity Source { get; init; }

    public SemanticIndexingContentDescriptor Content { get; init; }

    public SemanticIndexingPolicyOutcome Policy { get; init; }

    public string CorrelationId { get; init; }

    public string TaskId { get; init; }

    public string IdempotencyKey { get; init; }

    public byte[]? ContentBytes { get; init; }
}
