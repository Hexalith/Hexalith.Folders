using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;

using CancellationTokenSource cancellation = new(TimeSpan.FromMinutes(15));
JsonSerializerOptions json = new() { WriteIndented = true };
if (args[0] == "map")
{
    CompositionProcessResult probe = await CompositionProcess.RunAsync(
        CompositionProcess.CreateStartInfo("dotnet", ["--hexalith-controlled-interception-probe"], AppContext.BaseDirectory, null),
        TimeSpan.FromSeconds(10), cancellation.Token);
    if (probe.ExitCode != 0 || probe.Output.Trim() != "intercepted|certificate=false|serial=1")
        throw new InvalidOperationException("Controlled SDK interception is unavailable: " + probe.Output + probe.ErrorOutput);
    SourceMapping mapping = await WorkspaceRootResolver.ResolveAsync(args[1], WorkspaceMode.Source, cancellation.Token);
    await SourceMappingMaterializer.WriteAsync(mapping, args[2], cancellation.Token);
    await File.WriteAllTextAsync(Path.Combine(args[2], "environment.json"), JsonSerializer.Serialize(SourceMappingMaterializer.Environment(mapping, args[2]), json));
    Console.WriteLine(JsonSerializer.Serialize(mapping, json));
}
else if (args[0] == "production-controls")
{
    System.Diagnostics.ProcessStartInfo start = CompositionProcess.CreateStartInfo("dotnet", [], args[1], null);
    Console.WriteLine(JsonSerializer.Serialize(new { start.FileName, arguments = start.ArgumentList.ToArray(), environment = start.Environment.ToDictionary(pair => pair.Key, pair => pair.Value) }, json));
}
else if (args[0] == "production-model")
{
    SourceMapping mapping = SourceMapping.LoadVerified(args[1], args[2]);
    string assembly = await PlatformManifestCommand.BuildModelAsync(mapping, args[3], DateTimeOffset.UtcNow.AddMinutes(12), cancellation.Token);
    Console.WriteLine(JsonSerializer.Serialize(new { assembly, evidence = "unwrapped production BuildModelAsync; compilation only; no startup/runtime/tool acceptance" }, json));
}
else if (args[0] == "model")
{
    SourceMapping mapping = SourceMapping.LoadVerified(args[1], args[2]);
    string assembly = await PlatformManifestCommand.BuildModelAsync(mapping, args[3], DateTimeOffset.UtcNow.AddMinutes(12), cancellation.Token);
    Console.WriteLine(JsonSerializer.Serialize(new { assembly, evidence = "compilation-only API with qualification SDK controls; no public-command/runtime compatibility credit" }, json));
}
else if (args[0] == "select")
{
    string? original = Environment.GetEnvironmentVariable("HEXALITH_G4_APPHOST");
    try
    {
        Environment.SetEnvironmentVariable("HEXALITH_G4_APPHOST", args[2]);
        CompositionEngineOptions selection = CompositionCommandOptions.Create(args[1]);
        Console.WriteLine(JsonSerializer.Serialize(new { selection.AppHostAssemblyPath, selection.DescriptorChildEntryAssemblyPath }, json));
    }
    finally { Environment.SetEnvironmentVariable("HEXALITH_G4_APPHOST", original); }
}
else if (args[0] == "inspect")
{
    // PEReader reads metadata only. It never loads the inspected application or its dependencies.
    List<object> rows = [];
    foreach (string path in JsonSerializer.Deserialize<string[]>(File.ReadAllText(args[1]))!)
    {
        using FileStream stream = File.OpenRead(path);
        using PEReader pe = new(stream);
        MetadataReader reader = pe.GetMetadataReader();
        AssemblyDefinition definition = reader.GetAssemblyDefinition();
        string Token(byte[] key, bool full) => key.Length == 0 ? "null" : Convert.ToHexStringLower(full ? SHA1.HashData(key)[^8..].Reverse().ToArray() : key);
        string Culture(StringHandle handle) => handle.IsNil || reader.GetString(handle).Length == 0 ? "neutral" : reader.GetString(handle);
        object Identity(string name, Version version, string culture, byte[] key, bool full) => new { name, version = version.ToString(), culture, publicKeyToken = Token(key, full) };
        object identity = Identity(reader.GetString(definition.Name), definition.Version, Culture(definition.Culture), reader.GetBlobBytes(definition.PublicKey), true);
        object[] references = reader.AssemblyReferences.Select(handle =>
        {
            AssemblyReference reference = reader.GetAssemblyReference(handle);
            return Identity(reader.GetString(reference.Name), reference.Version, Culture(reference.Culture), reader.GetBlobBytes(reference.PublicKeyOrToken), (reference.Flags & AssemblyFlags.PublicKey) != 0);
        }).ToArray();
        rows.Add(new { path, identity, references, mvid = reader.GetGuid(reader.GetModuleDefinition().Mvid) });
    }
    Console.WriteLine(JsonSerializer.Serialize(rows, json));
}
else throw new ArgumentException("Unknown diagnostic operation.");
