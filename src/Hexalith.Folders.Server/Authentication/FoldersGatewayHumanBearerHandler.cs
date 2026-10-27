using System.Net.Http.Headers;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Commands;

using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server.Authentication;

/// <summary>Relays the validated public human credential to the gateway that issues its command receipt.</summary>
internal sealed class FoldersGatewayHumanBearerHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpContext? context = accessor.HttpContext;
        if (context?.User.Identity?.IsAuthenticated != true
            || !(HistoricalMutationAuthorization.IsDirectPublicMutation(context)
                || context.Items.ContainsKey(Content.WorkspaceContentAuthority.ActionItem)
                    && (request.RequestUri?.OriginalString.Contains("streams/read", StringComparison.Ordinal) == true
                        || request.RequestUri?.OriginalString.Contains("commands/status/", StringComparison.Ordinal) == true)
                || context.Items.ContainsKey("Folders:ContentRetentionAuthorized")
                || await IsAuthorizedNativeSubmissionAsync(context, request, cancellationToken).ConfigureAwait(false))
            || !AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out AuthenticationHeaderValue? bearer)
            || !string.Equals(bearer.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(bearer.Parameter))
        {
            throw new InvalidOperationException("A validated public human credential is required for gateway submission.");
        }

        request.Headers.Authorization = bearer;
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
    private static async Task<bool> IsAuthorizedNativeSubmissionAsync(HttpContext context, HttpRequestMessage request, CancellationToken token)
    {
        if (request.Method != HttpMethod.Post || request.RequestUri?.AbsolutePath.EndsWith("/api/v1/commands", StringComparison.Ordinal) != true
            || context.Items[Content.WorkspaceNativeEndpoints.AuthorizedSubmissionItem] is not SubmitCommandRequest admitted
            || admitted.CommandType is not (FoldersServerModule.NativeTaskCommandType or FoldersServerModule.MutateFilesCommandType)
            || request.Content is null) return false;
        try
        {
            var sent = JsonSerializer.Deserialize<SubmitCommandRequest>(await request.Content.ReadAsStringAsync(token).ConfigureAwait(false), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return sent is not null && sent.MessageId == admitted.MessageId && sent.Tenant == admitted.Tenant
                && sent.Domain == admitted.Domain && sent.AggregateId == admitted.AggregateId && sent.CommandType == admitted.CommandType
                && sent.CorrelationId == admitted.CorrelationId && sent.IdempotencyKey == admitted.IdempotencyKey
                && sent.Extensions?.GetValueOrDefault("taskId") == admitted.Extensions?.GetValueOrDefault("taskId")
                && JsonElement.DeepEquals(sent.Payload, admitted.Payload);
        }
        catch (JsonException) { return false; }
    }
}
