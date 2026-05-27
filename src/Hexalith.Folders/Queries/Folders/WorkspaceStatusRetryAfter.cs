namespace Hexalith.Folders.Queries.Folders;

public sealed record WorkspaceStatusRetryAfter(
    int RetryAfterSeconds,
    bool AdvisoryOnly = true);
