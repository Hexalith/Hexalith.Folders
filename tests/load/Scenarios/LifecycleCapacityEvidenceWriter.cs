using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using NBomber.CSharp;

namespace Hexalith.Folders.LoadTests.Scenarios;

public static class LifecycleCapacityEvidenceWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static string Write(
        string reportFolder,
        LifecycleCapacityProfile profile,
        LifecycleCapacityRunRecorder recorder,
        IReadOnlyList<string> scenarioNames,
        IReadOnlyList<string> reportPaths,
        string runId,
        DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportFolder);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(scenarioNames);
        ArgumentNullException.ThrowIfNull(reportPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        Directory.CreateDirectory(reportFolder);
        string path = Path.Combine(reportFolder, "lifecycle-capacity-evidence.json");
        CapacityEvidence evidence = new(
            RunId: runId,
            UtcTimestamp: timestamp,
            GitCommit: ResolveGitCommit(),
            TargetFramework: AppContext.TargetFrameworkName ?? "unknown",
            NBomberVersion: typeof(NBomberRunner).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(NBomberRunner).Assembly.GetName().Version?.ToString()
                ?? "unknown",
            ScenarioNames: scenarioNames,
            ProfileName: profile.Name,
            Dimensions: profile.Dimensions(),
            LoadSimulations: [profile.LoadSimulation()],
            Thresholds: "reference_pending",
            ResultArtifactPaths: reportPaths.Select(SafeArtifactPath).ToArray(),
            ObservedCounts: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["tenant_count"] = recorder.TenantCount,
                ["folder_count"] = recorder.FolderCount,
                ["workspace_count"] = recorder.WorkspaceCount,
                ["task_count"] = recorder.TaskCount,
                ["operation_count"] = recorder.OperationCount,
                ["idempotency_key_count"] = recorder.IdempotencyKeyCount,
            },
            ResultCodes: recorder.ResultCodes);

        File.WriteAllText(path, JsonSerializer.Serialize(evidence, JsonOptions));
        return path;
    }

    public static CapacityEvidenceShape CreateShapeForSelfCheck(LifecycleCapacityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new(
            Thresholds: "reference_pending",
            HasTenantDimensions: profile.Dimensions().ContainsKey("tenant_count"),
            HasLoadSimulation: profile.LoadSimulation().ContainsKey("duration_seconds"),
            HasNoFinalCapacityClaims: true);
    }

    private static string ResolveGitCommit()
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo("git", "rev-parse HEAD")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };

        try
        {
            process.Start();
            string value = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(2000);
            return process.ExitCode == 0 && value.Length > 0 ? value : "unknown";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return "unknown";
        }
    }

    private static string SafeArtifactPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.IsPathFullyQualified(path)
            ? Path.GetFileName(path)
            : path.Replace('\\', '/');
    }

    private sealed record CapacityEvidence(
        string RunId,
        DateTimeOffset UtcTimestamp,
        string GitCommit,
        string TargetFramework,
        string NBomberVersion,
        IReadOnlyList<string> ScenarioNames,
        string ProfileName,
        IReadOnlyDictionary<string, object> Dimensions,
        IReadOnlyList<IReadOnlyDictionary<string, object>> LoadSimulations,
        string Thresholds,
        IReadOnlyList<string> ResultArtifactPaths,
        IReadOnlyDictionary<string, object> ObservedCounts,
        IReadOnlyDictionary<string, int> ResultCodes);
}

public sealed record CapacityEvidenceShape(
    string Thresholds,
    bool HasTenantDimensions,
    bool HasLoadSimulation,
    bool HasNoFinalCapacityClaims);
