namespace Hexalith.Folders.Workers.SemanticIndexing;

public sealed record SemanticIndexingPolicyOutcome
{
    public SemanticIndexingPolicyOutcome(
        bool authorizedForIndexing,
        string sensitivityClassification,
        string pathPolicyOutcome)
    {
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(sensitivityClassification);
        SemanticIndexingValidation.ThrowIfNullOrWhiteSpace(pathPolicyOutcome);

        AuthorizedForIndexing = authorizedForIndexing;
        SensitivityClassification = sensitivityClassification;
        PathPolicyOutcome = pathPolicyOutcome;
    }

    public bool AuthorizedForIndexing { get; init; }

    public string SensitivityClassification { get; init; }

    public string PathPolicyOutcome { get; init; }
}
