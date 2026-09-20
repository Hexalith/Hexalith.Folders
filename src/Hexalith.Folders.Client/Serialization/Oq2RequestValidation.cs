using System.Security.Cryptography;
using System.Text.RegularExpressions;

using Hexalith.Folders.Client.Generated;

using Newtonsoft.Json;

namespace Hexalith.Folders.Client.Serialization;

/// <summary>Validates constructed OQ2 request objects immediately before wire serialization.</summary>
internal static class Oq2RequestValidation
{
    private static readonly Regex OpaqueIdentifier = new("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$", RegexOptions.CultureInvariant);
    private static readonly Regex HashReference = new("^hashref_[A-Za-z0-9]{32,96}$", RegexOptions.CultureInvariant);
    private static readonly Regex MediaType = new("^[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]*/[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]*$", RegexOptions.CultureInvariant);
    private const int MaximumBase64Characters = 349528;

    public static void ValidatePath(PathMetadata? path)
    {
        string? normalizedPath = path?.NormalizedPath;
        string? displayName = path?.DisplayName;
        string[] segments = normalizedPath?.Split('/') ?? [];
        if (path is null
            || string.IsNullOrEmpty(normalizedPath)
            || normalizedPath.Length > 500
            || normalizedPath[0] == '/'
            || normalizedPath[^1] == '/'
            || normalizedPath.Contains("//", StringComparison.Ordinal)
            || normalizedPath.Any(static c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or '/'))
            || segments.Any(static segment => segment is "." or ".." || segment.EndsWith(".", StringComparison.Ordinal) || IsReservedPathSegment(segment))
            || string.IsNullOrEmpty(displayName)
            || displayName.Length > 128
            || displayName.Any(static c => c <= 0x1f || c == 0x7f || c is '/' or '\\')
            || path.UnicodeNormalization != PathMetadataUnicodeNormalization.NFC
            || !Enum.IsDefined(path.PathPolicyClass))
        {
            throw new JsonSerializationException("Path metadata is incomplete or noncanonical.");
        }
    }

    private static bool IsReservedPathSegment(string segment)
    {
        string baseName = segment.Split('.', 2)[0];
        return string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase)
            || new[] { "con", "prn", "aux", "nul" }.Contains(baseName, StringComparer.OrdinalIgnoreCase)
            || (baseName.Length == 4
                && (baseName.StartsWith("com", StringComparison.OrdinalIgnoreCase) || baseName.StartsWith("lpt", StringComparison.OrdinalIgnoreCase))
                && baseName[3] is >= '1' and <= '9');
    }

    public static void ValidateMutation(FileMutationRequest request, FileMutationRequestFileOperationKind expectedKind)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.FileOperationKind)
            || !Enum.IsDefined(expectedKind)
            || request.FileOperationKind != expectedKind
            || request.RequestSchemaVersion != "v2"
            || !OpaqueIdentifier.IsMatch(request.OperationId ?? string.Empty))
        {
            throw new JsonSerializationException("File mutation identity or operation kind is noncanonical.");
        }

        ValidatePath(request.PathMetadata);

        if (expectedKind == FileMutationRequestFileOperationKind.Remove)
        {
            if (request.TransportOperation != FileMutationRequestTransportOperation.MetadataOnlyRemoval
                || request.ContentHashReference is not null
                || request.ByteLength is not null
                || request.InlineContent is not null
                || request.StreamDescriptor is not null)
            {
                throw new JsonSerializationException("Remove requests require metadataOnlyRemoval and omit all content evidence.");
            }

            return;
        }

        if (!HashReference.IsMatch(request.ContentHashReference ?? string.Empty)
            || request.ByteLength is not int byteLength
            || byteLength is < 0 or > 1048576)
        {
            throw new JsonSerializationException("File content length/hash evidence is missing or malformed.");
        }

        if (request.TransportOperation == FileMutationRequestTransportOperation.PutFileInline)
        {
            PutFileInline inline = request.InlineContent
                ?? throw new JsonSerializationException("Inline content evidence is required.");
            if (byteLength > 262144
                || request.StreamDescriptor is not null
                || !IsMediaType(inline.MediaType)
                || (inline.ContentMediaType is not null && !IsMediaType(inline.ContentMediaType)))
            {
                throw new JsonSerializationException("Inline content evidence violates its canonical branch.");
            }

            string encodedContent = inline.ContentBytes ?? string.Empty;
            if (!IsCanonicalBase64(encodedContent))
            {
                throw new JsonSerializationException("Inline content evidence is not canonical base64.");
            }

            byte[] content;
            try
            {
                content = Convert.FromBase64String(encodedContent);
            }
            catch (FormatException exception)
            {
                throw new JsonSerializationException("Inline content evidence is not valid base64.", exception);
            }

            string observedHash = "hashref_" + Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            if (content.Length != byteLength || !string.Equals(observedHash, request.ContentHashReference, StringComparison.Ordinal))
            {
                throw new JsonSerializationException("Inline content length/hash evidence is inconsistent.");
            }

            return;
        }

        if (request.TransportOperation == FileMutationRequestTransportOperation.PutFileStream)
        {
            PutFileStream stream = request.StreamDescriptor
                ?? throw new JsonSerializationException("Stream descriptor evidence is required.");
            if (byteLength <= 262144
                || request.InlineContent is not null
                || stream.DeclaredLength != byteLength
                || stream.ObservedLength != byteLength
                || !string.Equals(stream.ObservedContentHashReference, request.ContentHashReference, StringComparison.Ordinal)
                || !OpaqueIdentifier.IsMatch(stream.StagingReference ?? string.Empty)
                || !IsMediaType(stream.MediaType)
                || stream.UploadMode != PutFileStreamUploadMode.Request_body_stream)
            {
                throw new JsonSerializationException("Stream descriptor evidence violates its canonical branch.");
            }

            return;
        }

        throw new JsonSerializationException("Upload transportOperation is noncanonical.");
    }

    private static bool IsMediaType(string? value) =>
        value is { Length: > 0 and <= 128 } && MediaType.IsMatch(value);

    private static bool IsCanonicalBase64(string value)
    {
        if (value.Length > MaximumBase64Characters || (value.Length & 3) != 0)
        {
            return false;
        }

        Span<byte> decoded = value.Length <= 1024
            ? stackalloc byte[(value.Length / 4) * 3]
            : new byte[(value.Length / 4) * 3];
        if (!Convert.TryFromBase64Chars(value, decoded, out int bytesWritten))
        {
            return false;
        }

        return Convert.ToBase64String(decoded[..bytesWritten]) == value;
    }
}
