using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Resumes one unchanged SDK action until durable completion or a bounded failed lane.</summary>
internal static class FolderDurabilitySharedRebuild
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static async Task<DomainSharedProjectionRebuildResponse> SendAsync(HttpClient client,
        HttpRequestMessage original, DomainSharedProjectionRebuildAction action, CancellationToken cancellationToken,
        TimeSpan? retryBudget = null)
    {
        DomainSharedProjectionRebuildResponse? last = null;
        try
        {
            using HttpResponseMessage response = await FolderDurabilityHttp.SendAsync(client, original, true,
                cancellationToken, async (reply, token) =>
                {
                    // Buffer the response independently of the unchanged request retained by the HTTP helper.
                    byte[] bytes = await reply.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
                    last = JsonSerializer.Deserialize<DomainSharedProjectionRebuildResponse>(bytes, Json)
                        ?? throw new InvalidOperationException("SDK shared rebuild returned no outcome.");
                    if (last.Version != DomainSharedProjectionRebuildProtocol.Version
                        || !Enum.IsDefined(last.Status) || !Enum.IsDefined(last.Phase))
                        throw new InvalidOperationException("SDK shared rebuild returned a malformed outcome.");
                    bool resume = last.Status == ProjectionDispatchStatus.Retryable
                        || last.Status == ProjectionDispatchStatus.Indeterminate
                            && last.ReasonCode == ProjectionDispatchReasonCodes.HandlerFailure;
                    if (resume)
                        Console.WriteLine($"SDK shared {action} unresolved: {last.Status}/{last.ReasonCode}; resuming the same action.");
                    return resume;
                }, retryBudget).ConfigureAwait(false);
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(response, HttpStatusCode.OK,
                "SDK shared " + action).ConfigureAwait(false);
            return last ?? throw new InvalidOperationException("SDK shared rebuild returned no outcome.");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"SDK shared {action} did not complete within {retryBudget ?? TimeSpan.FromSeconds(75)}; "
                + $"last status={last?.Status.ToString() ?? "no_response"}, reason={last?.ReasonCode ?? "none"}.", exception);
        }
    }
}
