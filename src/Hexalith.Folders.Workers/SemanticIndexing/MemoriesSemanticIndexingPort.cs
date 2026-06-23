using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;

namespace Hexalith.Folders.Workers.SemanticIndexing;

internal sealed class MemoriesSemanticIndexingPort : ISemanticIndexingPort
{
    private const string IngestedBy = "hexalith-folders-workers";
    private readonly MemoriesClient _memoriesClient;

    public MemoriesSemanticIndexingPort(MemoriesClient memoriesClient)
    {
        ArgumentNullException.ThrowIfNull(memoriesClient);
        _memoriesClient = memoriesClient;
    }

    public async ValueTask<SemanticIndexingResult> IndexFileVersionAsync(
        SemanticIndexingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.ContentBytes is null || request.ContentBytes.Length == 0)
        {
            return new SemanticIndexingResult(SemanticIndexingStatus.Failed, "content_bytes_unavailable", retryable: true);
        }

        try
        {
            Dictionary<string, MetadataField> metadata = CreateMetadata(request);

#pragma warning disable HXL001 // Approved Story 10.3 integration point for Folders worker-side Memories ingestion.
            string workflowInstanceId = await _memoriesClient.IngestAsync(
                FoldersSemanticIndexingDefaults.IndexTenant,
                CaseId(request),
                request.Source.ToUriString(),
                request.ContentBytes,
                request.Content.MediaType,
                IngestedBy,
                metadata,
                cancellationToken).ConfigureAwait(false);
#pragma warning restore HXL001

            return new SemanticIndexingResult(
                SemanticIndexingStatus.Accepted,
                "memories_accepted",
                retryable: false,
                workflowInstanceId: workflowInstanceId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (MemoriesRemoteException exception)
        {
            string reasonCode = string.Equals(exception.Error.Code, "INVALID_RESPONSE", StringComparison.Ordinal)
                ? "memories_invalid_response"
                : "memories_remote_error";
            return new SemanticIndexingResult(SemanticIndexingStatus.Failed, reasonCode, retryable: true);
        }
        catch (HttpRequestException)
        {
            return new SemanticIndexingResult(SemanticIndexingStatus.Failed, "memories_transport_error", retryable: true);
        }
        catch (TaskCanceledException)
        {
            return new SemanticIndexingResult(SemanticIndexingStatus.Failed, "memories_timeout", retryable: true);
        }
        catch (InvalidOperationException)
        {
            return new SemanticIndexingResult(SemanticIndexingStatus.Failed, "memories_invalid_response", retryable: true);
        }
    }

    private static Dictionary<string, MetadataField> CreateMetadata(SemanticIndexingRequest request)
        => new(StringComparer.Ordinal)
        {
            ["folders.managedTenantId"] = Field(request.ManagedTenantId),
            ["folders.organizationId"] = Field(request.OrganizationId),
            ["folders.folderId"] = Field(request.FolderId),
            ["folders.fileVersionId"] = Field(request.FileVersionId),
            ["folders.contentHash"] = Field(request.ContentHash),
            ["folders.contentDescriptor"] = Field(request.Content.IndexingTextDescriptor),
            ["folders.sizeClassification"] = Field(request.Content.SizeClassification),
            ["folders.typeClassification"] = Field(request.Content.TypeClassification),
            ["folders.sensitivityClassification"] = Field(request.Policy.SensitivityClassification),
            ["folders.pathPolicyOutcome"] = Field(request.Policy.PathPolicyOutcome),
            ["folders.correlationId"] = Field(request.CorrelationId),
            ["folders.taskId"] = Field(request.TaskId),
            ["folders.idempotencyKey"] = Field(request.IdempotencyKey),
        };

    private static MetadataField Field(string value)
        => new(value, MetadataOrigin.Human, 1.0f);

    private static string CaseId(SemanticIndexingRequest request)
        => string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{request.ManagedTenantId}:{request.OrganizationId}:{request.FolderId}");
}
