using Hexalith.Folders.Contracts;

namespace Hexalith.Folders.PatternExamples;

public sealed record GovernanceCompletenessExample(
    string CriterionId,
    string Owner,
    string Status,
    string ArtifactPath)
{
    public string QualifiedCriterion => $"{FoldersContractMetadata.ModuleName}/{CriterionId}";

    public bool IsBoundedReferencePending =>
        Status == "reference_pending"
        && !string.IsNullOrWhiteSpace(Owner)
        && !string.IsNullOrWhiteSpace(ArtifactPath);
}
