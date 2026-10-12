using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server.Authorization;

/// <summary>Marks a bounded authorization denial for the trusted EventStore invocation.</summary>
internal sealed class AuthorizationDenialTransportResult(IResult result) : IResult
{
    internal const string HeaderName = "X-Hexalith-Authorization-Denial";

    internal IResult InnerResult => result;

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        httpContext.Response.Headers[HeaderName] = "v1";
        return result.ExecuteAsync(httpContext);
    }
}
