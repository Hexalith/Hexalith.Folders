// Proposed experiment only. This file has not been compiled or executed.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Observes explicit dependency binding without invoking application methods.</summary>
internal static class LoaderProbe
{
    /// <summary>Writes one timestamped observation.</summary>
    private static void Log(string kind, object data) => Console.WriteLine(JsonSerializer.Serialize(
        new { atUtc = DateTimeOffset.UtcNow, kind, data }));

    /// <summary>Describes an actual loaded assembly without instantiating its attributes.</summary>
    private static object Describe(Assembly assembly) => new
    {
        identity = assembly.FullName,
        location = assembly.Location,
        context = AssemblyLoadContext.GetLoadContext(assembly)?.Name,
        mvid = assembly.ManifestModule.ModuleVersionId,
        sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant(),
    };

    /// <summary>Performs only the explicitly named dependency demand.</summary>
    private static Assembly Demand(string label, string identity)
    {
        Log("request", new { label, identity });
        Assembly result = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(identity));
        Log("resolved", new { label, assembly = Describe(result) });
        return result;
    }

    /// <summary>Runs one fresh-context case. Exit 10 means the final EventStore demand failed.</summary>
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
            string tpa = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "";
            Log("context", new
            {
                args,
                framework = RuntimeInformation.FrameworkDescription,
                coreLibrary = typeof(object).Assembly.Location,
                depsFiles = AppContext.GetData("APP_CONTEXT_DEPS_FILES"),
                trustedPlatformAssemblies = tpa,
                appPaths = AppContext.GetData("APP_PATHS"),
                baseDirectory = AppContext.BaseDirectory,
                initiallyLoaded = AssemblyLoadContext.Default.Assemblies.Select(Describe).ToArray(),
            });
            if (RuntimeInformation.FrameworkDescription != ".NET 10.0.13"
                || tpa.Split(Path.PathSeparator).Where(p => p.Contains("/shared/Microsoft."))
                    .Any(p => !p.Contains("/10.0.13/")))
            {
                throw new InvalidOperationException("Unexpected runtime/framework selection.");
            }

            AppDomain.CurrentDomain.AssemblyLoad += (_, e) => Log("assembly-load", Describe(e.LoadedAssembly));
            // Same resolution algorithm as candidate RunTopology.ComposePlatform, with observations added.
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                string dependency = Path.Combine(modelDirectory, name.Name + ".dll");
                Log("resolving", new { requested = name.FullName, dependency, present = File.Exists(dependency) });
                return File.Exists(dependency) ? context.LoadFromAssemblyPath(dependency) : null;
            };

            phase = "host-baseline";
            _ = Demand("host Aspire", "Aspire.Hosting, Version=13.6.3.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
            _ = Demand("shared Tooling", "Hexalith.Builds.Tooling, Version=4.38.0.0, Culture=neutral, PublicKeyToken=null");
            if (args[1] == "host-first")
            {
                _ = Demand("deliberate host-first EventStore", "Hexalith.EventStore.Aspire, Version=3.127.0.0, Culture=neutral, PublicKeyToken=null");
            }

            phase = "model-load-and-method-metadata";
            Assembly model = AssemblyLoadContext.Default.LoadFromAssemblyPath(modelPath);
            Log("model", Describe(model));
            Type modelType = model.GetType("Hexalith.Platform.Composition.PlatformAppModel", throwOnError: true)!;
            MethodInfo compose = modelType.GetMethod("ComposeRun", BindingFlags.Public | BindingFlags.Static)
                ?? throw new MissingMethodException("ComposeRun");
            Log("method-metadata-only", new { compose.Name, compose.MetadataToken, module = compose.Module.Name });

            phase = "explicit-redis-demands";
            _ = Demand("compile-transitive Redis2; not a direct model AssemblyRef", "StackExchange.Redis, Version=2.0.0.0, Culture=neutral, PublicKeyToken=c219ff1ca8c2ce46");
            _ = Demand("Tooling Redis3", "StackExchange.Redis, Version=3.0.0.0, Culture=neutral, PublicKeyToken=c219ff1ca8c2ce46");
            phase = "explicit-optional-ServiceDefaults-demand";
            _ = Demand("optional ServiceDefaults; not proof Parties executes it", "Hexalith.EventStore.ServiceDefaults, Version=3.128.0.0, Culture=neutral, PublicKeyToken=null");

            phase = "final-EventStore3128-demand";
            _ = Demand("model EventStore Aspire", "Hexalith.EventStore.Aspire, Version=3.128.0.0, Culture=neutral, PublicKeyToken=null");
            Log("complete", new { bindingObservationOnly = true, explicitTargetMethodInvocations = 0,
                initializerExecutionNotExcluded = true });
            return 0;
        }
        catch (Exception exception)
        {
            Log("failure", new { phase, exception = exception.ToString(), exception.HResult });
            return phase == "final-EventStore3128-demand" ? 10 : 20;
        }
    }
}
