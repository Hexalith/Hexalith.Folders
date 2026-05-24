namespace Hexalith.Folders.Projections.TenantAccess;

public sealed class FoldersTenantEventOptions
{
    public const string SectionName = "Folders:TenantEvents";

    /// <summary>
    /// Selects the single host allowed to durably write Folders tenant-access projections
    /// from the Tenants event subscription during Server-to-Workers migration.
    /// </summary>
    public FoldersTenantEventProjectionWriter ProjectionWriter { get; set; } = FoldersTenantEventProjectionWriter.Workers;
}
