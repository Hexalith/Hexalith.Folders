using Hexalith.Folders.Client.Idempotency;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Client.Tests;

public sealed class IdempotencyHasherCancellationTests
{
    [Fact]
    public void CancelledHashDoesNotEnumerateFields()
    {
        bool enumerated = false;
        OperationCanceledException exception = Should.Throw<OperationCanceledException>(
            () => HexalithIdempotencyHasher.Compute("Mutation", Fields(), new CancellationToken(canceled: true)));
        exception.CancellationToken.IsCancellationRequested.ShouldBeTrue();
        enumerated.ShouldBeFalse();

        IEnumerable<IdempotencyField> Fields()
        {
            enumerated = true;
            yield return new IdempotencyField("path", true, "value");
        }
    }

    [Fact]
    public void CancellationStopsAndDisposesTheFieldEnumerator()
    {
        using CancellationTokenSource cancellation = new();
        bool disposed = false;
        OperationCanceledException exception = Should.Throw<OperationCanceledException>(
            () => HexalithIdempotencyHasher.Compute("Mutation", Fields(), cancellation.Token));
        exception.CancellationToken.ShouldBe(cancellation.Token);
        disposed.ShouldBeTrue();

        IEnumerable<IdempotencyField> Fields()
        {
            try
            {
                cancellation.Cancel();
                yield return new IdempotencyField("path", true, "value");
                throw new InvalidOperationException("Enumeration continued after cancellation.");
            }
            finally
            {
                disposed = true;
            }
        }
    }

    [Fact]
    public void ActiveCancellationTokenPreservesTheHash()
    {
        using CancellationTokenSource cancellation = new();
        IdempotencyField[] fields = [new("path", true, "value")];
        Func<string, IEnumerable<IdempotencyField>, string> originalOverload = HexalithIdempotencyHasher.Compute;

        HexalithIdempotencyHasher.Compute("Mutation", fields, cancellation.Token)
            .ShouldBe(originalOverload("Mutation", fields));
    }
}
