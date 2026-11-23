namespace Hexalith.Folders.Testing.Factories;

public sealed record TestFolderContext
{
    public TestFolderContext(
        string managedTenantId,
        string organizationId,
        string folderId,
        string taskId,
        string correlationId,
        string idempotencyKey)
    {
        ValidateStreamSegment(managedTenantId, nameof(managedTenantId));
        ValidateStreamSegment(organizationId, nameof(organizationId));
        ValidateStreamSegment(folderId, nameof(folderId));
        ValidateHeaderValue(taskId, nameof(taskId));
        ValidateHeaderValue(correlationId, nameof(correlationId));
        ValidateHeaderValue(idempotencyKey, nameof(idempotencyKey));

        ManagedTenantId = managedTenantId;
        OrganizationId = organizationId;
        FolderId = folderId;
        TaskId = taskId;
        CorrelationId = correlationId;
        IdempotencyKey = idempotencyKey;
    }

    public string ManagedTenantId { get; private set; }

    public string OrganizationId { get; private set; }

    public string FolderId { get; private set; }

    public string TaskId { get; private set; }

    public string CorrelationId { get; private set; }

    public string IdempotencyKey { get; private set; }

    public string FolderStreamName => $"{ManagedTenantId}:folders:{FolderId}";

    public string OrganizationStreamName => $"{ManagedTenantId}:organizations:{OrganizationId}";

    /// <summary>
    /// Deconstructs the fixture identifiers in constructor order.
    /// </summary>
    /// <param name="managedTenantId">The managed tenant identifier.</param>
    /// <param name="organizationId">The organization identifier.</param>
    /// <param name="folderId">The folder identifier.</param>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="correlationId">The correlation identifier.</param>
    /// <param name="idempotencyKey">The idempotency key.</param>
    public void Deconstruct(
        out string managedTenantId,
        out string organizationId,
        out string folderId,
        out string taskId,
        out string correlationId,
        out string idempotencyKey)
    {
        managedTenantId = ManagedTenantId;
        organizationId = OrganizationId;
        folderId = FolderId;
        taskId = TaskId;
        correlationId = CorrelationId;
        idempotencyKey = IdempotencyKey;
    }

    private static void ValidateStreamSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Stream name segments must be populated.", parameterName);
        }

        if (value.Any(character => character == ':' || char.IsControl(character)))
        {
            throw new ArgumentException("Stream name segments must not contain ':' or control characters.", parameterName);
        }
    }

    private static void ValidateHeaderValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Header values must be populated.", parameterName);
        }

        if (value.Any(char.IsControl))
        {
            throw new ArgumentException("Header values must not contain control characters.", parameterName);
        }
    }
}
