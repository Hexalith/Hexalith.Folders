using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Folders.ServiceDefaults;

public static class Extensions
{
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder) => builder;

    public static IEndpointRouteBuilder MapDefaultEndpoints(this IEndpointRouteBuilder endpoints) => endpoints;
}
