using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Authorization;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Qualifies the production head adapter's native evidence, exact own-actor scopes and existing deadline.</summary>
public sealed class NativeSourceHeadReaderTests
{
    /// <summary>Only valid exact-source proof yields a head; missing, erased and foreign evidence never becomes zero.</summary>
    [Theory]
    [InlineData("zero")]
    [InlineData("positive")]
    [InlineData("missing")]
    [InlineData("foreign")]
    [InlineData("erased")]
    [InlineData("malformed")]
    public async Task ExactNativeObservationPreservesZeroAndPositiveHeadsAndRejectsUnsafeEvidence(string scenario)
    {
        var identity = new AggregateIdentity("tenant-a", "folders", "organization-a");
        NativeSourceProof proof = NativeSourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        if (scenario == "positive") proof = NativeSourceProofCodec.ExtendDigest(proof, identity, 1, "original-message", new string('a', 64));
        proof = scenario switch
        {
            "foreign" => proof with { AggregateId = "other-aggregate" },
            "erased" => proof with { Erased = true },
            "malformed" => proof with { CumulativeDigest = "invalid" },
            _ => proof,
        };
        var source = SourceHeadBoundaryProxy.Create<INativeSourceMetadataReader>((_, args) =>
        {
            args![0].ShouldBe(identity);
            return Task.FromResult(scenario == "missing" ? null : new NativeSourceObservation(proof, DateTimeOffset.UtcNow));
        });
        var reader = new DaprOrganizationStreamSequenceReader(source);

        if (scenario is "zero" or "positive")
            (await reader.GetCurrentSequenceAsync(identity.TenantId, identity.AggregateId, TestContext.Current.CancellationToken))
                .ShouldBe(scenario == "zero" ? 0 : 1);
        else await Should.ThrowAsync<InvalidOperationException>(() => reader.GetCurrentSequenceAsync(
            identity.TenantId, identity.AggregateId, TestContext.Current.CancellationToken));
        ((SourceHeadBoundaryProxy)(object)source).Calls.ShouldBe(1);
    }

    /// <summary>The retained own-actor shortcut avoids self-calls only for both exact identity segments.</summary>
    [Theory]
    [InlineData(true, "exact")]
    [InlineData(false, "exact")]
    [InlineData(true, "tenant")]
    [InlineData(false, "tenant")]
    [InlineData(true, "aggregate")]
    [InlineData(false, "aggregate")]
    public async Task ExactOwnActorScopesNeverStandInForAnotherSource(bool organization, string scope)
    {
        var source = SourceHeadBoundaryProxy.Create<INativeSourceMetadataReader>((_, _) => Task.FromResult<NativeSourceObservation?>(null));
        using IDisposable entered = organization ? OrganizationActorSequenceScope.Enter("tenant-a", "source-a", 7)
            : FolderActorSequenceScope.Enter("tenant-a", "source-a", 7);
        var reader = new DaprOrganizationStreamSequenceReader(source);
        string tenant = scope == "tenant" ? "tenant-b" : "tenant-a";
        string aggregate = scope == "aggregate" ? "source-b" : "source-a";

        if (scope == "exact")
            (await reader.GetCurrentSequenceAsync(tenant, aggregate, TestContext.Current.CancellationToken)).ShouldBe(7);
        else await Should.ThrowAsync<InvalidOperationException>(() => reader.GetCurrentSequenceAsync(tenant, aggregate, TestContext.Current.CancellationToken));
        ((SourceHeadBoundaryProxy)(object)source).Calls.ShouldBe(scope == "exact" ? 0 : 1);
    }

    /// <summary>Caller cancellation takes precedence and prevents independent observation.</summary>
    [Fact]
    public async Task CanceledCallerDoesNotObserveSourceEvenInsideOwnActorScope()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var source = SourceHeadBoundaryProxy.Create<INativeSourceMetadataReader>((_, _) => throw new InvalidOperationException("No read permitted."));
        using IDisposable scope = OrganizationActorSequenceScope.Enter("tenant-a", "source-a", 7);
        var reader = new DaprOrganizationStreamSequenceReader(source);

        await Should.ThrowAsync<OperationCanceledException>(() => reader.GetCurrentSequenceAsync("tenant-a", "source-a", canceled.Token));

        ((SourceHeadBoundaryProxy)(object)source).Calls.ShouldBe(0);
    }

    /// <summary>A noncooperative dependency cannot exceed the existing three-second source-reader ceiling.</summary>
    [Fact]
    public async Task NoncooperativeSourceCannotOutliveExistingThreeSecondCeiling()
    {
        var pending = new TaskCompletionSource<NativeSourceObservation?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken sourceToken = default;
        var source = SourceHeadBoundaryProxy.Create<INativeSourceMetadataReader>((_, args) =>
        {
            sourceToken = (CancellationToken)args![1]!;
            return pending.Task;
        });
        var reader = new DaprOrganizationStreamSequenceReader(source);
        try
        {
            await Should.ThrowAsync<TimeoutException>(() => reader.GetCurrentSequenceAsync("tenant-a", "source-a",
                TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            sourceToken.IsCancellationRequested.ShouldBeTrue();
        }
        finally { pending.TrySetResult(null); }
    }
}
