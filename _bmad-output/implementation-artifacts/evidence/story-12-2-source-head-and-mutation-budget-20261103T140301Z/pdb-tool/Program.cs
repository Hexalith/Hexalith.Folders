using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Reads portable PDB and source identities without loading inspected code.</summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        List<object> results = [];
        bool failed = false;
        foreach (string path in JsonSerializer.Deserialize<string[]>(File.ReadAllText(args[0]))!)
        {
            try
            {
                using FileStream peFile = File.OpenRead(path);
                using PEReader pe = new(peFile);
                MetadataReader metadata = pe.GetMetadataReader();
                AssemblyDefinition assembly = metadata.GetAssemblyDefinition();
                string pdbPath = Path.ChangeExtension(path, ".pdb");
                List<object> documents = [];
                List<string> mismatches = [];
                List<string> missing = [];
                bool? pdbGuidMatches = null;
                if (File.Exists(pdbPath))
                {
                    using FileStream pdbFile = File.OpenRead(pdbPath);
                    using MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbStream(pdbFile);
                    MetadataReader pdb = provider.GetMetadataReader();
                    Guid pdbGuid = new BlobContentId(pdb.DebugMetadataHeader!.Id).Guid;
                    var codeView = pe.ReadDebugDirectory().Where(static e => e.Type == DebugDirectoryEntryType.CodeView)
                        .Select(e => pe.ReadCodeViewDebugDirectoryData(e)).ToArray();
                    pdbGuidMatches = codeView.Any(e => e.Guid == pdbGuid);
                    foreach (DocumentHandle handle in pdb.Documents)
                    {
                        Document document = pdb.GetDocument(handle);
                        string name = pdb.GetString(document.Name);
                        byte[] expected = pdb.GetBlobBytes(document.Hash);
                        Guid algorithm = pdb.GetGuid(document.HashAlgorithm);
                        bool? match = null;
                        string? actual = null;
                        if (File.Exists(name))
                        {
                            using FileStream source = File.OpenRead(name);
                            byte[]? digest = algorithm == new Guid("8829d00f-11b8-4213-878b-770e8597ac16")
                                ? SHA256.HashData(source)
                                : algorithm == new Guid("ff1816ec-aa5e-4d10-87f7-6f4963833460") ? SHA1.HashData(source) : null;
                            if (digest is not null)
                            {
                                actual = Convert.ToHexString(digest);
                                match = digest.AsSpan().SequenceEqual(expected);
                                if (match is false) mismatches.Add(name);
                            }
                        }
                        else if (name.StartsWith("/home/administrator/projects/hexalith/folders/", StringComparison.Ordinal)
                            && !name.Contains("/obj/", StringComparison.Ordinal)
                            && !name.EndsWith(".g.cs", StringComparison.Ordinal)
                            && !name.Contains("/.nuget/", StringComparison.Ordinal))
                            missing.Add(name);
                        documents.Add(new { path = name, expected = Convert.ToHexString(expected), actual, match });
                    }
                }
                failed |= pdbGuidMatches is false || mismatches.Count > 0 || missing.Count > 0;
                results.Add(new
                {
                    path,
                    sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
                    assemblyName = metadata.GetString(assembly.Name),
                    version = assembly.Version.ToString(),
                    mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid),
                    pdbPath = File.Exists(pdbPath) ? pdbPath : null,
                    pdbGuidMatches,
                    mismatches,
                    missingOrdinarySource = missing,
                    documents,
                });
            }
            catch (Exception ex)
            {
                failed = true;
                results.Add(new { path, error = ex.GetType().Name, message = ex.Message });
            }
        }
        Console.WriteLine(JsonSerializer.Serialize(results));
        return failed ? 1 : 0;
    }
}

