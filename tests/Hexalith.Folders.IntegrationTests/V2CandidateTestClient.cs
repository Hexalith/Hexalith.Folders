using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace Hexalith.Folders.IntegrationTests;

/// <summary>
/// Creates a test client for the opt-in PD10 v2 candidate compatibility seam.
/// </summary>
internal static class V2CandidateTestClient
{
    public static HttpClient Create(WebApplication app) => new(app.GetTestServer().CreateHandler())
    {
        BaseAddress = new Uri("http://localhost"),
    };
}
