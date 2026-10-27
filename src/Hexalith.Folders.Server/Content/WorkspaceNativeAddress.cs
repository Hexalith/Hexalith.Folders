namespace Hexalith.Folders.Server.Content;

/// <summary>Resolves the fixed public SHA256 address into a separator-free owner aggregate/workspace address.</summary>
internal static class WorkspaceNativeAddress
{
    internal static string Resolve(string value, string prefix)
        => value.StartsWith(prefix + ":", StringComparison.Ordinal)
            && value.Length == prefix.Length + 65 && value.AsSpan(prefix.Length + 1).IndexOfAnyExcept("0123456789abcdef") < 0
                ? prefix + "_" + value[(prefix.Length + 1)..] : value;
    internal static bool IsSdkKey(string? value) => value is { Length: 71 }
        && value.StartsWith("sha256:", StringComparison.Ordinal) && value.AsSpan(7).IndexOfAnyExcept("0123456789abcdef") < 0;
}
