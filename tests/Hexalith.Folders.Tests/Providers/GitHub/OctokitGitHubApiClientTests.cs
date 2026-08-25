using System.Net;
using System.Text;
using System.Text.Json;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.GitHub;
using Octokit.Internal;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.GitHub;

public sealed class OctokitGitHubApiClientTests
{
    [Fact]
    public async Task CreateRepositorySendsPinnedHermeticRequestAndReturnsCanonicalIdentity()
    {
        RecordingGitHubHttpMessageHandler handler = SuccessHandler(HttpStatusCode.Created, RepositoryJson(101));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubRepositoryCreationResult result = await client.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.EquivalentExisting.ShouldBeFalse();
        result.CanonicalRepositoryId.ShouldBe("101");
        RecordedGitHubHttpRequest sent = handler.Requests.ShouldHaveSingleItem();
        sent.Method.ShouldBe(HttpMethod.Post);
        sent.RequestUri.AbsolutePath.ShouldBe("/orgs/octokit-owner-sentinel/repos");
        sent.Headers["X-GitHub-Api-Version"].ShouldBe(["2022-11-28"]);
        sent.Headers["Authorization"].Single().ShouldContain("token-sentinel", Case.Sensitive);
        sent.Headers["User-Agent"].ShouldContain(value => value.Contains("Hexalith-Folders", StringComparison.Ordinal));
        sent.Headers["Accept"].ShouldContain(value => value.Contains("application/vnd.github", StringComparison.Ordinal));

        using JsonDocument body = JsonDocument.Parse(sent.Body.ShouldNotBeNull());
        body.RootElement.GetProperty("name").GetString().ShouldBe("octokit-repository-sentinel");
        body.RootElement.GetProperty("auto_init").GetBoolean().ShouldBeFalse();
        body.RootElement.TryGetProperty("license_template", out _).ShouldBeFalse();
        body.RootElement.TryGetProperty("gitignore_template", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task CreateRepositoryReconcilesEquivalentExistingByCanonicalIdentity()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls == 1
            ? JsonResponse(HttpStatusCode.UnprocessableEntity, RepositoryExistsJson())
            : JsonResponse(HttpStatusCode.OK, RepositoryJson(101))));
        IGitHubApiClient client = await CreateClientAsync(handler);
        GitHubRepositoryCreationRequest request = CreationRequest() with
        {
            Target = CreationRequest().Target with
            {
                ExpectedCanonicalRepositoryId = "101",
                EquivalentExistingAuthorized = true,
            },
        };

