using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Client.Serialization;

/// <summary>Validates read-consistency values against the selected generated operation contract.</summary>
public static class OperationFreshness
{
    /// <summary>Parses a freshness value only when the selected operation declares that exact value.</summary>
    /// <param name="operationId">The generated OpenAPI operation identifier.</param>
    /// <param name="value">The caller-supplied wire value, or <see langword="null"/>.</param>
    /// <param name="freshness">The parsed generated enum value.</param>
    /// <returns><see langword="true"/> when the value is omitted or exactly accepted by the operation.</returns>
    public static bool TryParse(
        string operationId,
        string? value,
        out ReadConsistencyClass? freshness)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        freshness = null;
        if (value is null)
        {
            return true;
        }

        if (!string.Equals(
                value,
                HexalithFoldersGeneratedOperationCatalog.AcceptedFreshness(operationId),
                StringComparison.Ordinal))
        {
            return false;
        }

        freshness = value switch
        {
            "snapshot_per_task" => ReadConsistencyClass.Snapshot_per_task,
            "read_your_writes" => ReadConsistencyClass.Read_your_writes,
            "eventually_consistent" => ReadConsistencyClass.Eventually_consistent,
            _ => null,
        };
        return freshness is not null;
    }
}
