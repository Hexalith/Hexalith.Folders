using System.Globalization;
using System.Text;

using Hexalith.Folders.Projections.SemanticIndexing;

namespace Hexalith.Folders.Workers.SemanticIndexing;

internal sealed class MetadataDerivedSemanticIndexingContentMaterializer : ISemanticIndexingContentMaterializer
{
    private const string ContentDescriptor = "metadata-derived";
    private const long SmallContentMaximumBytes = 65536;

    public ValueTask<SemanticIndexingContentMaterializationResult> MaterializeAsync(
        SemanticIndexingContentMaterializationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Identity);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.ExpectedMediaType))
        {
            return ValueTask.FromResult(SemanticIndexingContentMaterializationResult.Unavailable(
                "content_descriptor_unavailable",
                retryable: true));
        }

        string contentType = request.ExpectedMediaType;
        long lengthBytes = request.ObservedByteLength ?? request.ExpectedByteLength ?? 0;
        string sizeClassification = lengthBytes <= SmallContentMaximumBytes ? "small" : "medium";
        string typeClassification = ClassifyContentType(contentType);
        string curatedText = string.Create(
            CultureInfo.InvariantCulture,
            $"{typeClassification} {request.Identity.FileVersionId} {request.Identity.OrganizationId} {request.Identity.FolderId} {sizeClassification}");
        SortedDictionary<string, string> curatedAttributes = new(StringComparer.Ordinal)
        {
            [FoldersSemanticIndexingAttributes.ContentDescriptorAttribute] = ContentDescriptor,
            [FoldersSemanticIndexingAttributes.FileVersionIdAttribute] = request.Identity.FileVersionId,
            [FoldersSemanticIndexingAttributes.FolderIdAttribute] = request.Identity.FolderId,
            [FoldersSemanticIndexingAttributes.ManagedTenantIdAttribute] = request.Identity.ManagedTenantId,
            [FoldersSemanticIndexingAttributes.OrganizationIdAttribute] = request.Identity.OrganizationId,
            [FoldersSemanticIndexingAttributes.SizeClassificationAttribute] = sizeClassification,
            [FoldersSemanticIndexingAttributes.StatusAttribute] = FoldersSemanticIndexingAttributes.StatusActive,
            [FoldersSemanticIndexingAttributes.TypeClassificationAttribute] = typeClassification,
            [FoldersSemanticIndexingAttributes.WorkspaceIdAttribute] = request.Identity.WorkspaceId,
        };

        return ValueTask.FromResult(SemanticIndexingContentMaterializationResult.Available(
            Encoding.UTF8.GetBytes(curatedText),
            contentType,
            lengthBytes,
            ContentDescriptor,
            sizeClassification,
            typeClassification,
            curatedText,
            curatedAttributes));
    }

    private static string ClassifyContentType(string contentType)
    {
        if (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return "text";
        }

        if (string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            return "json";
        }

        if (string.Equals(contentType, "application/xml", StringComparison.OrdinalIgnoreCase))
        {
            return "xml";
        }

        if (string.Equals(contentType, "application/x-yaml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(contentType, "application/yaml", StringComparison.OrdinalIgnoreCase))
        {
            return "yaml";
        }

        if (string.Equals(contentType, "application/markdown", StringComparison.OrdinalIgnoreCase))
        {
            return "markdown";
        }

        return "other";
    }
}
