using System.Net;
using System.Text;
using System.Text.Json;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.GitHub;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.GitHub;

public sealed partial class OctokitGitHubApiClientTests
{
    private const string HeadSha = "1111111111111111111111111111111111111111";
    private const string TreeSha = "2222222222222222222222222222222222222222";
    private const string CommitSha = "3333333333333333333333333333333333333333";
    private const string BaseTreeSha = "4444444444444444444444444444444444444444";
    private const string BlobSha = "5555555555555555555555555555555555555555";

    [Fact]
    public async Task StageFileChangesUsesGitDataInCallerOrderWithoutContentsWriteOrRefMovement()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
            3 => JsonResponse(HttpStatusCode.OK, BaseTreeJson()),
            4 => JsonResponse(HttpStatusCode.Created, BlobJson(BlobSha)),
            5 => JsonResponse(HttpStatusCode.Created, TreeJson(TreeSha)),
            _ => JsonResponse(HttpStatusCode.OK, ResultingTreeJson(TreeSha, ("docs/one.txt", BlobSha))),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.TreeSha.ShouldBe(TreeSha);
        handler.Requests.Select(static request => request.Method.Method)
            .ShouldBe(["GET", "GET", "GET", "POST", "POST", "GET"]);
        handler.Requests.Select(static request => request.RequestUri.AbsolutePath)
            .ShouldBe(
            [
                "/repos/octokit-owner-sentinel/octokit-repository-sentinel/git/refs/heads%2Fmain",
                $"/repos/octokit-owner-sentinel/octokit-repository-sentinel/git/commits/{HeadSha}",
                $"/repos/octokit-owner-sentinel/octokit-repository-sentinel/git/trees/{BaseTreeSha}",
                "/repos/octokit-owner-sentinel/octokit-repository-sentinel/git/blobs",
                "/repos/octokit-owner-sentinel/octokit-repository-sentinel/git/trees",
                $"/repos/octokit-owner-sentinel/octokit-repository-sentinel/git/trees/{TreeSha}",
            ]);
        handler.Requests.ShouldAllBe(static request =>
            request.Headers["X-GitHub-Api-Version"].Contains("2022-11-28", StringComparer.Ordinal));
        handler.Requests.ShouldNotContain(static request =>
            request.RequestUri.AbsolutePath.Contains("/contents/", StringComparison.Ordinal));
        handler.Requests.ShouldNotContain(static request => request.Method == HttpMethod.Patch);

