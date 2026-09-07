using System.Net;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.Forgejo;
using LibGit2Sharp;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.Forgejo;

[Collection(ForgejoNativeOperationGateCollection.Name)]
public sealed class ForgejoSmartHttpGitTransportTests
{
    private static readonly SemaphoreSlim NativePermitVerificationGate = new(1, 1);

    [Fact]
    public void NativeRuntimeBuildsExactOrderedTreeWithoutChangingUnrelatedPaths()
    {
        string path = CreateRepositoryPath();
        try
        {
            using Repository repository = new(path);
            Blob changedSource = Blob(repository, "old");
            Blob removedSource = Blob(repository, "remove");
            Blob untouchedSource = Blob(repository, "untouched");
            TreeDefinition original = new();
            original.Add("docs/change.txt", changedSource, Mode.NonExecutableFile);
            original.Add("docs/remove.txt", removedSource, Mode.NonExecutableFile);
            original.Add("bin/untouched.sh", untouchedSource, Mode.ExecutableFile);
            Tree originalTree = repository.ObjectDatabase.CreateTree(original);
            Commit parent = Commit(repository, originalTree);
            ProviderGitOperationResolvedTarget target = Target(parent.Id.Sha);
            ProviderResolvedFileChange[] changes =
            [
                new(0, ProviderFileChangeKind.Add, "docs/add.txt", Encoding.UTF8.GetBytes("add"), ProviderFileContentType.RegularFile),
                new(1, ProviderFileChangeKind.Change, "docs/change.txt", Encoding.UTF8.GetBytes("new"), ProviderFileContentType.RegularFile, changedSource.Id.Sha),
                new(2, ProviderFileChangeKind.Remove, "docs/remove.txt", ReadOnlyMemory<byte>.Empty, ProviderFileContentType.RegularFile, removedSource.Id.Sha),
            ];
            ForgejoNativeOperationState state = new(path, CancellationToken.None, TimeSpan.FromMinutes(1), long.MaxValue, long.MaxValue);

            bool created = ForgejoSmartHttpGitTransport.TryCreateTree(
                repository,
                target,
                changes,
                state,
                out Tree? tree,
                out ForgejoApiFailureCondition failure);

            created.ShouldBeTrue();
            failure.ShouldBe(ForgejoApiFailureCondition.None);
            tree.ShouldNotBeNull()["docs/add.txt"].Target.ShouldBeOfType<Blob>().GetContentText().ShouldBe("add");
            tree["docs/change.txt"].Target.ShouldBeOfType<Blob>().GetContentText().ShouldBe("new");
            tree["docs/remove.txt"].ShouldBeNull();
            tree["bin/untouched.sh"].Mode.ShouldBe(Mode.ExecutableFile);
            tree["bin/untouched.sh"].Target.Id.ShouldBe(untouchedSource.Id);
            GlobalSettings.Version.ShouldNotBeNull();
            GlobalSettings.Version.InformationalVersion.ShouldContain("0.32.0+libgit2-5853918");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Theory]
    [InlineData(100755)]
    [InlineData(120000)]
    public void TouchedExecutableOrSymbolicLinkIsRejected(int rawMode)
    {
        string path = CreateRepositoryPath();
        try
        {
            using Repository repository = new(path);
            Blob source = Blob(repository, "source");
            TreeDefinition original = new();
            original.Add("target", source, rawMode == 100755 ? Mode.ExecutableFile : Mode.SymbolicLink);
            Commit parent = Commit(repository, repository.ObjectDatabase.CreateTree(original));
            ProviderResolvedFileChange[] changes =
            [
                new(0, ProviderFileChangeKind.Change, "target", Encoding.UTF8.GetBytes("replacement"), ProviderFileContentType.RegularFile, source.Id.Sha),
            ];
            ForgejoNativeOperationState state = new(path, CancellationToken.None, TimeSpan.FromMinutes(1), long.MaxValue, long.MaxValue);

            ForgejoSmartHttpGitTransport.TryCreateTree(
                repository,
                Target(parent.Id.Sha),
                changes,
                state,
                out _,
                out _).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Theory]
    [InlineData(100644)]
    [InlineData(160000)]
    public void AddBelowBlobOrGitlinkAncestorIsRejected(int rawMode)
    {
        string path = CreateRepositoryPath();
        try
        {
            using Repository repository = new(path);
            TreeDefinition original = new();
            if (rawMode == 100644)
            {
                original.Add("parent", Blob(repository, "source"), Mode.NonExecutableFile);
            }
            else
            {
                Commit gitlink = Commit(repository, repository.ObjectDatabase.CreateTree(new TreeDefinition()));
                original.Add("parent", gitlink.Id, Mode.GitLink);
            }

            Commit parent = Commit(repository, repository.ObjectDatabase.CreateTree(original));
            ProviderResolvedFileChange[] changes =
            [
                new(0, ProviderFileChangeKind.Add, "parent/child.txt", "replacement"u8.ToArray(), ProviderFileContentType.RegularFile),
            ];
            ForgejoNativeOperationState state = new(path, CancellationToken.None, TimeSpan.FromMinutes(1), long.MaxValue, long.MaxValue);

            ForgejoSmartHttpGitTransport.TryCreateTree(
                repository,
                Target(parent.Id.Sha),
                changes,
                state,
                out _,
                out _).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void NativeCallbackFailureIsSticky()
    {
        string path = Path.Combine(Path.GetTempPath(), $"hxf-forgejo-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        try
        {
            using CancellationTokenSource cancellation = new();
            ForgejoNativeOperationState state = new(path, cancellation.Token, TimeSpan.FromMinutes(1), 8, long.MaxValue);

            state.Check(9).ShouldBeFalse();
            state.FailureCondition.ShouldBe(ForgejoApiFailureCondition.TransferLimitExceeded);
            cancellation.Cancel();
            state.Check(0).ShouldBeFalse();
            state.FailureCondition.ShouldBe(ForgejoApiFailureCondition.TransferLimitExceeded);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public async Task UserCancelledExceptionPreservesTheCallbackSelectedCeiling()
    {
        ProviderGitOperationResolvedTarget target = Target(new string('a', 40));
        AdvertisementHandler handler = new(target);
        using HttpClient httpClient = new(handler);
        ForgejoSmartHttpGitTransport transport = new(
            httpClient,
            new Uri("https://forgejo.invalid/", UriKind.Absolute),
            new ForgejoAuthorizationHeader("Bearer", "token"),
            testHooks: new ForgejoSmartHttpGitTransportTestHooks
            {
                AllowAmbientConfigurationForTests = true,
                MaximumTransferBytes = 8,
                BeforeFetch = (_, state) =>
                {
                    state.Check(9).ShouldBeFalse();
                    throw new UserCancelledException();
                },
            });

        ForgejoFileMutationResult result = await transport.StageAsync(
            StageRequest(target),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.TransferLimitExceeded);
    }

    [Fact]
    public void TransferAndTemporaryDiskCeilingsFailWhileProgressIsObserved()
    {
        string path = Path.Combine(Path.GetTempPath(), $"hxf-forgejo-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        try
        {
            File.WriteAllBytes(Path.Combine(path, "object"), new byte[17]);
            ForgejoNativeOperationState transfer = new(path, CancellationToken.None, TimeSpan.FromMinutes(1), 8, long.MaxValue);
            ForgejoNativeOperationState disk = new(path, CancellationToken.None, TimeSpan.FromMinutes(1), long.MaxValue, 16);

            transfer.Check(9).ShouldBeFalse();
            transfer.FailureCondition.ShouldBe(ForgejoApiFailureCondition.TransferLimitExceeded);
            disk.Check(0).ShouldBeFalse();
            disk.FailureCondition.ShouldBe(ForgejoApiFailureCondition.TemporaryDiskLimitExceeded);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommitCrossingTransportCeilingFailsBeforeDispatchAndCleansExactlyOnce(bool crossDiskCeiling)
    {
        ProviderGitOperationResolvedTarget target = Target(new string('a', 40));
        AdvertisementHandler handler = new(target);
        using HttpClient httpClient = new(handler);
        int cleanupAttempts = 0;
        int dispatches = 0;
        string? temporaryPath = null;
        ForgejoSmartHttpGitTransportTestHooks hooks = new()
        {
            AllowAmbientConfigurationForTests = true,
            MaximumTransferBytes = 8,
            MaximumTemporaryDiskBytes = crossDiskCeiling ? 16 : long.MaxValue,
            BeforeFetch = (path, state) =>
            {
                temporaryPath = path;
                if (crossDiskCeiling)
                {
                    File.WriteAllBytes(Path.Combine(path, "boundary"), new byte[17]);
                    state.Check(0).ShouldBeFalse();
                }
                else
                {
                    state.Check(9).ShouldBeFalse();
                }
            },
            CleanupAttempted = () => Interlocked.Increment(ref cleanupAttempts),
            ReceivePackDispatched = () => Interlocked.Increment(ref dispatches),
        };
        ForgejoSmartHttpGitTransport transport = new(
            httpClient,
            new Uri("https://forgejo.invalid/", UriKind.Absolute),
            new ForgejoAuthorizationHeader("Bearer", "token"),
            testHooks: hooks);

        ForgejoCommitResult result = await transport.CommitAsync(
            CommitRequest(target),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(
            crossDiskCeiling
                ? ForgejoApiFailureCondition.TemporaryDiskLimitExceeded
                : ForgejoApiFailureCondition.TransferLimitExceeded);
        result.MutationDispatched.ShouldBeFalse();
        dispatches.ShouldBe(0);
        cleanupAttempts.ShouldBe(1);
        temporaryPath.ShouldNotBeNull();
        Directory.Exists(temporaryPath).ShouldBeFalse();
        handler.Requests.Count(static request =>
            request.Method == HttpMethod.Post
            && request.Uri.AbsolutePath.EndsWith("/git-receive-pack", StringComparison.Ordinal)).ShouldBe(0);
    }

    [Fact]
    public async Task CleanupFailureBeforeDispatchReturnsAllowListedFailureWithoutLeakingPath()
    {
        ProviderGitOperationResolvedTarget target = Target(new string('a', 40));
        AdvertisementHandler handler = new(target);
        using HttpClient httpClient = new(handler);
        int cleanupAttempts = 0;
        int dispatches = 0;
        string? temporaryPath = null;
        ForgejoSmartHttpGitTransportTestHooks hooks = new()
        {
            AllowAmbientConfigurationForTests = true,
            MaximumTransferBytes = 8,
            BeforeFetch = (path, state) =>
            {
                temporaryPath = path;
                state.Check(9).ShouldBeFalse();
            },
            CleanupAttempted = () => Interlocked.Increment(ref cleanupAttempts),
            CleanupResult = static _ => false,
            ReceivePackDispatched = () => Interlocked.Increment(ref dispatches),
        };
        ForgejoSmartHttpGitTransport transport = new(
            httpClient,
            new Uri("https://forgejo.invalid/", UriKind.Absolute),
            new ForgejoAuthorizationHeader("Bearer", "token"),
            testHooks: hooks);

        ForgejoCommitResult result = await transport.CommitAsync(
            CommitRequest(target),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.TemporaryRepositoryCleanupFailed);
        result.ToString().ShouldBe(nameof(ForgejoCommitResult));
        dispatches.ShouldBe(0);
        cleanupAttempts.ShouldBe(1);
        temporaryPath.ShouldNotBeNull();
        Directory.Exists(temporaryPath).ShouldBeFalse();
        handler.Requests.ShouldNotContain(static request =>
            request.Method == HttpMethod.Post
            && request.Uri.AbsolutePath.EndsWith("/git-receive-pack", StringComparison.Ordinal));
        ForgejoFailureMapper.ToProviderOperationFailure(result.FailureCondition).ShouldBe(
            (ProviderFailureCategory.ProviderFailureKnown, "forgejo_temporary_repository_cleanup_failed"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PartialSetupAndUnexpectedExecutionFailuresCleanExactlyOnce(bool failDuringSetup)
    {
        ProviderGitOperationResolvedTarget target = Target(new string('a', 40));
        AdvertisementHandler handler = new(target);
        using HttpClient httpClient = new(handler);
        int cleanupAttempts = 0;
        string? temporaryPath = null;
        ForgejoSmartHttpGitTransport transport = new(
            httpClient,
            new Uri("https://forgejo.invalid/", UriKind.Absolute),
            new ForgejoAuthorizationHeader("Bearer", "token"),
            testHooks: new ForgejoSmartHttpGitTransportTestHooks
            {
                AllowAmbientConfigurationForTests = true,
                RepositoryOpened = failDuringSetup
                    ? repository =>
                    {
                        temporaryPath = repository.Info.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        throw new InvalidOperationException("setup failure");
                    }
                    : null,
                BeforeFetch = failDuringSetup
                    ? (path, _) => temporaryPath = path
                    : (path, _) =>
                    {
                        temporaryPath = path;
                        throw new InvalidOperationException("execution failure");
                    },
                CleanupAttempted = () => Interlocked.Increment(ref cleanupAttempts),
            });

        ForgejoFileMutationResult result = await transport.StageAsync(
            StageRequest(target),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.ServerUnavailable);
        cleanupAttempts.ShouldBe(1);
        temporaryPath.ShouldNotBeNull();
        Directory.Exists(temporaryPath).ShouldBeFalse();
    }

    [Fact]
    public void EffectiveGlobalXdgOrSystemConfigurationIsRejected()
    {
        string root = Path.Combine(Path.GetTempPath(), $"hxf-forgejo-config-{Guid.NewGuid():N}");
        string repositoryPath = Path.Combine(root, "repository");
        Directory.CreateDirectory(root);
        try
        {
            Repository.Init(repositoryPath, isBare: true);
            string global = Path.Combine(root, "global.config");
            string xdg = Path.Combine(root, "xdg.config");
            string system = Path.Combine(root, "system.config");
            File.WriteAllText(global, "[http]\n\tproxy = https://proxy.invalid\n");
            File.WriteAllText(xdg, string.Empty);
            File.WriteAllText(system, string.Empty);
            using Configuration configuration = Configuration.BuildFrom(repositoryPath, global, xdg, system);

            ForgejoSmartHttpGitTransport.HasForbiddenAmbientConfiguration(configuration).ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StageAndCommitUseTheProductionAmbientConfigurationScannerBeforeFetch(bool commit)
    {
        ProviderGitOperationResolvedTarget target = Target(new string('a', 40));
        AdvertisementHandler handler = new(target);
        using HttpClient httpClient = new(handler);
        string root = Path.Combine(Path.GetTempPath(), $"hxf-forgejo-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string global = Path.Combine(root, "global.config");
        string xdg = Path.Combine(root, "xdg.config");
        string system = Path.Combine(root, "system.config");
        File.WriteAllText(global, "[http]\n\tproxy = https://proxy.invalid\n");
        File.WriteAllText(xdg, string.Empty);
        File.WriteAllText(system, string.Empty);
        int configurationSources = 0;
        int fetches = 0;
        int cleanupAttempts = 0;
        ForgejoSmartHttpGitTransport transport = new(
            httpClient,
            new Uri("https://forgejo.invalid/", UriKind.Absolute),
            new ForgejoAuthorizationHeader("Bearer", "token"),
            testHooks: new ForgejoSmartHttpGitTransportTestHooks
            {
                EffectiveConfigurationFactory = repositoryPath =>
                {
                    Interlocked.Increment(ref configurationSources);
                    return Configuration.BuildFrom(repositoryPath, global, xdg, system);
                },
                BeforeFetch = (_, _) => Interlocked.Increment(ref fetches),
                CleanupAttempted = () => Interlocked.Increment(ref cleanupAttempts),
            });

        try
        {
            if (commit)
            {
                ForgejoCommitResult result = await transport.CommitAsync(
                    CommitRequest(target),
                    TestContext.Current.CancellationToken).ConfigureAwait(true);
                result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.AmbientConfigurationUnsupported);
                result.MutationDispatched.ShouldBeFalse();
            }
            else
            {
                ForgejoFileMutationResult result = await transport.StageAsync(
                    StageRequest(target),
                    TestContext.Current.CancellationToken).ConfigureAwait(true);
                result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.AmbientConfigurationUnsupported);
            }

            configurationSources.ShouldBe(1);
            fetches.ShouldBe(0);
            cleanupAttempts.ShouldBe(1);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreExistingTemporaryPathIsNeverClaimedOrDeleted(bool commit)
    {
        ProviderGitOperationResolvedTarget target = Target(new string('a', 40));
        AdvertisementHandler handler = new(target);
        using HttpClient httpClient = new(handler);
        string path = Path.Combine(Path.GetTempPath(), $"hxf-forgejo-owned-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        string sentinel = Path.Combine(path, "owned-by-someone-else");
        File.WriteAllText(sentinel, "sentinel");
        int cleanupAttempts = 0;
        ForgejoSmartHttpGitTransport transport = new(
            httpClient,
            new Uri("https://forgejo.invalid/", UriKind.Absolute),
            new ForgejoAuthorizationHeader("Bearer", "token"),
            testHooks: new ForgejoSmartHttpGitTransportTestHooks
            {
                TemporaryRepositoryPathFactory = () => path,
                CleanupAttempted = () => Interlocked.Increment(ref cleanupAttempts),
            });

        try
        {
            if (commit)
            {
                ForgejoCommitResult result = await transport.CommitAsync(
                    CommitRequest(target),
                    TestContext.Current.CancellationToken).ConfigureAwait(true);
                result.IsSuccess.ShouldBeFalse();
                result.MutationDispatched.ShouldBeFalse();
            }
            else
            {
                ForgejoFileMutationResult result = await transport.StageAsync(
                    StageRequest(target),
                    TestContext.Current.CancellationToken).ConfigureAwait(true);
                result.IsSuccess.ShouldBeFalse();
            }

            cleanupAttempts.ShouldBe(0);
            File.ReadAllText(sentinel).ShouldBe("sentinel");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public async Task StageCreatesTheProductionTemporaryDirectoryWithCurrentUserOnlyProtection()
    {
        ProviderGitOperationResolvedTarget target = Target(new string('a', 40));
        AdvertisementHandler handler = new(target);
        using HttpClient httpClient = new(handler);
        bool protectionObserved = false;
        string? temporaryPath = null;
        ForgejoSmartHttpGitTransport transport = new(
            httpClient,
            new Uri("https://forgejo.invalid/", UriKind.Absolute),
            new ForgejoAuthorizationHeader("Bearer", "token"),
            testHooks: new ForgejoSmartHttpGitTransportTestHooks
            {
                AllowAmbientConfigurationForTests = true,
                MaximumTransferBytes = 0,
                BeforeFetch = (path, state) =>
                {
                    temporaryPath = path;
                    if (OperatingSystem.IsWindows())
                    {
                        AssertWindowsDirectoryProtection(path);
                    }
                    else
                    {
                        File.GetUnixFileMode(path).ShouldBe(
                            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                    }

                    protectionObserved = true;
                    state.Check(1).ShouldBeFalse();
                },
            });

        ForgejoFileMutationResult result = await transport.StageAsync(
            StageRequest(target),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.TransferLimitExceeded);
        protectionObserved.ShouldBeTrue();
        temporaryPath.ShouldNotBeNull();
        Directory.Exists(temporaryPath).ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeclaredAndStreamedAdvertisementOverflowHaveTheSameClassification(bool declaredLength)
    {
        byte[] oversized = new byte[(1024 * 1024) + 1];
        HttpContent content = declaredLength
            ? new ByteArrayContent(oversized)
            : new ForgejoUnknownLengthContent(oversized);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-git-upload-pack-advertisement");
        using HttpClient httpClient = new(new ForgejoStaticHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content,
        }));
        ForgejoSmartHttpGitTransport transport = new(
            httpClient,
            new Uri("https://forgejo.invalid/", UriKind.Absolute),
            new ForgejoAuthorizationHeader("Bearer", "token"));

        ForgejoFileMutationResult result = await transport.StageAsync(
            StageRequest(Target(new string('a', 40))),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.ResponseLimitExceeded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimedOutOrCancelledNativeWorkerRetainsPermitUntilExactlyOnceCleanup(bool cancelCaller)
    {
        await NativePermitVerificationGate.WaitAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        try
        {
            await VerifyTimedOutOrCancelledNativeWorkerAsync(cancelCaller).ConfigureAwait(true);
        }
        finally
        {
            NativePermitVerificationGate.Release();
        }
    }

    private static async Task VerifyTimedOutOrCancelledNativeWorkerAsync(bool cancelCaller)
    {
        ProviderGitOperationResolvedTarget target = Target(new string('a', 40));
        AdvertisementHandler handler = new(target);
        using HttpClient firstHttpClient = AuthorizedHttpClient(handler);
        using HttpClient secondHttpClient = AuthorizedHttpClient(handler);
        using ManualResetEventSlim releaseWorker = new(initialState: false);
        TaskCompletionSource workerStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource workerCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int cleanupAttempts = 0;
        int secondWorkerStarts = 0;
        string? temporaryPath = null;
        ForgejoSmartHttpGitTransportTestHooks firstHooks = new()
        {
            AllowAmbientConfigurationForTests = true,
            OperationTimeout = cancelCaller ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(250),
            BeforeRepositoryOpen = path =>
            {
                temporaryPath = path;
                workerStarted.TrySetResult();
                releaseWorker.Wait();
                throw new InvalidOperationException("controlled worker completion");
            },
            CleanupAttempted = () => Interlocked.Increment(ref cleanupAttempts),
            NativeOperationCompleted = () => workerCompleted.TrySetResult(),
        };
        ForgejoSmartHttpGitTransportTestHooks secondHooks = new()
        {
            AllowAmbientConfigurationForTests = true,
            OperationTimeout = TimeSpan.FromMilliseconds(100),
            BeforeFetch = (_, _) => Interlocked.Increment(ref secondWorkerStarts),
        };
        ForgejoHttpApiClient firstClient = new(
            firstHttpClient,
            new Uri("https://forgejo.invalid/", UriKind.Absolute),
            transportTestHooks: firstHooks);
        ForgejoHttpApiClient secondClient = new(
            secondHttpClient,
            new Uri("https://forgejo.invalid/", UriKind.Absolute),
            transportTestHooks: secondHooks);
        using CancellationTokenSource callerCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        if (cancelCaller)
        {
            callerCancellation.CancelAfter(TimeSpan.FromMilliseconds(250));
        }

        ForgejoFileMutationResult? firstResult = null;
        ForgejoFileMutationResult? secondResult = null;
        try
        {
            Task<ForgejoFileMutationResult> firstCall = firstClient.StageFileChangesAsync(
                StageRequest(target),
                cancelCaller ? callerCancellation.Token : TestContext.Current.CancellationToken);
            await workerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ConfigureAwait(true);
            firstResult = await firstCall.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ConfigureAwait(true);
            secondResult = await secondClient.StageFileChangesAsync(
                StageRequest(target),
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            secondWorkerStarts.ShouldBe(0);
            cleanupAttempts.ShouldBe(0);
            temporaryPath.ShouldNotBeNull();
            Directory.Exists(temporaryPath).ShouldBeTrue();
        }
        finally
        {
            releaseWorker.Set();
            await workerCompleted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            await firstClient.DisposeAsync().ConfigureAwait(true);
            await secondClient.DisposeAsync().ConfigureAwait(true);
        }

        firstResult.ShouldNotBeNull().FailureCondition.ShouldBe(
            cancelCaller
                ? ForgejoApiFailureCondition.CancellationBeforeDispatch
                : ForgejoApiFailureCondition.OperationTimedOut);
        secondResult.ShouldNotBeNull().FailureCondition.ShouldBe(ForgejoApiFailureCondition.OperationTimedOut);
        cleanupAttempts.ShouldBe(1);
        Directory.Exists(temporaryPath).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimedOutOrCancelledCommitWorkerRetainsPermitUntilExactlyOnceCleanup(bool cancelCaller)
    {
        await NativePermitVerificationGate.WaitAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        try
        {
            await VerifyTimedOutOrCancelledCommitWorkerAsync(cancelCaller).ConfigureAwait(true);
        }
        finally
        {
            NativePermitVerificationGate.Release();
        }
    }

    private static async Task VerifyTimedOutOrCancelledCommitWorkerAsync(bool cancelCaller)
    {
        ProviderGitOperationResolvedTarget target = Target(new string('a', 40));
        ForgejoCommitRequest commitRequest = CommitRequest(target) with
        {
            Changes = StageRequest(target).Changes,
        };
        AdvertisementHandler handler = new(target);
        using HttpClient firstHttpClient = AuthorizedHttpClient(handler);
        using HttpClient secondHttpClient = AuthorizedHttpClient(handler);
        using ManualResetEventSlim releaseWorker = new(initialState: false);
        TaskCompletionSource workerStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource workerCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int cleanupAttempts = 0;
        int secondWorkerStarts = 0;
        string? temporaryPath = null;
        ForgejoSmartHttpGitTransportTestHooks firstHooks = new()
        {
            AllowAmbientConfigurationForTests = true,
            OperationTimeout = cancelCaller ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(250),
            BeforeRepositoryOpen = path =>
            {
                temporaryPath = path;
                workerStarted.TrySetResult();
                releaseWorker.Wait();
                throw new InvalidOperationException("controlled worker completion");
            },
            CleanupAttempted = () => Interlocked.Increment(ref cleanupAttempts),
            NativeOperationCompleted = () => workerCompleted.TrySetResult(),
        };
        ForgejoSmartHttpGitTransportTestHooks secondHooks = new()
        {
            AllowAmbientConfigurationForTests = true,
            OperationTimeout = TimeSpan.FromMilliseconds(100),
            BeforeFetch = (_, _) => Interlocked.Increment(ref secondWorkerStarts),
        };
        ForgejoHttpApiClient firstClient = new(
            firstHttpClient,
            new Uri("https://forgejo.invalid/", UriKind.Absolute),
            transportTestHooks: firstHooks);
        ForgejoHttpApiClient secondClient = new(
            secondHttpClient,
            new Uri("https://forgejo.invalid/", UriKind.Absolute),
            transportTestHooks: secondHooks);
        using CancellationTokenSource callerCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        if (cancelCaller)
        {
            callerCancellation.CancelAfter(TimeSpan.FromMilliseconds(250));
        }

        ForgejoCommitResult? firstResult = null;
        ForgejoCommitResult? secondResult = null;
        try
        {
            Task<ForgejoCommitResult> firstCall = firstClient.CommitAsync(
                commitRequest,
                cancelCaller ? callerCancellation.Token : TestContext.Current.CancellationToken);
            await workerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ConfigureAwait(true);
            firstResult = await firstCall.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ConfigureAwait(true);
            secondResult = await secondClient.CommitAsync(
                commitRequest,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

            secondWorkerStarts.ShouldBe(0);
            cleanupAttempts.ShouldBe(0);
            temporaryPath.ShouldNotBeNull();
            Directory.Exists(temporaryPath).ShouldBeTrue();
        }
        finally
        {
            releaseWorker.Set();
            await workerCompleted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            await firstClient.DisposeAsync().ConfigureAwait(true);
            await secondClient.DisposeAsync().ConfigureAwait(true);
        }

        firstResult.ShouldNotBeNull().FailureCondition.ShouldBe(
            cancelCaller
                ? ForgejoApiFailureCondition.CancellationBeforeDispatch
                : ForgejoApiFailureCondition.OperationTimedOut);
        firstResult.MutationDispatched.ShouldBeFalse();
        secondResult.ShouldNotBeNull().FailureCondition.ShouldBe(ForgejoApiFailureCondition.OperationTimedOut);
        secondResult.MutationDispatched.ShouldBeFalse();
        cleanupAttempts.ShouldBe(1);
        Directory.Exists(temporaryPath).ShouldBeFalse();
    }

    [Fact]
    public void CallerInterruptionAndReceivePackDispatchAreMutuallyExclusive()
    {
        ForgejoCommitDispatchState interruptedFirst = new();
        interruptedFirst.TryRecordCallerInterruption().ShouldBeTrue();
        interruptedFirst.TryClaimMutationDispatch().ShouldBeFalse();
        interruptedFirst.MutationDispatched.ShouldBeFalse();

        ForgejoCommitDispatchState dispatchedFirst = new();
        dispatchedFirst.TryClaimMutationDispatch().ShouldBeTrue();
        dispatchedFirst.TryRecordCallerInterruption().ShouldBeFalse();
        dispatchedFirst.MutationDispatched.ShouldBeTrue();
    }

    [Fact]
    public void ReceiveAdvertisementRequiresSha1ExpectedHeadAndReportStatus()
    {
        ProviderGitOperationResolvedTarget target = Target(new string('a', 40));
        byte[] valid = Advertisement("git-receive-pack", target.ExpectedHeadSha, target.FullRef, "report-status delete-refs object-format=sha1");
        byte[] noReportStatus = Advertisement("git-receive-pack", target.ExpectedHeadSha, target.FullRef, "delete-refs");
        byte[] sha256 = Advertisement("git-receive-pack", new string('b', 64), target.FullRef, "report-status object-format=sha256");

        ForgejoSmartHttpGitTransport.TryValidateAdvertisement(valid, "git-receive-pack", target, true, out _).ShouldBeTrue();
        ForgejoSmartHttpGitTransport.TryValidateAdvertisement(noReportStatus, "git-receive-pack", target, true, out _).ShouldBeFalse();
        ForgejoSmartHttpGitTransport.TryValidateAdvertisement(sha256, "git-receive-pack", target, true, out ForgejoApiFailureCondition failure).ShouldBeFalse();
        failure.ShouldBe(ForgejoApiFailureCondition.ObjectFormatUnsupported);
    }

    [Fact]
    public void AdvertisementRequiresServiceFlushAndCapabilitiesOnTheFirstAdvertisedRef()
    {
        ProviderGitOperationResolvedTarget target = Target(new string('a', 40));
        string serviceLine = "# service=git-receive-pack\n";
        string targetLine = $"{target.ExpectedHeadSha} {target.FullRef}\0report-status object-format=sha1\n";
        byte[] missingServiceFlush = Encoding.UTF8.GetBytes(
            $"{serviceLine.Length + 4:x4}{serviceLine}{targetLine.Length + 4:x4}{targetLine}0000");
        string firstLine = $"{target.ExpectedHeadSha} refs/heads/other\0delete-refs object-format=sha1\n";
        string laterTargetLine = $"{target.ExpectedHeadSha} {target.FullRef}\0report-status\n";
        byte[] laterRefCapability = Encoding.UTF8.GetBytes(
            $"{serviceLine.Length + 4:x4}{serviceLine}0000{firstLine.Length + 4:x4}{firstLine}{laterTargetLine.Length + 4:x4}{laterTargetLine}0000");

        ForgejoSmartHttpGitTransport.TryValidateAdvertisement(
            missingServiceFlush,
            "git-receive-pack",
            target,
            requireReportStatus: true,
            out _).ShouldBeFalse();
        ForgejoSmartHttpGitTransport.TryValidateAdvertisement(
            laterRefCapability,
            "git-receive-pack",
            target,
            requireReportStatus: true,
            out _).ShouldBeFalse();
    }

    private static byte[] Advertisement(string service, string objectId, string fullRef, string capabilities)
    {
        string serviceLine = $"# service={service}\n";
        string refLine = $"{objectId} {fullRef}\0{capabilities}\n";
        return Encoding.UTF8.GetBytes($"{serviceLine.Length + 4:x4}{serviceLine}0000{refLine.Length + 4:x4}{refLine}0000");
    }

    [SupportedOSPlatform("windows")]
    private static void AssertWindowsDirectoryProtection(string path)
    {
        DirectorySecurity security = new DirectoryInfo(path).GetAccessControl(
            AccessControlSections.Access | AccessControlSections.Owner);
        SecurityIdentifier current = WindowsIdentity.GetCurrent().User.ShouldNotBeNull();
        security.AreAccessRulesProtected.ShouldBeTrue();
        security.GetOwner(typeof(SecurityIdentifier)).ShouldBe(current);
        security.GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .ShouldAllBe(rule => rule.IdentityReference == current);
    }

    private static Blob Blob(Repository repository, string content)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(content), writable: false);
        return repository.ObjectDatabase.CreateBlob(stream);
    }

    private static Commit Commit(Repository repository, Tree tree)
    {
        Signature signature = new("Test", "test@localhost.invalid", DateTimeOffset.UnixEpoch);
        return repository.ObjectDatabase.CreateCommit(signature, signature, "base", tree, [], prettifyMessage: false);
    }

    private static string CreateRepositoryPath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"hxf-forgejo-test-{Guid.NewGuid():N}");
        Repository.Init(path, isBare: true);
        return path;
    }

    private static ProviderGitOperationResolvedTarget Target(string expectedHead)
        => new("owner", "repository", "heads/main", expectedHead);

    private static ForgejoCommitRequest CommitRequest(ProviderGitOperationResolvedTarget target)
        => new(
            target,
            [],
            new string('b', 40),
            "commit",
            "16.0.3",
            static _ => ValueTask.FromResult(ForgejoReservationValidationStatus.Valid),
            static _ => ValueTask.FromResult(true));

    private static ForgejoFileMutationRequest StageRequest(ProviderGitOperationResolvedTarget target)
        => new(
            target,
            [new ProviderResolvedFileChange(0, ProviderFileChangeKind.Add, "file.txt", new byte[] { 1 }, ProviderFileContentType.RegularFile)],
            "16.0.3",
            static _ => ValueTask.FromResult(ForgejoReservationValidationStatus.Valid));

    private static HttpClient AuthorizedHttpClient(HttpMessageHandler handler)
    {
        HttpClient client = new(handler, disposeHandler: false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token");
        return client;
    }

    private sealed class AdvertisementHandler(ProviderGitOperationResolvedTarget target) : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri Uri)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Uri uri = request.RequestUri.ShouldNotBeNull();
            Requests.Add((request.Method, uri));
            if (uri.AbsolutePath.EndsWith("/api/v1/version", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"version\":\"16.0.3\"}", Encoding.UTF8, "application/json"),
                    RequestMessage = request,
                });
            }

            string service = uri.Query.Contains("git-receive-pack", StringComparison.Ordinal)
                ? "git-receive-pack"
                : "git-upload-pack";
            byte[] advertisement = Advertisement(
                service,
                target.ExpectedHeadSha,
                target.FullRef,
                service == "git-receive-pack" ? "report-status object-format=sha1" : "object-format=sha1");
            ByteArrayContent content = new(advertisement);
            content.Headers.ContentType = new MediaTypeHeaderValue($"application/x-{service}-advertisement");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content,
                RequestMessage = request,
            });
        }
    }
}
