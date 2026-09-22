using System.Threading;

using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Client.Serialization;

internal static class HexalithFoldersOperationContext
{
    private static readonly AsyncLocal<string?> Operation = new();

    internal static string? Current => Operation.Value;

    internal static void Set(string method, string url)
    {
        string path = Uri.TryCreate(url, UriKind.Absolute, out Uri? absolute)
            ? absolute.AbsolutePath
            : url.Split('?', 2)[0];
        path = "/" + path.TrimStart('/');
        Operation.Value = HexalithFoldersGeneratedOperationCatalog.Routes
            .Where(route => string.Equals(route.Method, method, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(route => Matches(route.Path, path))
            .OperationId;
    }

    private static bool Matches(string template, string path)
    {
        string[] expected = template.Trim('/').Split('/');
        string[] actual = path.Trim('/').Split('/');
        if (expected.Length != actual.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            bool placeholder = expected[index].Length > 2
                && expected[index][0] == '{'
                && expected[index][^1] == '}';
            if (placeholder ? actual[index].Length == 0 : !string.Equals(expected[index], actual[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
