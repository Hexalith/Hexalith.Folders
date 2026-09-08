using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Aspire.Hosting.Testing;

using Hexalith.Folders.Aspire;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>
/// Story 10.8 / FR58 acceptance: an authenticated public Folders file mutation must become a non-empty,
/// hydrated, metadata-only public search/status result. This scenario never seeds
/// <c>SearchIndexEntryChanged</c>, never hand-publishes <c>WorkspaceFileMutationAccepted</c>, and never
/// queries Memories for acceptance. A governed opt-in run that skips this test is a failure.
/// </summary>
public sealed class Fr58PublicMutationSearchRoundTripTests
{
    /// <summary>
    /// Live-path Task 0 gate. Stay <see langword="false"/> until Stories 12.1–12.3 and 12.5, DW-292 ACL
    /// population, a production EventStore validator, Story 12.6 event emission (not only admission), and
    /// Story 11.15 are available. Owned facade/CLI work may land while this remains false.
    /// </summary>
    public static bool LivePathPrerequisitesSatisfied()
        => false;

    private const string LivePathBlocker =
        "Story 10.8 FR58 live path is blocked by Task 0: Stories 12.1–12.3 and 12.5 are backlog; "
        + "FolderDomainProcessor still maps accepted mutations to PayloadNoOpDomainResult (12.6 admission is not "
        + "event emission); AddFoldersLayeredAuthorization defaults to unpopulated InMemoryEffectivePermissionsReadModel "
        + "(DW-292) and DenyAllEventStoreAuthorizationValidator; Story 11.15 DCP lane is backlog. "
        + "A skipped governed run is not FR58 acceptance.";

    [Fact]
    public void Task0BlockersShouldKeepLivePathPrerequisitesUnsatisfied()
    {
        LivePathPrerequisitesSatisfied().ShouldBeFalse(LivePathBlocker);
    }

