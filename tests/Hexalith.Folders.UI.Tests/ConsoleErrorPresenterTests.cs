using System.Collections.Generic;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components.Models;
using Hexalith.Folders.UI.Services;

using Newtonsoft.Json;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.6 / §3.9 — the safe-denial presenter consumes only validated canonical A-8 Problem Details fields,
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
        {"type":"about:blank","title":"Resource not available","status":404,"category":"tenant_access_denied","code":"resource_unavailable","message":"server message that must not be shown verbatim","correlationId":"correlation-from-body","retryable":false,"clientAction":"no_action","details":{"visibility":"redacted"}}
        """;
        HexalithFoldersApiException exception = ProblemException(404, body);

        ConsoleErrorView view = ConsoleErrorPresenter.FromException(exception, "corr-fallback");

        view.ReasonToken.ShouldBe("tenant_access_denied");
        view.CorrelationId.ShouldBe("correlation-from-body");
        view.Retryable.ShouldBe(false);
        view.ClientAction.ShouldBe("no_action");
        view.Disposition.ShouldBe(ConsoleErrorDisposition.Denied);
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
    [InlineData("read_model_unavailable", "projection_unavailable")]
    [InlineData("projection_stale", "projection_stale")]
    [InlineData("projection_unavailable", "projection_unavailable")]
    public void FromException_DistinguishesEveryAuthorityOutageCategoryFromDenial(string category, string code)
    {
        int status = category == "projection_stale" ? 409 : 503;
        string body = JsonConvert.SerializeObject(new
        {
            type = "about:blank",
            title = "Read model unavailable",
            status,
            category,
            code,
            message = "Projection data is temporarily unavailable.",
            correlationId = "correlation-read-model",
            retryable = true,
            clientAction = "retry",
            details = new { visibility = "metadata_only" },
        });
        HexalithFoldersApiException exception = ProblemException(status, body);

        ConsoleErrorView view = ConsoleErrorPresenter.FromException(exception, "corr-fallback");

        view.ReasonToken.ShouldBe(category);
        view.Disposition.ShouldBe(ConsoleErrorDisposition.AuthorityUnavailable);
        view.SafeExplanation.ShouldNotBe(ConsoleStatusText.DefaultErrorExplanation);
    }

    [Fact]
    public void FromException_MapsAuthorizationRevocationToDenied()
    {
        const string body = """{"type":"about:blank","title":"Authorization revoked","status":409,"category":"authorization_revocation_detected","code":"authorization_revocation_detected","message":"Authorization was revoked.","correlationId":"correlation-revoked","retryable":false,"clientAction":"contact_operator","details":{"visibility":"metadata_only","currentState":"inaccessible"}}""";
        HexalithFoldersApiException exception = ProblemException(409, body);

        ConsoleErrorView view = ConsoleErrorPresenter.FromException(exception, "corr-fallback");

        view.Disposition.ShouldBe(ConsoleErrorDisposition.Denied);
        view.SafeExplanation.ShouldBe(ConsoleStatusText.ResolveErrorExplanation("authorization_revocation_detected"));
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
        const string body = """{"category":"tenant_access_denied","taskId":"task-should-not-leak"}""";
        HexalithFoldersApiException exception = new("nf", 404, body, _noHeaders, innerException: null);

        ConsoleErrorView view = ConsoleErrorPresenter.FromException(exception, "corr-fallback");

        view.ReasonToken.ShouldBe("internal_error");
        view.SafeExplanation.ShouldNotContain("task-should-not-leak");
    }

    [Fact]
    public void FromException_DoesNotTrustMalformed500DenialMetadata()
    {
        const string body = """{"status":500,"category":"tenant_access_denied","correlationId":"correlation-malformed","retryable":false,"clientAction":"no_action"}""";
        var result = new ProblemDetails { Status = 500 };
        var exception = new HexalithFoldersApiException<ProblemDetails>("malformed", 500, body, _noHeaders, result, null!);

        ConsoleErrorView view = ConsoleErrorPresenter.FromException(exception, "correlation-fallback");

        view.ReasonToken.ShouldBe("internal_error");
        view.Disposition.ShouldBe(ConsoleErrorDisposition.Failure);
        view.CorrelationId.ShouldBe("correlation-fallback");
    }

    private static HexalithFoldersApiException ProblemException(int status, string body)
    {
        ProblemDetails problem = JsonConvert.DeserializeObject<ProblemDetails>(body).ShouldNotBeNull();
        return new HexalithFoldersApiException<ProblemDetails>("problem", status, body, _noHeaders, problem, null!);
    }
}
