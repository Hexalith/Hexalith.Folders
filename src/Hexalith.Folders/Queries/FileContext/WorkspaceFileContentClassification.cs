using System.Text;

namespace Hexalith.Folders.Queries.FileContext;

/// <summary>Classifies exact bytes without repairing, transcoding, stripping or exposing content.</summary>
public static class WorkspaceFileContentClassification
{
    /// <summary>Tests strict UTF-8, the single optional initial BOM and OQ2 permitted controls.</summary>
    public static bool IsContentReadable(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > 1048576) return false;
        try
        {
            string text = new UTF8Encoding(false, true).GetString(bytes);
            if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];
            return text.All(character => character != '\uFEFF'
                && (!char.IsControl(character) || character is '\t' or '\r' or '\n'));
        }
        catch (DecoderFallbackException) { return false; }
    }
}
