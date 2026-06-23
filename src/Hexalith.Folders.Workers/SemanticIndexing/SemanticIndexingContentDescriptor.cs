namespace Hexalith.Folders.Workers.SemanticIndexing;

public sealed record SemanticIndexingContentDescriptor
{
    public SemanticIndexingContentDescriptor(
        string indexingTextDescriptor,
        long lengthBytes,
        string mediaType,
        string sizeClassification,
        string typeClassification)
    {
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(indexingTextDescriptor);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(mediaType);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(sizeClassification);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(typeClassification);

        if (lengthBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lengthBytes), lengthBytes, "Content length must not be negative.");
        }

        IndexingTextDescriptor = indexingTextDescriptor;
        LengthBytes = lengthBytes;
        MediaType = mediaType;
        SizeClassification = sizeClassification;
        TypeClassification = typeClassification;
    }

    public string IndexingTextDescriptor { get; init; }

    public long LengthBytes { get; init; }

    public string MediaType { get; init; }

    public string SizeClassification { get; init; }

    public string TypeClassification { get; init; }
}
