using System.Text.Json;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components.Models;

namespace Hexalith.Folders.UI.Services;

/// <summary>
/// Story 6.6 / §3.9 — translates a thrown <see cref="HexalithFoldersApiException"/> into a
/// metadata-only <see cref="ConsoleErrorView"/> for the safe-denial / safe-error path. Parses only the
/// canonical A-8 Problem Details extension fields (<c>category</c>, <c>correlationId</c>,
/// <c>retryable</c>, <c>clientAction</c>); it never surfaces the raw body, a stack trace, or a
/// <c>taskId</c> off the error body (not an A-8 extension). Displayed explanations come from
/// <see cref="ConsoleStatusText.ResolveErrorExplanation(string)"/> (our safe copy), never the server
/// message, so <c>not_found</c> and <c>*_denied</c> can never be expanded into an existence oracle.
/// </summary>
public static class ConsoleErrorPresenter
{
    /// <summary>
    /// Builds a safe-error view from an SDK exception. <paramref name="fallbackCorrelationId"/> is the
    /// <c>x-correlation-id</c> the page sent, used when the Problem Details body carries none.
    /// </summary>
    public static ConsoleErrorView FromException(HexalithFoldersApiException exception, string fallbackCorrelationId)
    {
        ArgumentNullException.ThrowIfNull(exception);

        string reasonToken = "internal_error";
        string correlationId = fallbackCorrelationId ?? string.Empty;
        bool? retryable = null;
        string? clientAction = null;

        if (!string.IsNullOrWhiteSpace(exception.Response))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(exception.Response);
                JsonElement root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    // Only adopt the body's category if it is a known, vetted canonical token. An
                    // unrecognized/free-text category is never echoed into the DOM — fall back to the
                    // generic safe envelope (§3.9: surface only vetted metadata, never arbitrary server text).
                    if (TryGetString(root, "category", out string? category)
                        && ConsoleStatusText.IsKnownReasonToken(category))
                    {
                        reasonToken = category!;
                    }

                    if (TryGetString(root, "correlationId", out string? bodyCorrelation) && !string.IsNullOrWhiteSpace(bodyCorrelation))
                    {
                        correlationId = bodyCorrelation;
                    }

                    if (root.TryGetProperty("retryable", out JsonElement retryableElement)
                        && (retryableElement.ValueKind == JsonValueKind.True || retryableElement.ValueKind == JsonValueKind.False))
                    {
                        retryable = retryableElement.GetBoolean();
                    }

                    if (TryGetString(root, "clientAction", out string? action) && !string.IsNullOrWhiteSpace(action))
                    {
                        clientAction = action;
                    }
                }
            }
            catch (JsonException)
            {
                // A non-JSON or malformed body must not leak; fall back to the generic safe envelope.
                reasonToken = "internal_error";
            }
        }

        return new ConsoleErrorView(
            reasonToken,
            ConsoleStatusText.ResolveErrorExplanation(reasonToken),
            correlationId,
            retryable,
            clientAction);
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string? value)
    {
        if (root.TryGetProperty(propertyName, out JsonElement element) && element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString();
            return true;
        }

        value = null;
        return false;
    }
}
