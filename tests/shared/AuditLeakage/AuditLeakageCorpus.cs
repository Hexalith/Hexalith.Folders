using System.Collections.Frozen;
using System.Text.Json;

namespace Hexalith.Folders.Tests.Shared.AuditLeakage;

/// <summary>Loads the repository's labelled synthetic audit leakage samples.</summary>
internal static class AuditLeakageCorpus
{
    /// <summary>The relative path of the canonical sample fixture.</summary>
    internal const string CorpusFile = "audit-leakage-corpus.json";

    private static readonly Lazy<(IReadOnlyList<AuditLeakageSample> Samples, IReadOnlySet<string> Surfaces)> _corpus = new(Load);

    /// <summary>Gets every declared sample in fixture order.</summary>
    internal static IReadOnlyList<AuditLeakageSample> Samples => _corpus.Value.Samples;

    /// <summary>Gets the fixture's vocabulary for output channels.</summary>
    internal static IReadOnlySet<string> OutputSurfaces => _corpus.Value.Surfaces;

    /// <summary>Resolves a sample by the stable ID used in test data.</summary>
    internal static AuditLeakageSample Get(string id)
        => Samples.FirstOrDefault(sample => string.Equals(sample.Id, id, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"The audit leakage corpus does not declare sample '{id}'.");

    /// <summary>Resolves an audit leakage fixture relative to the test assembly.</summary>
    internal static string FixturePath(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "tests", "fixtures", relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"The test fixture '{relativePath}' could not be located.");
    }

    /// <summary>Reads samples from a document whose input has already been checked by the caller.</summary>
    internal static IReadOnlyList<AuditLeakageSample> ReadSamples(JsonElement root)
        => ReadSamples(root, ReadOutputSurfaces(root));

    private static (IReadOnlyList<AuditLeakageSample> Samples, IReadOnlySet<string> Surfaces) Load()
    {
        using FileStream stream = File.OpenRead(FixturePath(CorpusFile));
        using JsonDocument document = JsonDocument.Parse(stream);
        IReadOnlySet<string> surfaces = ReadOutputSurfaces(document.RootElement);
        return (ReadSamples(document.RootElement, surfaces), surfaces);
    }

    private static IReadOnlySet<string> ReadOutputSurfaces(JsonElement root)
        => ReadRequiredStrings(root, "forbidden_output_surfaces").ToFrozenSet(StringComparer.Ordinal);

    private static IReadOnlyList<AuditLeakageSample> ReadSamples(JsonElement root, IReadOnlySet<string> outputSurfaces)
    {
        List<AuditLeakageSample> samples = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (JsonElement entry in root.GetProperty("sentinel_samples").EnumerateArray())
        {
            string id = ReadRequiredString(entry, "id");
            if (!ids.Add(id))
            {
                throw new InvalidOperationException($"The audit leakage corpus declares duplicate sample ID '{id}'.");
            }

            string[] forbiddenSurfaces = ReadRequiredStrings(entry, "forbidden_output_surfaces");
            if (forbiddenSurfaces.Length == 0 || forbiddenSurfaces.Any(surface => !outputSurfaces.Contains(surface)))
            {
                throw new InvalidOperationException($"Audit leakage sample '{id}' must declare known forbidden output surfaces.");
            }

            samples.Add(new(
                id,
                ReadRequiredString(entry, "value"),
                ReadRequiredString(entry, "classification"),
                ReadRequiredString(entry, "category"),
                forbiddenSurfaces,
                ReadRequiredStrings(entry, "participates_in")));
        }

        return samples.Count > 0
            ? samples.AsReadOnly()
            : throw new InvalidOperationException("The audit leakage corpus declares no samples.");
    }

    private static string ReadRequiredString(JsonElement entry, string propertyName)
        => RequireText(entry.GetProperty(propertyName), propertyName);

    private static string[] ReadRequiredStrings(JsonElement entry, string propertyName)
        => entry.GetProperty(propertyName).EnumerateArray()
            .Select(value => RequireText(value, propertyName))
            .ToArray();

    private static string RequireText(JsonElement element, string propertyName)
    {
        string? value = element.GetString();
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"The audit leakage corpus has a blank '{propertyName}' entry.");
    }
}
