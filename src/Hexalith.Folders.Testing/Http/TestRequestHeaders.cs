using Hexalith.Folders.Testing.Factories;

namespace Hexalith.Folders.Testing.Http;

public static class TestRequestHeaders
{
    public const string CorrelationId = "X-Correlation-Id";

    public const string IdempotencyKey = "Idempotency-Key";

    public const string TaskId = "X-Task-Id";

    public static IReadOnlyDictionary<string, string> FromFolderContext(TestFolderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [CorrelationId] = ValidateHeaderValue(context.CorrelationId, nameof(context.CorrelationId)),
            [IdempotencyKey] = ValidateHeaderValue(context.IdempotencyKey, nameof(context.IdempotencyKey)),
            [TaskId] = ValidateHeaderValue(context.TaskId, nameof(context.TaskId))
        };
    }

    private static string ValidateHeaderValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Header values must be populated.", parameterName);
        }

        if (value.Any(char.IsControl))
        {
            throw new ArgumentException("Header values must not contain control characters.", parameterName);
        }

        return value;
    }
}
