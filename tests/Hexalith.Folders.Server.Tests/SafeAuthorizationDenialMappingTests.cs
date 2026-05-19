using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class SafeAuthorizationDenialMappingTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 19, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("authentication_denied", 401, "authentication_failure", "no_action", false)]
    [InlineData("claim_transform_denied", 403, "authorization_denied", "no_action", false)]
    [InlineData("tenant_access_denied", 403, "tenant_access_denied", "no_action", false)]
    [InlineData("eventstore_validator_denied", 403, "authorization_denied", "no_action", false)]
    [InlineData("authorization_evidence_malformed", 403, "authorization_denied", "no_action", false)]
    [InlineData("safe_not_found", 404, "not_found_to_caller", "no_action", false)]
    [InlineData("folder_acl_denied", 404, "not_found_to_caller", "no_action", false)]
    [InlineData("folder_acl_unavailable", 503, "read_model_unavailable", "retry", true)]
    [InlineData("tenant_projection_unavailable", 503, "read_model_unavailable", "retry", true)]
    [InlineData("dapr_policy_denied", 503, "policy_evidence_unavailable", "retry", true)]
    [InlineData("dapr_policy_denied", 403, "policy_denied", "no_action", false)]
    public void MapperShouldReturnSafeProblemDetailsFromDecisionSnapshot(
        string outcomeCode,
        int expectedStatusCode,
        string expectedCategory,
        string expectedClientAction,
        bool retryable)
    {
        LayeredFolderAuthorizationResult result = LayeredFolderAuthorizationResult.Denied(new LayeredFolderAuthorizationDecisionSnapshot(
            TerminalLayer: AuthorizationLayer.FolderAcl,
            OutcomeCode: outcomeCode,
            Retryable: retryable,
            FreshnessClass: "unknown",
            FreshnessWatermark: null,
            CorrelationId: "corr-safe",
            TaskId: "task-safe",
            ActorSafeIdentifier: "actor-safe",
            OperationPolicyClass: "mutation",
            TimingBucket: "not_recorded",
            DecidedAt: Now));

        IResult mapped = FolderAuthorizationDenialMapper.ToHttpResult(result);

        ProblemHttpResult problem = mapped.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(expectedStatusCode);
        problem.ProblemDetails.Extensions["category"].ShouldBe(expectedCategory);
        problem.ProblemDetails.Extensions["code"].ShouldBe(outcomeCode);
        problem.ProblemDetails.Extensions["clientAction"].ShouldBe(expectedClientAction);
        problem.ProblemDetails.Extensions["retryable"].ShouldBe(retryable);
        problem.ProblemDetails.Extensions["correlationId"].ShouldBe("corr-safe");
        problem.ProblemDetails.Extensions["taskId"].ShouldBe("task-safe");
        problem.ProblemDetails.Extensions.ShouldNotContainKey("tenantId");
        problem.ProblemDetails.Extensions.ShouldNotContainKey("folderId");
        problem.ProblemDetails.Extensions.ShouldNotContainKey("exception");
    }

    [Fact]
    public void MapperShouldNotLeakProtectedIdentifiersInBodyExtensions()
    {
        LayeredFolderAuthorizationResult result = LayeredFolderAuthorizationResult.Denied(new LayeredFolderAuthorizationDecisionSnapshot(
            TerminalLayer: AuthorizationLayer.FolderAcl,
            OutcomeCode: LayeredAuthorizationOutcomeCodes.FolderAclDenied,
            Retryable: false,
            FreshnessClass: "fresh",
            FreshnessWatermark: "folder-secret-watermark",
            CorrelationId: "corr-safe",
            TaskId: "task-safe",
            ActorSafeIdentifier: "actor-safe",
            OperationPolicyClass: "mutation",
            TimingBucket: "not_recorded",
            DecidedAt: Now));

        ProblemHttpResult problem = FolderAuthorizationDenialMapper.ToHttpResult(result).ShouldBeOfType<ProblemHttpResult>();
        string body = string.Join("|", problem.ProblemDetails.Extensions.Select(static pair => $"{pair.Key}:{pair.Value}"));

        body.ShouldNotContain("folder-secret-watermark", Case.Sensitive);
        body.ShouldNotContain("tenant-secret", Case.Sensitive);
        body.ShouldNotContain("folder-secret", Case.Sensitive);
    }
}
