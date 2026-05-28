using System.Collections.Generic;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components.Models;
using Hexalith.Folders.UI.Services;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.6 / §3.9 — the safe-denial presenter parses only the canonical A-8 Problem Details fields,
/// uses our safe explanation (never the server message), and never surfaces a stack trace or raw body.
/// </summary>
public sealed class ConsoleErrorPresenterTests
{
    private static readonly IReadOnlyDictionary<string, IEnumerable<string>> _noHeaders =
        new Dictionary<string, IEnumerable<string>>();

    [Fact]
    public void FromException_ParsesCanonicalProblemDetails()
    {
        const string body = """
        {"category":"tenant_access_denied","code":"E-TENANT","message":"server message that must not be shown verbatim","correlationId":"corr-from-body","retryable":false,"clientAction":"escalate"}
        """;
        HexalithFoldersApiException exception = new("denied", 403, body, _noHeaders, innerException: null);

        ConsoleErrorView view = ConsoleErrorPresenter.FromException(exception, "corr-fallback");

        view.ReasonToken.ShouldBe("tenant_access_denied");
        view.CorrelationId.ShouldBe("corr-from-body");
        view.Retryable.ShouldBe(false);
        view.ClientAction.ShouldBe("escalate");
        view.SafeExplanation.ShouldBe(ConsoleStatusText.ResolveErrorExplanation("tenant_access_denied"));
        view.SafeExplanation.ShouldNotContain("server message");
    }

    [Fact]
    public void FromException_FallsBackToRequestCorrelation_WhenBodyHasNone()
    {
        HexalithFoldersApiException exception = new("oops", 500, "not-json-at-all", _noHeaders, innerException: null);

        ConsoleErrorView view = ConsoleErrorPresenter.FromException(exception, "corr-fallback");

        view.ReasonToken.ShouldBe("internal_error");
        view.CorrelationId.ShouldBe("corr-fallback");
        view.SafeExplanation.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("not_found_to_caller")]
    [InlineData("authorization_denied")]
    [InlineData("policy_denied")]
    [InlineData("policy_evidence_unavailable")]
    [InlineData("path_policy_denied")]
    public void FromException_MapsServerEmittedDenialCategories_ToSpecificSafeCopy(string category)
    {
        // These are the tokens FolderAuthorizationDenialMapper / file-path policy actually emit on the
        // live denial path; each must resolve to specific safe copy, never the generic envelope (AC #10).
        string body = $$"""{"category":"{{category}}","correlationId":"corr-1","retryable":false}""";
        HexalithFoldersApiException exception = new("denied", 403, body, _noHeaders, innerException: null);

        ConsoleErrorView view = ConsoleErrorPresenter.FromException(exception, "corr-fallback");

        view.ReasonToken.ShouldBe(category);
        view.SafeExplanation.ShouldNotBe(ConsoleStatusText.DefaultErrorExplanation);
    }

    [Fact]
    public void FromException_DoesNotEchoUnknownCategory_FallsBackToInternalError()
    {
        // §3.9: an unrecognized/free-text category must never be echoed into the operator's DOM.
        const string body = """{"category":"totally_made_up_category","correlationId":"corr-1"}""";
        HexalithFoldersApiException exception = new("oops", 500, body, _noHeaders, innerException: null);

        ConsoleErrorView view = ConsoleErrorPresenter.FromException(exception, "corr-fallback");

        view.ReasonToken.ShouldBe("internal_error");
        view.ReasonToken.ShouldNotBe("totally_made_up_category");
    }

    [Fact]
    public void FromException_DoesNotReadTaskIdFromBody()
    {
        // taskId is not a canonical A-8 Problem Details extension; the presenter must not surface it.
        const string body = """{"category":"not_found","taskId":"task-should-not-leak"}""";
        HexalithFoldersApiException exception = new("nf", 404, body, _noHeaders, innerException: null);

        ConsoleErrorView view = ConsoleErrorPresenter.FromException(exception, "corr-fallback");

        view.ReasonToken.ShouldBe("not_found");
        view.SafeExplanation.ShouldNotContain("task-should-not-leak");
    }
}
