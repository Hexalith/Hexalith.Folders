using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Observes the two approved binding cases without invoking target members.</summary>
internal static class LoaderProbe
{
    /// <summary>Writes a timestamped observation.</summary>
    private static void Log(string kind, object data) => Console.WriteLine(JsonSerializer.Serialize(
        new { atUtc = DateTimeOffset.UtcNow, kind, data }));

    /// <summary>Identifies actual loaded bytes without creating target attributes.</summary>
    private static object Describe(Assembly assembly) => new
    {
        identity = assembly.FullName,
        location = assembly.Location,
        context = AssemblyLoadContext.GetLoadContext(assembly)?.Name,
        mvid = assembly.ManifestModule.ModuleVersionId,
        sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant(),
    };

    /// <summary>Demands an identity derived from the sealed file, recording actual binding.</summary>
    private static void Demand(string label, string path, bool optional = false)
    {
        string identity = AssemblyName.GetAssemblyName(path).FullName;
        Log("request", new { label, identity, optional, boundPath = path });
        Assembly loaded = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(identity));
        Log("resolved", new { label, optional, assembly = Describe(loaded) });
    }

    /// <summary>Runs a metadata and identity observation only.</summary>
    public static int Main(string[] args)
    {
        string phase = "preflight";
        try
        {
            if (args.Length != 2 || (args[1] != "cold" && args[1] != "host-first"))
            {
                throw new ArgumentException("Expected absolute model path and cold|host-first.");
            }

            string modelPath = Path.GetFullPath(args[0]);
            string modelDirectory = Path.GetDirectoryName(modelPath)!;
            string hostDirectory = AppContext.BaseDirectory;
            AssemblyName boundEventStore = AssemblyName.GetAssemblyName(Path.Combine(modelDirectory, "Hexalith.EventStore.Aspire.dll"));
            if (boundEventStore.Version != new Version(3, 128, 0, 0))
            {
                throw new InvalidOperationException("The diagnostic requires source EventStore 3.128.0.0: " + boundEventStore.FullName);
            }
            string tpa = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "";
            Log("context", new
            {
                args,
                framework = RuntimeInformation.FrameworkDescription,
                coreLibrary = typeof(object).Assembly.Location,
                depsFiles = AppContext.GetData("APP_CONTEXT_DEPS_FILES"),
                trustedPlatformAssemblies = tpa,
                baseDirectory = hostDirectory,
                initiallyLoaded = AssemblyLoadContext.Default.Assemblies.Select(Describe).ToArray(),
            });
            if (RuntimeInformation.FrameworkDescription != ".NET 10.0.13"
                || tpa.Split(Path.PathSeparator).Where(p => p.Contains("/shared/Microsoft."))
                    .Any(p => !p.Contains("/10.0.13/")))
            {
                throw new InvalidOperationException("Unexpected runtime/framework selection.");
            }

            AppDomain.CurrentDomain.AssemblyLoad += (_, e) => Log("assembly-load", Describe(e.LoadedAssembly));
            // The current RunTopology.ComposePlatform resolver, with observation added.
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                string dependency = Path.Combine(modelDirectory, name.Name + ".dll");
                Log("resolving", new { requested = name.FullName, dependency, present = File.Exists(dependency) });
                return File.Exists(dependency) ? context.LoadFromAssemblyPath(dependency) : null;
            };

            phase = "host-baseline";
            Demand("host Aspire", Path.Combine(hostDirectory, "Aspire.Hosting.dll"));
            Demand("matching source Tooling", Path.Combine(hostDirectory, "Hexalith.Builds.Tooling.dll"));
            if (args[1] == "host-first")
            {
                Demand("deliberate host-first EventStore", Path.Combine(hostDirectory, "Hexalith.EventStore.Aspire.dll"));
            }

            phase = "model-load-and-ComposeRun-metadata";
            Assembly model = AssemblyLoadContext.Default.LoadFromAssemblyPath(modelPath);
            Log("model", Describe(model));
            Type modelType = model.GetType("Hexalith.Platform.Composition.PlatformAppModel", throwOnError: true)!;
            MethodInfo compose = modelType.GetMethod("ComposeRun", BindingFlags.Public | BindingFlags.Static)
                ?? throw new MissingMethodException("ComposeRun");
            string[] signature = compose.GetParameters().Select(parameter => parameter.ParameterType.FullName!).ToArray();
            string[] expected = ["Aspire.Hosting.IDistributedApplicationBuilder", "Hexalith.Builds.Tooling.Runtime.CompositionRunPlan", "System.String", "System.String"];
            if (compose.ReturnType != typeof(void) || compose.IsGenericMethod || !signature.SequenceEqual(expected))
            {
                throw new MissingMethodException("Unexpected public static void ComposeRun signature: " + string.Join(",", signature));
            }

            Log("method-metadata-only", new { compose.Name, compose.MetadataToken, module = compose.Module.Name,
                signature, returnType = compose.ReturnType.FullName,
                references = model.GetReferencedAssemblies().Select(name => name.FullName).ToArray() });

            phase = "bound-Redis-demands";
            Demand("host Tooling Redis", Path.Combine(hostDirectory, "StackExchange.Redis.dll"));
            string modelRedis = Path.Combine(modelDirectory, "StackExchange.Redis.dll");
            Demand("model transitive Redis; no direct model AssemblyRef; use sealed host bytes when model copy is absent",
                File.Exists(modelRedis) ? modelRedis : Path.Combine(hostDirectory, "StackExchange.Redis.dll"));
            phase = "optional-ServiceDefaults-demand";
            Demand("optional ServiceDefaults; no claim of Parties execution", Path.Combine(modelDirectory, "Hexalith.EventStore.ServiceDefaults.dll"), optional: true);
            phase = "model-EventStore-demand";
            Demand("model EventStore Aspire", Path.Combine(modelDirectory, "Hexalith.EventStore.Aspire.dll"));
            Log("complete", new { bindingObservationOnly = true, explicitTargetMemberInvocations = 0,
                initializerExecutionNotExcluded = true });
            return 0;
        }
        catch (Exception exception)
        {
            Log("failure", new { phase, exception = exception.ToString(), exception.HResult });
            return 10;
        }
    }
}
