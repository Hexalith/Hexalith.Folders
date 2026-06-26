namespace Hexalith.Folders.Workers.SemanticIndexing;

public sealed record SemanticIndexingContentDescriptor
{
    public SemanticIndexingContentDescriptor(
        string indexingTextDescriptor,
        long lengthBytes,
        string mediaType,
        string sizeClassification,
        string typeClassification,
        string? curatedText = null,
        IReadOnlyDictionary<string, string>? curatedAttributes = null)
    {
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(indexingTextDescriptor);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(mediaType);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(sizeClassification);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(typeClassification);
        string text = string.IsNullOrWhiteSpace(curatedText) ? indexingTextDescriptor : curatedText;
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(text);

        if (lengthBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lengthBytes), lengthBytes, "Content length must not be negative.");
        }

        IndexingTextDescriptor = indexingTextDescriptor;
        LengthBytes = lengthBytes;
        MediaType = mediaType;
        SizeClassification = sizeClassification;
        TypeClassification = typeClassification;
        CuratedText = text;
        CuratedAttributes = curatedAttributes is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(curatedAttributes, StringComparer.Ordinal);
    }

    public string IndexingTextDescriptor { get; init; }

    public long LengthBytes { get; init; }

    public string MediaType { get; init; }

    public string SizeClassification { get; init; }

    public string TypeClassification { get; init; }

    public string CuratedText { get; init; }

    public IReadOnlyDictionary<string, string> CuratedAttributes { get; init; }
}
