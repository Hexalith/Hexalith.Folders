using System.Net;
using System.Net.Http.Json;
using Dapr.Client;
using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.Folders.Server.Provisioning;

/// <summary>Reads the admitted original actor via the authenticated G-1 v1 wire contract.</summary>
internal sealed class DurableTaskStepLookupClient(
    DaprClient dapr,
    IHttpClientFactory clients,
    IWorkloadAssertionIssuer issuer,
    IConfiguration configuration)
{
    internal const string Section = "Folders:Provisioning:";

    internal async Task<CommittedProvisioningStep?> LookupAsync(
        string tenantId, string folderId, FolderProvisioningRequest request, CancellationToken token)
    {
        string application = configuration[Section + "DurableTaskAppId"]
            ?? throw new InvalidOperationException("The Durable Task lookup application is unavailable.");
        const string domain = FoldersServerModule.DomainName;
        string? assertion = await issuer.IssueAsync(new WorkloadAssertionRequest(
            application, "eventstore:durable-task-step-lookup", Bindings: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = tenantId,
                [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = domain,
            }), token).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(assertion))
        {
            throw new InvalidOperationException("The Durable Task lookup assertion is unavailable.");
        }

        using HttpRequestMessage message = dapr.CreateInvokeMethodRequest(
            application, "internal/durable-tasks/v1/steps/lookup", new
            {
                request.TaskId,
                request.StepKey,
                request.AdmissionOperation,
                request.RequestSha256,
                TargetDomain = domain,
                TargetId = folderId,
                request.Fence,
            });
        message.Headers.TryAddWithoutValidation(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
        using HttpClient client = clients.CreateClient(nameof(DurableTaskStepLookupClient));
        using HttpResponseMessage response = await client.SendAsync(message, token).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CommittedProvisioningStep>(token).ConfigureAwait(false);
        if (result is null || result.TenantId != tenantId || result.TaskId != request.TaskId
            || result.StepKey != request.StepKey || result.AdmissionOperation != request.AdmissionOperation
            || result.RequestSha256 != request.RequestSha256 || string.IsNullOrWhiteSpace(result.OriginalActorId)
            || result.AcceptsExecution && result.Receipt is not null)
        {
            throw new InvalidOperationException("The Durable Task lookup response is unavailable or malformed.");
        }

        return result;
    }
}
