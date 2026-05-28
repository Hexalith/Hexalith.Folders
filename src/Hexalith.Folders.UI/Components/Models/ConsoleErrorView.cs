namespace Hexalith.Folders.UI.Components.Models;

/// <summary>
/// Story 6.6 / §3.9 — a render-ready, metadata-only safe-denial / safe-error view. Built from a thrown
/// <c>HexalithFoldersApiException</c> (the SDK surfaces denials as HTTP status + Problem Details, not as
/// data fields). Carries only the canonical A-8 metadata the error panel renders — never a stack trace,
/// secret, raw body, or resource-existence oracle. A UI-assembly view-model record (not a Contracts type).
/// </summary>
/// <param name="ReasonToken">Canonical error category token (e.g. <c>tenant_access_denied</c>), shown verbatim.</param>
/// <param name="SafeExplanation">Operator-facing safe explanation; never confirms whether a resource exists.</param>
/// <param name="CorrelationId">Correlation evidence (request echo when the body carries none); monospace safe-copy.</param>
/// <param name="Retryable">Advisory retryability from Problem Details, when present.</param>
/// <param name="ClientAction">Advisory client-action token from Problem Details, when present.</param>
public sealed record ConsoleErrorView(
    string ReasonToken,
    string SafeExplanation,
    string CorrelationId,
    bool? Retryable,
    string? ClientAction);
