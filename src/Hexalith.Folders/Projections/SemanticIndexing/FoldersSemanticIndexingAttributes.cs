namespace Hexalith.Folders.Projections.SemanticIndexing;

/// <summary>
/// The metadata-only Memories search-index contract shared between the Folders worker-side producer (which
/// writes these attributes on every <c>SearchIndexEntryChanged</c>) and the Story 10.5 Folders Server query
/// facade (which filters and re-checks on them). Centralizing the keys and values keeps producer and consumer
/// in lockstep: a drift between the written attribute key and the filtered key would silently break the
/// tenant security-trim that is the load-bearing isolation control on the shared <c>folders-index</c> tenant.
/// Keys and values are stable, ordinal, and lowercase; they are part of the wire contract and changing one
/// requires updating both the producer and the facade together.
/// </summary>
public static class FoldersSemanticIndexingAttributes
{
    /// <summary>
    /// The single physical Memories tenant under which every Folders managed tenant's units are indexed. This
    /// shared index does NOT isolate Folders managed tenants by itself — the facade MUST security-trim every hit
    /// by the caller's authoritative <c>(managedTenantId, organizationId, folderId)</c>.
    /// </summary>
    public const string IndexTenant = "folders-index";

    /// <summary>The syntactic (BM25) search axis the Folders query facade queries (not the RAG/semantic path).</summary>
    public const string SearchAxis = "syntactic";

    /// <summary>Exact-match attribute carrying the authoritative managed tenant id; the primary security-trim key.</summary>
    public const string ManagedTenantIdAttribute = "folders.managedTenantId";

    /// <summary>Exact-match attribute carrying the organization id.</summary>
    public const string OrganizationIdAttribute = "folders.organizationId";

    /// <summary>Exact-match attribute carrying the folder id.</summary>
    public const string FolderIdAttribute = "folders.folderId";

    /// <summary>Exact-match attribute carrying the workspace id.</summary>
    public const string WorkspaceIdAttribute = "folders.workspaceId";

    /// <summary>Exact-match attribute carrying the opaque file-version id.</summary>
    public const string FileVersionIdAttribute = "folders.fileVersionId";

    /// <summary>
    /// Lifecycle status attribute: <see cref="StatusActive"/> on the live upsert, <see cref="StatusArchived"/> on
    /// the archive soft-delete re-send. The facade filters <see cref="StatusActive"/> to exclude soft-deleted units
    /// without re-evaluating folder state (Story 10.4 enabler for Story 10.5).
    /// </summary>
    public const string StatusAttribute = "folders.status";

    /// <summary>The <see cref="StatusAttribute"/> value on the live (non-archived) upsert path.</summary>
    public const string StatusActive = "active";

    /// <summary>The <see cref="StatusAttribute"/> value on the archive soft-delete path.</summary>
    public const string StatusArchived = "archived";
}