    [Fact]
    public async Task AuthenticatedPublicAddWorkspaceFileShouldConvergeOnPublicSearchAndStatus()
    {
        bool governedOptIn = OptInEnabled();
        if (!LivePathPrerequisitesSatisfied())
        {
            if (governedOptIn)
            {
                Assert.Fail(LivePathBlocker);
            }

            Assert.Skip(LivePathBlocker);
            return;
        }

        if (!governedOptIn)
        {
            Assert.Skip(
                $"Set {AspireFoldersAppHostFixture.OptInEnvironmentVariable}=true on a DCP-capable host to run "
                + "the Story 10.8 FR58 public-REST round trip.");
            return;
        }

        AspireFoldersAppHostFixture fixture = new();
        await fixture.InitializeAsync().ConfigureAwait(true);
        try
        {
            fixture.SkipIfUnavailable();
            await ExecutePublicRoundTripAsync(fixture, TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
        finally
        {
            await fixture.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static async Task ExecutePublicRoundTripAsync(
        AspireFoldersAppHostFixture fixture,
        CancellationToken cancellationToken)
    {
        using HttpClient folders = fixture.App.CreateHttpClient(FoldersAspireModule.FoldersAppId);
        folders.BaseAddress.ShouldNotBeNull();

        string folderId = NewUlid();
        string workspaceId = NewUlid();
        string operationId = NewUlid();
        string correlationId = NewUlid();
        string taskId = NewUlid();
        string idempotencyKey = NewUlid();
        const string queryText = "text";
        byte[] content = Encoding.UTF8.GetBytes("fr58 metadata token sample");
        string contentHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        using HttpRequestMessage add = new(
            HttpMethod.Post,
            $"/api/v1/folders/{folderId}/workspaces/{workspaceId}/files/add")
        {
            Content = JsonContent.Create(new
            {
                requestSchemaVersion = "v1",
                fileOperationKind = "add",
                transportOperation = "PutFileInline",
                operationId,
                pathMetadata = new
                {
                    normalizedPath = "notes/fr58.txt",
                    displayName = "fr58.txt",
                    pathPolicyClass = "metadata_only",
                    unicodeNormalization = "NFC",
                },
                contentHashReference = "sha256:" + contentHash,
                byteLength = content.Length,
                inlineContent = new
                {
                    mediaType = "text/plain",
                    contentBytes = Convert.ToBase64String(content),
                },
            }),
        };
        add.Headers.Add("X-Correlation-Id", correlationId);
        add.Headers.Add("X-Hexalith-Task-Id", taskId);
        add.Headers.Add("Idempotency-Key", idempotencyKey);

        HttpResponseMessage addResponse = await folders.SendAsync(add, cancellationToken).ConfigureAwait(true);
        string addBody = await addResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(true);
        addResponse.IsSuccessStatusCode.ShouldBeTrue(addBody);
        addBody.ShouldNotContain("folders://", Case.Sensitive);

        JsonElement search = await WaitForPublicSearchHitAsync(
            folders,
            folderId,
            workspaceId,
            queryText,
            correlationId,
            taskId,
            cancellationToken).ConfigureAwait(true);
        JsonElement items = search.GetProperty("items");
        items.GetArrayLength().ShouldBeGreaterThan(0);
        string? fileVersion = items[0].GetProperty("fileVersionReference").GetString();
        fileVersion.ShouldNotBeNullOrWhiteSpace();
        items[0].GetProperty("indexingStatus").GetString().ShouldBe("indexed");

        string searchJson = search.GetRawText();
        searchJson.ShouldNotContain("folders://", Case.Sensitive);
        searchJson.ShouldNotContain("snippet", Case.Insensitive);
        searchJson.ShouldNotContain("sourceUri", Case.Insensitive);
        searchJson.ShouldNotContain("normalizedPath", Case.Insensitive);
        searchJson.ShouldNotContain("rawCount", Case.Insensitive);
        searchJson.ShouldNotContain("totalCount", Case.Insensitive);

        using HttpRequestMessage status = new(
            HttpMethod.Get,
            $"/api/v1/folders/{folderId}/indexing-status");
        status.Headers.Add("X-Correlation-Id", correlationId);
        HttpResponseMessage statusResponse = await folders.SendAsync(status, cancellationToken).ConfigureAwait(true);
        string statusBody = await statusResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(true);
        statusResponse.IsSuccessStatusCode.ShouldBeTrue(statusBody);
        using JsonDocument statusDocument = JsonDocument.Parse(statusBody);
        bool statusMatches = false;
        foreach (JsonElement item in statusDocument.RootElement.GetProperty("items").EnumerateArray())
        {
            if (string.Equals(item.GetProperty("fileVersionReference").GetString(), fileVersion, StringComparison.Ordinal)
                && string.Equals(item.GetProperty("indexingStatus").GetString(), "indexed", StringComparison.Ordinal))
            {
                statusMatches = true;
                break;
            }
        }

        statusMatches.ShouldBeTrue("public indexing-status must report the same indexed file version as search");
        statusBody.ShouldNotContain("folders://", Case.Sensitive);
        statusBody.ShouldNotContain("/api/search", Case.Sensitive);
    }

    private static async Task<JsonElement> WaitForPublicSearchHitAsync(
        HttpClient folders,
        string folderId,
        string workspaceId,
        string queryText,
        string correlationId,
        string taskId,
        CancellationToken cancellationToken)
    {
        JsonElement last = default;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            using HttpRequestMessage search = new(
                HttpMethod.Post,
                $"/api/v1/folders/{folderId}/workspaces/{workspaceId}/context/index-search")
            {
                Content = JsonContent.Create(new
                {
                    requestSchemaVersion = "v1",
                    queryFamily = "semantic_reference_pending",
                    queryText,
                }),
            };
            search.Headers.Add("X-Correlation-Id", correlationId);
            search.Headers.Add("X-Hexalith-Task-Id", taskId);

            HttpResponseMessage response = await folders.SendAsync(search, cancellationToken).ConfigureAwait(true);
            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(true);
            json.ShouldNotContain("/api/search", Case.Sensitive);
            using JsonDocument document = JsonDocument.Parse(json);
            last = document.RootElement.Clone();
            if (response.IsSuccessStatusCode
                && last.TryGetProperty("items", out JsonElement items)
                && items.GetArrayLength() > 0)
            {
                return last;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(true);
        }

        last.TryGetProperty("items", out JsonElement finalItems).ShouldBeTrue();
        finalItems.GetArrayLength().ShouldBeGreaterThan(0, "public search did not converge on a hydrated hit");
        return last;
    }

    private static bool OptInEnabled()
    {
        string? value = Environment.GetEnvironmentVariable(AspireFoldersAppHostFixture.OptInEnvironmentVariable);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.Ordinal);
    }

    private static string NewUlid()
    {
        Span<char> buffer = stackalloc char[26];
        const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        long milliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (int i = 9; i >= 0; i--)
        {
            buffer[i] = Alphabet[(int)(milliseconds % 32)];
            milliseconds /= 32;
        }

        Span<byte> entropy = stackalloc byte[10];
        RandomNumberGenerator.Fill(entropy);
        int value = 0;
        int bits = 0;
        int index = 10;
        foreach (byte b in entropy)
        {
            value = (value << 8) | b;
            bits += 8;
            while (bits >= 5 && index < 26)
            {
                buffer[index++] = Alphabet[(value >> (bits - 5)) & 31];
                bits -= 5;
            }
        }

        return new string(buffer);
    }
}
