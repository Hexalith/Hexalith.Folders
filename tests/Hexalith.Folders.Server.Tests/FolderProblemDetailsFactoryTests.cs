using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FolderProblemDetailsFactoryTests
{
    [Fact]
    public void DomainProblemShouldIncludeCoreExtensionsAndTaskIdExtra()
    {
        ProblemHttpResult problem = FolderProblemDetailsFactory.ForDomain(
            StatusCodes.Status400BadRequest,
            "validation_error",
            "validation_error",
            retryable: false,
            correlationId: "corr-a",
            taskId: "task_1").ShouldBeOfType<ProblemHttpResult>();

        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.ProblemDetails.Title.ShouldBe("Validation failure.");
        problem.ProblemDetails.Extensions["category"].ShouldBe("validation_error");
        problem.ProblemDetails.Extensions["code"].ShouldBe("validation_error");
        problem.ProblemDetails.Extensions["correlationId"].ShouldBe("corr-a");
        problem.ProblemDetails.Extensions["taskId"].ShouldBe("task_1");
        problem.ProblemDetails.Extensions["retryable"].ShouldBe(false);
        problem.ProblemDetails.Extensions["clientAction"].ShouldBe("no_action");
        IDictionary<string, object?> details = Details(problem);
        details["visibility"].ShouldBe("metadata_only");
        details["evidenceSource"].ShouldBe("http_boundary");
        details["taskId"].ShouldBe("task_1");
        AssertNoGatewayLeak(problem);
    }

    [Fact]
    public void AuditProblemShouldKeepEvidenceSourceTodoRefAndStaleConflict()
    {
        ProblemHttpResult problem = FolderProblemDetailsFactory.ForAudit(
            StatusCodes.Status409Conflict,
            "projection_stale",
            "projection_stale",
            retryable: true,
            correlationId: "corr-a",
            taskId: "task_1",
            todoRef: "C4",
            evidenceSource: "timeline").ShouldBeOfType<ProblemHttpResult>();

        problem.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        problem.ProblemDetails.Title.ShouldBe("Audit evidence is not currently fresh enough for this operation.");
        problem.ProblemDetails.Extensions["taskId"].ShouldBe("task_1");
        IDictionary<string, object?> details = Details(problem);
        details["evidenceSource"].ShouldBe("timeline");
        details["todoRef"].ShouldBe("C4");
        details["taskId"].ShouldBe("task_1");
        AssertNoGatewayLeak(problem);
    }

    [Theory]
    [InlineData("idempotency_key_not_allowed", "corr-a", "task-a", "corr-a", "task-a")]
    [InlineData("idempotency_key_not_allowed", "malformed correlation", "malformed task", null, null)]
    [InlineData("idempotency_key_not_allowed", "installation-reference", "installation-task", null, null)]
    [InlineData("validation_error", "corr-a", "malformed task", "corr-a", null)]
    public void DomainAndAuditProblemsShouldFilterUnsafeRequestMetadata(
        string code,
        string correlationId,
        string taskId,
        string? expectedCorrelationId,
        string? expectedTaskId)
    {
        IResult[] results =
        [
            FolderProblemDetailsFactory.ForDomain(StatusCodes.Status400BadRequest, "validation_error", code, false, correlationId, taskId),
            FolderProblemDetailsFactory.ForAudit(StatusCodes.Status400BadRequest, "validation_error", code, false, correlationId, taskId),
        ];

        foreach (IResult result in results)
        {
            ProblemHttpResult problem = result.ShouldBeOfType<ProblemHttpResult>();
            problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
            problem.ProblemDetails.Extensions["code"].ShouldBe(code);
            problem.ProblemDetails.Extensions["correlationId"].ShouldBe(expectedCorrelationId);
            if (expectedTaskId is null)
            {
                problem.ProblemDetails.Extensions.ShouldNotContainKey("taskId");
                Details(problem).ShouldNotContainKey("taskId");
            }
            else
            {
                problem.ProblemDetails.Extensions["taskId"].ShouldBe(expectedTaskId);
                Details(problem)["taskId"].ShouldBe(expectedTaskId);
            }
        }
    }

    [Fact]
    public void ProviderProblemShouldSanitizeSensitiveCorrelationAndExposeRetryAfter()
    {
        ProblemHttpResult problem = FolderProblemDetailsFactory.ForProviderReadiness(
            StatusCodes.Status429TooManyRequests,
            "provider_rate_limited",
            "provider_rate_limited",
            retryable: true,
            correlationId: "https://provider.example.test/owner/repository",
            retryAfter: TimeSpan.FromMilliseconds(1500)).ShouldBeOfType<ProblemHttpResult>();

        string? correlationId = problem.ProblemDetails.Extensions["correlationId"] as string;
        correlationId.ShouldNotBeNull();
        correlationId.ShouldStartWith("correlation_");
        correlationId.ShouldNotContain("repository");
        problem.ProblemDetails.Extensions["retryAfterSeconds"].ShouldBe(2L);
        Details(problem)["evidenceSource"].ShouldBe("provider_readiness");
        AssertNoGatewayLeak(problem);
    }

    [Fact]
    public void OpsConsoleProblemShouldSanitizeSensitiveCorrelationAndKeepStaleConflict()
    {
        ProblemHttpResult problem = FolderProblemDetailsFactory.ForOpsConsole(
            StatusCodes.Status409Conflict,
            "projection_stale",
            "projection_stale",
            retryable: true,
            correlationId: "installation-secret").ShouldBeOfType<ProblemHttpResult>();

        problem.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        problem.ProblemDetails.Title.ShouldBe("Diagnostic projection is stale.");
        string? correlationId = problem.ProblemDetails.Extensions["correlationId"] as string;
        correlationId.ShouldNotBeNull();
        correlationId.ShouldStartWith("correlation_");
        correlationId.ShouldNotContain("installation");
        Details(problem)["evidenceSource"].ShouldBe("ops_console_diagnostics");
        AssertNoGatewayLeak(problem);
    }

    [Fact]
    public void DomainStaleProjectionShouldRemainServiceUnavailable()
    {
        ProblemHttpResult problem = FolderProblemDetailsFactory.ForDomain(
            FolderCanonicalErrorMapper.StatusFor("projection_stale"),
            "projection_stale",
            "projection_stale",
            retryable: true,
            correlationId: "corr-a",
            taskId: null).ShouldBeOfType<ProblemHttpResult>();

        problem.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        problem.ProblemDetails.Title.ShouldBe("Read model unavailable.");
    }

    [Theory]
    [InlineData("idempotency_conflict", "Idempotency conflict.")]
    [InlineData("idempotency_key_expired", "Idempotency key expired.")]
    [InlineData("lock_conflict", "Conflict.")]
    [InlineData("duplicate_binding", "Conflict.")]
    [InlineData("repository_conflict", "Conflict.")]
    [InlineData("reconciliation_required", "Conflict.")]
    public void DomainConflictTitleShouldReflectItsCategory(string category, string expectedTitle)
    {
        ProblemHttpResult problem = FolderProblemDetailsFactory.ForDomain(
            FolderCanonicalErrorMapper.StatusFor(category),
            category,
            category,
            retryable: false,
            correlationId: "corr-a",
            taskId: null).ShouldBeOfType<ProblemHttpResult>();

        problem.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        problem.ProblemDetails.Title.ShouldBe(expectedTitle);
        problem.ProblemDetails.Extensions["category"].ShouldBe(category);
    }

    [Theory]
    [InlineData(StatusCodes.Status410Gone, "lock_expired", "Lock expired.", "The workspace lock lease is no longer active.")]
    [InlineData(StatusCodes.Status429TooManyRequests, "provider_rate_limited", "Provider rate limited.", "The provider is rate limited. Retry later.")]
    public void DomainProblemShouldDescribeItsOutcome(int statusCode, string category, string expectedTitle, string expectedMessage)
    {
        ProblemHttpResult problem = FolderProblemDetailsFactory.ForDomain(
            FolderCanonicalErrorMapper.StatusFor(category),
            category,
            category,
            retryable: true,
            correlationId: "corr-a",
            taskId: null).ShouldBeOfType<ProblemHttpResult>();

        problem.StatusCode.ShouldBe(statusCode);
        problem.ProblemDetails.Title.ShouldBe(expectedTitle);
        problem.ProblemDetails.Extensions["message"].ShouldBe(expectedMessage);
        problem.ProblemDetails.Extensions["category"].ShouldBe(category);
        problem.ProblemDetails.Extensions["code"].ShouldBe(category);
        problem.ProblemDetails.Extensions["retryable"].ShouldBe(true);
    }

    [Fact]
    public void ProviderNotFoundProblemShouldUseASafeUnavailableTitle()
    {
        ProblemHttpResult problem = FolderProblemDetailsFactory.ForProviderReadiness(
            StatusCodes.Status404NotFound,
            "not_found",
            "not_found",
            retryable: false,
            correlationId: "corr-a").ShouldBeOfType<ProblemHttpResult>();

        problem.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        problem.ProblemDetails.Title.ShouldBe("Resource not available.");
        problem.ProblemDetails.Extensions["message"].ShouldBe("The requested resource is not available to the caller.");
        problem.ProblemDetails.Extensions["category"].ShouldBe("not_found");
        problem.ProblemDetails.Extensions["code"].ShouldBe("not_found");
        problem.ProblemDetails.Extensions["retryable"].ShouldBe(false);
        AssertNoGatewayLeak(problem);
    }

    [Theory]
    [InlineData(StatusCodes.Status503ServiceUnavailable, "unknown_provider_outcome")]
    [InlineData(StatusCodes.Status409Conflict, "reconciliation_required")]
    public void DomainUnresolvedOutcomeShouldCarryFinalStateDetail(int statusCode, string category)
    {
        // The client OQ2 projection requires details.finalState on exactly these two tuples.
        ProblemHttpResult problem = FolderProblemDetailsFactory.ForDomain(
            statusCode,
            category,
            category,
            retryable: false,
            correlationId: "corr-a",
            taskId: null).ShouldBeOfType<ProblemHttpResult>();

        Details(problem)["finalState"].ShouldBe(category);
        problem.ProblemDetails.Extensions["clientAction"].ShouldBe("wait_for_reconciliation");
    }

    [Fact]
    public void DomainResolvedOutcomeShouldOmitFinalStateDetail()
    {
        ProblemHttpResult problem = FolderProblemDetailsFactory.ForDomain(
            StatusCodes.Status422UnprocessableEntity,
            "commit_failed",
            "commit_failed",
            retryable: false,
            correlationId: "corr-a",
            taskId: null).ShouldBeOfType<ProblemHttpResult>();

        Details(problem).ShouldNotContainKey("finalState");
    }

    private static IDictionary<string, object?> Details(ProblemHttpResult problem)
        => problem.ProblemDetails.Extensions["details"].ShouldBeAssignableTo<IDictionary<string, object?>>()!;

    private static void AssertNoGatewayLeak(ProblemHttpResult problem)
    {
        problem.ProblemDetails.Extensions.ShouldNotContainKey("tenantId");
        problem.ProblemDetails.Extensions.ShouldNotContainKey("folderId");
        problem.ProblemDetails.Extensions.ShouldNotContainKey("exception");
        problem.ProblemDetails.Extensions.ShouldNotContainKey("gateway");
    }
}
