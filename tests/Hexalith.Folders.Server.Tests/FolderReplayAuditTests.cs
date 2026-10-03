using Hexalith.Folders.Observability;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FolderReplayAuditTests
{
    [Theory]
    [InlineData(true, FolderAuditResult.Success)]
    [InlineData(false, FolderAuditResult.Replayed)]
    public async Task ReplayTelemetryShouldLeaveExistingAuditContentsIntact(bool replay, FolderAuditResult result)
    {
        InMemoryFolderAuditObserver observer = new();
        FolderTelemetryEmitter emitter = new([observer], NullLogger<FolderTelemetryEmitter>.Instance);
        FolderAuditObservation first = new FolderAuditObservationBuilder
        {
            OperationKind = FolderAuditOperationKind.ProcessCommand,
            Result = FolderAuditResult.Success,
            TenantId = "tenant-a",
            CorrelationId = "correlation-a",
            SanitizedCategory = "accepted",
        }.Build();
        await emitter.EmitAsync(first, TestContext.Current.CancellationToken).ConfigureAwait(true);

        await emitter.EmitAsync(first with { IsIdempotentReplay = replay, Result = result },
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        observer.Observations.ShouldHaveSingleItem().ShouldBe(first);
    }
}
