namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Per-field diagnostic classification entry (<c>fieldClassifications[]</c> wire object).
/// </summary>
/// <param name="Field">Field identifier (camelCase body property or snake_case audit-metadata key).</param>
/// <param name="Classification">Field classification (<c>consumer_safe</c>|<c>operator_sanitized</c>|<c>forbidden</c>).</param>
public sealed record DiagnosticFieldClassificationView(
    string Field,
    string Classification);
