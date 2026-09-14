namespace Hexalith.Folders.Client.Convenience;

/// <summary>
/// Signals that decoded or observed file content exceeds the canonical per-file maximum.
/// </summary>
public sealed class FileContentLimitExceededException : ArgumentOutOfRangeException
{
    /// <summary>Initializes a new instance of the <see cref="FileContentLimitExceededException"/> class.</summary>
    /// <param name="parameterName">The bounded content parameter.</param>
    internal FileContentLimitExceededException(string parameterName)
        : base(parameterName, "File content exceeds the canonical per-file maximum.")
    {
    }
}
