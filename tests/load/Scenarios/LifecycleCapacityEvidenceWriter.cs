using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
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
            Thresholds: profile.IsReleaseCalibration ? "release_calibrated" : "reference_pending",
            ResultArtifactPaths: reportPaths.Select(SafeArtifactPath).ToArray(),
            MeasuredSteps: recorder.MeasuredSteps,
            ObservedStepCounts: recorder.ObservedStepCounts,
            ObservedCounts: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["tenant_count"] = recorder.TenantCount,
                ["folder_count"] = recorder.FolderCount,
                ["workspace_count"] = recorder.WorkspaceCount,
                ["task_count"] = recorder.TaskCount,
                ["operation_count"] = recorder.OperationCount,
                ["idempotency_key_count"] = recorder.IdempotencyKeyCount,
            },
            ResultCodes: recorder.ResultCodes,
            HardwareProfile: profile.IsReleaseCalibration ? CreateHardwareProfile(profile) : null,
            StepLatencyStatistics: profile.IsReleaseCalibration ? CreateLatencyStatistics(recorder.StepLatencyMilliseconds) : null,
            Throughput: profile.IsReleaseCalibration ? CreateThroughput(profile, recorder) : null,
            FreshnessObservations: profile.IsReleaseCalibration ? CreateFreshnessObservations(recorder.FreshnessLagMilliseconds) : null,
            TargetComparison: profile.IsReleaseCalibration ? CreateTargetComparison(profile, recorder) : null);

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

    private static IReadOnlyDictionary<string, object> CreateHardwareProfile(LifecycleCapacityProfile profile)
        => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runner_profile"] = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true"
                ? "github-actions-ubuntu-latest"
                : "local-hermetic",
            ["target_hardware_profile"] = "github-actions-ubuntu-latest-or-local-hermetic",
            ["processor_count"] = Environment.ProcessorCount,
            ["process_architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["os_architecture"] = RuntimeInformation.OSArchitecture.ToString(),
            ["os_family"] = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux",
            ["profile_name"] = profile.Name,
        };

    private static IReadOnlyDictionary<string, LatencyStatistic> CreateLatencyStatistics(
        IReadOnlyDictionary<string, IReadOnlyList<double>> samples)
        => samples.ToDictionary(
            static pair => pair.Key,
            static pair => LatencyStatistic.FromSamples(pair.Value),
            StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, object> CreateThroughput(
        LifecycleCapacityProfile profile,
        LifecycleCapacityRunRecorder recorder)
    {
        double durationSeconds = Math.Max(1, profile.Duration.TotalSeconds);
        int statusReads = recorder.ObservedStepCounts.TryGetValue(LifecycleCapacityScenario.StatusStepName, out int count) ? count : 0;
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["duration_seconds"] = durationSeconds,
            ["lifecycle_iterations_per_second"] = Math.Round(statusReads / durationSeconds, 3),
            ["operations_per_second"] = Math.Round(recorder.OperationCount / durationSeconds, 3),
            ["measured_status_reads"] = statusReads,
        };
    }

    private static IReadOnlyDictionary<string, FreshnessObservation> CreateFreshnessObservations(
        IReadOnlyDictionary<string, IReadOnlyList<double>> samples)
        => samples.ToDictionary(
            static pair => pair.Key,
            static pair => FreshnessObservation.FromSamples(pair.Value),
            StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, TargetComparison>> CreateTargetComparison(
        LifecycleCapacityProfile profile,
        LifecycleCapacityRunRecorder recorder)
    {
        var c1 = new Dictionary<string, TargetComparison>(StringComparer.Ordinal)
        {
            ["max_concurrent_tenants"] = TargetComparison.AtLeast(profile.TenantCount, recorder.TenantCount, "count"),
            ["folders_per_tenant"] = TargetComparison.AtLeast(profile.FoldersPerTenant, ObservedPerTenant(recorder.FolderCount, recorder.TenantCount), "count"),
            ["active_workspaces_per_tenant"] = TargetComparison.AtLeast(profile.WorkspacesPerTenant, ObservedPerTenant(recorder.WorkspaceCount, recorder.TenantCount), "count"),
            ["concurrent_agent_tasks_per_tenant"] = TargetComparison.AtLeast(profile.TasksPerWorkspace, ObservedPerTenant(recorder.TaskCount, recorder.TenantCount), "count"),
        };

        var c2 = new Dictionary<string, TargetComparison>(StringComparer.Ordinal)
        {
            ["max_commit_to_status_read_freshness_ms"] = TargetComparison.AtMost(
                500,
                ObservedFreshnessP95(recorder.FreshnessLagMilliseconds, "commit_to_status_read_ms"),
                "milliseconds"),
        };

        var c5 = new Dictionary<string, TargetComparison>(StringComparer.Ordinal)
        {
            ["tenant_scale_units"] = TargetComparison.AtLeast(profile.TenantCount, recorder.TenantCount, "count"),
            ["folder_scale_units_per_tenant"] = TargetComparison.AtLeast(profile.FoldersPerTenant, ObservedPerTenant(recorder.FolderCount, recorder.TenantCount), "count"),
            ["workspace_scale_units_per_tenant"] = TargetComparison.AtLeast(profile.WorkspacesPerTenant, ObservedPerTenant(recorder.WorkspaceCount, recorder.TenantCount), "count"),
            ["agent_task_scale_units_per_tenant"] = TargetComparison.AtLeast(profile.TasksPerWorkspace, ObservedPerTenant(recorder.TaskCount, recorder.TenantCount), "count"),
            ["minimum_lifecycle_iterations_per_second"] = TargetComparison.AtLeast(1, Math.Round((recorder.ObservedStepCounts.TryGetValue(LifecycleCapacityScenario.StatusStepName, out int statusReads) ? statusReads : 0) / Math.Max(1, profile.Duration.TotalSeconds), 3), "operations_per_second"),
        };

        return new Dictionary<string, IReadOnlyDictionary<string, TargetComparison>>(StringComparer.Ordinal)
        {
            ["c1"] = c1,
            ["c2"] = c2,
            ["c5"] = c5,
        };
    }

    private static double ObservedPerTenant(int observedCount, int tenantCount)
        => tenantCount <= 0 ? 0 : Math.Floor(observedCount / (double)tenantCount);

    private static double ObservedFreshnessP95(
        IReadOnlyDictionary<string, IReadOnlyList<double>> samples,
        string observationName)
        => samples.TryGetValue(observationName, out IReadOnlyList<double>? values)
            ? FreshnessObservation.FromSamples(values).P95Ms
            : 999_999;

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
        IReadOnlyList<string> MeasuredSteps,
        IReadOnlyDictionary<string, int> ObservedStepCounts,
        IReadOnlyDictionary<string, object> ObservedCounts,
        IReadOnlyDictionary<string, int> ResultCodes,
        IReadOnlyDictionary<string, object>? HardwareProfile,
        IReadOnlyDictionary<string, LatencyStatistic>? StepLatencyStatistics,
        IReadOnlyDictionary<string, object>? Throughput,
        IReadOnlyDictionary<string, FreshnessObservation>? FreshnessObservations,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, TargetComparison>>? TargetComparison);
}

