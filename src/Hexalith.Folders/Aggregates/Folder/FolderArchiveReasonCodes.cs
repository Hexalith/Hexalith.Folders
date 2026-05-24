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
                // Coerce miss to the explicit None sentinel rather than the default-valued
                // member of FolderArchiveReasonCode, so a downstream caller cannot mistake a
                // parse failure for a supported reason code.
                reasonCode = FolderArchiveReasonCode.None;
                return false;
        }
    }

    public static string ToContractValue(FolderArchiveReasonCode reasonCode)
        => reasonCode switch
        {
            FolderArchiveReasonCode.CallerRequested => CallerRequested,
            FolderArchiveReasonCode.PolicyRetention => PolicyRetention,
            FolderArchiveReasonCode.OperatorReview => OperatorReview,
            // None is an internal sentinel and must never reach the wire. Returning
            // string.Empty would silently produce an empty contract value for downstream
            // consumers that cannot distinguish missing from unknown.
            FolderArchiveReasonCode.None => throw new ArgumentOutOfRangeException(
                nameof(reasonCode),
                reasonCode,
                "FolderArchiveReasonCode.None is an internal sentinel and is not a valid contract value."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(reasonCode),
                reasonCode,
                $"Unsupported FolderArchiveReasonCode '{reasonCode}'."),
        };
}
