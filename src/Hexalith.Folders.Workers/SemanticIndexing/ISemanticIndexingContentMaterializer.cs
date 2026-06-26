using Hexalith.Folders.Projections.SemanticIndexing;

namespace Hexalith.Folders.Workers.SemanticIndexing;

public interface ISemanticIndexingContentMaterializer
{
    ValueTask<SemanticIndexingContentMaterializationResult> MaterializeAsync(
        SemanticIndexingContentMaterializationRequest request,
        CancellationToken cancellationToken);
}

public sealed record SemanticIndexingContentMaterializationRequest(
    SemanticIndexingFileVersionIdentity Identity,
    string ContentHashReference,
    string? PathPolicyClass,
    long? ExpectedByteLength,
    string? ExpectedMediaType,
    string? TransportEvidenceKind,
    long? ObservedByteLength,
    string SensitivityClassification,
    string PathPolicyOutcome,
    string CorrelationId,
    string TaskId);

public sealed record SemanticIndexingContentMaterializationResult
{
    private SemanticIndexingContentMaterializationResult(
        SemanticIndexingContentMaterializationStatus status,
        byte[]? contentBytes,
        string? contentType,
        long lengthBytes,
        string reasonCode,
        bool retryable,
        string sizeClassification,
        string typeClassification,
        string? curatedText,
        IReadOnlyDictionary<string, string>? curatedAttributes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(sizeClassification);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeClassification);
        if (lengthBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lengthBytes), lengthBytes, "Content length must not be negative.");
        }

        Status = status;
        ContentBytes = contentBytes;
        ContentType = contentType;
        LengthBytes = lengthBytes;
        ReasonCode = reasonCode;
        Retryable = retryable;
        SizeClassification = sizeClassification;
        TypeClassification = typeClassification;
        CuratedText = curatedText;
        CuratedAttributes = curatedAttributes is null
            ? null
            : new Dictionary<string, string>(curatedAttributes, StringComparer.Ordinal);
    }

    public SemanticIndexingContentMaterializationStatus Status { get; }

    public byte[]? ContentBytes { get; }

    public string? ContentType { get; }

    public long LengthBytes { get; }

    public string ReasonCode { get; }

    public bool Retryable { get; }

    public string SizeClassification { get; }

    public string TypeClassification { get; }

    public string? CuratedText { get; }

    public IReadOnlyDictionary<string, string>? CuratedAttributes { get; }

    public static SemanticIndexingContentMaterializationResult Available(
        byte[] contentBytes,
        string contentType,
        long lengthBytes,
        string reasonCode,
        string sizeClassification,
        string typeClassification)
        => Available(
            contentBytes,
            contentType,
            lengthBytes,
            reasonCode,
            sizeClassification,
            typeClassification,
            reasonCode,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["folders.contentDescriptor"] = reasonCode,
                ["folders.sizeClassification"] = sizeClassification,
                ["folders.typeClassification"] = typeClassification,
            });

    public static SemanticIndexingContentMaterializationResult Available(
        byte[] contentBytes,
        string contentType,
        long lengthBytes,
        string reasonCode,
        string sizeClassification,
        string typeClassification,
        string curatedText,
        IReadOnlyDictionary<string, string> curatedAttributes)
    {
        ArgumentNullException.ThrowIfNull(contentBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(curatedText);
        ArgumentNullException.ThrowIfNull(curatedAttributes);

        return new(
            SemanticIndexingContentMaterializationStatus.Available,
            contentBytes,
            contentType,
            lengthBytes,
            reasonCode,
            retryable: false,
            sizeClassification,
            typeClassification,
            curatedText,
            curatedAttributes);
    }

    public static SemanticIndexingContentMaterializationResult Skipped(string reasonCode, bool retryable)
        => new(
            SemanticIndexingContentMaterializationStatus.Skipped,
            contentBytes: null,
            contentType: null,
            lengthBytes: 0,
            reasonCode,
            retryable,
            sizeClassification: "unknown",
            typeClassification: "unknown",
            curatedText: null,
            curatedAttributes: null);

    public static SemanticIndexingContentMaterializationResult Unavailable(string reasonCode, bool retryable)
        => new(
            SemanticIndexingContentMaterializationStatus.Unavailable,
            contentBytes: null,
            contentType: null,
            lengthBytes: 0,
            reasonCode,
            retryable,
            sizeClassification: "unknown",
            typeClassification: "unknown",
            curatedText: null,
            curatedAttributes: null);
}

public enum SemanticIndexingContentMaterializationStatus
{
    Available,
    Skipped,
    Unavailable,
}
