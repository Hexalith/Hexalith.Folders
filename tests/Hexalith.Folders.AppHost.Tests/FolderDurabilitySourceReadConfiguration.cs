using System.Text.Json.Nodes;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Selects only the actual disposable source-reader identities, preserving their metadata-only issuer scopes.</summary>
internal static class FolderDurabilitySourceReadConfiguration
{
    /// <summary>Replaces canonical source clients with each independently credentialed actual physical caller.</summary>
    internal static JsonObject Realm(JsonObject original, IReadOnlyList<string> callers)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(callers);
        if (callers.Count is < 1 or > 8 || callers.Distinct(StringComparer.Ordinal).Count() != callers.Count
            || callers.Any(caller => string.IsNullOrWhiteSpace(caller) || caller != caller.Trim()))
            throw new ArgumentException("Select one bounded unique set of actual source callers.", nameof(callers));
        JsonObject realm = original.DeepClone().AsObject();
        JsonArray clients = realm["clients"]!.AsArray();
        JsonObject template = clients.Single(client => client!["clientId"]!.GetValue<string>() == "folders")!.DeepClone().AsObject();
        for (int index = clients.Count - 1; index >= 0; index--)
            if (clients[index]!["clientId"]!.GetValue<string>() is "folders" or "folders-workers") clients.RemoveAt(index);
        for (int index = 0; index < callers.Count; index++)
        {
            if (clients.Any(client => client!["clientId"]!.GetValue<string>() == callers[index]))
                throw new ArgumentException("A source caller collides with another issuer identity.", nameof(callers));
            JsonObject client = template.DeepClone().AsObject();
            client["clientId"] = callers[index];
            client["name"] = "Isolated source metadata reader " + index;
            client["secret"] = "${HEXALITH_FOLDERS_SOURCE_" + index + "_CLIENT_SECRET}";
            clients.Add(client);
        }
        return realm;
    }
}
