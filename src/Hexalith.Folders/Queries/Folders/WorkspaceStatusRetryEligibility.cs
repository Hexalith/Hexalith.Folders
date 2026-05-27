namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceStatusRetryEligibility(
    bool Eligible,
    string ReasonCode,
    bool AdvisoryOnly = true);
