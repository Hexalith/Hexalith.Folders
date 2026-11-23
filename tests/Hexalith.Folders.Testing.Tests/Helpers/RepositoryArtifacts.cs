using System.Text;

namespace Hexalith.Folders.Testing.Tests.Helpers;

/// <summary>Locates and reads the repository artifacts used by the documentation checks.</summary>
internal static class RepositoryArtifacts
{
    private const string SolutionName = "Hexalith.Folders.slnx";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Finds the nearest ancestor (including the starting directory) containing the solution.</summary>
    internal static string FindRoot(string? start = null)
    {
        start ??= AppContext.BaseDirectory;
        for (DirectoryInfo? directory = new(start); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionName)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"Could not find {SolutionName} in '{start}' or its ancestors.");
    }

    /// <summary>Reads a UTF-8 text artifact with an optional byte order mark.</summary>
    internal static string ReadText(string path)
    {
        ReadOnlySpan<byte> bytes = File.ReadAllBytes(path);
        if (bytes.StartsWith(Encoding.UTF8.Preamble))
        {
            bytes = bytes[Encoding.UTF8.Preamble.Length..];
        }

        // NUL is not document text and also identifies unmarked UTF-16/32 ASCII.
        if (bytes.Contains((byte)0))
        {
            throw new InvalidDataException($"Expected UTF-8 text in '{path}', found a NUL byte.");
        }

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidDataException($"Expected UTF-8 text in '{path}', found an invalid byte sequence.");
        }
    }
}
