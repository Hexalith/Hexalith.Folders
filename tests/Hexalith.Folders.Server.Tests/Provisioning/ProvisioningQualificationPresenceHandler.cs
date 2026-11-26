using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.IdentityModel.JsonWebTokens;

using Shouldly;

namespace Hexalith.Folders.Server.Tests.Provisioning;

/// <summary>Exposes the fixture's direct actor metadata observation over the bound presence wire shape.</summary>
internal sealed class ProvisioningQualificationPresenceHandler(
    ProvisioningQualificationActorHost host, string publicKey, TimeProvider clock) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        message.RequestUri!.AbsolutePath.ShouldBe($"/v1.0/invoke/{ProvisioningQualificationActorHost.ApplicationId}/method/api/v1/streams/read/workload");
        var assertion = new JsonWebToken(message.Headers.GetValues(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName).Single());
        assertion.Audiences.ShouldBe([EventStoreTrustedEffectSubmissionExtensions.DefaultGatewayAudience]);
        StreamReadRequest request = (await message.Content!.ReadFromJsonAsync<StreamReadRequest>(cancellationToken)
            .ConfigureAwait(true)).ShouldNotBeNull();
        request.Tenant.ShouldBe(ProvisioningQualificationActorHost.Tenant);
        request.Domain.ShouldBe(ProvisioningQualificationActorHost.Domain);
        request.FromSequence.ShouldBe(0);
        request.ToSequence.ShouldBe(0);
        request.PageSize.ShouldBe(1);
        string proof = message.Headers.GetValues(BoundStreamReadProof.HeaderName).Single();
        BoundStreamReadProof.Verify(proof, request, ProvisioningQualificationActorHost.ApplicationId,
            publicKey, clock.GetUtcNow()).ShouldBeTrue();

        var metadata = await host.Actor(request.Tenant, request.AggregateId.ShouldNotBeNull())
            .GetStreamMetadataAsync().WaitAsync(cancellationToken).ConfigureAwait(true);
        return metadata.Exists
            ? new(HttpStatusCode.OK)
            : new(HttpStatusCode.NotFound)
            {
                Content = JsonContent.Create(new { reasonCode = StreamReplayReasonCodes.MissingStream }),
            };
    }
}
