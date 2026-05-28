using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Services;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.6 / AC #12 — totality (drift-sentinel) coverage for every SDK-enum switch in
/// <see cref="ConsoleStatusText"/>. Each switch must resolve every declared member and throw on an
/// undefined value — never a silent default.
/// </summary>
public sealed class ConsoleStatusTextTests
{
    public static TheoryData<LockState> LockStates => [.. Enum.GetValues<LockState>()];

    public static TheoryData<CleanupStatus> CleanupStatuses => [.. Enum.GetValues<CleanupStatus>()];

    public static TheoryData<FileMetadataItemKind> FileKinds => [.. Enum.GetValues<FileMetadataItemKind>()];

    public static TheoryData<FileMetadataItemRedaction> FileRedactions => [.. Enum.GetValues<FileMetadataItemRedaction>()];

    public static TheoryData<CommitEvidenceCommitReferenceClassification> CommitClassifications => [.. Enum.GetValues<CommitEvidenceCommitReferenceClassification>()];

    public static TheoryData<CanonicalErrorCategory> ErrorCategories => [.. Enum.GetValues<CanonicalErrorCategory>()];

    [Theory]
    [MemberData(nameof(LockStates))]
    public void ResolveLock_IsTotal(LockState state)
    {
        ConsoleStatusText.ResolveLockLabel(state).ShouldNotBeNullOrWhiteSpace();
        _ = ConsoleStatusText.ResolveLockSlot(state);
    }

    [Fact]
    public void ResolveLock_ThrowsOnUndefined()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => ConsoleStatusText.ResolveLockLabel((LockState)999));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => ConsoleStatusText.ResolveLockSlot((LockState)999));
    }

    [Theory]
    [MemberData(nameof(CleanupStatuses))]
    public void ResolveCleanup_IsTotal(CleanupStatus status)
    {
        ConsoleStatusText.ResolveCleanupLabel(status).ShouldNotBeNullOrWhiteSpace();
        _ = ConsoleStatusText.ResolveCleanupSlot(status);
    }

    [Fact]
    public void ResolveCleanup_ThrowsOnUndefined()
        => Should.Throw<ArgumentOutOfRangeException>(() => ConsoleStatusText.ResolveCleanupLabel((CleanupStatus)999));

    [Theory]
    [MemberData(nameof(FileKinds))]
    public void ResolveFileKind_IsTotal(FileMetadataItemKind kind)
        => ConsoleStatusText.ResolveFileKindLabel(kind).ShouldNotBeNullOrWhiteSpace();

    [Fact]
    public void ResolveFileKind_ThrowsOnUndefined()
        => Should.Throw<ArgumentOutOfRangeException>(() => ConsoleStatusText.ResolveFileKindLabel((FileMetadataItemKind)999));

    [Theory]
    [MemberData(nameof(FileRedactions))]
    public void ResolveFileAccess_IsTotal(FileMetadataItemRedaction redaction)
    {
        ConsoleStatusText.ResolveFileAccessLabel(redaction).ShouldNotBeNullOrWhiteSpace();
        _ = ConsoleStatusText.ResolveFileAccessSlot(redaction);
    }

    [Fact]
    public void ResolveFileAccess_ThrowsOnUndefined()
        => Should.Throw<ArgumentOutOfRangeException>(() => ConsoleStatusText.ResolveFileAccessLabel((FileMetadataItemRedaction)999));

    [Theory]
    [MemberData(nameof(CommitClassifications))]
    public void ResolveCommit_IsTotal(CommitEvidenceCommitReferenceClassification classification)
    {
        _ = ConsoleStatusText.ResolveCommitDisclosure(classification);
        ConsoleStatusText.ResolveCommitReferenceText(classification).ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ResolveCommit_ThrowsOnUndefined()
        => Should.Throw<ArgumentOutOfRangeException>(
            () => ConsoleStatusText.ResolveCommitDisclosure((CommitEvidenceCommitReferenceClassification)999));

    [Theory]
    [MemberData(nameof(ErrorCategories))]
    public void ResolveErrorReasonToken_IsTotal(CanonicalErrorCategory category)
    {
        // Reflection-backed, total by construction: every declared category yields its snake_case wire token.
        ConsoleStatusText.ResolveErrorReasonToken(category).ShouldNotBeNullOrWhiteSpace();
        ConsoleStatusText.ResolveErrorExplanation(ConsoleStatusText.ResolveErrorReasonToken(category)).ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ResolveErrorExplanation_FallsBackSafely_ForUnmappedToken()
        => ConsoleStatusText.ResolveErrorExplanation("some_future_category").ShouldBe(ConsoleStatusText.DefaultErrorExplanation);

    [Theory]
    [InlineData(0L, "empty")]
    [InlineData(512L, "≤ 1 KiB")]
    [InlineData(2048L, "≤ 1 MiB")]
    public void ResolveSizeClass_BucketsBySize(long bytes, string expected)
        => ConsoleStatusText.ResolveSizeClass(bytes).ShouldBe(expected);

    [Fact]
    public void ResolveSizeClass_Directory_IsNotApplicable()
        => ConsoleStatusText.ResolveSizeClass(null).ShouldBe("—");
}
