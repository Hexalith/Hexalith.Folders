using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Records mapped managed modules from the task-owned Linux processes without observing private configuration.</summary>
internal static class FolderDurabilityRuntimeBinding
{
    /// <summary>Records actual mapped modules from the specified launcher and its owned managed descendants.</summary>
    /// <param name="resource">The task-owned Aspire resource name.</param>
    /// <param name="processId">The resource launcher process identity.</param>
    /// <param name="observe">The retained evidence sink, or console output when omitted.</param>
    internal static void Record(string resource, int processId, Action<string>? observe = null)
    {
        if (!TryRecord(resource, processId, observe))
            throw new InvalidOperationException("The task-owned launcher has no descendant mapping the expected host assembly.");
    }

    /// <summary>Waits for actual managed mappings within the caller's existing lifecycle cancellation budget.</summary>
    /// <param name="resource">The task-owned Aspire resource name.</param>
    /// <param name="processId">The resource launcher process identity.</param>
    /// <param name="cancellationToken">The existing bounded resource lifecycle token.</param>
    /// <returns>A task that completes after the expected host is observed.</returns>
    internal static async Task RecordWhenReadyAsync(string resource, int processId, CancellationToken cancellationToken)
    {
        while (!TryRecord(resource, processId, null))
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryRecord(string resource, int processId, Action<string>? observe)
    {
        if (!OperatingSystem.IsLinux()) return true;
        string project = resource switch
        {
            "folders" => "Hexalith.Folders.Server",
            "eventstore" => "Hexalith.Folders.EventStore",
            _ => "Hexalith.Folders.Workers",
        };
        string expected = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src", project,
            "bin/Debug/net10.0", project + ".dll"));
        bool observedHost = false;
        foreach (int managedProcessId in DescendantsAndSelf(processId))
        {
            string[] paths;
            try
            {
                paths = File.ReadLines($"/proc/{managedProcessId}/maps")
                    .Select(static line => line.IndexOf('/') is int start && start >= 0 ? line[start..] : string.Empty)
                    .Where(static path => path.EndsWith(".dll", StringComparison.Ordinal) && Path.GetFileName(path).StartsWith("Hexalith.", StringComparison.Ordinal))
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            }
            catch (IOException) { continue; }
            if (!paths.Contains(expected, StringComparer.Ordinal)) continue;
            observedHost = true;
            foreach (string path in paths)
            {
                using FileStream stream = File.OpenRead(path);
                using PEReader pe = new(stream);
                MetadataReader metadata = pe.GetMetadataReader();
                string pdb = Path.ChangeExtension(path, ".pdb");
                (observe ?? Console.WriteLine)("RUNTIME_BINDING " + JsonSerializer.Serialize(new
                {
                    resource,
                    launcherProcessId = processId,
                    processId = managedProcessId,
                    path,
                    sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
                    mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid),
                    pdbPath = File.Exists(pdb) ? pdb : null,
                    pdbSha256 = File.Exists(pdb) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pdb))) : null,
                }));
            }
        }
        return observedHost;
    }

    private static IEnumerable<int> DescendantsAndSelf(int root)
    {
        Stack<int> pending = new([root]);
        HashSet<int> seen = [];
        while (pending.TryPop(out int current))
        {
            if (!seen.Add(current)) continue;
            yield return current;
            string children;
            try { children = File.ReadAllText($"/proc/{current}/task/{current}/children"); }
            catch (IOException) { continue; }
            foreach (string child in children.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                pending.Push(int.Parse(child, CultureInfo.InvariantCulture));
        }
    }
}
