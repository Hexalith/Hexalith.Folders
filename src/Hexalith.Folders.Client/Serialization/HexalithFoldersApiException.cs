using Hexalith.Folders.Client.Serialization;

namespace Hexalith.Folders.Client.Generated;

/// <summary>Exposes the canonical Problem Details for a failed SDK operation.</summary>
public partial class HexalithFoldersApiException
{
    private Tuple<ProblemDetails?, string?>? _parsedProblemDetails;

    /// <summary>Gets the operation associated with the exception when it was created.</summary>
    internal string? OriginatingOperationId { get; } = HexalithFoldersOperationContext.Current;

    /// <summary>Gets the projected Problem Details when the response matches the operation's contract.</summary>
    public ProblemDetails? ProblemDetails => GetParsedProblemDetails().Item1;

    /// <summary>Gets the bounded diagnostic when the response cannot be projected.</summary>
    public string? ProblemDetailsParseDiagnostic => GetParsedProblemDetails().Item2;

    private Tuple<ProblemDetails?, string?> GetParsedProblemDetails()
    {
        if (Volatile.Read(ref _parsedProblemDetails) is { } cached)
        {
            return cached;
        }

        (ProblemDetails? problem, string? diagnostic) = Oq2ProblemProjection.Project(this);
        var fresh = Tuple.Create(problem, diagnostic);

        // Parsing can run concurrently. All readers use the same published result.
        return Interlocked.CompareExchange(ref _parsedProblemDetails, fresh, null) ?? fresh;
    }
}
