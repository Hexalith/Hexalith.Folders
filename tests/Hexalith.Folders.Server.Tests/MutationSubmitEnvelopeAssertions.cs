using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Contracts.Commands;

using Shouldly;

namespace Hexalith.Folders.Server.Tests;

internal static class MutationSubmitEnvelopeAssertions
{
    public static void ShouldBeKeyedUlidEnvelope(this SubmitCommandRequest submitted, string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(submitted);
        submitted.IdempotencyKey.ShouldBe(idempotencyKey);
        submitted.MessageId.ShouldNotBe(idempotencyKey);
        submitted.MessageId.Length.ShouldBe(26);
        _ = UniqueIdHelper.ExtractTimestamp(submitted.MessageId);
    }
}
