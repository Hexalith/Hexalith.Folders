namespace Hexalith.Folders.Client.Generated;

/// <summary>Configures generated response handling required for closed problem-envelope validation.</summary>
public partial class Client
{
    partial void Initialize()
    {
        // Exact OQ2/PD10 union projection must validate the original closed JSON object, including
        // unexpected members and duplicate-name rejection; the stream-only NSwag path discards it.
        ReadResponseAsString = true;
    }

    partial void PrepareRequest(HttpClient client, HttpRequestMessage request, string url)
    {
        _ = ReadResponseAsString;
        Hexalith.Folders.Client.Serialization.HexalithFoldersOperationContext.Set(request.Method.Method, url);
    }
}
