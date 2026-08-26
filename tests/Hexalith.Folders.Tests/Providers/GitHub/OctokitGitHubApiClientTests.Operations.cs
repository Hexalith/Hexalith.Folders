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
                ("cafe\u0301.txt", "100644", "blob", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
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
    public async Task StageSnapshotsMutableCallerChangesAndContentBeforeTheFirstAwait()
    {
        byte[] content = "one"u8.ToArray();
        List<ProviderResolvedFileChange> changes = FileMutationTransportRequest().Changes.ToList();
        changes[0] = changes[0] with { Content = content };
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) =>
        {
            calls++;
            if (calls == 1)
            {
                content[0] = (byte)'X';
                changes[0] = changes[0] with { Path = "docs/substituted.txt" };
            }

            return Task.FromResult(calls switch
            {
                1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
                2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
                3 => JsonResponse(HttpStatusCode.OK, BaseTreeJson()),
                4 => JsonResponse(HttpStatusCode.Created, BlobJson(BlobSha)),
                5 => JsonResponse(HttpStatusCode.Created, TreeJson(TreeSha)),
                _ => JsonResponse(HttpStatusCode.OK, ResultingTreeJson(TreeSha, ("docs/one.txt", BlobSha))),
            });
        });
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            new GitHubFileMutationRequest(TransportTarget(), changes, static _ => ValueTask.FromResult(true)),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.FailureCondition.ToString());
        using JsonDocument blob = JsonDocument.Parse(handler.Requests[3].Body!);
        blob.RootElement.GetProperty("content").GetString().ShouldBe(Convert.ToBase64String("one"u8));
        using JsonDocument tree = JsonDocument.Parse(handler.Requests[4].Body!);
        tree.RootElement.GetProperty("tree")[0].GetProperty("path").GetString().ShouldBe("docs/one.txt");
    }

    [Fact]
    public async Task StageMapsAHostileCallerCollectionToValidationWithoutAnyRequest()
    {
        RecordingGitHubHttpMessageHandler handler = new((_, _) => throw new InvalidOperationException("not expected"));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            new GitHubFileMutationRequest(
                TransportTarget(),
                new ThrowingReadOnlyList<ProviderResolvedFileChange>(),
                static _ => ValueTask.FromResult(true)),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.ValidationFailure);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RawOperationBoundariesRejectNonCanonicalAndInvalidUnicodeBeforeHttpAccess()
    {
        RecordingGitHubHttpMessageHandler handler = new((_, _) => throw new InvalidOperationException("not expected"));
        IGitHubApiClient client = await CreateClientAsync(handler);
        ProviderGitOperationResolvedTarget baselineTarget = TransportTarget();
        ProviderGitOperationResolvedTarget[] invalidTargets =
        [
            baselineTarget with { Owner = "cafe\u0301" },
            baselineTarget with { RepositoryName = "cafe\u0301" },
            baselineTarget with { RefName = "heads/cafe\u0301" },
            baselineTarget with { Owner = "owner\uD800" },
        ];

        foreach (ProviderGitOperationResolvedTarget target in invalidTargets)
        {
            GitHubOperationStatusResult status = await client.GetOperationStatusAsync(
                new GitHubOperationStatusRequest(target, CommitSha),
                TestContext.Current.CancellationToken);
            status.FailureCondition.ShouldBe(GitHubApiFailureCondition.ValidationFailure);
        }

        foreach (string path in new[] { "docs/cafe\u0301.txt", "docs/invalid\uD800.txt" })
        {
            GitHubFileMutationResult mutation = await client.StageFileChangesAsync(
                new GitHubFileMutationRequest(
                    baselineTarget,
                    [new ProviderResolvedFileChange(0, ProviderFileChangeKind.Add, path, "one"u8.ToArray(), ProviderFileContentType.RegularFile)],
                    static _ => ValueTask.FromResult(true)),
                TestContext.Current.CancellationToken);
            mutation.FailureCondition.ShouldBe(GitHubApiFailureCondition.PathPolicyViolation);
        }

        foreach (string message in new[] { "cafe\u0301", "invalid\uD800" })
        {
            GitHubCommitResult commit = await client.CommitAsync(
                CommitTransportRequest() with { CommitMessage = message },
                TestContext.Current.CancellationToken);
            commit.FailureCondition.ShouldBe(GitHubApiFailureCondition.ValidationFailure);
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

    [Fact]
    public async Task RealTransportReservationInvalidationStopsBeforeTheFirstWrite()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler stagingHandler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
            _ => JsonResponse(HttpStatusCode.OK, BaseTreeJson()),
        }));
        IGitHubApiClient stagingClient = await CreateClientAsync(stagingHandler);
        GitHubFileMutationResult staging = await stagingClient.StageFileChangesAsync(
            FileMutationTransportRequest() with { ValidateReservationAsync = static _ => ValueTask.FromResult(false) },
            TestContext.Current.CancellationToken);

        staging.FailureCondition.ShouldBe(GitHubApiFailureCondition.ReservationInvalidated);
        stagingHandler.Requests.ShouldAllBe(static request => request.Method == HttpMethod.Get);

        RecordingGitHubHttpMessageHandler commitHandler = new((_, _) => Task.FromResult(
            JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha))));
        IGitHubApiClient commitClient = await CreateClientAsync(commitHandler);
        GitHubCommitResult commit = await commitClient.CommitAsync(
            CommitTransportRequest() with { ValidateReservationAsync = static _ => ValueTask.FromResult(false) },
            TestContext.Current.CancellationToken);

        commit.FailureCondition.ShouldBe(GitHubApiFailureCondition.ReservationInvalidated);
        commitHandler.Requests.ShouldAllBe(static request => request.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task RealTransportCreatedCommitRecordingRejectionPreventsRefUpdate()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.Created, BlobJson(CommitSha)),
            _ => JsonResponse(HttpStatusCode.OK, CommitJson(CommitSha, TreeSha)),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubCommitResult result = await client.CommitAsync(
            CommitTransportRequest() with { RecordCreatedCommitAsync = static _ => ValueTask.FromResult(false) },
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.OutcomeRecordingFailed);
        result.CreatedCommitSha.ShouldBe(CommitSha);
        handler.Requests.ShouldNotContain(static request => request.Method == HttpMethod.Patch);
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
        handler.Requests.Count.ShouldBe(statusCode == HttpStatusCode.NotFound ? 2 : 1);
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

    [Fact]
    public async Task RecursiveTruncationIsHandledBeforeTheFallbackEntryCap()
    {
        const string docsTreeSha = "6666666666666666666666666666666666666666";
        (string Path, string Mode, string Type, string ObjectSha)[] recursiveEntries = Enumerable.Range(0, 257)
            .Select(static index => ($"unrelated-{index}", "100644", "blob", BlobSha))
            .ToArray();
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
            3 => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(BaseTreeSha, truncated: true, recursiveEntries)),
            4 => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(BaseTreeSha, truncated: false, ("docs", "040000", "tree", docsTreeSha))),
            5 => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(docsTreeSha, truncated: false, ("two.txt", "100644", "blob", BlobSha))),
            6 => JsonResponse(HttpStatusCode.Created, BlobJson(BlobSha)),
            7 => JsonResponse(HttpStatusCode.Created, TreeJson(TreeSha)),
            _ => JsonResponse(HttpStatusCode.OK, ResultingTreeJson(TreeSha, ("docs/one.txt", BlobSha))),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.FailureCondition.ToString());
        handler.Requests.Count.ShouldBe(8);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, (int)GitHubApiFailureCondition.PrimaryRateLimit)]
    [InlineData(HttpStatusCode.InternalServerError, (int)GitHubApiFailureCondition.ServerUnavailable)]
    public async Task PreWriteFallbackResponseFailuresPreserveTheirObservationCondition(
        HttpStatusCode statusCode,
        int expectedCondition)
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
            3 => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(BaseTreeSha, truncated: true)),
            _ => JsonResponse(statusCode, SafeErrorJson()),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe((GitHubApiFailureCondition)expectedCondition);
        result.FailureCondition.ShouldNotBe(GitHubApiFailureCondition.ContentPolicyViolation);
        handler.Requests.ShouldNotContain(static request => request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task PreWriteFallbackDisconnectAndCallerCancellationRemainNoDispatchObservations()
    {
        int disconnectCalls = 0;
        RecordingGitHubHttpMessageHandler disconnectHandler = new((_, _) => ++disconnectCalls switch
        {
            1 => Task.FromResult(JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha))),
            2 => Task.FromResult(JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha))),
            3 => Task.FromResult(JsonResponse(HttpStatusCode.OK, TreeEntriesJson(BaseTreeSha, truncated: true))),
            _ => throw new HttpRequestException("provider-body-sentinel"),
        });
        IGitHubApiClient disconnectClient = await CreateClientAsync(disconnectHandler);
        GitHubFileMutationResult disconnected = await disconnectClient.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        disconnected.FailureCondition.ShouldBe(GitHubApiFailureCondition.TimeoutDuringObservation);

        using CancellationTokenSource callerCancellation = new();
        int cancellationCalls = 0;
        RecordingGitHubHttpMessageHandler cancellationHandler = new((_, _) => Task.FromResult(++cancellationCalls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
            3 => CancelCallerAndReturn(
                callerCancellation,
                JsonResponse(HttpStatusCode.OK, TreeEntriesJson(BaseTreeSha, truncated: true))),
            _ => throw new InvalidOperationException("No fallback request should be dispatched with a cancelled caller."),
        }));
        IGitHubApiClient cancellationClient = await CreateClientAsync(cancellationHandler);
        GitHubFileMutationResult cancelled = await cancellationClient.StageFileChangesAsync(
            FileMutationTransportRequest(),
            callerCancellation.Token);

        cancelled.FailureCondition.ShouldBe(GitHubApiFailureCondition.CancellationBeforeDispatch);
        cancellationHandler.Requests.ShouldAllBe(static request => request.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task InternalFallbackDeadlineIsDistinctFromCallerCancellation()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new(async (_, cancellationToken) =>
        {
            calls++;
            if (calls == 1)
            {
                return JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha));
            }

            if (calls == 2)
            {
                return JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha));
            }

            if (calls == 3)
            {
                return JsonResponse(HttpStatusCode.OK, TreeEntriesJson(BaseTreeSha, truncated: true));
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("unreachable");
        });
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.TimeoutDuringObservation);
        result.FailureCondition.ShouldNotBe(GitHubApiFailureCondition.CancellationBeforeDispatch);
        handler.Requests.ShouldNotContain(static request => request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task PostWriteFallbackRateLimitRemainsAmbiguous()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
            3 => JsonResponse(HttpStatusCode.OK, BaseTreeJson()),
            4 => JsonResponse(HttpStatusCode.Created, BlobJson(BlobSha)),
            5 => JsonResponse(HttpStatusCode.Created, TreeJson(TreeSha)),
            6 => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(TreeSha, truncated: true)),
            _ => JsonResponse(HttpStatusCode.TooManyRequests, SafeErrorJson()),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.AmbiguousMutationResponse);
        result.RetryAfter.ShouldBeNull();
    }

    [Fact]
    public async Task FallbackResponsesOverTwoHundredFiftySixEntriesAreResponseLimited()
    {
        (string Path, string Mode, string Type, string ObjectSha)[] entries = Enumerable.Range(0, 257)
            .Select(static index => ($"entry-{index}", "100644", "blob", BlobSha))
            .ToArray();
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
            3 => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(BaseTreeSha, truncated: true)),
            _ => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(BaseTreeSha, truncated: false, entries)),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.ResponseLimitExceeded);
        handler.Requests.Count.ShouldBe(4);
        handler.Requests.ShouldNotContain(static request => request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task FallbackTraversalStopsBeforeRequestSixtyFiveWithoutAnyWrite()
    {
        string[] treeShas = Enumerable.Range(1, 64)
            .Select(static index => index.ToString("x40", System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        (string Path, string Mode, string Type, string ObjectSha)[] rootEntries = treeShas
            .Select(static (sha, index) => ($"dir-{index:D2}", "040000", "tree", sha))
            .ToArray();
        ProviderResolvedFileChange[] changes = treeShas
            .Select(static (_, index) => new ProviderResolvedFileChange(
                index,
                ProviderFileChangeKind.Add,
                $"dir-{index:D2}/file.txt",
                "x"u8.ToArray(),
                ProviderFileContentType.RegularFile))
            .ToArray();
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((request, _) =>
        {
            calls++;
            if (calls == 1)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)));
            }

            if (calls == 2)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)));
            }

            if (calls == 3)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, TreeEntriesJson(BaseTreeSha, truncated: true)));
            }

            if (calls == 4)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, TreeEntriesJson(BaseTreeSha, truncated: false, rootEntries)));
            }

            string requestedTreeSha = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath.Split('/').Last());
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, TreeEntriesJson(requestedTreeSha, truncated: false)));
        });
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            new GitHubFileMutationRequest(TransportTarget(), changes, static _ => ValueTask.FromResult(true)),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.ResponseLimitExceeded);
        handler.Requests.Count.ShouldBe(67);
        handler.Requests.ShouldNotContain(static request => request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task FallbackTraversalRejectsDepthThirtyThreeWithoutAnyWrite()
    {
        string[] segments = Enumerable.Range(0, 34).Select(static index => $"d{index:D2}").ToArray();
        string touchedPath = string.Join('/', segments) + "/file.txt";
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) =>
        {
            calls++;
            return Task.FromResult(calls switch
            {
                1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
                2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
                3 => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(BaseTreeSha, truncated: true)),
                4 => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(BaseTreeSha, truncated: false, (segments[0], "040000", "tree", TreeSha))),
                _ => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(
                    TreeSha,
                    truncated: false,
                    (segments[calls - 4], "040000", "tree", TreeSha))),
            });
        });
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            new GitHubFileMutationRequest(
                TransportTarget(),
                [new ProviderResolvedFileChange(0, ProviderFileChangeKind.Add, touchedPath, "x"u8.ToArray(), ProviderFileContentType.RegularFile)],
                static _ => ValueTask.FromResult(true)),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.ResponseLimitExceeded);
        handler.Requests.Count.ShouldBe(36);
        handler.Requests.ShouldNotContain(static request => request.Method == HttpMethod.Post);
    }

    [Theory]
    [InlineData("docs/nested", "040000")]
    [InlineData("docs", "100644")]
    public async Task MalformedFallbackAncestorEvidenceFailsAsObservationEvidence(string localPath, string mode)
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
            3 => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(BaseTreeSha, truncated: true)),
            _ => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(BaseTreeSha, truncated: false, (localPath, mode, "tree", TreeSha))),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.MalformedResponse);
        handler.Requests.ShouldNotContain(static request => request.Method == HttpMethod.Post);
    }

    [Theory]
    [InlineData("100644", "blob")]
    [InlineData("120000", "blob")]
    [InlineData("160000", "commit")]
    public async Task ExistingNonTreeTouchedAncestorIsRejectedBeforeAnyWrite(string mode, string type)
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
            _ => JsonResponse(HttpStatusCode.OK, TreeEntriesJson(
                BaseTreeSha,
                truncated: false,
                ("docs", mode, type, TreeSha),
                ("docs/two.txt", "100644", "blob", BlobSha))),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.ContentPolicyViolation);
        handler.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ResponseLimitAfterAWriteIsAmbiguousRatherThanRetryableKnownFailure()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
            3 => JsonResponse(HttpStatusCode.OK, BaseTreeJson()),
            4 => JsonResponse(HttpStatusCode.Created, BlobJson(BlobSha)),
            _ => JsonResponse(HttpStatusCode.Created, new string(' ', (1024 * 1024) + 1)),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.AmbiguousMutationResponse);
        result.RetryAfter.ShouldBeNull();
        handler.Requests.Count(static request => request.Method == HttpMethod.Post).ShouldBe(2);
    }

    [Fact]
    public async Task CallerCancellationAfterTheFinalGateCannotTurnADispatchedWriteIntoNoDispatch()
    {
        using CancellationTokenSource callerCancellation = new();
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.OK, CommitJson(HeadSha, BaseTreeSha)),
            3 => JsonResponse(HttpStatusCode.OK, BaseTreeJson()),
            4 => CancelCallerAndReturn(callerCancellation, JsonResponse(HttpStatusCode.Created, BlobJson(BlobSha))),
            _ => throw new InvalidOperationException("No later request is expected."),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubFileMutationResult result = await client.StageFileChangesAsync(
            FileMutationTransportRequest(),
            callerCancellation.Token);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.TimeoutDuringMutation);
        result.FailureCondition.ShouldNotBe(GitHubApiFailureCondition.CancellationBeforeDispatch);
        handler.Requests.Count(static request => request.Method == HttpMethod.Post).ShouldBe(1);
    }

    [Fact]
    public async Task ExactRefNotFoundIsDisambiguatedByOneBoundedRepositoryVisibilityProbe()
    {
        int visibleCalls = 0;
        RecordingGitHubHttpMessageHandler visibleHandler = new((_, _) => Task.FromResult(++visibleCalls == 1
            ? JsonResponse(HttpStatusCode.NotFound, SafeErrorJson())
            : JsonResponse(HttpStatusCode.OK, "{}")));
        IGitHubApiClient visibleClient = await CreateClientAsync(visibleHandler);

        GitHubOperationStatusResult deleted = await visibleClient.GetOperationStatusAsync(
            StatusTransportRequest(),
            TestContext.Current.CancellationToken);

        deleted.IsSuccess.ShouldBeTrue();
        deleted.Status.ShouldBe(ProviderOperationStatusKind.Conflicting);
        visibleHandler.Requests.Select(static request => request.RequestUri.AbsolutePath)
            .ShouldBe(
            [
                "/repos/octokit-owner-sentinel/octokit-repository-sentinel/git/refs/heads%2Fmain",
                "/repos/octokit-owner-sentinel/octokit-repository-sentinel",
            ]);

        RecordingGitHubHttpMessageHandler concealedHandler = new((_, _) => Task.FromResult(
            JsonResponse(HttpStatusCode.NotFound, SafeErrorJson())));
        IGitHubApiClient concealedClient = await CreateClientAsync(concealedHandler);
        GitHubOperationStatusResult concealed = await concealedClient.GetOperationStatusAsync(
            StatusTransportRequest(),
            TestContext.Current.CancellationToken);

        concealed.IsSuccess.ShouldBeFalse();
        concealed.FailureCondition.ShouldBe(GitHubApiFailureCondition.NotFoundOrHidden);
        concealedHandler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RawRateLimitResetIsCaseInsensitiveBoundedAndRangeSafe()
    {
        foreach ((string reset, bool expectedRetry) in new[]
        {
            (DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), true),
            (long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture), false),
        })
        {
            RecordingGitHubHttpMessageHandler handler = new((_, _) =>
            {
                HttpResponseMessage response = JsonResponse(HttpStatusCode.TooManyRequests, SafeErrorJson());
                response.Headers.TryAddWithoutValidation("x-ratelimit-reset", reset);
                return Task.FromResult(response);
            });
            IGitHubApiClient client = await CreateClientAsync(handler);

            GitHubOperationStatusResult result = await client.GetOperationStatusAsync(
                StatusTransportRequest(),
                TestContext.Current.CancellationToken);

            (result.RetryAfter is not null).ShouldBe(expectedRetry);
            if (result.RetryAfter is { } retryAfter)
            {
                retryAfter.ShouldBeLessThanOrEqualTo(TimeSpan.FromHours(24));
            }
        }
    }

    [Fact]
    public async Task StatusRejectsAnIntendedCommitEqualToExpectedHeadBeforeTransport()
    {
        RecordingGitHubHttpMessageHandler handler = new((_, _) => throw new InvalidOperationException("not expected"));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubOperationStatusResult result = await client.GetOperationStatusAsync(
            StatusTransportRequest() with { IntendedCommitSha = HeadSha },
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.ValidationFailure);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task CommitMessageMismatchIsAmbiguousAndPreventsRefMovement()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            2 => JsonResponse(HttpStatusCode.Created, CommitJson(CommitSha, TreeSha)),
            _ => JsonResponse(
                HttpStatusCode.OK,
                CommitJson(CommitSha, TreeSha).Replace("safe commit message", "changed commit message", StringComparison.Ordinal)),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubCommitResult result = await client.CommitAsync(
            CommitTransportRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.AmbiguousMutationResponse);
        handler.Requests.ShouldNotContain(static request => request.Method == HttpMethod.Patch);
    }

    [Fact]
    public async Task CommitRejectsACreatedIdentityEqualToTheExpectedHead()
    {
        int calls = 0;
        RecordingGitHubHttpMessageHandler handler = new((_, _) => Task.FromResult(++calls switch
        {
            1 => JsonResponse(HttpStatusCode.OK, ReferenceJson(HeadSha)),
            _ => JsonResponse(HttpStatusCode.Created, CommitJson(HeadSha, TreeSha)),
        }));
        IGitHubApiClient client = await CreateClientAsync(handler);

        GitHubCommitResult result = await client.CommitAsync(
            CommitTransportRequest(),
            TestContext.Current.CancellationToken);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.AmbiguousMutationResponse);
        result.CreatedCommitSha.ShouldBeNull();
        handler.Requests.ShouldNotContain(static request => request.Method == HttpMethod.Patch);
    }

    private static HttpResponseMessage CancelCallerAndReturn(
        CancellationTokenSource cancellationTokenSource,
        HttpResponseMessage response)
    {
        cancellationTokenSource.Cancel();
        return response;
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
