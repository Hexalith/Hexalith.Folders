namespace Hexalith.Folders.Server.Tests;

/// <summary>Fails the reentrant actor call that would deadlock a production domain invocation.</summary>
internal sealed class LockedActorSequenceHandler : HttpMessageHandler
{
    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => throw new TimeoutException("The invoking actor is locked for this domain-service request.");
}