        using JsonDocument blob = JsonDocument.Parse(handler.Requests[3].Body!);
        blob.RootElement.GetProperty("encoding").GetString().ShouldBe("base64");
        blob.RootElement.GetProperty("content").GetString().ShouldBe(Convert.ToBase64String("one"u8));
        using JsonDocument tree = JsonDocument.Parse(handler.Requests[4].Body!);
        tree.RootElement.GetProperty("base_tree").GetString().ShouldBe(BaseTreeSha);
        JsonElement.ArrayEnumerator entries = tree.RootElement.GetProperty("tree").EnumerateArray();
        entries.MoveNext().ShouldBeTrue();
        entries.Current.GetProperty("path").GetString().ShouldBe("docs/one.txt");
        entries.Current.GetProperty("sha").GetString().ShouldBe(BlobSha);
        entries.MoveNext().ShouldBeTrue();
        entries.Current.GetProperty("path").GetString().ShouldBe("docs/two.txt");
        entries.Current.GetProperty("sha").ValueKind.ShouldBe(JsonValueKind.Null);
        entries.MoveNext().ShouldBeFalse();
    }

    [Fact]
    public async Task StageFileChangesRejectsMovedHeadBeforeBlobOrTreeDispatch()
    {
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(
            JsonResponse(HttpStatusCode.OK, ReferenceJson(CommitSha))));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.RefHeadConflict);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task StageFileChangesCancellationBeforeDispatchSendsNoProviderRequest()
    {
        RecordingGitHubHttpMessageHandler handler = new((_, _) => throw new InvalidOperationException("not expected"));
        IGitHubApiClient client = await CreateClientAsync(handler);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            cancellation.Token);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.CancellationBeforeDispatch);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task OperationHandlerConstructionFailureMapsToKnownUnavailableWithoutEscaping()
    {
        OctokitGitHubApiClientFactory factory = new(
            static () => new Octokit.Internal.HttpClientAdapter(static () => new HttpClientHandler()),
            static () => throw new InvalidOperationException("provider-body-sentinel"));
        GitHubCredentialLease credential = GitHubCredentialLease.CreateForTesting("token-sentinel");
        IGitHubApiClient client;
        try
        {
            client = await factory.CreateAsync(
                new GitHubApiClientRequest(
                    "Hexalith-Folders",
                    "2022-11-28",
                    ProviderCredentialMode.AppInstallationReference,
                    "provider-binding-a",
                    "correlation-a"),
                credential,
                TestContext.Current.CancellationToken);
        }
        finally
        {
            await credential.DisposeAsync();
        }

        GitHubFileMutationResult mutation = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);
        GitHubCommitResult commit = await client.CommitAsync(
            CommitTransportRequest(),
            TestContext.Current.CancellationToken);
        GitHubOperationStatusResult status = await client.GetOperationStatusAsync(
            StatusTransportRequest(),
            TestContext.Current.CancellationToken);

        mutation.FailureCondition.ShouldBe(GitHubApiFailureCondition.ServerUnavailable);
        commit.FailureCondition.ShouldBe(GitHubApiFailureCondition.ServerUnavailable);
        status.FailureCondition.ShouldBe(GitHubApiFailureCondition.ServerUnavailable);
        JsonSerializer.Serialize(new { mutation, commit, status }).ShouldNotContain("provider-body-sentinel", Case.Sensitive);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, (int)GitHubApiFailureCondition.ContentPolicyViolation)]
    [InlineData(HttpStatusCode.Unauthorized, (int)GitHubApiFailureCondition.AuthenticationRequired)]
    [InlineData(HttpStatusCode.Forbidden, (int)GitHubApiFailureCondition.PermissionInsufficient)]
    [InlineData(HttpStatusCode.NotFound, (int)GitHubApiFailureCondition.NotFoundOrHidden)]
    [InlineData(HttpStatusCode.Conflict, (int)GitHubApiFailureCondition.ContentPolicyViolation)]
    [InlineData(HttpStatusCode.UnprocessableEntity, (int)GitHubApiFailureCondition.ContentPolicyViolation)]
    [InlineData(HttpStatusCode.TooManyRequests, (int)GitHubApiFailureCondition.TimeoutDuringMutation)]
    [InlineData(HttpStatusCode.InternalServerError, (int)GitHubApiFailureCondition.TimeoutDuringMutation)]
    public async Task StageFileChangesMapsKnownAndAmbiguousStatusesWithoutSecondMutation(
        HttpStatusCode statusCode,
        int expectedCondition)
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
            3 => JsonResponse(HttpStatusCode.OK, BaseTreeJson()),
            _ => JsonResponse(statusCode, SafeErrorJson()),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe((GitHubApiFailureCondition)expectedCondition);
        handler.Requests.Count.ShouldBe(4);
    }

    [Theory]
    [InlineData(false, (int)GitHubApiFailureCondition.TimeoutDuringMutation)]
    [InlineData(true, (int)GitHubApiFailureCondition.TimeoutDuringMutation)]
    public async Task StageFileChangesSeparatesPrimaryAndSecondaryRateLimits(
        bool secondary,
        int expectedCondition)
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) =>
        {
            if (++calls == 1)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)));
            }

            if (calls == 2)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)));
            }

            if (calls == 3)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, BaseTreeJson()));
            }

            HttpResponseMessage response = JsonResponse(HttpStatusCode.Forbidden, SafeErrorJson());
            response.Headers.TryAddWithoutValidation("Retry-After", "60");
            response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", secondary ? "1" : "0");
            return Task.FromResult(response);
        });
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe((GitHubApiFailureCondition)expectedCondition);
        result.RetryAfter.ShouldBe(TimeSpan.FromSeconds(60));
        handler.Requests.Count.ShouldBe(4);
    }

    [Fact]
    public async Task StageRecognizesSecondaryLimitWithoutRetryAfterWhenPrimaryQuotaRemains()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) =>
        {
            HttpResponseMessage response = ++calls switch
            {
                1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
                2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
                3 => JsonResponse(HttpStatusCode.OK, BaseTreeJson()),
                _ => JsonResponse(HttpStatusCode.Forbidden, SafeErrorJson()),
            };
            if (calls == 4)
            {
                response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "42");
            }

            return Task.FromResult(response);
        });
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.TimeoutDuringMutation);
        result.RetryAfter.ShouldBeNull();
    }

    [Fact]
    public async Task StageParsesBoundedHttpDateRetryAfter()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) =>
        {
            HttpResponseMessage response = ++calls switch
            {
                1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
                2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
                3 => JsonResponse(HttpStatusCode.OK, BaseTreeJson()),
                _ => JsonResponse(HttpStatusCode.TooManyRequests, SafeErrorJson()),
            };
            if (calls == 4)
            {
                response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "0");
                response.Headers.TryAddWithoutValidation("Retry-After", DateTimeOffset.UtcNow.AddMinutes(1).ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            }

            return Task.FromResult(response);
        });
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.TimeoutDuringMutation);
        result.RetryAfter.ShouldNotBeNull();
        result.RetryAfter.Value.ShouldBeGreaterThan(TimeSpan.Zero);
        result.RetryAfter.Value.ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task StageFileChangesDisconnectAfterBlobDispatchIsUnknownWithoutRetry()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => ++calls switch
        {
            1 => Task.FromResult(JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha))),
            2 => Task.FromResult(JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha))),
            3 => Task.FromResult(JsonResponse(HttpStatusCode.OK, BaseTreeJson())),
            _ => throw new HttpRequestException("provider-body-sentinel"),
        });
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.UnexpectedTransportFailure);
        handler.Requests.Count.ShouldBe(4);
        JsonSerializer.Serialize(result).ShouldNotContain("provider-body-sentinel", Case.Sensitive);
    }

    [Fact]
    public async Task StageFileChangesCancellationAfterBlobDispatchIsUnknownWithoutRetry()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => ++calls switch
        {
            1 => Task.FromResult(JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha))),
            2 => Task.FromResult(JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha))),
            3 => Task.FromResult(JsonResponse(HttpStatusCode.OK, BaseTreeJson())),
            _ => throw new OperationCanceledException("provider-body-sentinel"),
        });
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.TimeoutDuringMutation);
        handler.Requests.Count.ShouldBe(4);
        JsonSerializer.Serialize(result).ShouldNotContain("provider-body-sentinel", Case.Sensitive);
    }

    [Fact]
    public async Task StageChangeOnlyUsesBlobAndTreeWithoutDeletionTransport()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
            3 => JsonResponse(HttpStatusCode.OK, BaseTreeJson("docs/one.txt")),
            4 => JsonResponse(HttpStatusCode.Created, BlobJson(BlobSha)),
            5 => JsonResponse(HttpStatusCode.Created, TreeJson(TreeSha)),
            _ => JsonResponse(HttpStatusCode.OK, ResultingTreeJson(TreeSha, ("docs/one.txt", BlobSha))),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);
        GitHubFileMutationRequest request = new(
            TransportTarget(),
            [new ProviderResolvedFileChange(0, ProviderFileChangeKind.Change, "docs/one.txt", "changed"u8.ToArray(), ProviderFileContentType.RegularFile)],
            static _ => ValueTask.FromResult(true));

        GitHubFileMutationResult result = await client.StageFileChangesAsync(request, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.FailureCondition.ToString());
        handler.Requests.Select(static request => request.Method.Method).ShouldBe(["GET", "GET", "GET", "POST", "POST", "GET"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity, true, (int)GitHubApiFailureCondition.ContentPolicyViolation)]
    [InlineData(HttpStatusCode.InternalServerError, true, (int)GitHubApiFailureCondition.TimeoutDuringMutation)]
    [InlineData(HttpStatusCode.Created, false, (int)GitHubApiFailureCondition.AmbiguousMutationResponse)]
    public async Task RemoveOnlyTreeFailureAtFourthRequestIsMappedWithoutRetry(
        HttpStatusCode statusCode,
        bool safeError,
        int expectedCondition)
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
            3 => JsonResponse(HttpStatusCode.OK, BaseTreeJson("docs/two.txt")),
            _ => JsonResponse(statusCode, safeError ? SafeErrorJson() : "{}"),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);
        GitHubFileMutationRequest request = new(
            TransportTarget(),
            [new ProviderResolvedFileChange(0, ProviderFileChangeKind.Remove, "docs/two.txt", ReadOnlyMemory<byte>.Empty, ProviderFileContentType.RegularFile)],
            static _ => ValueTask.FromResult(true));

        GitHubFileMutationResult result = await client.StageFileChangesAsync(request, TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe((GitHubApiFailureCondition)expectedCondition);
        handler.Requests.Count.ShouldBe(4);
    }

    [Theory]
    [InlineData(ProviderFileChangeKind.Add, "docs/two.txt")]
    [InlineData(ProviderFileChangeKind.Change, "docs/missing.txt")]
    [InlineData(ProviderFileChangeKind.Remove, "docs/missing.txt")]
    public async Task StageEnforcesBaseTreePathExistence(ProviderFileChangeKind kind, string path)
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
            _ => JsonResponse(HttpStatusCode.OK, BaseTreeJson("docs/two.txt")),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);
        ReadOnlyMemory<byte> content = kind == ProviderFileChangeKind.Remove ? ReadOnlyMemory<byte>.Empty : "content"u8.ToArray();

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            new GitHubFileMutationRequest(
                TransportTarget(),
                [new ProviderResolvedFileChange(0, kind, path, content, ProviderFileContentType.RegularFile)],
                static _ => ValueTask.FromResult(true)),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.ContentPolicyViolation);
        handler.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task StageValidatesOnlyTouchedRegularFilesAndAcceptsUnrelatedGitModes()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
            3 => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(
                BaseTreeSha,
                truncated: false,
                ("docs/two.txt", "100644", "blob", BlobSha),
                ("tools/run.sh", "100755", "blob", "6666666666666666666666666666666666666666"),
                ("latest", "120000", "blob", "7777777777777777777777777777777777777777"),
                ("vendor/module", "160000", "commit", "8888888888888888888888888888888888888888"),
                ("nested", "040000", "tree", "9999999999999999999999999999999999999999"))),
            4 => JsonResponse(HttpStatusCode.Created, BlobJson(BlobSha)),
            5 => JsonResponse(HttpStatusCode.Created, TreeJson(TreeSha)),
            _ => JsonResponse(HttpStatusCode.OK, ResultingTreeJson(TreeSha, ("docs/one.txt", BlobSha))),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.FailureCondition.ToString());
        handler.Requests.Count.ShouldBe(6);
    }

    [Fact]
    public async Task StageFallsBackThroughTouchedAncestorsWhenRecursiveTreesAreTruncated()
    {
        const string docsBaseTreeSha = "6666666666666666666666666666666666666666";
        const string docsResultTreeSha = "7777777777777777777777777777777777777777";
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
            3 => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(BaseTreeSha, truncated: true)),
            4 => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(BaseTreeSha, truncated: false, ("docs", "040000", "tree", docsBaseTreeSha))),
            5 => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(docsBaseTreeSha, truncated: false, ("two.txt", "100644", "blob", BlobSha))),
            6 => JsonResponse(HttpStatusCode.Created, BlobJson(BlobSha)),
            7 => JsonResponse(HttpStatusCode.Created, TreeJson(TreeSha)),
            8 => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(TreeSha, truncated: true)),
            9 => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(TreeSha, truncated: false, ("docs", "040000", "tree", docsResultTreeSha))),
            _ => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(docsResultTreeSha, truncated: false, ("one.txt", "100644", "blob", BlobSha))),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.FailureCondition.ToString());
        handler.Requests.Count.ShouldBe(10);
        handler.Requests[3].RequestUri.Query.ShouldBeEmpty();
        handler.Requests[4].RequestUri.AbsolutePath.ShouldEndWith($"/git/trees/{docsBaseTreeSha}", Case.Sensitive);
    }

    [Fact]
    public async Task StageRejectsOversizedTreeEvidenceAsResponseLimitRatherThanCallerPolicy()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
            _ => JsonResponse(HttpStatusCode.OK, new string(' ', (7 * 1024 * 1024) + 1)),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.ResponseLimitExceeded);
        result.FailureCondition.ShouldNotBe(GitHubApiFailureCondition.ContentPolicyViolation);
        handler.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task StageEnforcesCountPerFileAndAggregateLimitsBeforeAnyRequest()
    {
        RecordingGitHubHttpMessageHandler handler = new((_, _) => throw new InvalidOperationException("not expected"));
        IGitHubApiClient client = await CreateClientAsync(handler);
        ProviderResolvedFileChange[] tooMany = Enumerable.Range(0, 101)
            .Select(static index => new ProviderResolvedFileChange(index, ProviderFileChangeKind.Add, $"many/{index}.txt", "x"u8.ToArray(), ProviderFileContentType.RegularFile))
            .ToArray();
        ProviderResolvedFileChange[] tooLarge =
        [
            new ProviderResolvedFileChange(0, ProviderFileChangeKind.Add, "large.bin", new byte[(1024 * 1024) + 1], ProviderFileContentType.RegularFile),
        ];
        ProviderResolvedFileChange[] aggregateTooLarge = Enumerable.Range(0, 11)
            .Select(static index => new ProviderResolvedFileChange(index, ProviderFileChangeKind.Add, $"aggregate/{index}.bin", new byte[1024 * 1024], ProviderFileContentType.RegularFile))
            .ToArray();

        foreach (ProviderResolvedFileChange[] changes in new[] { tooMany, tooLarge, aggregateTooLarge })
        {
            GitHubFileMutationResult result = await client.StageFileChangesAsync(
                new GitHubFileMutationRequest(TransportTarget(), changes, static _ => ValueTask.FromResult(true)),
                TestContext.Current.CancellationToken);

            result.FailureCondition.ShouldBe(GitHubApiFailureCondition.ContentPolicyViolation);
        }

        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task CommitCreatesOneCommitThenPerformsOneNonForceRefUpdateAndConfirmsIdentity()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.Created, BlobJson(CommitSha)),
            3 => JsonResponse(HttpStatusCode.OK, CommitJson(CommitSha, TreeSha)),
            4 => JsonResponse(HttpStatusCode.OK, ReferenceJson(CommitSha)),
            _ => JsonResponse(HttpStatusCode.OK, ReferenceJson(CommitSha)),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubCommitResult result = await client.CommitAsync(
            CommitTransportRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.CommitSha.ShouldBe(CommitSha);
        handler.Requests.Select(static request => request.Method.Method).ShouldBe(["GET", "POST", "GET", "PATCH", "GET"]);
        handler.Requests[1].RequestUri.AbsolutePath.ShouldEndWith("/git/commits", Case.Sensitive);
        handler.Requests[3].RequestUri.AbsolutePath.ShouldEndWith("/git/refs/heads%2Fmain", Case.Sensitive);
        using JsonDocument commit = JsonDocument.Parse(handler.Requests[1].Body!);
        commit.RootElement.GetProperty("tree").GetString().ShouldBe(TreeSha);
        commit.RootElement.GetProperty("parents")[0].GetString().ShouldBe(HeadSha);
        using JsonDocument update = JsonDocument.Parse(handler.Requests[3].Body!);
        update.RootElement.GetProperty("sha").GetString().ShouldBe(CommitSha);
        update.RootElement.GetProperty("force").GetBoolean().ShouldBeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task CommitRefConflictDoesNotForceOrRetryUpdate(HttpStatusCode statusCode)
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.Created, BlobJson(CommitSha)),
            3 => JsonResponse(HttpStatusCode.OK, CommitJson(CommitSha, TreeSha)),
            _ => JsonResponse(statusCode, SafeErrorJson()),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubCommitResult result = await client.CommitAsync(
            CommitTransportRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.BranchProtectionConflict);
        handler.Requests.Count.ShouldBe(4);
        using JsonDocument update = JsonDocument.Parse(handler.Requests[3].Body!);
        update.RootElement.GetProperty("force").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task CommitMovedHeadStopsBeforeCommitCreation()
    {
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(
            JsonResponse(HttpStatusCode.OK, ReferenceJson(CommitSha))));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubCommitResult result = await client.CommitAsync(
            CommitTransportRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.RefHeadConflict);
        handler.Requests.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CommitPostDispatchFailuresAreUnknownAndNeverRetried(
        bool failAfterCommit,
        bool serverFailure)
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) =>
        {
            calls++;
            if (calls == 1)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)));
            }

            if (calls == 2 && !failAfterCommit)
            {
                return serverFailure
                    ? Task.FromResult(JsonResponse(HttpStatusCode.InternalServerError, SafeErrorJson()))
                    : throw new OperationCanceledException("provider-body-sentinel");
            }

            if (calls == 2)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.Created, CommitJson(CommitSha, TreeSha)));
            }

            return serverFailure
                ? Task.FromResult(JsonResponse(HttpStatusCode.InternalServerError, SafeErrorJson()))
                : throw new HttpRequestException("provider-body-sentinel");
        });
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubCommitResult result = await client.CommitAsync(CommitTransportRequest(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBeOneOf(GitHubApiFailureCondition.TimeoutDuringMutation, GitHubApiFailureCondition.UnexpectedTransportFailure);
        calls.ShouldBe(failAfterCommit ? 3 : 2);
        if (failAfterCommit)
        {
            result.CreatedCommitSha.ShouldBe(CommitSha);
        }
    }

    [Theory]
    [InlineData("refs/heads/other", CommitSha)]
    [InlineData("refs/heads/main", TreeSha)]
    public async Task CommitRejectsMismatchedUpdatedRefIdentityOrSha(string refIdentity, string updatedSha)
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.Created, BlobJson(CommitSha)),
            3 => JsonResponse(HttpStatusCode.OK, CommitJson(CommitSha, TreeSha)),
            4 => JsonResponse(HttpStatusCode.OK, ReferenceJson(CommitSha)),
            _ => JsonResponse(HttpStatusCode.OK, ReferenceJson(updatedSha).Replace("refs/heads/main", refIdentity, StringComparison.Ordinal)),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubCommitResult result = await client.CommitAsync(CommitTransportRequest(), TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.AmbiguousMutationResponse);
        result.CreatedCommitSha.ShouldBe(CommitSha);
        handler.Requests.Count.ShouldBe(5);
    }

    [Theory]
    [InlineData(BaseTreeSha, HeadSha)]
    [InlineData(TreeSha, BaseTreeSha)]
    public async Task CommitRejectsCreatedCommitWithWrongTreeOrParent(string returnedTree, string returnedParent)
    {
        int calls = 0;
        string invalidCommit = CommitJson(CommitSha, returnedTree).Replace(HeadSha, returnedParent, StringComparison.Ordinal);
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.Created, BlobJson(CommitSha)),
            _ => JsonResponse(HttpStatusCode.OK, invalidCommit),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubCommitResult result = await client.CommitAsync(CommitTransportRequest(), TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.AmbiguousMutationResponse);
        handler.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task CommitRecordsCreatedIdentityBeforeTheOnlyRefUpdate()
    {
        bool commitRecorded = false;
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) =>
        {
            calls++;
            if (calls == 4)
            {
                commitRecorded.ShouldBeTrue("the private commit identity must be acknowledged before PATCH");
            }

            return Task.FromResult(calls switch
            {
                1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
                2 => JsonResponse(HttpStatusCode.Created, BlobJson(CommitSha)),
                3 => JsonResponse(HttpStatusCode.OK, CommitJson(CommitSha, TreeSha)),
                _ => JsonResponse(HttpStatusCode.OK, ReferenceJson(CommitSha)),
            });
        });
        IGitHubApiClient client = await CreateClientAsync(handler);
        GitHubCommitRequest request = CommitTransportRequest() with
        {
            RecordCreatedCommitAsync = _ =>
            {
                commitRecorded = true;
                return ValueTask.FromResult(true);
            },
        };

        GitHubCommitResult result = await client.CommitAsync(request, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        commitRecorded.ShouldBeTrue();
        handler.Requests.Select(static request => request.Method.Method).ShouldBe(["GET", "POST", "GET", "PATCH", "GET"]);
    }

    [Theory]
    [InlineData(CommitSha, ProviderOperationStatusKind.Confirmed)]
    [InlineData(HeadSha, ProviderOperationStatusKind.NotApplied)]
    [InlineData(TreeSha, ProviderOperationStatusKind.Conflicting)]
    public async Task StatusUsesExactlyOneReadOnlyRefObservation(
        string observedSha,
        ProviderOperationStatusKind expectedStatus)
    {
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(
            JsonResponse(HttpStatusCode.OK, ReferenceJson(observedSha))));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubOperationStatusResult result = await client.GetOperationStatusAsync(
            StatusTransportRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Status.ShouldBe(expectedStatus);
        handler.Requests.Count.ShouldBe(1);
        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].RequestUri.AbsolutePath.ShouldEndWith("/git/refs/heads%2Fmain", Case.Sensitive);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, (int)GitHubApiFailureCondition.ValidationFailure)]
    [InlineData(HttpStatusCode.Unauthorized, (int)GitHubApiFailureCondition.AuthenticationRequired)]
    [InlineData(HttpStatusCode.Forbidden, (int)GitHubApiFailureCondition.PermissionInsufficient)]
    [InlineData(HttpStatusCode.NotFound, (int)GitHubApiFailureCondition.NotFoundOrHidden)]
    [InlineData(HttpStatusCode.TooManyRequests, (int)GitHubApiFailureCondition.PrimaryRateLimit)]
    [InlineData(HttpStatusCode.InternalServerError, (int)GitHubApiFailureCondition.ServerUnavailable)]
    public async Task StatusMapsReadOnlyFailuresAsUnavailableRatherThanUnknownMutation(
        HttpStatusCode statusCode,
        int expectedCondition)
    {
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(
            JsonResponse(statusCode, SafeErrorJson())));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubOperationStatusResult result = await client.GetOperationStatusAsync(
            StatusTransportRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe((GitHubApiFailureCondition)expectedCondition);
        result.FailureCondition.ShouldNotBe(GitHubApiFailureCondition.TimeoutDuringMutation);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task StatusMapsMismatchedRefIdentityToConflictingEvidence()
    {
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(
            JsonResponse(
                HttpStatusCode.OK,
                ReferenceJson(CommitSha).Replace("refs/heads/main", "refs/heads/other", StringComparison.Ordinal))));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubOperationStatusResult result = await client.GetOperationStatusAsync(
            StatusTransportRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Status.ShouldBe(ProviderOperationStatusKind.Conflicting);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task StatusMapsNonCommitObjectAtExactRefToConflictingEvidence()
    {
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(
            JsonResponse(HttpStatusCode.OK, ReferenceJson(CommitSha).Replace("\"type\": \"commit\"", "\"type\": \"tag\"", StringComparison.Ordinal))));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubOperationStatusResult result = await client.GetOperationStatusAsync(
            StatusTransportRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Status.ShouldBe(ProviderOperationStatusKind.Conflicting);
        result.ObservedObjectType.ShouldBe("tag");
        handler.Requests.Count.ShouldBe(1);
    }

    private static GitHubFileMutationRequest FileMutationTransportRequest()
        => new(
            TransportTarget(),
            [
                new ProviderResolvedFileChange(
                    0,
                    ProviderFileChangeKind.Add,
                    "docs/one.txt",
                    "one"u8.ToArray(),
                    ProviderFileContentType.RegularFile),
                new ProviderResolvedFileChange(
                    1,
                    ProviderFileChangeKind.Remove,
                    "docs/two.txt",
                    ReadOnlyMemory<byte>.Empty,
                    ProviderFileContentType.RegularFile),
            ],
            static _ => ValueTask.FromResult(true));

    private static GitHubCommitRequest CommitTransportRequest()
        => new(
            TransportTarget(),
            TreeSha,
            "safe commit message",
            static _ => ValueTask.FromResult(true),
            static _ => ValueTask.FromResult(true));

    private static GitHubOperationStatusRequest StatusTransportRequest()
        => new(TransportTarget(), CommitSha);

    private static ProviderGitOperationResolvedTarget TransportTarget()
        => new(
            "octokit-owner-sentinel",
            "octokit-repository-sentinel",
            "heads/main",
            HeadSha);

    private static string ReferenceJson(string sha)
        => $$"""
            {
              "ref": "refs/heads/main",
              "node_id": "safe-node",
              "url": "https://api.github.test/ref",
              "object": {
                "type": "commit",
                "sha": "{{sha}}",
                "url": "https://api.github.test/object"
              }
            }
            """;

    private static string CommitJson(string sha, string treeSha)
        => $$"""
            {
              "sha": "{{sha}}",
              "node_id": "safe-node",
              "url": "https://api.github.test/commit",
              "message": "safe commit message",
              "tree": {
                "sha": "{{treeSha}}",
                "url": "https://api.github.test/tree"
              },
              "parents": [
                { "sha": "{{HeadSha}}", "url": "https://api.github.test/parent" }
              ]
            }
            """;

    private static string BlobJson(string sha)
        => $$"""{"sha":"{{sha}}","url":"https://api.github.test/blob"}""";

    private static string TreeJson(string sha)
        => $$"""{"sha":"{{sha}}","url":"https://api.github.test/tree","tree":[],"truncated":false}""";

    private static string ResultingTreeJson(string sha, params (string Path, string BlobSha)[] entries)
        => JsonSerializer.Serialize(new
        {
            sha,
            tree = entries.Select(static entry => new { path = entry.Path, mode = "100644", type = "blob", sha = entry.BlobSha }),
            truncated = false,
        });

    private static string TreeEntriesJson(
        string sha,
        bool truncated,
        params (string Path, string Mode, string Type, string ObjectSha)[] entries)
        => JsonSerializer.Serialize(new
        {
            sha,
            tree = entries.Select(static entry => new { path = entry.Path, mode = entry.Mode, type = entry.Type, sha = entry.ObjectSha }),
            truncated,
        });

    private static string BaseTreeJson(params string[] paths)
    {
        paths = paths.Length == 0 ? ["docs/two.txt"] : paths;
        return JsonSerializer.Serialize(new
        {
            sha = BaseTreeSha,
            tree = paths.Select(static path => new { path, mode = "100644", type = "blob", sha = BlobSha }),
            truncated = false,
        });
    }
}
