using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.Folders.Server.Provisioning;

/// <summary>Distinguishes a missing stream from a native proof outage through the authenticated zero-window read.</summary>
internal sealed class FolderProvisioningStreamPresence(
    DaprClient dapr,
    IHttpClientFactory clients,
    IWorkloadAssertionIssuer issuer,
    IConfiguration configuration,
    TimeProvider clock)
{
    internal async Task<bool> IsMissingAsync(AggregateIdentity identity, CancellationToken token)
    {
        string application = configuration["EventStore:Gateway:AppId"] ?? "eventstore";
        string? workload = configuration["Authentication:WorkloadIssuer:Workload"];
        string? key = configuration["Authentication:BoundStreamRead:PrivateKey"];
        if (string.IsNullOrWhiteSpace(workload) || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        string? assertion = await issuer.IssueAsync(new WorkloadAssertionRequest(
            EventStoreTrustedEffectSubmissionExtensions.DefaultGatewayAudience, EventStoreWorkloadOperations.GatewayStreamRead,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = identity.TenantId,
                [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = identity.Domain,
            }), token).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(assertion))
        {
            return false;
        }

        var request = new StreamReadRequest(identity.TenantId, identity.Domain, identity.AggregateId, ToSequence: 0, PageSize: 1);
        using HttpRequestMessage message = dapr.CreateInvokeMethodRequest(application, "api/v1/streams/read/workload", request);
        message.Headers.TryAddWithoutValidation(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
        message.Headers.TryAddWithoutValidation(BoundStreamReadProof.HeaderName, BoundStreamReadProof.Issue(request, workload, key, clock.GetUtcNow()));
        using HttpClient client = clients.CreateClient(nameof(FolderProvisioningStreamPresence));
        using HttpResponseMessage response = await client.SendAsync(message, token).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            return false;
        }

        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>(token).ConfigureAwait(false);
        return IsMissingProblem(problem);
    }

    internal static bool IsMissingProblem(JsonElement problem)
        => problem.ValueKind == JsonValueKind.Object && problem.TryGetProperty("reasonCode", out JsonElement reason)
            && reason.ValueKind == JsonValueKind.String && reason.GetString() == StreamReplayReasonCodes.MissingStream;
}
