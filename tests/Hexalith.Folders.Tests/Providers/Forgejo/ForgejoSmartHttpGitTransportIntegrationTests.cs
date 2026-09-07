using System.Net.Http.Headers;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.Forgejo;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.Forgejo;

public sealed class ForgejoSmartHttpGitTransportIntegrationTests
{
    [Fact]
    public async Task AlpineTlsProfileReceivesExactPackAndRejectsPostAdvertisementStaleOld()
    {
        string? address = Environment.GetEnvironmentVariable("HEXALITH_FORGEJO_SMART_HTTP_BASE_URL");
        string? token = Environment.GetEnvironmentVariable("HEXALITH_FORGEJO_SMART_HTTP_TOKEN");
        string? certificateSha256 = Environment.GetEnvironmentVariable("HEXALITH_FORGEJO_SMART_HTTP_CERT_SHA256");
        string version = Environment.GetEnvironmentVariable("HEXALITH_FORGEJO_SMART_HTTP_VERSION") ?? "16.0.3";
        if (string.IsNullOrWhiteSpace(address)
            || string.IsNullOrWhiteSpace(token)
            || string.IsNullOrWhiteSpace(certificateSha256))
        {
            return;
        }

        Uri baseUri = new(address, UriKind.Absolute);
        CertificateCheckHandler certificateCheck = (certificate, valid, host) => valid
            || string.Equals(host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
                && certificate is CertificateX509 x509
                && string.Equals(
                    x509.Certificate.GetCertHashString(HashAlgorithmName.SHA256),
                    certificateSha256,
                    StringComparison.OrdinalIgnoreCase);
        PushOptions fixturePushOptions = new()
        {
            CertificateCheck = certificateCheck,
            CustomHeaders = [$"Authorization: Bearer {token}"],
        };

        string repositoryName = $"smart-http-{Guid.NewGuid():N}";
        using HttpClient setupClient = CreateHttpClient(baseUri, token, certificateSha256);
        await CreateRepositoryAsync(setupClient, repositoryName, TestContext.Current.CancellationToken).ConfigureAwait(true);

        string seedPath = CreateRepositoryPath();
        string verificationPath = CreateRepositoryPath();
        try
        {
            using Repository seed = new(seedPath);
            Blob changedSource = Blob(seed, "old");
            Blob removedSource = Blob(seed, "remove");
            Blob untouchedSource = Blob(seed, "untouched");
            Blob keptSource = Blob(seed, "keep");
            TreeDefinition original = new();
            original.Add("docs/change.txt", changedSource, Mode.NonExecutableFile);
            original.Add("docs/remove.txt", removedSource, Mode.NonExecutableFile);
            original.Add("bin/untouched.sh", untouchedSource, Mode.ExecutableFile);
            original.Add("docs/keep.txt", keptSource, Mode.NonExecutableFile);
            Commit parent = Commit(seed, seed.ObjectDatabase.CreateTree(original), "base", []);
            Remote seedRemote = seed.Network.Remotes.Add("origin", RemoteUrl(baseUri, repositoryName));
            seed.Network.Push(seedRemote, parent.Id.Sha, "refs/heads/main", fixturePushOptions);
            ProbeIsolatedFetch(
                verificationPath,
                RemoteUrl(baseUri, repositoryName),
                parent.Id.Sha,
                certificateCheck,
                token);
            Directory.Delete(verificationPath, recursive: true);
            verificationPath = CreateRepositoryPath();

            ProviderGitOperationResolvedTarget target = new(
                "smoke-admin",
                repositoryName,
                "heads/main",
                parent.Id.Sha);
            ProviderResolvedFileChange[] changes =
            [
                new(0, ProviderFileChangeKind.Add, "docs/add.txt", Encoding.UTF8.GetBytes("add"), ProviderFileContentType.RegularFile),
                new(1, ProviderFileChangeKind.Change, "docs/change.txt", Encoding.UTF8.GetBytes("new"), ProviderFileContentType.RegularFile, changedSource.Id.Sha),
                new(2, ProviderFileChangeKind.Remove, "docs/remove.txt", ReadOnlyMemory<byte>.Empty, ProviderFileContentType.RegularFile, removedSource.Id.Sha),
            ];
            using HttpClient operationHttpClient = CreateHttpClient(baseUri, token, certificateSha256);
            ForgejoHttpApiClient client = new(operationHttpClient, baseUri, certificateCheck);
            await using ConfiguredAsyncDisposable configuredClient = client.ConfigureAwait(true);

            ForgejoFileMutationResult stage = await client.StageFileChangesAsync(
                new ForgejoFileMutationRequest(target, changes, version, static _ => ValueTask.FromResult(true)),
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            stage.IsSuccess.ShouldBeTrue(stage.FailureCondition.ToString());
            (await ReadRemoteHeadAsync(setupClient, repositoryName, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(parent.Id.Sha);
            string? recordedCommitSha = null;
            ForgejoCommitResult commit = await client.CommitAsync(
                new ForgejoCommitRequest(
                    target,
                    changes,
                    stage.TreeSha!,
                    "smart HTTPS smoke",
                    version,
                    static _ => ValueTask.FromResult(true),
                    value =>
                    {
                        recordedCommitSha = value;
                        return ValueTask.FromResult(true);
                    }),
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            commit.IsSuccess.ShouldBeTrue(commit.FailureCondition.ToString());
            commit.CommitSha.ShouldBe(recordedCommitSha);
            (await ReadRemoteHeadAsync(setupClient, repositoryName, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(commit.CommitSha);

            using Repository verification = new(verificationPath);
            Remote verificationRemote = verification.Network.Remotes.Add(
                "origin",
                RemoteUrl(baseUri, repositoryName));
            verification.Network.Fetch(
                verificationRemote.Name,
                ["refs/heads/main:refs/hexalith/verified"],
                new FetchOptions
                {
                    CertificateCheck = certificateCheck,
                    CustomHeaders = [$"Authorization: Bearer {token}"],
                    TagFetchMode = TagFetchMode.None,
                });
            Commit received = verification.Lookup<Commit>(commit.CommitSha!).ShouldNotBeNull();
            received.Parents.Select(static value => value.Id.Sha).ShouldBe([parent.Id.Sha], ignoreOrder: false);
            received.Tree.Id.Sha.ShouldBe(stage.TreeSha);
            received.Message.ShouldBe("smart HTTPS smoke");
            IReadOnlyDictionary<string, TreeEntry> receivedEntries = FlattenTree(received.Tree);
            receivedEntries.Keys.Order(StringComparer.Ordinal).ShouldBe(
                ["bin/untouched.sh", "docs/add.txt", "docs/change.txt", "docs/keep.txt"],
                ignoreOrder: false);
            BlobBytes(receivedEntries["docs/add.txt"]).ShouldBe(Encoding.UTF8.GetBytes("add"));
            BlobBytes(receivedEntries["docs/change.txt"]).ShouldBe(Encoding.UTF8.GetBytes("new"));
            receivedEntries["bin/untouched.sh"].Mode.ShouldBe(Mode.ExecutableFile);
            receivedEntries["bin/untouched.sh"].Target.Id.ShouldBe(untouchedSource.Id);
            receivedEntries["docs/keep.txt"].Target.Id.ShouldBe(keptSource.Id);

            await VerifyStaleOldRaceAsync(
                baseUri,
                token,
                certificateSha256,
                certificateCheck,
                fixturePushOptions,
                setupClient,
                version,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            await VerifyPostDispatchCleanupFailureAsync(
                baseUri,
                token,
                certificateSha256,
                certificateCheck,
                fixturePushOptions,
                setupClient,
                version,
                TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
        finally
        {
            Directory.Delete(seedPath, recursive: true);
            Directory.Delete(verificationPath, recursive: true);
        }
    }

    private static async Task VerifyPostDispatchCleanupFailureAsync(
        Uri baseUri,
        string token,
        string certificateSha256,
        CertificateCheckHandler certificateCheck,
        PushOptions fixturePushOptions,
        HttpClient setupClient,
        string version,
        CancellationToken cancellationToken)
    {
        string repositoryName = $"smart-http-cleanup-{Guid.NewGuid():N}";
        await CreateRepositoryAsync(setupClient, repositoryName, cancellationToken).ConfigureAwait(false);
        string seedPath = CreateRepositoryPath();
        try
        {
            using Repository seed = new(seedPath);
            Blob source = Blob(seed, "base");
            TreeDefinition baseDefinition = new();
            baseDefinition.Add("target.txt", source, Mode.NonExecutableFile);
            Commit parent = Commit(seed, seed.ObjectDatabase.CreateTree(baseDefinition), "base", []);
            Remote remote = seed.Network.Remotes.Add("origin", RemoteUrl(baseUri, repositoryName));
            seed.Network.Push(remote, parent.Id.Sha, "refs/heads/main", fixturePushOptions);
            ProviderGitOperationResolvedTarget target = new(
                "smoke-admin",
                repositoryName,
                "heads/main",
                parent.Id.Sha);
            ProviderResolvedFileChange[] changes =
            [
                new(0, ProviderFileChangeKind.Change, "target.txt", Encoding.UTF8.GetBytes("updated"), ProviderFileContentType.RegularFile, source.Id.Sha),
            ];

            ForgejoFileMutationResult stage;
            using (HttpClient stageHttpClient = CreateHttpClient(baseUri, token, certificateSha256))
            {
                ForgejoHttpApiClient stageClient = new(stageHttpClient, baseUri, certificateCheck);
                await using ConfiguredAsyncDisposable configuredStageClient = stageClient.ConfigureAwait(false);
                stage = await stageClient.StageFileChangesAsync(
                    new ForgejoFileMutationRequest(target, changes, version, static _ => ValueTask.FromResult(true)),
                    cancellationToken).ConfigureAwait(false);
            }

            stage.IsSuccess.ShouldBeTrue(stage.FailureCondition.ToString());
            int dispatches = 0;
            int cleanupAttempts = 0;
            string? temporaryPath = null;
            ForgejoSmartHttpGitTransportTestHooks hooks = new()
            {
                BeforeFetch = (path, _) => temporaryPath = path,
                CleanupAttempted = () => Interlocked.Increment(ref cleanupAttempts),
                CleanupResult = static _ => false,
                ReceivePackDispatched = () => Interlocked.Increment(ref dispatches),
            };
            using HttpClient commitHttpClient = CreateHttpClient(baseUri, token, certificateSha256);
            ForgejoHttpApiClient commitClient = new(
                commitHttpClient,
                baseUri,
                certificateCheck,
                transportTestHooks: hooks);
            await using ConfiguredAsyncDisposable configuredCommitClient = commitClient.ConfigureAwait(false);
            ForgejoCommitResult commit = await commitClient.CommitAsync(
                new ForgejoCommitRequest(
                    target,
                    changes,
                    stage.TreeSha!,
                    "cleanup result precedence",
                    version,
                    static _ => ValueTask.FromResult(true),
                    static _ => ValueTask.FromResult(true)),
                cancellationToken).ConfigureAwait(false);

            commit.IsSuccess.ShouldBeFalse();
            commit.FailureCondition.ShouldBe(ForgejoApiFailureCondition.AmbiguousMutationResponse);
            commit.CommitSha.ShouldBeNull();
            commit.ObservedCommitSha.ShouldNotBeNull();
            commit.ToString().ShouldBe(nameof(ForgejoCommitResult));
            dispatches.ShouldBe(1);
            cleanupAttempts.ShouldBe(1);
            temporaryPath.ShouldNotBeNull();
            Directory.Exists(temporaryPath).ShouldBeFalse();
            (await ReadRemoteHeadAsync(setupClient, repositoryName, cancellationToken).ConfigureAwait(false))
                .ShouldBe(commit.ObservedCommitSha);
        }
        finally
        {
            Directory.Delete(seedPath, recursive: true);
        }
    }

    private static async Task VerifyStaleOldRaceAsync(
        Uri baseUri,
        string token,
        string certificateSha256,
        CertificateCheckHandler certificateCheck,
        PushOptions fixturePushOptions,
        HttpClient setupClient,
        string version,
        CancellationToken cancellationToken)
    {
        string repositoryName = $"smart-http-stale-{Guid.NewGuid():N}";
        await CreateRepositoryAsync(setupClient, repositoryName, cancellationToken).ConfigureAwait(false);
        string seedPath = CreateRepositoryPath();
        try
        {
            using Repository seed = new(seedPath);
            Blob source = Blob(seed, "base");
            TreeDefinition baseDefinition = new();
            baseDefinition.Add("target.txt", source, Mode.NonExecutableFile);
            Commit parent = Commit(seed, seed.ObjectDatabase.CreateTree(baseDefinition), "base", []);
            Remote remote = seed.Network.Remotes.Add("origin", RemoteUrl(baseUri, repositoryName));
            seed.Network.Push(remote, parent.Id.Sha, "refs/heads/main", fixturePushOptions);
            TreeDefinition competingDefinition = TreeDefinition.From(parent.Tree);
            competingDefinition.Add("race.txt", Blob(seed, "competitor"), Mode.NonExecutableFile);
            Commit competitor = Commit(
                seed,
                seed.ObjectDatabase.CreateTree(competingDefinition),
                "competitor",
                [parent]);
            int raceDispatched = 0;
            Action race = () =>
            {
                if (Interlocked.Exchange(ref raceDispatched, 1) == 0)
                {
                    seed.Network.Push(remote, competitor.Id.Sha, "refs/heads/main", fixturePushOptions);
                }
            };
            using HttpClient operationHttpClient = CreateHttpClient(baseUri, token, certificateSha256);
            ForgejoHttpApiClient client = new(operationHttpClient, baseUri, certificateCheck, race);
            await using ConfiguredAsyncDisposable configuredClient = client.ConfigureAwait(false);
            ProviderGitOperationResolvedTarget target = new(
                "smoke-admin",
                repositoryName,
                "heads/main",
                parent.Id.Sha);
            ProviderResolvedFileChange[] changes =
            [
                new(0, ProviderFileChangeKind.Change, "target.txt", Encoding.UTF8.GetBytes("adapter"), ProviderFileContentType.RegularFile, source.Id.Sha),
            ];
            ForgejoFileMutationResult stage = await client.StageFileChangesAsync(
                new ForgejoFileMutationRequest(target, changes, version, static _ => ValueTask.FromResult(true)),
                cancellationToken).ConfigureAwait(false);
            stage.IsSuccess.ShouldBeTrue(stage.FailureCondition.ToString());
            ForgejoCommitResult commit = await client.CommitAsync(
                new ForgejoCommitRequest(
                    target,
                    changes,
                    stage.TreeSha!,
                    "losing commit",
                    version,
                    static _ => ValueTask.FromResult(true),
                    static _ => ValueTask.FromResult(true)),
                cancellationToken).ConfigureAwait(false);

            raceDispatched.ShouldBe(1);
            commit.IsSuccess.ShouldBeFalse();
            commit.FailureCondition.ShouldBe(ForgejoApiFailureCondition.RefHeadConflict);
            (await ReadRemoteHeadAsync(setupClient, repositoryName, cancellationToken).ConfigureAwait(false)).ShouldBe(competitor.Id.Sha);
        }
        finally
        {
            Directory.Delete(seedPath, recursive: true);
        }
    }

    private static HttpClient CreateHttpClient(Uri baseUri, string token, string certificateSha256)
    {
        HttpClientHandler handler = new()
        {
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (request, certificate, _, errors) =>
                errors == SslPolicyErrors.None
                || request.RequestUri is not null
                    && string.Equals(request.RequestUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
                    && certificate is not null
                    && string.Equals(
                        certificate.GetCertHashString(HashAlgorithmName.SHA256),
                        certificateSha256,
                        StringComparison.OrdinalIgnoreCase),
        };
        HttpClient client = new(handler)
        {
            BaseAddress = baseUri,
            Timeout = TimeSpan.FromSeconds(30),
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Hexalith-Folders-Smart-HTTP-Smoke/1.0");
        return client;
    }

    private static async Task CreateRepositoryAsync(
        HttpClient client,
        string repositoryName,
        CancellationToken cancellationToken)
    {
        string payload = JsonSerializer.Serialize(new
        {
            name = repositoryName,
            @private = true,
            auto_init = false,
            default_branch = "main",
        });
        using StringContent content = new(payload, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await client.PostAsync("api/v1/user/repos", content, cancellationToken).ConfigureAwait(false);
        response.IsSuccessStatusCode.ShouldBeTrue(response.StatusCode.ToString());
    }

    private static async Task<string> ReadRemoteHeadAsync(
        HttpClient client,
        string repositoryName,
        CancellationToken cancellationToken)
    {
        string reference = Uri.EscapeDataString("heads/main");
        using HttpResponseMessage response = await client.GetAsync(
            $"api/v1/repos/smoke-admin/{repositoryName}/git/refs/{reference}",
            cancellationToken).ConfigureAwait(false);
        response.IsSuccessStatusCode.ShouldBeTrue(response.StatusCode.ToString());
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        return document.RootElement[0].GetProperty("object").GetProperty("sha").GetString().ShouldNotBeNull();
    }

    private static string RemoteUrl(Uri baseUri, string repositoryName)
        => new Uri(baseUri, $"smoke-admin/{repositoryName}.git").AbsoluteUri;

    private static void ProbeIsolatedFetch(
        string path,
        string remoteUrl,
        string expectedHead,
        CertificateCheckHandler certificateCheck,
        string token)
    {
        using Repository repository = new(path);
        repository.Config.Set("core.bare", true);
        repository.Config.Set("core.hooksPath", Path.Combine(path, "disabled-hooks"));
        repository.Config.Set("credential.helper", string.Empty);
        repository.Config.Set("http.followRedirects", false);
        repository.Config.Set("http.proxy", string.Empty);
        repository.Config.Set("protocol.version", 0);
        repository.Config.Set("filter.lfs.required", false);
        Remote remote = repository.Network.Remotes.Add("origin", remoteUrl);
        repository.Network.Fetch(
            remote.Name,
            ["refs/heads/main:refs/hexalith/expected"],
            new FetchOptions
            {
                CertificateCheck = certificateCheck,
                CustomHeaders = [$"Authorization: Bearer {token}"],
                Depth = 1,
                Prune = false,
                TagFetchMode = TagFetchMode.None,
            });
        repository.Refs["refs/hexalith/expected"].TargetIdentifier.ShouldBe(expectedHead);
    }

    private static string CreateRepositoryPath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"hxf-forgejo-smoke-{Guid.NewGuid():N}");
        Repository.Init(path, isBare: true);
        return path;
    }

    private static Blob Blob(Repository repository, string content)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(content), writable: false);
        return repository.ObjectDatabase.CreateBlob(stream);
    }

    private static Commit Commit(
        Repository repository,
        Tree tree,
        string message,
        IEnumerable<Commit> parents)
    {
        Signature signature = new("Smoke", "smoke@localhost.invalid", DateTimeOffset.UnixEpoch);
        return repository.ObjectDatabase.CreateCommit(signature, signature, message, tree, parents, prettifyMessage: false);
    }

    private static IReadOnlyDictionary<string, TreeEntry> FlattenTree(Tree root)
    {
        Dictionary<string, TreeEntry> result = new(StringComparer.Ordinal);
        AddTree(root, string.Empty, result);
        return result;
    }

    private static void AddTree(Tree tree, string prefix, IDictionary<string, TreeEntry> result)
    {
        foreach (TreeEntry entry in tree)
        {
            string path = string.IsNullOrEmpty(prefix) ? entry.Name : $"{prefix}/{entry.Name}";
            if (entry.Target is Tree nested)
            {
                AddTree(nested, path, result);
            }
            else
            {
                result.Add(path, entry);
            }
        }
    }

    private static byte[] BlobBytes(TreeEntry entry)
    {
        using Stream source = ((Blob)entry.Target).GetContentStream();
        using MemoryStream destination = new();
        source.CopyTo(destination);
        return destination.ToArray();
    }
}
