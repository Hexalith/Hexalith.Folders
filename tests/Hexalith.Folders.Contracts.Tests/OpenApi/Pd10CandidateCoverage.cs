using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

/// <summary>Independently discovers conservative local runtime dependencies of the PD10 transport boundaries.</summary>
internal static class Pd10CandidateCoverage
{
    private static readonly string[] BoundaryRoots =
    [
        "src/Hexalith.Folders.Server/Program.cs",
        "src/Hexalith.Folders.Server/FoldersServerHostComposition.cs",
        "src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs",
        "src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs",
        "src/Hexalith.Folders.Server/Pd10V2CandidateCompatibilitySeam.cs",
        "src/Hexalith.Folders.Server/Authorization/Pd10ProtectedOperationCatalog.cs",
        "src/Hexalith.Folders/Queries/FileContext/WorkspaceFileContextQueryHandler.cs",
        "src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationService.cs",
        "src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs",
        "src/Hexalith.Folders.Cli/Commands/CommandPipeline.cs",
        "src/Hexalith.Folders.Mcp/Tooling/ToolPipeline.cs",
        "src/Hexalith.Folders.UI/Services/ConsoleErrorPresenter.cs",
    ];

    /// <summary>Finds runtime types transitively, retaining every same-name declaration and partial file.</summary>
    internal static HashSet<string> RequiredPaths(string repositoryRoot)
    {
        Dictionary<string, string> sources = Directory.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .ToDictionary(path => Relative(repositoryRoot, path), File.ReadAllText, StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> declarations = new(StringComparer.Ordinal);
        foreach ((string path, string source) in sources)
        {
            foreach (Match match in Regex.Matches(source, @"\b(?:class|interface|enum|struct|record(?:\s+(?:class|struct))?)\s+([A-Za-z_][A-Za-z0-9_]*)"))
            {
                string name = match.Groups[1].Value;
                if (!declarations.TryGetValue(name, out HashSet<string>? paths))
                {
                    declarations[name] = paths = new(StringComparer.Ordinal);
                }
                paths.Add(path);
            }
        }

        HashSet<string> required = new(BoundaryRoots, StringComparer.Ordinal);
        Queue<string> pending = new(BoundaryRoots);
        while (pending.TryDequeue(out string? path))
        {
            if (!sources.TryGetValue(path, out string? source))
            {
                throw new InvalidOperationException($"Missing PD10 runtime boundary: {path}");
            }
            // Identifiers in comments/strings and namespace collisions deliberately overapproximate.
            // No namespace ambiguity or partial extraction is silently discarded.
            foreach (Match token in Regex.Matches(source, @"\b[A-Za-z_][A-Za-z0-9_]*\b"))
            {
                if (!declarations.TryGetValue(token.Value, out HashSet<string>? dependencies)) continue;
                foreach (string dependency in dependencies)
                {
                    if (required.Add(dependency)) pending.Enqueue(dependency);
                }
            }
        }

        foreach (string project in Directory.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories))
        {
            string directory = Path.GetDirectoryName(project)!;
            string relativeDirectory = Relative(repositoryRoot, directory) + "/";
            if (!required.Any(path => path.StartsWith(relativeDirectory, StringComparison.Ordinal))) continue;
            required.Add(Relative(repositoryRoot, project));
            // Linked runtime sources are compilation inputs even if they contain no referenced type
            // (for example assembly attributes or extension methods).
            foreach (XElement compile in XDocument.Load(project).Descendants("Compile"))
            {
                string? include = compile.Attribute("Include")?.Value.Replace('\\', '/');
                if (include is null || include.Contains("$(", StringComparison.Ordinal)) continue;
                string full = Path.GetFullPath(Path.Combine(directory, include));
                string? parent = Path.GetDirectoryName(full);
                if (parent is null || !Directory.Exists(parent)) continue;
                foreach (string linked in Directory.EnumerateFiles(parent, Path.GetFileName(full)))
                {
                    string relative = Relative(repositoryRoot, linked);
                    if (relative.StartsWith("src/", StringComparison.Ordinal)) required.Add(relative);
                }
            }
        }

        foreach (string directory in new[]
        {
            "src/Hexalith.Folders.Server/Authentication",
            "src/shared/AdapterCore",
            "tests/Hexalith.Folders.Tests/Queries/FileContext",
            "tests/Hexalith.Folders.Server.Tests/Authentication",
            "tests/Hexalith.Folders.IntegrationTests/Routing",
        })
        {
            foreach (string path in Directory.EnumerateFiles(Path.Combine(repositoryRoot, directory), "*.cs", SearchOption.AllDirectories))
                required.Add(Relative(repositoryRoot, path));
        }
        foreach (string directory in new[]
        {
            "src/Hexalith.Folders.Cli", "src/Hexalith.Folders.Mcp", "src/shared",
            "tests/Hexalith.Folders.Contracts.Tests",
        })
        {
            foreach (string path in Directory.EnumerateFiles(Path.Combine(repositoryRoot, directory), "*", SearchOption.AllDirectories))
            {
                string relative = Relative(repositoryRoot, path);
                if (Path.GetExtension(path) is ".cs" or ".csproj"
                    && !relative.Split('/').Any(part => part is "bin" or "obj")) required.Add(relative);
            }
        }
        foreach (string path in Directory.EnumerateFiles(Path.Combine(repositoryRoot, "tests/Hexalith.Folders.Server.Tests"), "WorkspaceContent*.cs"))
            required.Add(Relative(repositoryRoot, path));
        required.UnionWith(new[]
        {
            "tests/Hexalith.Folders.Server.Tests/FileContextEndpointTests.cs",
            "tests/Hexalith.Folders.Contracts.Tests/OpenApi/Pd10CandidateCoverage.cs",
            "tests/Hexalith.Folders.Contracts.Tests/OpenApi/Pd10ConformanceSetTests.cs",
        });
        return required;
    }

    /// <summary>Reports omissions without reading or trusting the generator's allowlist.</summary>
    internal static string[] MissingPaths(string repositoryRoot, IEnumerable<string> inventory)
        => RequiredPaths(repositoryRoot).Except(inventory, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
}
