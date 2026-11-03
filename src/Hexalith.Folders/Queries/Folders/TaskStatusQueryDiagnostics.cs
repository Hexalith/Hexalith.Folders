using Hexalith.Folders.Authorization;

using Microsoft.Extensions.Logging;

namespace Hexalith.Folders.Queries.Folders;

/// <summary>Advisory task-read metadata; never observes identities, rows, messages or credentials.</summary>
internal static partial class TaskStatusQueryDiagnostics
{
    /// <summary>Records the existing tenant result without another read or clock observation.</summary>
    internal static void TenantObserved(ILogger logger, TenantAccessAuthorizationResult result)
    {
        try
        {
            if (!logger.IsEnabled(LogLevel.Debug)) return;
            int? age = result.ProjectionAge is { } value
                ? (int)Math.Clamp(value.TotalSeconds, -86400, 86400) : null;
            Tenant(logger, result.Outcome, result.FreshnessStatus, age);
        }
        catch { /* Optional diagnostics cannot change authorization. */ }
    }

    /// <summary>Records closed outcome fields after the original handler has completed.</summary>
    internal static void Completed(ILogger logger, TaskStatusQueryResult result)
    {
        try
        {
            if (!logger.IsEnabled(LogLevel.Debug)) return;
            LayeredFolderAuthorizationDecisionSnapshot? denial = result.FolderAuthorizationDenial?.Decision;
            Result(logger, result.Code, denial?.TerminalLayer, Outcome(denial?.OutcomeCode),
                Freshness(denial?.FreshnessClass));
        }
        catch { /* Optional diagnostics cannot replace the original result. */ }
    }

    /// <summary>Preserves the warning using a closed exception class and no exception object or message.</summary>
    internal static void ReadModelFailed(ILogger logger, Exception failure)
    {
        try
        {
            string kind = failure switch
            {
                TimeoutException => "timeout",
                InvalidOperationException => "invalid_operation",
                ArgumentException => "argument",
                HttpRequestException => "http_request",
                _ => "other",
            };
            Failure(logger, kind);
        }
        catch { /* Logging providers are advisory on this fail-closed path too. */ }
    }

    private static string Outcome(string? value) => value switch
    {
        LayeredAuthorizationOutcomeCodes.Allowed or LayeredAuthorizationOutcomeCodes.AuthenticationDenied
            or LayeredAuthorizationOutcomeCodes.ClaimTransformDenied or LayeredAuthorizationOutcomeCodes.TenantAccessDenied
            or LayeredAuthorizationOutcomeCodes.TenantProjectionStale or LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable
            or LayeredAuthorizationOutcomeCodes.FolderAclDenied or LayeredAuthorizationOutcomeCodes.FolderAclStale
            or LayeredAuthorizationOutcomeCodes.FolderAclUnavailable or LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied
            or LayeredAuthorizationOutcomeCodes.DaprPolicyDenied or LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed
            or LayeredAuthorizationOutcomeCodes.SafeNotFound => value,
        null => "none",
        _ => "other",
    };

    private static string Freshness(string? value) => value switch
    {
        "fresh" or "stale" or "unavailable" or "malformed" or "unknown" => value,
        null => "none",
        _ => "other",
    };

    [LoggerMessage(EventId = 1220, Level = LogLevel.Debug,
        Message = "Task status tenant observation: Outcome={Outcome}, Freshness={Freshness}, ProjectionAgeSeconds={ProjectionAgeSeconds}.")]
    private static partial void Tenant(ILogger logger, TenantAccessOutcome outcome,
        TenantProjectionFreshnessStatus freshness, int? projectionAgeSeconds);

    [LoggerMessage(EventId = 1221, Level = LogLevel.Debug,
        Message = "Task status completed: ResultCode={ResultCode}, FolderDenialLayer={FolderDenialLayer}, FolderDenialOutcome={FolderDenialOutcome}, FolderDenialFreshness={FolderDenialFreshness}.")]
    private static partial void Result(ILogger logger, TaskStatusQueryResultCode resultCode,
        AuthorizationLayer? folderDenialLayer, string folderDenialOutcome, string folderDenialFreshness);

    [LoggerMessage(EventId = 1222, Level = LogLevel.Warning,
        Message = "Task status read-model call failed; returning ReadModelUnavailable. ExceptionClass={ExceptionClass}.")]
    private static partial void Failure(ILogger logger, string exceptionClass);
}
