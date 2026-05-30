using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Queries.Folders;

using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;

namespace Hexalith.Folders.LoadTests.Scenarios;

public static class LifecycleCapacityScenario
{
    public const string FullLifecycleScenarioName = "folder_workspace_full_lifecycle";
    public const string PrepareStepName = "prepare_workspace";
    public const string LockStepName = "acquire_workspace_lock";
    public const string MutateStepName = "mutate_workspace_file";
    public const string CommitStepName = "commit_workspace";
    public const string StatusStepName = "read_workspace_status";

    public static async Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        LifecycleCapacityOptions options = LifecycleCapacityOptions.Parse(args);
        LifecycleCapacityProfile profile = LifecycleCapacityProfile.FromName(options.ProfileName);

        if (options.SelfCheck)
        {
            RunSelfChecks(profile, options.ReportFolder);
            return 0;
        }

        LifecycleCapacityRunRecorder recorder = new();
        string runId = options.RunId ?? $"lifecycle-capacity-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var scenario = Create(profile, recorder);
        var result = NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFolder(options.ReportFolder)
            .WithReportFileName(runId)
            .WithReportFormats(ReportFormat.Txt, ReportFormat.Md)
            .Run();

        SanitizeNbomberLogs(options.ReportFolder);
        IReadOnlyList<string> reportPaths = result.ReportFiles.Select(static file => file.FilePath).ToArray();
        string evidencePath = LifecycleCapacityEvidenceWriter.Write(
            options.ReportFolder,
            profile,
            recorder,
            [FullLifecycleScenarioName],
            reportPaths,
            runId,
            DateTimeOffset.UtcNow);