        GitHubRepositoryCreationResult result = await client.CreateRepositoryAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.EquivalentExisting.ShouldBeTrue();
        result.CanonicalRepositoryId.ShouldBe("101");
        handler.Requests.Count.ShouldBe(2);
        handler.Requests[1].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[1].RequestUri.AbsolutePath.ShouldBe("/repos/octokit-owner-sentinel/octokit-repository-sentinel");
    }

    [Fact]
    public async Task CreateRepositoryRejectsExistingRepositoryWithoutEquivalentIdentityProof()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls == 1
            ? JsonResponse(HttpStatusCode.UnprocessableEntity, RepositoryExistsJson())
            : JsonResponse(HttpStatusCode.OK, RepositoryJson(101))));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubRepositoryCreationResult result = await client.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.RepositoryConflict);
        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CreateRepositoryCancellationBeforeDispatchSendsNoMutation()
    {
        RecordingGitHubHttpMessageHandler handler = SuccessHandler(HttpStatusCode.Created, RepositoryJson(101));
        IGitHubApiClient client = await CreateClientAsync(handler);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        GitHubRepositoryCreationResult result = await client.CreateRepositoryAsync(
            CreationRequest(),
            cancellation.Token);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.CancellationBeforeDispatch);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateRepositoryTimeoutAfterDispatchReturnsUnknownOutcomeWithoutRetry()
    {
        RecordingGitHubHttpMessageHandler handler = new((_, _) => throw new TaskCanceledException("provider-body-sentinel"));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubRepositoryCreationResult result = await client.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.TimeoutDuringMutation);
        handler.Requests.Count.ShouldBe(1);
        JsonSerializer.Serialize(result).ShouldNotContain("provider-body-sentinel", Case.Sensitive);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "ValidationFailure")]
    [InlineData(HttpStatusCode.Unauthorized, "AuthenticationRequired")]
    [InlineData(HttpStatusCode.Forbidden, "PermissionInsufficient")]
    [InlineData(HttpStatusCode.NotFound, "NotFoundOrHidden")]
    [InlineData(HttpStatusCode.Conflict, "RepositoryConflict")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "ValidationFailure")]
    [InlineData(HttpStatusCode.InternalServerError, "ServerUnavailable")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "ServerUnavailable")]
    public async Task CreateRepositoryMapsKnownProviderStatusesWithoutLeakingBody(
        HttpStatusCode statusCode,
        string expectedConditionName)
    {
        RecordingGitHubHttpMessageHandler handler = SuccessHandler(
            statusCode,
            "{ \"message\": \"provider-body-sentinel\" }");
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubRepositoryCreationResult result = await client.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(Enum.Parse<GitHubApiFailureCondition>(expectedConditionName));
        handler.Requests.Count.ShouldBe(1);
        JsonSerializer.Serialize(result).ShouldNotContain("provider-body-sentinel", Case.Sensitive);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task CreateRepositoryMapsPrimaryRateLimitAndBoundsRetryAfter(HttpStatusCode statusCode)
    {
        RecordingGitHubHttpMessageHandler handler = new((_, _) =>
        {
            HttpResponseMessage response = JsonResponse(
                statusCode,
                "{ \"message\": \"API rate limit exceeded for provider-body-sentinel\" }");
            response.Headers.TryAddWithoutValidation("X-RateLimit-Limit", "5000");
            response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "0");
            response.Headers.TryAddWithoutValidation("X-RateLimit-Reset", "4102444800");
            response.Headers.TryAddWithoutValidation("Retry-After", "172800");
            return Task.FromResult(response);
        });
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubRepositoryCreationResult result = await client.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.PrimaryRateLimit);
        result.RetryAfter.ShouldBe(TimeSpan.FromHours(24));
        handler.Requests.Count.ShouldBe(1);
        JsonSerializer.Serialize(result).ShouldNotContain("provider-body-sentinel", Case.Sensitive);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task CreateRepositoryMapsSecondaryRateLimitWithoutLeakingEvidence(HttpStatusCode statusCode)
    {
        RecordingGitHubHttpMessageHandler handler = new((_, _) =>
        {
            HttpResponseMessage response = JsonResponse(
                statusCode,
                "{ \"message\": \"You have exceeded a secondary rate limit. provider-body-sentinel\" }");
            response.Headers.TryAddWithoutValidation("X-RateLimit-Limit", "5000");
            response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "4999");
            response.Headers.TryAddWithoutValidation("Retry-After", "60");
            return Task.FromResult(response);
        });
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubRepositoryCreationResult result = await client.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.SecondaryRateLimit);
        handler.Requests.Count.ShouldBe(1);
        JsonSerializer.Serialize(result).ShouldNotContain("provider-body-sentinel", Case.Sensitive);
    }

    [Fact]
    public async Task CreateRepositoryMapsMalformedResponseWithoutRetryOrPayloadLeakage()
    {
        RecordingGitHubHttpMessageHandler handler = SuccessHandler(HttpStatusCode.Created, "{ provider-body-sentinel");
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubRepositoryCreationResult result = await client.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.AmbiguousMutationResponse);
        handler.Requests.Count.ShouldBe(1);
        JsonSerializer.Serialize(result).ShouldNotContain("provider-body-sentinel", Case.Sensitive);
    }

    [Fact]
    public async Task CreateRepositoryMapsDisconnectAfterDispatchAsUnknownWithoutRetry()
    {
        RecordingGitHubHttpMessageHandler handler = new((_, _) =>
            throw new HttpRequestException("provider-body-sentinel"));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubRepositoryCreationResult result = await client.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.UnexpectedTransportFailure);
        handler.Requests.Count.ShouldBe(1);
        JsonSerializer.Serialize(result).ShouldNotContain("provider-body-sentinel", Case.Sensitive);
    }

    [Fact]
    public async Task ValidateBindingUsesCanonicalIdentityAndExactBranchWithoutProviderDtoLeakage()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls == 1
            ? JsonResponse(HttpStatusCode.OK, RepositoryJson(101))
            : JsonResponse(HttpStatusCode.OK, BranchJson("main", isProtected: false))));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubRepositoryBindingResult result = await client.ValidateRepositoryBindingAsync(
            BindingRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.CanonicalRepositoryId.ShouldBe("101");
        result.EquivalentExisting.ShouldBeFalse();
        handler.Requests.Count.ShouldBe(2);
        handler.Requests[0].RequestUri.AbsolutePath.ShouldBe("/repos/octokit-owner-sentinel/octokit-repository-sentinel");
        handler.Requests[1].RequestUri.AbsolutePath.ShouldBe("/repos/octokit-owner-sentinel/octokit-repository-sentinel/branches/main");
        foreach (RecordedGitHubHttpRequest observed in handler.Requests)
        {
            observed.Headers["X-GitHub-Api-Version"].ShouldBe(["2022-11-28"]);
        }

        string serialized = JsonSerializer.Serialize(result);
        serialized.ShouldNotContain("octokit-owner-sentinel", Case.Sensitive);
        serialized.ShouldNotContain("octokit-repository-sentinel", Case.Sensitive);
        serialized.ShouldNotContain("main", Case.Sensitive);
    }

    [Fact]
    public async Task ValidateBindingAcceptsAuthorizedAliasOnlyWhenCanonicalIdentityMatches()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls == 1
            ? JsonResponse(HttpStatusCode.OK, RepositoryJson(101))
            : JsonResponse(HttpStatusCode.OK, BranchJson("main", isProtected: false))));
        IGitHubApiClient client = await CreateClientAsync(handler);
        GitHubRepositoryBindingRequest request = BindingRequest() with
        {
            Target = BindingRequest().Target with
            {
                ExpectedCanonicalRepositoryId = "101",
                EquivalentExistingAuthorized = true,
            },
        };

        GitHubRepositoryBindingResult result = await client.ValidateRepositoryBindingAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.EquivalentExisting.ShouldBeTrue();
        result.CanonicalRepositoryId.ShouldBe("101");
    }

    [Fact]
    public async Task ValidateBindingRejectsDefaultBranchMismatchBeforeSelectedRefLookup()
    {
        RecordingGitHubHttpMessageHandler handler = SuccessHandler(HttpStatusCode.OK, RepositoryJson(101, defaultBranch: "provider-default"));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubRepositoryBindingResult result = await client.ValidateRepositoryBindingAsync(
            BindingRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.DefaultBranchConflict);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ValidateBindingUsesExactSelectedRefAndMapsMissingRefSafely()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls == 1
            ? JsonResponse(HttpStatusCode.OK, RepositoryJson(101))
            : JsonResponse(HttpStatusCode.NotFound, SafeErrorJson())));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubRepositoryBindingResult result = await client.ValidateRepositoryBindingAsync(
            BindingRequest() with
            {
                Target = BindingRequest().Target with { SelectedRef = "release/exact" },
            },
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.MissingBranchOrRef);
        handler.Requests[1].RequestUri.AbsolutePath.ShouldEndWith("/branches/release/exact", Case.Sensitive);
    }

    [Fact]
    public async Task ValidateBindingRejectsUnsupportedRefKindWithoutRefLookup()
    {
        RecordingGitHubHttpMessageHandler handler = SuccessHandler(HttpStatusCode.OK, RepositoryJson(101));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubRepositoryBindingResult result = await client.ValidateRepositoryBindingAsync(
            BindingRequest() with
            {
                Target = BindingRequest().Target with { SelectedRefKind = ProviderRepositoryRefKind.Tag },
            },
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.UnsupportedRefOperation);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ValidateBindingSeparatesContentsAndAdministrationPermissionFailures()
    {
        RecordingGitHubHttpMessageHandler contentsHandler = SuccessHandler(
            HttpStatusCode.OK,
            RepositoryJson(101, pull: false));
        IGitHubApiClient contentsClient = await CreateClientAsync(contentsHandler);

        GitHubRepositoryBindingResult contents = await contentsClient.ValidateRepositoryBindingAsync(
            BindingRequest(),
            TestContext.Current.CancellationToken);

        contents.FailureCondition.ShouldBe(GitHubApiFailureCondition.ContentsPermissionInsufficient);

        int calls = 0;
        RecordingGitHubHttpMessageHandler administrationHandler = new((_, _) => Task.FromResult(++calls == 1
            ? JsonResponse(HttpStatusCode.OK, RepositoryJson(101, admin: false))
            : JsonResponse(HttpStatusCode.OK, BranchJson("main", isProtected: true))));
        IGitHubApiClient administrationClient = await CreateClientAsync(administrationHandler);

        GitHubRepositoryBindingResult administration = await administrationClient.ValidateRepositoryBindingAsync(
            BindingRequest() with
            {
                Target = BindingRequest().Target with
                {
                    RequireProtectedRef = true,
                    RequireAdministrationPermission = true,
                },
            },
            TestContext.Current.CancellationToken);

        administration.FailureCondition.ShouldBe(GitHubApiFailureCondition.AdministrationPermissionInsufficient);
        administrationHandler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ValidateBindingInspectsRequiredBranchProtectionThroughExactRef()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, RepositoryJson(101)),
            2 => JsonResponse(HttpStatusCode.OK, BranchJson("main", isProtected: true)),
            _ => JsonResponse(HttpStatusCode.OK, BranchProtectionJson()),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubRepositoryBindingResult result = await client.ValidateRepositoryBindingAsync(
            BindingRequest() with
            {
                Target = BindingRequest().Target with
                {
                    RequireProtectedRef = true,
                    RequireAdministrationPermission = true,
                },
            },
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        handler.Requests.Count.ShouldBe(3);
        handler.Requests[2].RequestUri.AbsolutePath.ShouldEndWith("/branches/main/protection", Case.Sensitive);
    }

    [Fact]
    public async Task ValidateBindingConcealsMissingOrInaccessibleRepository()
    {
        RecordingGitHubHttpMessageHandler handler = SuccessHandler(HttpStatusCode.NotFound, SafeErrorJson());
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubRepositoryBindingResult result = await client.ValidateRepositoryBindingAsync(
            BindingRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.NotFoundOrHidden);
        handler.Requests.Count.ShouldBe(1);
    }

    private static async ValueTask<IGitHubApiClient> CreateClientAsync(RecordingGitHubHttpMessageHandler handler)
    {
        OctokitGitHubApiClientFactory factory = new(() => new HttpClientAdapter(() => handler));
        GitHubCredentialLease credential = GitHubCredentialLease.CreateForTesting("token-sentinel");
        try
        {
            return await factory.CreateAsync(
                new GitHubApiClientRequest(
                    "Hexalith-Folders",
                    "2022-11-28",
                    ProviderCredentialMode.AppInstallationReference,
                    "provider-binding-a",
                    "correlation-a"),
                credential,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
        finally
        {
            await credential.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static GitHubRepositoryCreationRequest CreationRequest()
        => new(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            ProviderBindingRef: "provider-binding-a",
            RepositoryBindingId: "repository-binding-a",
            Target: new ProviderRepositoryResolvedTarget(
                Owner: "octokit-owner-sentinel",
                RepositoryName: "octokit-repository-sentinel",
                Visibility: ProviderRepositoryVisibility.Private,
                DefaultBranch: "main",
                SelectedRef: "main",
                RequireProtectedRef: false,
                RequireContentsPermission: true,
                RequireAdministrationPermission: true,
                ExpectedCanonicalRepositoryId: null,
                EquivalentExistingAuthorized: false),
            CredentialMode: ProviderCredentialMode.AppInstallationReference,
            ApiVersion: "2022-11-28",
            SafeTargetFingerprint: "safe-target-fingerprint-a",
            CorrelationId: "correlation-a",
            IdempotencyKey: "idempotency-a");

    private static GitHubRepositoryBindingRequest BindingRequest()
        => new(
            ManagedTenantId: "tenant-a",
            OrganizationId: "organization-a",
            ProviderBindingRef: "provider-binding-a",
            RepositoryBindingId: "repository-binding-a",
            Target: new ProviderRepositoryResolvedTarget(
                Owner: "octokit-owner-sentinel",
                RepositoryName: "octokit-repository-sentinel",
                Visibility: ProviderRepositoryVisibility.Private,
                DefaultBranch: "main",
                SelectedRef: "main",
                RequireProtectedRef: false,
                RequireContentsPermission: true,
                RequireAdministrationPermission: false,
                ExpectedCanonicalRepositoryId: null,
                EquivalentExistingAuthorized: false),
            CredentialMode: ProviderCredentialMode.AppInstallationReference,
            ApiVersion: "2022-11-28",
            SafeTargetFingerprint: "safe-target-fingerprint-a",
            CorrelationId: "correlation-a",
            IdempotencyKey: "idempotency-a");

    private static RecordingGitHubHttpMessageHandler SuccessHandler(HttpStatusCode statusCode, string body)
        => new((_, _) => Task.FromResult(JsonResponse(statusCode, body)));

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
        => new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private static string RepositoryJson(
        long id,
        string defaultBranch = "main",
        bool pull = true,
        bool admin = true)
        => $$"""
        {
          "id": {{id}},
          "node_id": "repository-node-sentinel",
          "name": "octokit-repository-sentinel",
          "full_name": "octokit-owner-sentinel/octokit-repository-sentinel",
          "private": true,
          "owner": { "login": "octokit-owner-sentinel", "id": 7, "type": "Organization" },
          "html_url": "https://example.invalid/repository-sentinel",
          "url": "https://api.example.invalid/repos/owner/repository",
          "default_branch": "{{defaultBranch}}",
          "permissions": { "admin": {{admin.ToString().ToLowerInvariant()}}, "maintain": true, "push": true, "triage": true, "pull": {{pull.ToString().ToLowerInvariant()}} }
        }
        """;

    private static string RepositoryExistsJson()
        => """
        {
          "message": "Repository creation failed.",
          "errors": [
            { "resource": "Repository", "code": "custom", "field": "name", "message": "name already exists on this account" }
          ]
        }
        """;

    private static string BranchJson(string name, bool isProtected)
        => $$"""
        {
          "name": "{{name}}",
          "commit": { "sha": "0123456789abcdef0123456789abcdef01234567", "url": "https://api.example.invalid/commit" },
          "protected": {{isProtected.ToString().ToLowerInvariant()}}
        }
        """;

    private static string BranchProtectionJson()
        => """
        {
          "url": "https://api.example.invalid/protection",
          "required_status_checks": null,
          "enforce_admins": { "enabled": true },
          "required_pull_request_reviews": null,
          "restrictions": null
        }
        """;

    private static string SafeErrorJson()
        => "{ \"message\": \"Request could not be completed.\" }";
}
