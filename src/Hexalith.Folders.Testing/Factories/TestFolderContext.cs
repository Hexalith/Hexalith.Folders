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
}
