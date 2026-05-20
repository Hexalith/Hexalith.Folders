namespace Hexalith.Folders.Aggregates.Folder;

public static class FolderArchiveReasonCodes
{
    public const string CallerRequested = "caller_requested";
    public const string PolicyRetention = "policy_retention";
    public const string OperatorReview = "operator_review";

    public static bool IsSupported(string? value) => TryParse(value, out _);

    public static bool TryParse(string? value, out FolderArchiveReasonCode reasonCode)
    {
        switch (value)
        {
            case CallerRequested:
                reasonCode = FolderArchiveReasonCode.CallerRequested;
                return true;
            case PolicyRetention:
                reasonCode = FolderArchiveReasonCode.PolicyRetention;
                return true;
            case OperatorReview:
                reasonCode = FolderArchiveReasonCode.OperatorReview;
                return true;
            default:
                reasonCode = default;
                return false;
        }
    }

    public static string ToContractValue(FolderArchiveReasonCode reasonCode)
        => reasonCode switch
        {
            FolderArchiveReasonCode.CallerRequested => CallerRequested,
            FolderArchiveReasonCode.PolicyRetention => PolicyRetention,
            FolderArchiveReasonCode.OperatorReview => OperatorReview,
            _ => string.Empty,
        };
}
