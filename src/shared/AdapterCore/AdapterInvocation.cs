using System.Reflection;

using Hexalith.Folders.Client.Convenience;

using Newtonsoft.Json;

namespace Hexalith.Folders.AdapterCore;

/// <summary>Shared invocation sourcing and SDK request-validation classification.</summary>
internal static class AdapterInvocation
{
    /// <summary>Resolves the caller's correlation ID or the SDK's fresh identifier.</summary>
    public static string ResolveCorrelationId(string? value) => CorrelationAndTaskId.ResolveCorrelationId(value);

    /// <summary>Validates a caller-sourced task ID without generating or normalizing it.</summary>
    public static bool TryResolveTaskId(string? value, out string taskId)
    {
        try
        {
            taskId = CorrelationAndTaskId.ResolveTaskId(value);
            return true;
        }
        catch (InvalidOperationException)
        {
            taskId = string.Empty;
            return false;
        }
    }

    /// <summary>Resolves query task sourcing; surfaces decide how absence and invalid input are reported.</summary>
    public static bool TryResolveQueryTaskId(string? value, bool supplied, bool required, out string? taskId)
    {
        taskId = null;
        if (supplied)
        {
            if (!TryResolveTaskId(value, out string resolved))
            {
                return false;
            }

            taskId = resolved;
            return true;
        }

        return !required;
    }

    /// <summary>Resolves explicit keys before the optional CLI-only auto-key source.</summary>
    public static string? ResolveIdempotencyKey(string? value, Func<string>? autoGenerate = null)
        => !string.IsNullOrWhiteSpace(value) ? value : autoGenerate?.Invoke();

    /// <summary>Identifies generated request validation failures, including reflection wrappers.</summary>
    public static bool IsRequestValidationFailure(Exception exception)
        => exception is JsonSerializationException
            || (exception is TargetInvocationException { InnerException: { } inner }
                && IsRequestValidationFailure(inner));
}
