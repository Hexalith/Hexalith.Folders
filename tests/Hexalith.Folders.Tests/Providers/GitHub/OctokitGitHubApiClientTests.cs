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

    [Fact]
    public async Task StageFileChangesUsesOrderedGitDataRequestsWithoutMovingRef()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, GitReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, GitCommitJson(HeadSha, BaseTreeSha)),
            3 => JsonResponse(HttpStatusCode.OK, BaseTreeJson()),
            4 => JsonResponse(HttpStatusCode.Created, GitObjectJson(BlobOneSha)),
            5 => JsonResponse(HttpStatusCode.Created, GitObjectJson(BlobTwoSha)),
            _ => JsonResponse(HttpStatusCode.Created, GitTreeJson(StagedTreeSha)),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileChangeSetResult result = await client.StageFileChangesAsync(
            FileChangeSetRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.StagedTreeSha.ShouldBe(StagedTreeSha);
        handler.Requests.Count.ShouldBe(6);
        handler.Requests.Select(static request => request.Method).ShouldBe(
            [HttpMethod.Get, HttpMethod.Get, HttpMethod.Get, HttpMethod.Post, HttpMethod.Post, HttpMethod.Post]);
        handler.Requests[0].RequestUri.AbsolutePath.ShouldEndWith("/git/refs/heads/main", Case.Sensitive);
        handler.Requests[1].RequestUri.AbsolutePath.ShouldEndWith($"/git/commits/{HeadSha}", Case.Sensitive);
        handler.Requests[2].RequestUri.AbsolutePath.ShouldEndWith($"/git/trees/{BaseTreeSha}", Case.Sensitive);
        handler.Requests[2].RequestUri.Query.ShouldBe("?recursive=1");
        handler.Requests[3].RequestUri.AbsolutePath.ShouldEndWith("/git/blobs", Case.Sensitive);
        handler.Requests[4].RequestUri.AbsolutePath.ShouldEndWith("/git/blobs", Case.Sensitive);
        handler.Requests[5].RequestUri.AbsolutePath.ShouldEndWith("/git/trees", Case.Sensitive);
        handler.Requests.ShouldNotContain(static request => request.Method == HttpMethod.Patch);

        using JsonDocument firstBlob = JsonDocument.Parse(handler.Requests[3].Body.ShouldNotBeNull());
        firstBlob.RootElement.GetProperty("encoding").GetString().ShouldBe("base64");
        firstBlob.RootElement.GetProperty("content").GetString().ShouldBe(Convert.ToBase64String("alpha"u8));

        using JsonDocument tree = JsonDocument.Parse(handler.Requests[5].Body.ShouldNotBeNull());
        JsonElement items = tree.RootElement.GetProperty("tree");
        items.GetArrayLength().ShouldBe(3);
        items[0].GetProperty("path").GetString().ShouldBe("src/a.txt");
        items[0].GetProperty("sha").GetString().ShouldBe(BlobOneSha);
        items[1].GetProperty("path").GetString().ShouldBe("src/b.txt");
        items[1].GetProperty("mode").GetString().ShouldBe("100644");
        items[1].GetProperty("sha").ValueKind.ShouldBe(JsonValueKind.Null);
        items[2].GetProperty("path").GetString().ShouldBe("src/c.txt");
        items[2].GetProperty("mode").GetString().ShouldBe("100755");
        items[2].GetProperty("sha").GetString().ShouldBe(BlobTwoSha);
        tree.RootElement.GetProperty("base_tree").GetString().ShouldBe(BaseTreeSha);
        foreach (RecordedGitHubHttpRequest observed in handler.Requests)
        {
            observed.Headers["X-GitHub-Api-Version"].ShouldBe(["2022-11-28"]);
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "ValidationFailure")]
    [InlineData(HttpStatusCode.Unauthorized, "AuthenticationRequired")]
    [InlineData(HttpStatusCode.Forbidden, "PermissionInsufficient")]
    [InlineData(HttpStatusCode.NotFound, "NotFoundOrHidden")]
    [InlineData(HttpStatusCode.Conflict, "RepositoryConflict")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "ValidationFailure")]
    [InlineData(HttpStatusCode.TooManyRequests, "PrimaryRateLimit")]
    [InlineData(HttpStatusCode.InternalServerError, "AmbiguousMutationResponse")]
    public async Task StageFileChangesMapsKnownAndAmbiguousMutationStatusesWithoutRetry(
        HttpStatusCode statusCode,
        string expectedConditionName)
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, GitReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, GitCommitJson(HeadSha, BaseTreeSha)),
            3 => JsonResponse(HttpStatusCode.OK, BaseTreeJson()),
            _ => JsonResponse(statusCode, "{ \"message\": \"provider-body-sentinel\" }"),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileChangeSetResult result = await client.StageFileChangesAsync(
            FileChangeSetRequest() with { Changes = [FileChangeSetRequest().Changes[0]] },
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(Enum.Parse<GitHubApiFailureCondition>(expectedConditionName));
        handler.Requests.Count.ShouldBe(4);
        JsonSerializer.Serialize(result).ShouldNotContain("provider-body-sentinel", Case.Sensitive);
    }

    [Fact]
    public async Task StageFileChangesTimeoutAfterBlobDispatchIsUnknownAndNeverRetried()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => ++calls switch
        {
            1 => Task.FromResult(JsonResponse(HttpStatusCode.OK, GitReferenceJson(HeadSha))),
            2 => Task.FromResult(JsonResponse(HttpStatusCode.OK, GitCommitJson(HeadSha, BaseTreeSha))),
            3 => Task.FromResult(JsonResponse(HttpStatusCode.OK, BaseTreeJson())),
            _ => throw new TaskCanceledException("provider-body-sentinel"),
        });
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileChangeSetResult result = await client.StageFileChangesAsync(
            FileChangeSetRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.TimeoutDuringMutation);
        handler.Requests.Count.ShouldBe(4);
        JsonSerializer.Serialize(result).ShouldNotContain("provider-body-sentinel", Case.Sensitive);
    }

    [Fact]
    public async Task GitDataCancellationBeforeDispatchHasNoProviderEffect()
    {
        RecordingGitHubHttpMessageHandler handler = SuccessHandler(HttpStatusCode.OK, GitReferenceJson(HeadSha));
        IGitHubApiClient client = await CreateClientAsync(handler);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        GitHubFileChangeSetResult staged = await client.StageFileChangesAsync(FileChangeSetRequest(), cancellation.Token);
        GitHubCommitResult committed = await client.CommitAsync(CommitRequest(), cancellation.Token);
        GitHubMutationStatusResult status = await client.GetMutationStatusAsync(MutationStatusRequest(), cancellation.Token);

        staged.FailureCondition.ShouldBe(GitHubApiFailureCondition.CancellationBeforeDispatch);
        committed.FailureCondition.ShouldBe(GitHubApiFailureCondition.CancellationBeforeDispatch);
        status.FailureCondition.ShouldBe(GitHubApiFailureCondition.CancellationBeforeDispatch);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task StageFileChangesMapsSecondaryRateLimitWithoutLeakingEvidence()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) =>
        {
            calls++;
            if (calls == 1)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, GitReferenceJson(HeadSha)));
            }

            if (calls == 2)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, GitCommitJson(HeadSha, BaseTreeSha)));
            }

            if (calls == 3)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, BaseTreeJson()));
            }

            HttpResponseMessage response = JsonResponse(
                HttpStatusCode.TooManyRequests,
                "{ \"message\": \"provider-body-sentinel\" }");
            response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "4999");
            response.Headers.TryAddWithoutValidation("Retry-After", "60");
            return Task.FromResult(response);
        });
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileChangeSetResult result = await client.StageFileChangesAsync(
            FileChangeSetRequest() with { Changes = [FileChangeSetRequest().Changes[0]] },
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.SecondaryRateLimit);
        result.RetryAfter.ShouldBe(TimeSpan.FromSeconds(60));
        handler.Requests.Count.ShouldBe(4);
        JsonSerializer.Serialize(result).ShouldNotContain("provider-body-sentinel", Case.Sensitive);
    }

    [Fact]
    public async Task StageFileChangesRejectsMovedHeadBeforeAnyMutation()
    {
        RecordingGitHubHttpMessageHandler handler = SuccessHandler(
            HttpStatusCode.OK,
            GitReferenceJson("9999999999999999999999999999999999999999"));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileChangeSetResult result = await client.StageFileChangesAsync(
            FileChangeSetRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.RefMoved);
        handler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Get);
    }

    [Fact]
    public async Task StageFileChangesRejectsBaseCommitMismatchBeforeAnyMutation()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls == 1
            ? JsonResponse(HttpStatusCode.OK, GitReferenceJson(HeadSha))
            : JsonResponse(HttpStatusCode.OK, GitCommitJson(new string('9', 40), BaseTreeSha))));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileChangeSetResult result = await client.StageFileChangesAsync(
            FileChangeSetRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.MalformedResponse);
        handler.Requests.Count.ShouldBe(2);
        // `!= null` rather than `is not null`: Shouldly binds an expression tree here and
        // pattern syntax inside one is CS8122.
        handler.Requests.ShouldNotContain(static request => request.Method != null && request.Method != HttpMethod.Get);
    }

    [Theory]
    [InlineData("add-existing")]
    [InlineData("change-missing")]
    [InlineData("remove-missing")]
    [InlineData("non-blob")]
    [InlineData("unknown-kind")]
    public async Task StageFileChangesEnforcesExactBaseTreePreconditionsWithoutMutation(string scenario)
    {
        ProviderGitResolvedFileChange change = scenario switch
        {
            "add-existing" => new("change-a", "src/b.txt", ProviderFileChangeKind.Add, "alpha"u8.ToArray()),
            "change-missing" => new("change-a", "src/missing.txt", ProviderFileChangeKind.Change, "alpha"u8.ToArray()),
            "remove-missing" => new("change-a", "src/missing.txt", ProviderFileChangeKind.Remove, null),
            "non-blob" => new("change-a", "src/b.txt", ProviderFileChangeKind.Change, "alpha"u8.ToArray()),
            _ => new("change-a", "src/missing.txt", (ProviderFileChangeKind)999, null),
        };
        string baseTree = scenario == "non-blob"
            ? BaseTreeJson().Replace("\"type\": \"blob\"", "\"type\": \"tree\"", StringComparison.Ordinal)
            : BaseTreeJson();
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, GitReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, GitCommitJson(HeadSha, BaseTreeSha)),
            _ => JsonResponse(HttpStatusCode.OK, baseTree),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileChangeSetResult result = await client.StageFileChangesAsync(
            FileChangeSetRequest() with { Changes = [change] },
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.ValidationFailure);
        handler.Requests.Count.ShouldBe(3);
        handler.Requests.ShouldAllBe(static request => request.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task GitOperationsRejectNonCommitReferenceObjects()
    {
        RecordingGitHubHttpMessageHandler stageHandler = SuccessHandler(
            HttpStatusCode.OK,
            GitReferenceJson(HeadSha, "tag"));
        RecordingGitHubHttpMessageHandler commitHandler = SuccessHandler(
            HttpStatusCode.OK,
            GitReferenceJson(HeadSha, "tag"));
        RecordingGitHubHttpMessageHandler statusHandler = SuccessHandler(
            HttpStatusCode.OK,
            GitReferenceJson(CommitSha, "tag"));
        IGitHubApiClient stageClient = await CreateClientAsync(stageHandler);
        IGitHubApiClient commitClient = await CreateClientAsync(commitHandler);
        IGitHubApiClient statusClient = await CreateClientAsync(statusHandler);

        GitHubFileChangeSetResult staged = await stageClient.StageFileChangesAsync(
            FileChangeSetRequest(),
            TestContext.Current.CancellationToken);
        GitHubCommitResult committed = await commitClient.CommitAsync(
            CommitRequest(),
            TestContext.Current.CancellationToken);
        GitHubMutationStatusResult status = await statusClient.GetMutationStatusAsync(
            MutationStatusRequest(),
            TestContext.Current.CancellationToken);

        staged.FailureCondition.ShouldBe(GitHubApiFailureCondition.MalformedResponse);
        committed.FailureCondition.ShouldBe(GitHubApiFailureCondition.MalformedResponse);
        status.FailureCondition.ShouldBe(GitHubApiFailureCondition.MalformedResponse);
        stageHandler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Get);
        commitHandler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Get);
        statusHandler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Get);
    }

    [Fact]
    public async Task ExplicitCommitCreatesOneCommitAndPerformsOneNonForceRefUpdate()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, GitReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.Created, GitCommitJson(CommitSha, StagedTreeSha)),
            _ => JsonResponse(HttpStatusCode.OK, GitReferenceJson(CommitSha)),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubCommitResult result = await client.CommitAsync(
            CommitRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.CommitSha.ShouldBe(CommitSha);
        handler.Requests.Count.ShouldBe(3);
        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[1].Method.ShouldBe(HttpMethod.Post);
        handler.Requests[1].RequestUri.AbsolutePath.ShouldEndWith("/git/commits", Case.Sensitive);
        handler.Requests[2].Method.ShouldBe(HttpMethod.Patch);
        handler.Requests[2].RequestUri.AbsolutePath.ShouldEndWith("/git/refs/heads/main", Case.Sensitive);
        handler.Requests.Count(static request => request.Method == HttpMethod.Patch).ShouldBe(1);

        using JsonDocument commit = JsonDocument.Parse(handler.Requests[1].Body.ShouldNotBeNull());
        commit.RootElement.GetProperty("message").GetString().ShouldBe("provider commit message sentinel");
        commit.RootElement.GetProperty("tree").GetString().ShouldBe(StagedTreeSha);
        commit.RootElement.GetProperty("parents")[0].GetString().ShouldBe(HeadSha);

        using JsonDocument update = JsonDocument.Parse(handler.Requests[2].Body.ShouldNotBeNull());
        update.RootElement.GetProperty("sha").GetString().ShouldBe(CommitSha);
        update.RootElement.GetProperty("force").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task ExplicitCommitRejectsMovedHeadBeforeAnyMutation()
    {
        RecordingGitHubHttpMessageHandler handler = SuccessHandler(
            HttpStatusCode.OK,
            GitReferenceJson("9999999999999999999999999999999999999999"));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubCommitResult result = await client.CommitAsync(
            CommitRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.RefMoved);
        handler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Get);
    }

    [Fact]
    public async Task ExplicitCommitMapsRefConflictWithoutSecondUpdate()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, GitReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.Created, GitCommitJson(CommitSha, StagedTreeSha)),
            _ => JsonResponse(HttpStatusCode.UnprocessableEntity, SafeErrorJson()),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubCommitResult result = await client.CommitAsync(
            CommitRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.RefUpdateConflict);
        handler.Requests.Count(static request => request.Method == HttpMethod.Patch).ShouldBe(1);
        handler.Requests.Count.ShouldBe(3);
    }

    [Theory]
    [InlineData("wrong-tree")]
    [InlineData("wrong-parent")]
    [InlineData("uppercase-sha")]
    public async Task ExplicitCommitRejectsMalformedCreatedCommitBeforeRefMovement(string scenario)
    {
        string createdCommit = scenario switch
        {
            "wrong-tree" => GitCommitJson(CommitSha, new string('8', 40)),
            "wrong-parent" => GitCommitJson(CommitSha, StagedTreeSha)
                .Replace(HeadSha, new string('9', 40), StringComparison.Ordinal),
            _ => GitCommitJson(CommitSha.ToUpperInvariant(), StagedTreeSha),
        };
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls == 1
            ? JsonResponse(HttpStatusCode.OK, GitReferenceJson(HeadSha))
            : JsonResponse(HttpStatusCode.Created, createdCommit)));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubCommitResult result = await client.CommitAsync(
            CommitRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.AmbiguousMutationResponse);
        handler.Requests.Count.ShouldBe(2);
        handler.Requests.Count(static request => request.Method == HttpMethod.Post).ShouldBe(1);
        handler.Requests.ShouldNotContain(static request => request.Method == HttpMethod.Patch);
    }

    [Theory]
    [InlineData("timeout", "TimeoutDuringMutation")]
    [InlineData("disconnect", "UnexpectedTransportFailure")]
    [InlineData("malformed", "AmbiguousMutationResponse")]
    [InlineData("server", "AmbiguousMutationResponse")]
    [InlineData("rate", "PrimaryRateLimit")]
    [InlineData("conflict", "RepositoryConflict")]
    public async Task CommitPostFailuresNeverDispatchARefUpdate(
        string scenario,
        string expectedConditionName)
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) =>
        {
            calls++;
            if (calls == 1)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, GitReferenceJson(HeadSha)));
            }

            return scenario switch
            {
                "timeout" => throw new TaskCanceledException("post timeout sentinel"),
                "disconnect" => throw new HttpRequestException("post disconnect sentinel"),
                "malformed" => Task.FromResult(JsonResponse(HttpStatusCode.Created, "{ malformed sentinel")),
                "server" => Task.FromResult(JsonResponse(HttpStatusCode.InternalServerError, SafeErrorJson())),
                "rate" => Task.FromResult(JsonResponse(HttpStatusCode.TooManyRequests, SafeErrorJson())),
                _ => Task.FromResult(JsonResponse(HttpStatusCode.Conflict, SafeErrorJson())),
            };
        });
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubCommitResult result = await client.CommitAsync(
            CommitRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(Enum.Parse<GitHubApiFailureCondition>(expectedConditionName));
        handler.Requests.Count.ShouldBe(2);
        handler.Requests.Count(static request => request.Method == HttpMethod.Post).ShouldBe(1);
        handler.Requests.ShouldNotContain(static request => request.Method == HttpMethod.Patch);
    }

    [Theory]
    [InlineData("timeout", "TimeoutDuringMutation")]
    [InlineData("disconnect", "UnexpectedTransportFailure")]
    [InlineData("malformed", "AmbiguousMutationResponse")]
    [InlineData("server", "AmbiguousMutationResponse")]
    [InlineData("rate", "PrimaryRateLimit")]
    [InlineData("conflict", "RefUpdateConflict")]
    public async Task RefPatchFailuresPreservePrivateCommitEvidenceAndNeverRetry(
        string scenario,
        string expectedConditionName)
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) =>
        {
            calls++;
            if (calls == 1)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, GitReferenceJson(HeadSha)));
            }

            if (calls == 2)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.Created, GitCommitJson(CommitSha, StagedTreeSha)));
            }

            return scenario switch
            {
                "timeout" => throw new TaskCanceledException("patch timeout sentinel"),
                "disconnect" => throw new HttpRequestException("patch disconnect sentinel"),
                "malformed" => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{ malformed sentinel")),
                "server" => Task.FromResult(JsonResponse(HttpStatusCode.InternalServerError, SafeErrorJson())),
                "rate" => Task.FromResult(JsonResponse(HttpStatusCode.TooManyRequests, SafeErrorJson())),
                _ => Task.FromResult(JsonResponse(HttpStatusCode.UnprocessableEntity, SafeErrorJson())),
            };
        });
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubCommitResult result = await client.CommitAsync(
            CommitRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(Enum.Parse<GitHubApiFailureCondition>(expectedConditionName));
        result.CommitSha.ShouldBe(CommitSha);
        handler.Requests.Count.ShouldBe(3);
        handler.Requests.Count(static request => request.Method == HttpMethod.Post).ShouldBe(1);
        handler.Requests.Count(static request => request.Method == HttpMethod.Patch).ShouldBe(1);
    }

    [Theory]
    [InlineData(HeadSha, "NotApplied")]
    [InlineData(CommitSha, "Confirmed")]
    [InlineData("9999999999999999999999999999999999999999", "Conflicting")]
    public async Task MutationStatusReadsExactRefWithoutAnyMutation(
        string observedSha,
        string expectedDispositionName)
    {
        RecordingGitHubHttpMessageHandler handler = SuccessHandler(HttpStatusCode.OK, GitReferenceJson(observedSha));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubMutationStatusResult result = await client.GetMutationStatusAsync(
            MutationStatusRequest(),
            TestContext.Current.CancellationToken);

        result.Disposition.ShouldBe(Enum.Parse<GitHubMutationStatusDisposition>(expectedDispositionName));
        RecordedGitHubHttpRequest observed = handler.Requests.ShouldHaveSingleItem();
        observed.Method.ShouldBe(HttpMethod.Get);
        observed.RequestUri.AbsolutePath.ShouldEndWith("/git/refs/heads/main", Case.Sensitive);
    }

    [Fact]
    public async Task MutationStatusConcealsMissingOrHiddenRefAsUnavailableWithoutMutation()
    {
        RecordingGitHubHttpMessageHandler handler = SuccessHandler(HttpStatusCode.NotFound, SafeErrorJson());
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubMutationStatusResult result = await client.GetMutationStatusAsync(
            MutationStatusRequest(),
            TestContext.Current.CancellationToken);

        result.Disposition.ShouldBe(GitHubMutationStatusDisposition.Unavailable);
        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.NotFoundOrHidden);
        handler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Get);
    }

    [Fact]
    public async Task MutationStatusRejectsEqualOrNonCanonicalExpectedShasWithoutObservation()
    {
        RecordingGitHubHttpMessageHandler equalHandler = SuccessHandler(HttpStatusCode.OK, GitReferenceJson(HeadSha));
        RecordingGitHubHttpMessageHandler uppercaseHandler = SuccessHandler(HttpStatusCode.OK, GitReferenceJson(HeadSha));
        IGitHubApiClient equalClient = await CreateClientAsync(equalHandler);
        IGitHubApiClient uppercaseClient = await CreateClientAsync(uppercaseHandler);

        GitHubMutationStatusResult equal = await equalClient.GetMutationStatusAsync(
            MutationStatusRequest() with { ExpectedCommitSha = HeadSha },
            TestContext.Current.CancellationToken);
        GitHubMutationStatusResult uppercase = await uppercaseClient.GetMutationStatusAsync(
            MutationStatusRequest() with { ExpectedCommitSha = CommitSha.ToUpperInvariant() },
            TestContext.Current.CancellationToken);

        equal.FailureCondition.ShouldBe(GitHubApiFailureCondition.ValidationFailure);
        uppercase.FailureCondition.ShouldBe(GitHubApiFailureCondition.ValidationFailure);
        equalHandler.Requests.ShouldBeEmpty();
        uppercaseHandler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("timeout", "TimeoutDuringObservation")]
    [InlineData("disconnect", "TimeoutDuringObservation")]
    [InlineData("malformed", "MalformedResponse")]
    [InlineData("server", "ServerUnavailable")]
    [InlineData("rate", "PrimaryRateLimit")]
    public async Task MutationStatusTransportFailuresUseOneReadAndNoMutation(
        string scenario,
        string expectedConditionName)
    {
        RecordingGitHubHttpMessageHandler handler = new((_, _) => scenario switch
        {
            "timeout" => throw new TaskCanceledException("status timeout sentinel"),
            "disconnect" => throw new HttpRequestException("status disconnect sentinel"),
            "malformed" => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{ malformed sentinel")),
            "server" => Task.FromResult(JsonResponse(HttpStatusCode.InternalServerError, SafeErrorJson())),
            _ => Task.FromResult(JsonResponse(HttpStatusCode.TooManyRequests, SafeErrorJson())),
        });
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubMutationStatusResult result = await client.GetMutationStatusAsync(
            MutationStatusRequest(),
            TestContext.Current.CancellationToken);

        result.Disposition.ShouldBe(GitHubMutationStatusDisposition.Unavailable);
        result.FailureCondition.ShouldBe(Enum.Parse<GitHubApiFailureCondition>(expectedConditionName));
        handler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Get);
    }

    [Fact]
    public async Task InFlightCancellationClassifiesObservationAndMutationPhases()
    {
        using CancellationTokenSource observationCancellation = new();
        RecordingGitHubHttpMessageHandler observationHandler = new((_, _) =>
        {
            observationCancellation.Cancel();
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, GitReferenceJson(HeadSha)));
        });
        IGitHubApiClient observationClient = await CreateClientAsync(observationHandler);
        GitHubFileChangeSetResult observation = await observationClient.StageFileChangesAsync(
            FileChangeSetRequest(),
            observationCancellation.Token);

        using CancellationTokenSource stageCancellation = new();
        int stageCalls = 0;
        RecordingGitHubHttpMessageHandler stageHandler = new((_, _) =>
        {
            stageCalls++;
            if (stageCalls == 1)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, GitReferenceJson(HeadSha)));
            }

            if (stageCalls == 2)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, GitCommitJson(HeadSha, BaseTreeSha)));
            }

            if (stageCalls == 3)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, BaseTreeJson()));
            }

            stageCancellation.Cancel();
            return Task.FromResult(JsonResponse(HttpStatusCode.Created, GitObjectJson(BlobOneSha)));
        });
        IGitHubApiClient stageClient = await CreateClientAsync(stageHandler);
        GitHubFileChangeSetResult staged = await stageClient.StageFileChangesAsync(
            FileChangeSetRequest() with { Changes = [FileChangeSetRequest().Changes[0]] },
            stageCancellation.Token);

        using CancellationTokenSource commitCancellation = new();
        int commitCalls = 0;
        RecordingGitHubHttpMessageHandler commitHandler = new((_, _) =>
        {
            commitCalls++;
            if (commitCalls == 1)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, GitReferenceJson(HeadSha)));
            }

            commitCancellation.Cancel();
            return Task.FromResult(JsonResponse(HttpStatusCode.Created, GitCommitJson(CommitSha, StagedTreeSha)));
        });
        IGitHubApiClient commitClient = await CreateClientAsync(commitHandler);
        GitHubCommitResult committed = await commitClient.CommitAsync(
            CommitRequest(),
            commitCancellation.Token);

        using CancellationTokenSource statusCancellation = new();
        RecordingGitHubHttpMessageHandler statusHandler = new((_, _) =>
        {
            statusCancellation.Cancel();
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, GitReferenceJson(CommitSha)));
        });
        IGitHubApiClient statusClient = await CreateClientAsync(statusHandler);
        GitHubMutationStatusResult status = await statusClient.GetMutationStatusAsync(
            MutationStatusRequest(),
            statusCancellation.Token);

        observation.FailureCondition.ShouldBe(GitHubApiFailureCondition.CancellationBeforeDispatch);
        observationHandler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Get);
        staged.FailureCondition.ShouldBe(GitHubApiFailureCondition.AmbiguousMutationResponse);
        stageHandler.Requests.Count.ShouldBe(4);
        committed.FailureCondition.ShouldBe(GitHubApiFailureCondition.AmbiguousMutationResponse);
        committed.CommitSha.ShouldBe(CommitSha);
        commitHandler.Requests.Count.ShouldBe(2);
        commitHandler.Requests.ShouldNotContain(static request => request.Method == HttpMethod.Patch);
        status.Disposition.ShouldBe(GitHubMutationStatusDisposition.Unavailable);
        status.FailureCondition.ShouldBe(GitHubApiFailureCondition.TimeoutDuringObservation);
        statusHandler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Get);
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

    private static GitHubFileChangeSetRequest FileChangeSetRequest()
        => new(
            Target: GitTarget(),
            ExpectedHeadSha: HeadSha,
            Changes:
            [
                new ProviderGitResolvedFileChange("change-a", "src/a.txt", ProviderFileChangeKind.Add, "alpha"u8.ToArray()),
                new ProviderGitResolvedFileChange("change-b", "src/b.txt", ProviderFileChangeKind.Remove, null),
                new ProviderGitResolvedFileChange("change-c", "src/c.txt", ProviderFileChangeKind.Change, "beta"u8.ToArray()),
            ],
            SafeTargetFingerprint: new string('a', 64),
            ReconciliationReference: new string('b', 64));

    private static GitHubCommitRequest CommitRequest()
        => new(
            Target: GitTarget(),
            ExpectedHeadSha: HeadSha,
            StagedTreeSha: StagedTreeSha,
            CommitMessage: "provider commit message sentinel",
            SafeTargetFingerprint: new string('a', 64),
            ReconciliationReference: new string('b', 64));

    private static GitHubMutationStatusRequest MutationStatusRequest()
        => new(GitTarget(), HeadSha, CommitSha);

    private static ProviderRepositoryResolvedTarget GitTarget()
        => new(
            Owner: "octokit-owner-sentinel",
            RepositoryName: "octokit-repository-sentinel",
            Visibility: ProviderRepositoryVisibility.Private,
            DefaultBranch: "main",
            SelectedRef: "main",
            RequireProtectedRef: false,
            RequireContentsPermission: true,
            RequireAdministrationPermission: false,
            ExpectedCanonicalRepositoryId: "101",
            EquivalentExistingAuthorized: false);

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

    private static string GitReferenceJson(string sha, string objectType = "commit")
        => $$"""
        {
          "ref": "refs/heads/main",
          "node_id": "reference-node-sentinel",
          "url": "https://api.example.invalid/reference",
          "object": {
            "type": "{{objectType}}",
            "sha": "{{sha}}",
            "url": "https://api.example.invalid/object"
          }
        }
        """;

    private static string GitCommitJson(string sha, string treeSha)
        => $$"""
        {
          "sha": "{{sha}}",
          "url": "https://api.example.invalid/commit",
          "message": "provider commit message sentinel",
          "tree": { "sha": "{{treeSha}}", "url": "https://api.example.invalid/tree" },
          "parents": [ { "sha": "{{HeadSha}}", "url": "https://api.example.invalid/parent" } ]
        }
        """;

    private static string GitObjectJson(string sha)
        => $$"""{ "sha": "{{sha}}", "url": "https://api.example.invalid/object" }""";

    private static string GitTreeJson(string sha)
        => $$"""
        {
          "sha": "{{sha}}",
          "url": "https://api.example.invalid/tree",
          "tree": [],
          "truncated": false
        }
        """;

    private static string BaseTreeJson()
        => $$"""
        {
          "sha": "{{BaseTreeSha}}",
          "url": "https://api.example.invalid/tree",
          "tree": [
            { "path": "src/b.txt", "mode": "100644", "type": "blob", "sha": "{{BlobOneSha}}", "size": 5, "url": "https://api.example.invalid/blob-b" },
            { "path": "src/c.txt", "mode": "100755", "type": "blob", "sha": "{{BlobTwoSha}}", "size": 4, "url": "https://api.example.invalid/blob-c" }
          ],
          "truncated": false
        }
        """;

    private const string HeadSha = "1111111111111111111111111111111111111111";
    private const string BaseTreeSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string BlobOneSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string BlobTwoSha = "cccccccccccccccccccccccccccccccccccccccc";
    private const string StagedTreeSha = "2222222222222222222222222222222222222222";
    // Must contain hex letters: the non-canonical-SHA scenarios uppercase this constant, and a
    // digits-only value would make ToUpperInvariant() a no-op and silently pass a canonical SHA.
    private const string CommitSha = "33333333333333333333333333333333333333cc";

    private static string SafeErrorJson()
        => "{ \"message\": \"Request could not be completed.\" }";
}
