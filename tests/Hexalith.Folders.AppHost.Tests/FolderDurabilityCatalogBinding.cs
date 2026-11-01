using System.Text.Json;
using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Reads either real SDK commit receipt shape without inventing catalog authority.</summary>
internal static class FolderDurabilityCatalogBinding
{
    public static RouteCatalogGenerationReadback Read(ReadOnlyMemory<byte> receiptBytes, string expectedRoot)
    {
        using JsonDocument receipt = JsonDocument.Parse(receiptBytes);
        JsonElement value = receipt.RootElement.TryGetProperty("readback", out JsonElement resumed) ? resumed : receipt.RootElement;
        var readback = value.Deserialize<RouteCatalogGenerationReadback>()
            ?? throw new InvalidOperationException("The actual committed receiver readback is unavailable.");
        if (!readback.Serving || readback.RootDigest != expectedRoot || readback.Epoch <= 0 || readback.Generation <= 0
            || readback.Profile != "Development" || readback.ActivationBindingDigest is not { Length: 64 } binding
            || binding.Any(character => !char.IsAsciiHexDigitLower(character)))
            throw new InvalidOperationException("The receiver has not committed the selected catalog and complete peer binding.");
        return readback;
    }
}
