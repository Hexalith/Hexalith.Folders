using System.Collections.Generic;

using Bunit;

using Hexalith.Folders.Client.Generated;
using Hexalith.FrontComposer.Contracts.Rendering;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NSubstitute;

using Newtonsoft.Json;

namespace Hexalith.Folders.UI.Tests;

/// <summary>
/// Story 6.6 — bUnit context for the SDK-calling diagnostic pages/components. Builds on
/// <see cref="BadgeRenderingFixture"/> and registers an NSubstitute <see cref="IClient"/> plus an
/// <see cref="IUserContextAccessor"/> returning a known tenant/user (the <c>ShellCompositionTests</c>
/// pattern), so tenant provenance comes from the authenticated context, not the route.
/// </summary>
internal static class DiagnosticTestContext
{
    private static readonly IReadOnlyDictionary<string, IEnumerable<string>> EmptyHeaders =
        new Dictionary<string, IEnumerable<string>>();

    public static (BunitContext Ctx, IClient Client, IUserContextAccessor UserContext) Create(
        string? tenantId = "tenant-a",
        string? userId = "user-a")
    {
        BunitContext ctx = BadgeRenderingFixture.Create();

        IClient client = Substitute.For<IClient>();
        ctx.Services.Replace(ServiceDescriptor.Scoped(_ => client));

        IUserContextAccessor accessor = Substitute.For<IUserContextAccessor>();
        accessor.TenantId.Returns(tenantId);
        accessor.UserId.Returns(userId);
        ctx.Services.Replace(ServiceDescriptor.Scoped(_ => accessor));

        return (ctx, client, accessor);
    }

    public static HexalithFoldersApiException<SafeDenialProblem> SafeDenialException(string correlationId)
    {
        string body = JsonConvert.SerializeObject(new
        {
            type = "about:blank",
            title = "Resource not available",
            status = 404,
            category = "tenant_access_denied",
            code = "resource_unavailable",
            message = "The requested resource is unavailable.",
            correlationId,
            retryable = false,
            clientAction = "no_action",
            details = new { visibility = "redacted" },
        });
        SafeDenialProblem problem = JsonConvert.DeserializeObject<SafeDenialProblem>(body)
            ?? throw new JsonSerializationException("The canonical safe-denial fixture did not deserialize.");
        return new HexalithFoldersApiException<SafeDenialProblem>(
            "denied", 404, body, EmptyHeaders, problem, innerException: null);
    }
}
