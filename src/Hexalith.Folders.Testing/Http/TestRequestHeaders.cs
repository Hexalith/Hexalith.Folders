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
            [CorrelationId] = context.CorrelationId,
            [IdempotencyKey] = context.IdempotencyKey,
            [TaskId] = context.TaskId
        };
    }
}
