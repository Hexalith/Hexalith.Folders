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
