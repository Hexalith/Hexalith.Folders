using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Tests.Providers.GitHub;

internal sealed class RecordingProviderOperationSourceResolver : IProviderOperationSourceResolver
{
    internal const string HeadSha = "1111111111111111111111111111111111111111";
    internal const string TreeSha = "2222222222222222222222222222222222222222";
    internal const string CommitSha = "3333333333333333333333333333333333333333";

    private readonly ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>? _fileMutationResult;
    private readonly ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>? _commitResult;
    private readonly ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>? _statusResult;

    private RecordingProviderOperationSourceResolver(
        ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>? fileMutationResult,
        ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>? commitResult,
        ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>? statusResult)
    {
        _fileMutationResult = fileMutationResult;
        _commitResult = commitResult;
        _statusResult = statusResult;
    }

    public int FileMutationCalls { get; private set; }

    public int CommitCalls { get; private set; }

    public int StatusCalls { get; private set; }

    public static RecordingProviderOperationSourceResolver Success()
    {
        ProviderGitOperationResolvedTarget target = Target();
        return new(
            ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>.Success(
                new ProviderFileMutationResolvedSource(
                    target,
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
                    ])),
            ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>.Success(
                new ProviderCommitResolvedSource(target, TreeSha, "safe commit message")),
            ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>.Success(
                new ProviderOperationStatusResolvedSource(target, CommitSha)));
    }

    public static RecordingProviderOperationSourceResolver Failure(
        ProviderFailureCategory category = ProviderFailureCategory.ProviderConfigurationMissing)
        => new(
            ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>.Failure(category, "operation_source_unavailable"),
            ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>.Failure(category, "operation_source_unavailable"),
            ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>.Failure(category, "operation_source_unavailable"));

    public static RecordingProviderOperationSourceResolver UnsafeFailure()
        => new(
            new(false, null, ProviderFailureCategory.None, "token-sentinel://unsafe", TimeSpan.FromDays(30)),
            new(false, null, ProviderFailureCategory.None, "token-sentinel://unsafe", TimeSpan.FromDays(30)),
            new(false, null, ProviderFailureCategory.None, "token-sentinel://unsafe", TimeSpan.FromDays(30)));

    public static RecordingProviderOperationSourceResolver NullResults()
        => new(null, null, null);

    public static RecordingProviderOperationSourceResolver SuccessWithNullSources()
        => new(
            new(true, null, ProviderFailureCategory.None, "success", null),
            new(true, null, ProviderFailureCategory.None, "success", null),
            new(true, null, ProviderFailureCategory.None, "success", null));

    public ValueTask<ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>> ResolveFileMutationAsync(
        ProviderFileMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FileMutationCalls++;
        return ValueTask.FromResult(_fileMutationResult!);
    }

    public ValueTask<ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>> ResolveCommitAsync(
        ProviderCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommitCalls++;
        return ValueTask.FromResult(_commitResult!);
    }

    public ValueTask<ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>> ResolveStatusAsync(
        ProviderOperationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StatusCalls++;
        return ValueTask.FromResult(_statusResult!);
    }

    private static ProviderGitOperationResolvedTarget Target()
        => new(
            "octokit-owner-sentinel",
            "octokit-repository-sentinel",
            "heads/main",
            HeadSha);
}