        Console.WriteLine($"Evidence: {evidencePath}");
        return 0;
    }

    public static ScenarioProps Create(
        LifecycleCapacityProfile profile,
        LifecycleCapacityRunRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(recorder);
        return Scenario.Create(FullLifecycleScenarioName, async context =>
            {
                LifecycleCapacityIteration iteration = profile.CreateIteration(context.InvocationNumber);
                LifecycleCapacityDriver driver = new(iteration, recorder);

                var prepare = await Step.Run<string>(PrepareStepName, context, async () =>
                    ToResponse(await driver.PrepareAsync(context.ScenarioCancellationToken).ConfigureAwait(false))).ConfigureAwait(false);
                if (prepare.IsError)
                {
                    return prepare;
                }

                var lockResult = await Step.Run<string>(LockStepName, context, async () =>
                    ToResponse(await driver.AcquireLockAsync(context.ScenarioCancellationToken).ConfigureAwait(false))).ConfigureAwait(false);
                if (lockResult.IsError)
                {
                    return lockResult;
                }

                var mutation = await Step.Run<string>(MutateStepName, context, async () =>
                    ToResponse(await driver.MutateFileAsync(context.ScenarioCancellationToken).ConfigureAwait(false))).ConfigureAwait(false);
                if (mutation.IsError)
                {
                    return mutation;
                }

                var commit = await Step.Run<string>(CommitStepName, context, async () =>
                    ToResponse(await driver.CommitAsync(context.ScenarioCancellationToken).ConfigureAwait(false))).ConfigureAwait(false);
                if (commit.IsError)
                {
                    return commit;
                }

                return await Step.Run<string>(StatusStepName, context, async () =>
                    ToResponse(await driver.ReadStatusAsync(context.ScenarioCancellationToken).ConfigureAwait(false))).ConfigureAwait(false);
            })
            .WithoutWarmUp()
            .WithRestartIterationOnFail(shouldRestart: false)
            .WithMaxFailCount(1)
            .WithLoadSimulations(
                Simulation.Inject(
                    rate: profile.InjectRate,
                    interval: profile.InjectInterval,
                    during: profile.Duration));
    }

    private static void RunSelfChecks(LifecycleCapacityProfile profile, string reportFolder)
    {
        LifecycleCapacityIteration iteration = profile.CreateIteration(1);
        string[] ids =
        [
            iteration.TenantId,
            iteration.FolderId,
            iteration.WorkspaceId,
            iteration.TaskId,
            iteration.MutationOperationId,
            iteration.PrepareCorrelationId,
            iteration.PrepareIdempotencyKey,
        ];

        if (ids.Any(static id => !SafeOrdinalId.IsSafe(id)))
        {
            throw new InvalidOperationException("Self-check failed: synthetic identifiers are not ordinal-stable safe IDs.");
        }

        if (iteration.PrepareIdempotencyKey == iteration.MutationIdempotencyKey
            || iteration.MutationIdempotencyKey == iteration.CommitIdempotencyKey)
        {
            throw new InvalidOperationException("Self-check failed: mutating operation idempotency keys must be unique.");
        }

        CapacityEvidenceShape shape = LifecycleCapacityEvidenceWriter.CreateShapeForSelfCheck(profile);
        if (shape.Thresholds != "reference_pending"
            || !shape.HasTenantDimensions
            || !shape.HasLoadSimulation
            || !shape.HasNoFinalCapacityClaims)
        {
            throw new InvalidOperationException("Self-check failed: evidence shape does not preserve reference-pending thresholds.");
        }

        string readme = File.ReadAllText(FindLoadReadme());
        if (readme.Contains("--recursive", StringComparison.Ordinal)
            || readme.Contains("submodule update --init --recursive", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Self-check failed: load README contains a recursive submodule command.");
        }

        Directory.CreateDirectory(reportFolder);
        Console.WriteLine("Lifecycle capacity self-checks passed.");
    }

    private static void SanitizeNbomberLogs(string reportFolder)
    {
        if (!Directory.Exists(reportFolder))
        {
            return;
        }

        foreach (string path in Directory.EnumerateFiles(reportFolder, "nbomber-log-*.txt"))
        {
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("Reports folder:", StringComparison.Ordinal)
                    || lines[i].Contains("Reports saved in folder:", StringComparison.Ordinal))
                {
                    int markerIndex = lines[i].IndexOf("Reports", StringComparison.Ordinal);
                    if (markerIndex >= 0)
                    {
                        lines[i] = string.Concat(lines[i].AsSpan(0, markerIndex), "Reports folder: <report-folder>");
                    }
                }
            }

            File.WriteAllLines(path, lines);
        }
    }

    private static Response<string> ToResponse(FolderResultCode code)
        => code == FolderResultCode.Accepted || code == FolderResultCode.Created
            ? Response.Ok(payload: code.ToString(), statusCode: code.ToString())
            : Response.Fail<string>(statusCode: SafeFailureStatus(code));

    private static Response<string> ToResponse(WorkspaceStatusQueryResultCode code)
        => code == WorkspaceStatusQueryResultCode.Allowed
            ? Response.Ok(payload: code.ToString(), statusCode: code.ToString())
            : Response.Fail<string>(statusCode: SafeFailureStatus(code));

    private static string FindLoadReadme()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "README.md");
            if (File.Exists(candidate)
                && string.Equals(directory.Name, "load", StringComparison.Ordinal))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate tests/load/README.md from the load harness output directory.");
    }

    private static string SafeFailureStatus(FolderResultCode code)
        => code switch
        {
            FolderResultCode.ProviderUnavailable => "provider_unavailable",
            FolderResultCode.ProviderRateLimited => "provider_rate_limited",
            FolderResultCode.IdempotentReplay => "idempotent_replay",
            FolderResultCode.IdempotencyConflict => "idempotency_conflict",
            FolderResultCode.StateTransitionInvalid => "state_transition_invalid",
            FolderResultCode.PathPolicyDenied => "path_policy_denied",
            _ => "lifecycle_rejected",
        };

    private static string SafeFailureStatus(WorkspaceStatusQueryResultCode code)
        => code switch
        {
            WorkspaceStatusQueryResultCode.AuthenticationRequired => "authentication_required",
            WorkspaceStatusQueryResultCode.AuthorizationDenied => "authorization_denied",
            WorkspaceStatusQueryResultCode.NotFoundSafe => "not_found_safe",
            WorkspaceStatusQueryResultCode.ProjectionStale => "projection_stale",
            WorkspaceStatusQueryResultCode.ProjectionUnavailable => "projection_unavailable",
            WorkspaceStatusQueryResultCode.ReadModelUnavailable => "read_model_unavailable",
            _ => "status_rejected",
        };

    private sealed record LifecycleCapacityOptions(
        string ProfileName,
        string ReportFolder,
        bool SelfCheck,
        string? RunId)
    {
        public static LifecycleCapacityOptions Parse(string[] args)
        {
            string profile = "quick";
            string reportFolder = Path.Combine("tests", "load", "reports");
            bool selfCheck = false;
            string? runId = null;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, "--self-check", StringComparison.Ordinal))
                {
                    selfCheck = true;
                }
                else if (string.Equals(arg, "--profile", StringComparison.Ordinal) && i + 1 < args.Length)
                {
                    profile = args[++i];
                }
                else if (string.Equals(arg, "--report-folder", StringComparison.Ordinal) && i + 1 < args.Length)
                {
                    reportFolder = args[++i];
                }
                else if (string.Equals(arg, "--run-id", StringComparison.Ordinal) && i + 1 < args.Length)
                {
                    runId = args[++i];
                }
                else
                {
                    throw new ArgumentException($"Unknown or incomplete argument '{arg}'.");
                }
            }

            return new LifecycleCapacityOptions(profile, reportFolder, selfCheck, runId);
        }
    }
}
