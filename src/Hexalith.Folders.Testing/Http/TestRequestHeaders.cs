using Hexalith.Folders.Testing.Factories;

namespace Hexalith.Folders.Testing.Http;

public static class TestRequestHeaders
{
    public const string CorrelationId = "X-Correlation-Id";

    public const string IdempotencyKey = "Idempotency-Key";

    public const string TaskId = "X-Hexalith-Task-Id";

    public static IReadOnlyDictionary<string, string> FromFolderContext(TestFolderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase)
        {
            [CorrelationId] = context.CorrelationId,
            [IdempotencyKey] = context.IdempotencyKey,
            [TaskId] = context.TaskId
        };

        foreach ((string header, string value) in headers)
        {
            if (value.Any(character => !char.IsAscii(character)))
            {
                throw new ArgumentException($"The {header} header value must contain only ASCII characters.", nameof(context));
            }
        }

        return headers;
    }
}
