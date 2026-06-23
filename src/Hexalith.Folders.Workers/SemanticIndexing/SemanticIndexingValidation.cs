using System.Runtime.CompilerServices;

namespace Hexalith.Folders.Workers.SemanticIndexing;

internal static class SemanticIndexingValidation
{
    public static void ThrowIfNullOrWhiteSpace(string? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        => ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
}
