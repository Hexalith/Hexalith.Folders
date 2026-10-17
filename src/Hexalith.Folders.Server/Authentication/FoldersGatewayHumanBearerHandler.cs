using System.Net.Http.Headers;

using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server.Authentication;

/// <summary>Relays the validated public human credential to the gateway that issues its command receipt.</summary>
internal sealed class FoldersGatewayHumanBearerHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpContext? context = accessor.HttpContext;
        if (context?.User.Identity?.IsAuthenticated != true
            || !HistoricalMutationAuthorization.IsDirectPublicMutation(context)
            || !AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out AuthenticationHeaderValue? bearer)
            || !string.Equals(bearer.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(bearer.Parameter))
        {
            throw new InvalidOperationException("A validated public human credential is required for gateway submission.");
        }

        request.Headers.Authorization = bearer;
        return base.SendAsync(request, cancellationToken);
    }
}
