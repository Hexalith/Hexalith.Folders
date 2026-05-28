using Hexalith.Folders.UI;
using Hexalith.Folders.UI.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Story 3-1 ADR-030 — ValidateScopes catches Singleton-captures of services FrontComposer expects
// to be Scoped (the storage / lifecycle services in the shell) at boot instead of leaking circuits.
builder.Host.UseDefaultServiceProvider(o => o.ValidateScopes = true);

CompositionRoot.ConfigureServices(builder.Services, builder.Configuration, builder.Environment);

WebApplication app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.UseStaticFiles();
app.UseRequestLocalization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

namespace Hexalith.Folders.UI
{
    // Visible to the bUnit / Aspire.Hosting.Testing fixtures via InternalsVisibleTo.
    public sealed partial class Program;
}