public sealed record LatencyStatistic(int Count, double MinMs, double P50Ms, double P95Ms, double P99Ms, double MaxMs)
{
    public static LatencyStatistic FromSamples(IReadOnlyList<double> samples)
    {
        double[] values = samples.Order().ToArray();
        return values.Length == 0
            ? new(0, 0, 0, 0, 0, 0)
            : new(
                values.Length,
                Math.Round(values[0], 3),
                Percentile(values, 50),
                Percentile(values, 95),
                Percentile(values, 99),
                Math.Round(values[^1], 3));
    }

    private static double Percentile(double[] values, int percentile)
    {
        int index = Math.Clamp((int)Math.Ceiling(values.Length * percentile / 100.0) - 1, 0, values.Length - 1);
        return Math.Round(values[index], 3);
    }
}

public sealed record FreshnessObservation(int Count, double P50Ms, double P95Ms, double P99Ms, double MaxMs, string Scope)
{
    public static FreshnessObservation FromSamples(IReadOnlyList<double> samples)
    {
        LatencyStatistic statistic = LatencyStatistic.FromSamples(samples);
        return new(statistic.Count, statistic.P50Ms, statistic.P95Ms, statistic.P99Ms, statistic.MaxMs, "hermetic_commit_to_status_read");
    }
}

public sealed record TargetComparison(double Target, double Observed, string Units, string Comparator, bool Passed)
{
    public static TargetComparison AtLeast(double target, double observed, string units)
        => new(target, observed, units, "observed_greater_than_or_equal_target", observed >= target);

    public static TargetComparison AtMost(double target, double observed, string units)
        => new(target, observed, units, "observed_less_than_or_equal_target", observed <= target);
}

public sealed record CapacityEvidenceShape(
    string Thresholds,
    bool HasTenantDimensions,
    bool HasLoadSimulation,
    bool HasNoFinalCapacityClaims);
