using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
var manifest = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string,string>>>(File.ReadAllText(args[0]))!;
var documents = new List<object>();
var assemblies = new List<object>();
foreach (string pdb in manifest.Values.SelectMany(group => group.Keys).Where(path => path.EndsWith(".pdb") && (Path.GetFileName(path).StartsWith("Hexalith.") || Path.GetFileName(path).StartsWith("Task36"))).Distinct().Order())
{
    string dll = Path.ChangeExtension(pdb, ".dll");
    string? mvid = null; string? version = null;
    if (File.Exists(dll))
    {
        using var binary = File.OpenRead(dll); using var pe = new PEReader(binary);
        var metadata = pe.GetMetadataReader(); mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ToString(); version = metadata.GetAssemblyDefinition().Version.ToString();
    }
    assemblies.Add(new {dll,pdb,mvid,version});
    using var stream = File.OpenRead(pdb);
    using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
    var reader = provider.GetMetadataReader();
    foreach (var handle in reader.Documents)
    {
        var doc = reader.GetDocument(handle); string path = reader.GetString(doc.Name);
        byte[] expected = reader.GetBlobBytes(doc.Hash); var algorithm = reader.GetGuid(doc.HashAlgorithm);
        bool present = File.Exists(path); string? actual = null;
        if (present)
        {
            byte[] bytes = File.ReadAllBytes(path);
            actual = Convert.ToHexStringLower(algorithm == new Guid("8829d00f-11b8-4213-878b-770e8597ac16") ? SHA256.HashData(bytes) : SHA1.HashData(bytes));
        }
        documents.Add(new {pdb,path,algorithm,expected=Convert.ToHexStringLower(expected),actual,present,matches=present ? actual == Convert.ToHexStringLower(expected) : (bool?)null});
    }
}
Console.WriteLine(JsonSerializer.Serialize(new {assemblies,documents}, new JsonSerializerOptions {WriteIndented=true}));
