namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Source-folded latest applicability for one logical view, independent of its historical row proof.</summary>
/// <param name="ViewKey">The exact tenant/folder-scoped logical view key.</param>
/// <param name="Sequence">The latest source position that materialized this view.</param>
/// <param name="SourceDigest">The original source prefix digest.</param>
/// <param name="ValueDigest">The complete canonical value applicable at that position.</param>
public sealed record OperationalStatusViewApplicability(string ViewKey, long Sequence, string SourceDigest, string ValueDigest)
{
    /// <summary>Gets integrity evidence for this bounded applicability record.</summary>
    public string IntegrityDigest { get; init; } = string.Empty;

    /// <summary>Creates applicability exclusively from a validated source fold.</summary>
    internal static OperationalStatusViewApplicability Create(string key, OperationalStatusViewEvidence evidence)
    {
        OperationalStatusViewApplicability value = new(key, evidence.Sequence, evidence.SourceDigest, evidence.ValueDigest);
        return value with { IntegrityDigest = value.Digest() };
    }

    /// <summary>Checks scope, source boundary and the separately retained row evidence.</summary>
    internal bool Matches(string key, long head, OperationalStatusViewEvidence evidence)
        => ViewKey == key && Sequence > 0 && Sequence <= head && SourceDigest is { Length: 64 }
            && ValueDigest is { Length: 64 } && IntegrityDigest == Digest()
            && Sequence == evidence.Sequence && SourceDigest == evidence.SourceDigest && ValueDigest == evidence.ValueDigest;

    private string Digest() => OperationalStatusIntegrity.Digest(new { ViewKey, Sequence, SourceDigest, ValueDigest });
}
