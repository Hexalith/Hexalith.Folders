using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Records mapped managed modules from the task-owned Linux processes without observing private configuration.</summary>
internal static class FolderDurabilityRuntimeBinding
{
    internal static void Record(string resource, int processId)
    {
        if (!OperatingSystem.IsLinux()) return;
        string[] paths = File.ReadLines($"/proc/{processId}/maps")
            .Select(static line => line.IndexOf('/') is int start && start >= 0 ? line[start..] : string.Empty)
            .Where(static path => path.EndsWith(".dll", StringComparison.Ordinal) && Path.GetFileName(path).StartsWith("Hexalith.", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        foreach (string path in paths)
        {
            using FileStream stream = File.OpenRead(path);
            using PEReader pe = new(stream);
            MetadataReader metadata = pe.GetMetadataReader();
            string pdb = Path.ChangeExtension(path, ".pdb");
            Console.WriteLine("RUNTIME_BINDING " + JsonSerializer.Serialize(new
            {
                resource,
                processId,
                path,
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
                mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid),
                pdbPath = File.Exists(pdb) ? pdb : null,
                pdbSha256 = File.Exists(pdb) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pdb))) : null,
            }));
        }
    }
}
