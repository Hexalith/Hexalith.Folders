---
baseline_commit: 4d6efbd17980e9af4771efe6f305f0e4f3223420
---

# Story 6.2: Scaffold FrontComposer-hosted read-only operations console

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator,
I want a read-only Blazor Web App console hosted by `Hexalith.Folders.UI` and rendered through `FrontComposerShell`,
so that I can diagnose workspace state through a governed, tenant-aware UI.

## Acceptance Criteria

Source epic AC (epics.md#Story-6.2):

> **Given** projection query endpoints exist
> **When** the console shell is implemented
> **Then** `Hexalith.Folders.UI` is a Blazor Web App host using Interactive Server rendering, `FrontComposerShell` as the primary layout, Fluent UI through the FrontComposer/Shell pattern, OIDC auth, SDK or read-only query-service projection access, and no direct aggregate write paths
> **And** a real Folders/Tenants `IUserContextAccessor` replaces the fail-closed FrontComposer default before tenant-scoped queries are enabled
> **And** navigation supports tenant and folder diagnostic workflows
> **And** no FrontComposer mutation command forms, file browsing, file editing, raw diff display, repair actions, credential reveal, or unrestricted filesystem browsing are exposed in MVP.

Decomposed, testable acceptance criteria:

1. **`Hexalith.Folders.UI` is converted from "Hello-World scaffold" to a Blazor Web App + Interactive Server host that mounts `FrontComposerShell` as its primary layout.**
   - `src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj` keeps `Microsoft.NET.Sdk.Web`, the `EnableContainer=true` / `ContainerRepository=folders-ui` pair, and the existing `<ProjectReference Include="..\Hexalith.Folders.Client\Hexalith.Folders.Client.csproj" />`. Add **only**:
     - `<ProjectReference Include="..\..\Hexalith.FrontComposer\src\Hexalith.FrontComposer.Shell\Hexalith.FrontComposer.Shell.csproj" />` (FrontComposer is a root-level submodule already initialized at `/mnt/d/Hexalith.Folders/Hexalith.FrontComposer/` per `CLAUDE.md` — do **not** add a NuGet `PackageReference` for it).
     - `<ProjectReference Include="..\Hexalith.Folders.Server\Hexalith.Folders.Server.csproj" />` is **NOT** allowed (UI must not see server-only types per architecture line 1329 "`Hexalith.Folders.UI` is the read-only ops console. References Client only."). Tenant-context types that the UI needs are duplicated in the UI's own `Services/` folder, not project-referenced from Server.
     - **Do not** add `Microsoft.FluentUI.AspNetCore.Components` — `FrontComposerShell` transitively brings it.
   - `src/Hexalith.Folders.UI/Program.cs` is rewritten to compose the shell exactly like `samples/Counter/Counter.Web/Program.cs` in the FrontComposer submodule:
     - `builder.Host.UseDefaultServiceProvider(o => o.ValidateScopes = true);` (catches Singleton-capturing Scoped-service bugs at boot — required because `FrontComposerShell` enforces it via Story 3-1 ADR-030).
     - `builder.Services.AddRazorComponents().AddInteractiveServerComponents();`
     - `builder.Services.AddFluentUIComponents();`
     - `builder.Services.AddHexalithFrontComposerQuickstart(o => o.ScanAssemblies(typeof(Program).Assembly));` — chains `AddLocalization()` + `AddHexalithShellLocalization()` + `AddHexalithFrontComposer()` per the documented one-call quickstart.
     - `builder.Services.AddFrontComposerDevMode(builder.Environment);` (matches Counter sample; gates dev-mode overlays to non-prod environments).
     - **OIDC + claim wiring** per AC #3 (see below) — never `AddAuthentication()` without a registered scheme.
     - `builder.Services.AddFoldersClient(...)` so the SDK is resolvable; configure `FoldersClientOptions.BaseAddress` from `Folders:Client:BaseAddress` (Aspire injects this through service discovery — `WithReference(folders)` in `FoldersAspireModule.cs:109-112` already exposes the `folders` endpoint to the UI sidecar).
     - **Real `IUserContextAccessor` registration** (AC #2) before `app.Build()`: `builder.Services.Replace(new ServiceDescriptor(typeof(IUserContextAccessor), typeof(FoldersUserContextAccessor), ServiceLifetime.Scoped));`. **Do not leave the fail-closed `NullUserContextAccessor`** (Shell's `ServiceCollectionExtensions.cs:236` default) in place — every projection query would no-op silently per FrontComposer Decision D31.
     - `app.MapStaticAssets(); app.UseStaticFiles(); app.UseRequestLocalization(); app.UseAntiforgery();`
     - `app.MapRazorComponents<App>().AddInteractiveServerRenderMode();`
   - Add the three Razor scaffolding files mirroring the Counter sample exactly:
     - `src/Hexalith.Folders.UI/Components/App.razor` — `<!DOCTYPE html>` shell with `<HeadOutlet @rendermode="RenderMode.InteractiveServer" />`, the FluentUI bundle CSS link, the FrontComposer Shell styles CSS link (`_content/Hexalith.FrontComposer.Shell/Hexalith.FrontComposer.Shell.styles.css`), and `<Routes @rendermode="RenderMode.InteractiveServer" />`.
     - `src/Hexalith.Folders.UI/Components/Routes.razor` — `<CascadingAuthenticationState><Router AppAssembly="typeof(Program).Assembly"><Found Context="routeData"><RouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)" /><FocusOnNavigate RouteData="routeData" Selector="h1" /></Found></Router></CascadingAuthenticationState>`.
     - `src/Hexalith.Folders.UI/Components/Layout/MainLayout.razor` — `@inherits LayoutComponentBase` + `<FrontComposerShell>@Body</FrontComposerShell>` (one-line file, identical to the Counter sample's `MainLayout.razor`).
     - `src/Hexalith.Folders.UI/Components/_Imports.razor` — `@using` lines for `Microsoft.AspNetCore.Components.Routing`, `Microsoft.AspNetCore.Components.Web`, `Microsoft.AspNetCore.Components.Authorization`, `Microsoft.FluentUI.AspNetCore.Components`, `Fluxor`, `Fluxor.Blazor.Web`, `Hexalith.FrontComposer.Contracts.Registration`, `Hexalith.FrontComposer.Contracts.Rendering`, `Hexalith.Folders.UI.Components`.
     - `src/Hexalith.Folders.UI/Components/Pages/Home.razor` — `@page "/"` with a `<PageTitle>Hexalith Folders — Operations Console</PageTitle>`, a single `<h1>` element (so `FocusOnNavigate Selector="h1"` resolves), a one-sentence read-only-MVP statement, and a `data-testid="console-page-home-root"` attribute on the page-root container. **No** mutation controls, command forms, search/filter forms that mutate, or file/diff displays.

2. **A Folders/Tenants `IUserContextAccessor` adapter replaces the fail-closed FrontComposer default.** Per FrontComposer Decision D31 (`src/Hexalith.FrontComposer.Contracts/Rendering/IUserContextAccessor.cs`), the default `NullUserContextAccessor` returns `null` / whitespace and **causes every tenant-scoped projection read to no-op** with a single dev-mode diagnostic. The UI MUST replace it. New file `src/Hexalith.Folders.UI/Services/FoldersUserContextAccessor.cs`:
   - Implements `IUserContextAccessor` (return type `string?` for both `TenantId` and `UserId`).
   - Reads the current `AuthenticationState` from `Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider` (cascading from `<CascadingAuthenticationState>` in `Routes.razor`).
   - **TenantId source** = `User.FindFirstValue("tenant_id")` (the Server's `TenantContextOptions.TenantClaimType` default, `src/Hexalith.Folders.Server/Authentication/TenantContextOptions.cs:9`). **Do not** invent a UI-only claim name; the wire claim is the Server's tenant authority.
   - **UserId source** = `User.FindFirstValue(ClaimTypes.NameIdentifier)` (matches the Server's `PrincipalClaimType` default, `TenantContextOptions.cs:11`).
   - Treats null / empty / whitespace as semantically equivalent (per the D31 doc-comment: "MUST treat null, empty, and whitespace as semantically equivalent — do not return `\"   \"` when you mean 'unauthenticated.'"). Implementations use `string.IsNullOrWhiteSpace` and return `null` in the "unauthenticated" case.
   - Is registered as **Scoped** in DI (per FrontComposer's Story 3-1 ADR-030 scope contract: services are scoped per circuit on Blazor Server). Use `services.Replace(new ServiceDescriptor(typeof(IUserContextAccessor), typeof(FoldersUserContextAccessor), ServiceLifetime.Scoped));` — `Replace` is required because `AddHexalithFrontComposer` already registered `NullUserContextAccessor` via `TryAddScoped` and a `TryAddScoped` from this story would silently no-op.
   - **Reads through `AuthenticationStateProvider` only** — not `IHttpContextAccessor`. Interactive Server circuits keep state in the SignalR connection; the `HttpContext` is no longer authoritative once the circuit starts. (The Server's `HttpContextTenantContextAccessor` is fine for `Hexalith.Folders.Server` because every REST call is an HTTP request; the UI is not.)

3. **OIDC authentication is wired, JWT bearer tokens flow to the SDK, and the Aspire `Folders:Authentication:*` environment variables are honored.**
   - `Hexalith.Folders.AppHost/Program.cs:33-37` already wires Keycloak: `Folders__Authentication__Authority`, `Folders__Authentication__ClientId="hexalith-folders"`, with the realm URL bound to `keycloak/realms/hexalith`. The UI must bind these via configuration to:
     - `services.AddAuthentication(...)` with `OpenIdConnectDefaults.AuthenticationScheme` as the challenge scheme and `CookieAuthenticationDefaults.AuthenticationScheme` as the default sign-in scheme (standard ASP.NET Core OIDC pattern; cookie holds the session for the SignalR circuit).
     - `services.AddAuthorization();` (after OIDC; needed for `<CascadingAuthenticationState>` and `<AuthorizeView>`).
   - **Token forwarding to the SDK**: the SDK is registered via `services.AddFoldersClient(...)` and exposes the `IHttpClientBuilder` so callers can chain handlers (`FoldersClientServiceCollectionExtensions.cs:16-17`). Add a `BearerTokenDelegatingHandler` under `src/Hexalith.Folders.UI/Infrastructure/BearerTokenDelegatingHandler.cs` that pulls the `access_token` from the current `HttpContext` via `Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.GetTokenAsync(httpContext, "access_token")` and stamps it onto the outgoing `Authorization: Bearer <token>` header. Wire via `.AddHttpMessageHandler<BearerTokenDelegatingHandler>()`. **Required**: `services.AddTransient<BearerTokenDelegatingHandler>();` + `services.AddHttpContextAccessor();` (for the outbound-request side; the inbound `IUserContextAccessor` does **not** use it per AC #2).
   - **Configuration section**: bind from `Folders:Authentication` so the AppHost env vars (`Folders__Authentication__Authority`, `Folders__Authentication__ClientId`) flow in unchanged. New options record `src/Hexalith.Folders.UI/Configuration/FoldersAuthenticationOptions.cs`: `Authority`, `ClientId`, `Audience` (default `"hexalith-folders"`), `RequireHttpsMetadata` (default `false` for dev; production toggles via config).
   - **Failure mode**: if `Folders:Authentication:Authority` is null/whitespace **and** the host is not `Development`, fail at boot with `InvalidOperationException("Folders:Authentication:Authority is required outside Development; refusing to start with no auth in a non-dev environment.")`. This mirrors the Counter sample's Development-gated fake-auth pattern (`Counter.Web/Program.cs:91-96`) but inverted: missing real auth fails closed instead of silently registering a fake.
   - **No fake-auth registration in the Folders UI.** The Counter sample's `DemoUserContextAccessor` / `CounterFakeAuthUserContextAccessor` are sample-only. Production-aligned hosts (this is one) wire real OIDC; if a dev environment needs a stub, it is added in a separate story or as an opt-in `dev` configuration path. Story 6.2 does **not** ship a `DemoFoldersUserContextAccessor`.

4. **Navigation, app metadata, and tenant/folder workflow scaffolding are wired without exposing mutation paths.**
   - `FrontComposerShell` auto-populates its left navigation from the `IFrontComposerRegistry` and any `IDomainManifest`s registered through `AddHexalithDomain<TMarker>()` (FrontComposer Shell extension at `samples/Counter/Counter.Web/Program.cs:45`). Folders does **not** ship a `FoldersDomain` registration in MVP (no command forms = no manifest), so the shell renders the default `<FrontComposerNavigation />` which gracefully degrades to "no registered domains" — that is intentional for Epic 6 Story 6.2 (Story 6.5 owns the wireflow doc that prescribes the diagnostic-page navigation; Stories 6.6-6.8 add the actual pages and their navigation entries).
   - Add an `<AppTitle>` override on `FrontComposerShell` via `<FrontComposerShell AppTitle="Hexalith.Folders Operations Console">@Body</FrontComposerShell>` in `MainLayout.razor` so the shell header reads "Hexalith.Folders Operations Console" instead of the FrontComposer default. **Do not** mount custom `HeaderEnd` / `HeaderCenter` slots in Story 6.2 — Stories 6.3-6.6 add disposition badges, navigation chips, and tenant-scope banners.
   - Add **two** placeholder pages so the navigation is "discoverable" without mutation:
     - `Components/Pages/Home.razor` — already covered in AC #1.
     - `Components/Pages/Tenants.razor` (`@page "/tenants"`) — empty state "Select a tenant to begin diagnosis. Tenant pickers ship in Story 6.6." with `data-testid="console-page-tenants-root"`.
   - **NEGATIVE SCOPE** — none of these may appear in the rendered DOM:
     - Any `<FluentButton>` / `<FluentInputForm>` / `<FluentDialog>` that dispatches an `ICommandService` mutation.
     - Any `<FrontComposer:CommandPage>` or generated command-form component (these would auto-render if any `[Command]`-attributed type from a referenced assembly were in scope — the UI must not `AddHexalithDomain<T>()` against any domain assembly that carries `[Command]` types).
     - Any file-browsing widget, raw-diff renderer, credential-display, or unrestricted-filesystem affordance.
     - `<FcPaletteTriggerButton />` is OK to keep (shell-provided, read-only navigation aid) — the command palette in a no-domain registration shows an empty state, which is the intended Story 6.2 surface.
   - **Selector contract** (consumed by Story 6.11 + the existing E2E project): every page-root container exposes a kebab-case `data-testid="console-page-{name}-root"` attribute per `tests/Hexalith.Folders.UI.E2E.Tests/README.md` Route and Selector Contract. The two pages in this story land `data-testid="console-page-home-root"` and `data-testid="console-page-tenants-root"`.

5. **`Hexalith.Folders.UI.Tests` bUnit suite verifies the shell wiring, real `IUserContextAccessor` is registered, and forbidden surfaces are absent.** The current placeholder test (`tests/Hexalith.Folders.UI.Tests/UiSmokeTests.cs`) is upgraded — keep it (or rewrite it) into a real suite. Add `<PackageReference Include="bunit" />` to `tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj` (bunit is the standard Blazor component test framework; the SDK assembly is already wired). New test classes under `tests/Hexalith.Folders.UI.Tests/`:
   - `ShellCompositionTests.cs`:
     - `MainLayout_RendersFrontComposerShell` — bUnit renders `MainLayout` with `ChildContent="<p data-testid='inside-shell'>X</p>"` and asserts the rendered DOM contains the `fc-shell-root` class (from `FrontComposerShell.razor:26`) and the `data-testid='inside-shell'` element nested inside.
     - `Home_RendersWithoutMutationControls` — bUnit renders `Pages.Home` and asserts: `<h1>` exists; `data-testid="console-page-home-root"` exists; **zero** `<FluentInputForm>`, `<FluentDialog>`, or `<form>` elements; no element carries a `data-fc-command` or `data-fc-mutation` attribute.
   - `UserContextAccessorRegistrationTests.cs`:
     - `Composition_Registers_Folders_IUserContextAccessor` — builds the same service collection that `Program.cs` builds (extract the wiring into a static `CompositionRoot.ConfigureServices(IServiceCollection, IConfiguration)` so tests can call it), resolves `IUserContextAccessor`, and asserts the runtime type is `FoldersUserContextAccessor` — **NOT** `NullUserContextAccessor`. This guards FrontComposer's `TryAddScoped` from silently winning if the `Replace` call is later dropped.
     - `FoldersUserContextAccessor_ReturnsNullForUnauthenticatedUser` — supplies an `AuthenticationStateProvider` whose `GetAuthenticationStateAsync` returns an empty `ClaimsPrincipal`; asserts both `TenantId` and `UserId` return `null` (not `""`, not `"   "`).
     - `FoldersUserContextAccessor_ReadsTenantIdAndPrincipalIdFromExpectedClaims` — supplies a claims principal with `tenant_id="tenant-a"` and `NameIdentifier="user-a"`; asserts `TenantId == "tenant-a"` and `UserId == "user-a"`.
     - `FoldersUserContextAccessor_TreatsWhitespaceClaimAsUnauthenticated` — supplies `tenant_id="   "` and asserts `TenantId` returns `null` per D31.
   - `NavigationContractTests.cs`:
     - `Tenants_RendersWithoutMutationControls` — same shape as `Home_RendersWithoutMutationControls`, page `Tenants`, expected `data-testid="console-page-tenants-root"`.
     - `Console_DoesNotRegisterAnyDomainCommandManifest` — resolves `IFrontComposerRegistry` from the composition root and asserts its registered manifests collection contains **zero** entries (no `[Command]`-attributed registration sneaked in via an assembly scan).

6. **`Hexalith.Folders.UI.E2E.Tests` placeholder smoke is converted to a real `PlaywrightTest` against the running console.** The project's `README.md` documents the gating contract: "Replace the placeholder smoke test only after **all** of the following are true: ... 1. Story 6-2 has merged a working operations console host into `main`. 2. The console exposes at least one stable route ... 3. ... `data-testid` attributes on the elements being asserted. 4. A host fixture exists that stands up the console deterministically through `Aspire.Hosting.Testing` or equivalent." Story 6.2 ships:
   - Replace `OperationsConsolePlaceholderSmokeTests.PlaceholderConsoleHomePageLoads` with `HomePageLoads_AndExposesConsolePageHomeRoot`: navigates to `/`, waits for `[data-testid="console-page-home-root"]` to be visible, asserts the `<h1>` text. The test uses Playwright's `IBrowserContext.RouteAsync` **before** `IPage.GotoAsync` per the README's "Intercept-before-navigate" rule.
   - Add `tests/Hexalith.Folders.UI.E2E.Tests/Routes/ConsoleRoutes.cs` (the README sentence "Path constants live in a future `Routes/ConsoleRoutes.cs` (do not create until the first real test needs it)" — Story 6.2 is "the first real test needs it"). Exposes `public const string Home = "/";` and `public const string Tenants = "/tenants";`. No other paths until Stories 6.6-6.8 add the diagnostic routes.
   - Add `tests/Hexalith.Folders.UI.E2E.Tests/Fixtures/AspireConsoleHostFixture.cs` using `Aspire.Hosting.Testing.DistributedApplicationTestingBuilder` to stand up the UI deterministically. The fixture **must not** require Keycloak (Aspire's `EnableKeycloak=false` env-var path in `AppHost/Program.cs:7-15` is the supported off-switch for hermetic runs). For Story 6.2 the UI's auth bootstrapping needs a hermetic stub: introduce a **single** `Folders:Authentication:Mode` configuration value with allowed values `"oidc"` (production default) and `"hermetic-test"` (tests only). Under `"hermetic-test"`, the OIDC scheme registration is replaced with a JWT bearer stub that accepts a static token and produces a `tenant_id="tenant-a"` / `NameIdentifier="user-a"` claims principal. **The `"hermetic-test"` branch is rejected at boot unless `ASPNETCORE_ENVIRONMENT in {Development, Test}`** (mirrors the Counter sample's fake-auth Development guard at `Program.cs:91-96`). This is the **only** non-production auth path; do not add fake-auth flags for any other reason.
   - Keep the existing `[Fact(Skip = ...)]` pattern for any test that targets a route Story 6.2 doesn't ship — but the home-page smoke now runs unconditionally because route + selector are stable.

7. **Architecture compliance and negative-scope guards are enforced by tests, not documentation.**
   - **Project-reference direction (architecture line 1329; existing `Hexalith.Folders.Testing.Tests.ProjectReferencesFollowAllowedDependencyDirection`)**: `Hexalith.Folders.UI.csproj` may reference `Hexalith.Folders.Client.csproj` and the FrontComposer submodule's `Hexalith.FrontComposer.Shell.csproj` only. **NOT** `Hexalith.Folders.Server.csproj`, `Hexalith.Folders.csproj` (domain), `Hexalith.Folders.Workers.csproj`, or any provider-adapter project. The existing `ProjectReferencesFollowAllowedDependencyDirection` test will flag a violation; do not loosen it.
   - **Negative-scope assertion**: extend `tests/Hexalith.Folders.UI.Tests/ShellCompositionTests.cs` with a `Composition_DoesNotResolveAnyServerOnlyType` test that asserts the composition-root `IServiceProvider` does **not** contain a registration for `Hexalith.Folders.Server.ITenantContextAccessor`, `IDomainProcessor`, or any aggregate/handler from `src/Hexalith.Folders/Aggregates/*` — those types are server-only and a leaking ProjectReference would surface them.
   - **No PackageReference for FrontComposer / FluentUI in the UI csproj** — `Microsoft.FluentUI.AspNetCore.Components` flows transitively from the Shell ProjectReference per Story 1.1 / 1.2 module-scaffold rules. Adding a direct `<PackageReference>` would risk version drift versus FrontComposer's pin (the existing `Directory.Packages.props:41` `5.0.0-rc.2-26098.1` already governs the *transitive* pin; UI-direct usage is unnecessary).
   - **Submodule policy (CLAUDE.md, root)**: nested submodule initialization is forbidden. The UI's ProjectReference to `Hexalith.FrontComposer.Shell.csproj` works because `Hexalith.FrontComposer` is a root-level submodule (per the `.gitmodules` inventory: `Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants`). **Do not** add any `--init --recursive` command to docs, scripts, README, CI, or `eng/` files. If FrontComposer Shell's own dependencies (e.g. its `Hexalith.Commons` submodule) require initialization for the UI to build, surface that in Dev Notes "Build environment" and address through the root-level inventory only.

8. **Aspire wiring already references the UI; verify the existing reference still composes without `EnableKeycloak=false` failing the boot.**
   - `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs:109-112` already attaches `foldersUi` to the `folders` server with `.WithExternalHttpEndpoints()`. **Do not modify** this file; the Story 6.2 work is on the UI host's `Program.cs`, not the AppHost composition. The existing `FoldersUiAppId = "folders-ui"` constant stays.
   - `src/Hexalith.Folders.AppHost/Program.cs:33-37` already wires Keycloak realm URL + client ID env vars when Keycloak is enabled. Verify the UI **boots** both with `EnableKeycloak` unset (Keycloak on) and `EnableKeycloak=false` (Keycloak off; the `if` block at line 26 skips the env-var injection). In the `Keycloak=false` case the UI must hit the AC #3 fail-closed branch and refuse to start outside `Development`. Add an `AppHostBootSmokeTests.cs` under `tests/Hexalith.Folders.UI.Tests/` (NOT under `tests/Hexalith.Folders.IntegrationTests/` — keep this in the UI lane to avoid cross-suite flakiness) that uses `Aspire.Hosting.Testing` to attempt boot in `Development` with no Keycloak and asserts the UI sidecar reports a healthy `/` response within a 30s timeout.

9. **Build clean and hermetic; no production-tree edits outside `Hexalith.Folders.UI` and its tests; no `.slnx` change; no new package references beyond bunit in the UI test project.**
   - Build with the WSL-accessible Windows SDK (`/mnt/c/Program\ Files/dotnet/dotnet.exe`; the WSL-native SDK fails the `global.json` 10.0.300 pin — see Dev Notes "Build environment"): `dotnet.exe restore Hexalith.Folders.slnx` → `dotnet.exe build Hexalith.Folders.slnx --no-restore` → 0 warnings / 0 errors.
   - Focused tests:
     - `dotnet.exe test tests/Hexalith.Folders.UI.Tests` — every test new in this story green.
     - `dotnet.exe test tests/Hexalith.Folders.UI.E2E.Tests` — the home-page smoke green; placeholder `[Skip]` tests remain skipped.
     - `dotnet.exe test tests/Hexalith.Folders.Testing.Tests --filter "FullyQualifiedName~ProjectReferencesFollowAllowedDependencyDirection"` — already a pre-existing red per Epic 5 retro carry-over list; verify Story 6.2 does **not** add a NEW failure to that test (the test enforces the architecture line 1329 constraint). If the test was previously failing for an unrelated reason, fixing it is OUT of scope; if Story 6.2's ProjectReferences introduce a new failure, the dev MUST fix the ProjectReference, not edit the test.
   - **Regression check**: `dotnet.exe test tests/Hexalith.Folders.Server.Tests` and `dotnet.exe test tests/Hexalith.Folders.{Cli,Mcp,Client,Contracts,Tests}.Tests` — none should change.
   - **Pre-existing reds carried in from Epic 5 / Story 6.1** (per Epic 5 retro Action Items + Story 6.1 carry-over): `ClientGenerationTests.GeneratedClientAndHelpersMatchIsolatedRegeneration` (whitespace-only env noise), `BranchRefPolicyEndpointTests.GetBranchRefPolicyShouldUseSafeDenialEnvelopeForTenantMismatch`, and `Hexalith.Folders.Testing.Tests.ProjectReferencesFollowAllowedDependencyDirection`. Confirm they are the **only** pre-existing reds; do **not** mask new failures behind the carry-over list.
   - **Drift sanity (per Story 5.5/5.6/5.7 pattern)**: temporarily flip one expected value (e.g. remove the `Replace` call so FrontComposer's `NullUserContextAccessor` wins; confirm `Composition_Registers_Folders_IUserContextAccessor` fails with a specific message naming `NullUserContextAccessor` as the resolved type). Revert.

10. **Selector and accessibility minimums for Story 6.11 to consume.** Even though Story 6.11 owns the formal WCAG 2.2 AA verification, Story 6.2 lays the foundation so that future stories cannot introduce regressions invisibly:
    - Every page in this story renders exactly one `<h1>` element (per UX-DR30 semantic-headings expectation; `FocusOnNavigate Selector="h1"` in `Routes.razor` depends on it).
    - Every page-root container exposes `data-testid="console-page-{name}-root"` (AC #4 — repeated here for the accessibility-tooling contract).
    - The shell's `<a class="fc-skip-link" href="#fc-main-content">` is already present in `FrontComposerShell.razor:29` — do **not** add a competing skip link in `App.razor` or `MainLayout.razor`.
    - The `<title>` is set per page via `<PageTitle>` (UX-DR4 — page identity visible before evidence).
    - No element relies on color alone for meaning in this story (no status badges yet; Story 6.3 ships disposition badges with the F-4 mapping).
    - **Do not** add `aria-*` attributes manually on FluentUI components — the FluentUI Blazor library emits the WCAG-compliant attributes automatically; manual aria-overrides are an anti-pattern per UX-DR30.

## Tasks / Subtasks

- [x] **Task 1 — Read the FrontComposer sample (`samples/Counter/Counter.Web`) end-to-end as the canonical wiring template** (AC: #1, #2, #3, #4)
  - [x] Read `Hexalith.FrontComposer/samples/Counter/Counter.Web/Program.cs` line-by-line; note the call order: `UseDefaultServiceProvider(ValidateScopes)` → `AddRazorComponents().AddInteractiveServerComponents()` → `AddFluentUIComponents()` → `AddHexalithFrontComposerQuickstart(...)` → `AddFrontComposerDevMode(...)` → `services.Replace(IUserContextAccessor)`.
  - [x] Read `Components/App.razor`, `Components/Routes.razor`, `Components/Layout/MainLayout.razor`, `Components/_Imports.razor`, `Components/Pages/Home.razor` (5 files). These are the canonical scaffolding; do not reinvent.
  - [x] Read `Counter.Web.csproj` for the ProjectReference shape (note: the SourceTools analyzer is a `OutputItemType="Analyzer"` reference; Story 6.2 does NOT need SourceTools — there are no Razor templates with `[ProjectionTemplate]` markers).
  - [x] Read `FrontComposerShell.razor` (the shell component) and `FrontComposerShell.razor.cs` (the code-behind) so the dev knows what slots exist (`HeaderStart`, `HeaderCenter`, `HeaderEnd`, `Navigation`, `Footer`, `AppTitle`, `ChildContent`) and which are mounted automatically vs. opt-in.

- [x] **Task 2 — Rewrite `src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj`** (AC: #1, #7)
  - [x] Add `<ProjectReference Include="..\..\Hexalith.FrontComposer\src\Hexalith.FrontComposer.Shell\Hexalith.FrontComposer.Shell.csproj" />`.
  - [x] Do not add `Microsoft.FluentUI.AspNetCore.Components`, `Microsoft.AspNetCore.Components.Web`, or any other `PackageReference` — they flow transitively. Confirm by running `dotnet.exe restore` and then `dotnet.exe list package --include-transitive` and matching the FluentUI / FrontComposer entries against the FrontComposer submodule's pin.
  - [x] Do not add a SourceTools analyzer reference. Story 6.2 ships no `[ProjectionTemplate]` markers.

- [x] **Task 3 — Rewrite `src/Hexalith.Folders.UI/Program.cs`** (AC: #1, #3)
  - [x] Replace the current 8-line scaffold with the full host composition (per AC #1 + AC #3). Mirror the Counter sample's order.
  - [x] Extract the service-registration block into a static `CompositionRoot.ConfigureServices(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)` so `Hexalith.Folders.UI.Tests` can call it without going through `WebApplication.CreateBuilder` (otherwise bUnit can't replicate the boot deterministically).
  - [x] Wire OIDC: `services.AddAuthentication(...)`. If `Folders:Authentication:Mode == "hermetic-test"` AND environment ∈ {Development, Test}, register a JWT bearer stub instead (one path; see AC #6). Otherwise register Microsoft.IdentityModel OIDC against `Folders:Authentication:Authority` / `Folders:Authentication:ClientId`.
  - [x] Wire SDK + bearer-forwarding handler: `services.AddFoldersClient(o => o.BaseAddress = new Uri(configuration["Folders:Client:BaseAddress"] ?? throw new InvalidOperationException(...)))`. Chain `.AddHttpMessageHandler<BearerTokenDelegatingHandler>()`.
  - [x] `services.Replace(new ServiceDescriptor(typeof(IUserContextAccessor), typeof(FoldersUserContextAccessor), ServiceLifetime.Scoped));` — Replace, not TryAdd; the FrontComposer default is already there.
  - [x] Fail-closed at boot if `Folders:Authentication:Authority` is missing in non-Development environments (AC #3).

- [x] **Task 4 — Add the Razor scaffolding files** (AC: #1, #4, #10)
  - [x] `src/Hexalith.Folders.UI/Components/App.razor` — top-level HTML skeleton with HeadOutlet + FluentUI bundle CSS + FrontComposer Shell styles + `<Routes>`.
  - [x] `src/Hexalith.Folders.UI/Components/Routes.razor` — `<CascadingAuthenticationState>` wrapping the `<Router>`.
  - [x] `src/Hexalith.Folders.UI/Components/Layout/MainLayout.razor` — one-line `<FrontComposerShell AppTitle="Hexalith.Folders Operations Console">@Body</FrontComposerShell>`.
  - [x] `src/Hexalith.Folders.UI/Components/_Imports.razor` — the `@using` lines per AC #1.
  - [x] `src/Hexalith.Folders.UI/Components/Pages/Home.razor` — `@page "/"` + `<PageTitle>` + single `<h1>` + `data-testid="console-page-home-root"`.
  - [x] `src/Hexalith.Folders.UI/Components/Pages/Tenants.razor` — `@page "/tenants"` + the empty-state copy per AC #4 + `data-testid="console-page-tenants-root"`.

- [x] **Task 5 — Implement `FoldersUserContextAccessor` and its supporting wiring** (AC: #2)
  - [x] `src/Hexalith.Folders.UI/Services/FoldersUserContextAccessor.cs` — implements `IUserContextAccessor` with the AC #2 claim semantics.
  - [x] Constructor takes `AuthenticationStateProvider` (Scoped) — **not** `IHttpContextAccessor`.
  - [x] Use `User.FindFirstValue(...)` + `string.IsNullOrWhiteSpace` per the D31 doc-comment.
  - [x] Register via `services.Replace(new ServiceDescriptor(typeof(IUserContextAccessor), typeof(FoldersUserContextAccessor), ServiceLifetime.Scoped));` in `CompositionRoot.ConfigureServices`.

- [x] **Task 6 — Implement `BearerTokenDelegatingHandler` and the configuration options** (AC: #3)
  - [x] `src/Hexalith.Folders.UI/Infrastructure/BearerTokenDelegatingHandler.cs` — `DelegatingHandler` that resolves `IHttpContextAccessor`, awaits `httpContext.GetTokenAsync("access_token")`, and stamps `Authorization: Bearer <token>` on the outgoing request if a token is present (no-op if absent — the SDK call will fail-closed at the server with a `401`, surfaced as a canonical denial per the server's existing safe-denial pipeline).
  - [x] `src/Hexalith.Folders.UI/Configuration/FoldersAuthenticationOptions.cs` — record/class with `Authority`, `ClientId`, `Audience`, `RequireHttpsMetadata`, `Mode` ("oidc" | "hermetic-test").
  - [x] Bind via `services.Configure<FoldersAuthenticationOptions>(configuration.GetSection("Folders:Authentication"))`.

- [x] **Task 7 — bUnit suite under `tests/Hexalith.Folders.UI.Tests`** (AC: #5, #7)
  - [x] Add `<PackageReference Include="bunit" />` to `Hexalith.Folders.UI.Tests.csproj`. (The `bunit` PackageVersion goes into `Directory.Packages.props` only if it is not already there; do not change the existing version pin if present.)
  - [x] Delete or rewrite `UiSmokeTests.cs` — its current assertion `typeof(Program).Assembly.GetName().Name.ShouldBe("Hexalith.Folders.UI")` is meaningful only against the stub and is invalidated by Story 6.2.
  - [x] `ShellCompositionTests.cs` — `MainLayout_RendersFrontComposerShell` + `Home_RendersWithoutMutationControls` + `Composition_DoesNotResolveAnyServerOnlyType`.
  - [x] `UserContextAccessorRegistrationTests.cs` — 4 tests per AC #5.
  - [x] `NavigationContractTests.cs` — `Tenants_RendersWithoutMutationControls` + `Console_DoesNotRegisterAnyDomainCommandManifest`.
  - [x] `AppHostBootSmokeTests.cs` — Aspire.Hosting.Testing-driven boot smoke per AC #8.

- [x] **Task 8 — E2E suite under `tests/Hexalith.Folders.UI.E2E.Tests`** (AC: #6, #10)
  - [x] Replace `Smoke/OperationsConsolePlaceholderSmokeTests.cs` with `Smoke/ConsoleSmokeTests.cs` carrying `HomePageLoads_AndExposesConsolePageHomeRoot` (running, not skipped).
  - [x] Add `Routes/ConsoleRoutes.cs` with `Home = "/"` and `Tenants = "/tenants"`.
  - [x] Add `Fixtures/AspireConsoleHostFixture.cs` — Aspire.Hosting.Testing host bringing up only the UI sidecar with `Folders:Authentication:Mode=hermetic-test` and `ASPNETCORE_ENVIRONMENT=Test`. Mock the `folders` server endpoint only enough that the UI's bearer-token handler can issue an outbound request without crashing (or null it out — the Story 6.2 smoke only renders Home, no projection calls).
  - [x] Verify the test runs with `dotnet.exe test tests/Hexalith.Folders.UI.E2E.Tests` and that the Playwright browser-install path resolves under WSL.

- [x] **Task 9 — Build, test, drift-bite verification** (AC: #9)
  - [x] Build with the WSL-accessible Windows SDK: `/mnt/c/Program\ Files/dotnet/dotnet.exe restore Hexalith.Folders.slnx` then `dotnet.exe build Hexalith.Folders.slnx --no-restore`. Expect 0 warnings / 0 errors.
  - [x] Focused tests: `dotnet.exe test tests/Hexalith.Folders.UI.Tests` and `dotnet.exe test tests/Hexalith.Folders.UI.E2E.Tests`. Expect every new test green.
  - [x] Full Server.Tests + sibling adapter tests (regression sweep): no behavioral change expected.
  - [x] Drift sanity: temporarily remove the `services.Replace(IUserContextAccessor, ...)` line; confirm `Composition_Registers_Folders_IUserContextAccessor` fails with a specific message naming `NullUserContextAccessor` as the resolved type. Revert. Then temporarily flip the `Folders:Authentication:Mode` failure-closed branch to silently succeed in `Production`; confirm `AppHostBootSmokeTests` fails. Revert.
  - [x] Confirm: no edits under any `Generated/`; no `.slnx` edit; no `Directory.Packages.props` edits beyond a potential `bunit` add; no recursive submodule init commands; no edits to `src/Hexalith.Folders.{Server,Cli,Mcp,Client,Contracts,Workers,AppHost,Aspire}` or `src/Hexalith.Folders/`.

## Dev Notes

### Scope boundaries (read first)

- **In scope:** Convert `Hexalith.Folders.UI` from a 7-line stub into a Blazor Web App + Interactive Server host that mounts `FrontComposerShell` as the primary layout, wires OIDC against the Aspire-injected Keycloak realm, registers a real `FoldersUserContextAccessor` to replace FrontComposer's fail-closed default, exposes two read-only placeholder pages (`/`, `/tenants`) with stable `data-testid` selectors, forwards JWT bearer tokens through the SDK, fails closed at boot in non-Development without real auth, and ships a bUnit + Playwright test pair that proves the wiring. The shell is the foundation; Stories 6.3-6.11 own the disposition badges, redaction component, wireflow notes, diagnostic pages, incident-mode read path, performance budgets, and WCAG 2.2 AA verification.
- **OUT of scope (do NOT implement here):**
  - **Diagnostic pages (Stories 6.6 / 6.7 / 6.8).** The folder / workspace / provider / audit / timeline diagnostic pages all depend on the projection endpoints from Story 6.1 + future Epic-3/4 projection work + the wireflow notes from Story 6.5. Story 6.2 ships only `/` and `/tenants` as empty-state pages so the shell, OIDC, and `IUserContextAccessor` can be verified in isolation.
  - **Operator-disposition badge (Story 6.3).** F-4 mandates disposition-label-primary rendering; the badge component (`Components/OperatorDispositionBadge.razor`) + `DispositionLabelMapper.cs` are Story 6.3 deliverables. Story 6.2 must NOT preemptively author them.
  - **Redaction affordance (Story 6.4).** F-5 mandates a visible lock-icon affordance; the `RedactedField.razor` component is Story 6.4 deliverables.
  - **Console wireflow notes (Story 6.5).** `docs/ux/ops-console-wireflows.md` is Story 6.5 deliverables. Story 6.2 should leave the navigation auto-discovered (i.e. empty FrontComposer manifest collection) so Stories 6.6+ can add navigation entries together with the diagnostic pages.
  - **Incident-mode read path (Story 6.9).** `/_admin/incident-stream` + the F-6 three guardrails are Story 6.9 deliverables.
  - **Performance budgets (Story 6.10).** F-7 perceived-wait UX (skeleton at 400ms, cancel at 2s) is Story 6.10 deliverables.
  - **WCAG 2.2 AA verification (Story 6.11).** Story 6.11 owns the full automated + manual + zoom + responsive verification sweep. Story 6.2 ships only the **foundation** that Story 6.11 will verify — semantic `<h1>`, `<PageTitle>`, no manual `aria-*` overrides, no color-only signals (none in this story regardless), `data-testid` selectors.
  - **EventStore integration via `AddHexalithEventStore`.** Architecture line 152-153 instructs: "Defer `AddHexalithEventStore` until Folders Server implements the compatible command/query/projection-change endpoints, or provide a Folders-specific read-only `IQueryService` adapter backed by `Hexalith.Folders.Client`." Story 6.2 uses the second path (SDK-backed reads via `Hexalith.Folders.Client`). Do **not** call `AddHexalithEventStore` from the UI.
  - **Custom FrontComposer projection models / SourceTools.** Architecture line 148: "Keep FrontComposer SourceTools generated output deterministic and unedited. If UI-only projection annotations would pollute the Contract Spine, place FrontComposer projection DTOs in a UI/domain companion assembly instead of `Hexalith.Folders.Contracts`." Story 6.2 does NOT ship any projection DTOs; Stories 6.6-6.8 will introduce them in a UI/domain companion assembly (NOT in `Hexalith.Folders.Contracts`) if needed.
  - **Custom UI components for Workspace Trust Summary / Tenant Scope Banner / Metadata-Only Folder Tree / Diagnostic Timeline / Trust Matrix / Redaction-Inaccessibility State.** These are UX-DR5 through UX-DR10 components; Stories 6.6-6.8 own them.
  - **MCP / CLI / Server changes.** None — Story 6.2 is UI-only.
- **Negative-scope guard for the dev:** if you find yourself editing `src/Hexalith.Folders.Server`, `src/Hexalith.Folders` (domain), `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`, `tests/fixtures/parity-contract.yaml`, `tests/fixtures/audit-leakage-corpus.json`, `tests/Hexalith.Folders.{Server,Cli,Mcp,Client,Contracts,Workers}.Tests/*`, OR adding a `[ProjectionTemplate]` marker, OR writing a domain-command form — stop. None of those are in Story 6.2's surface area.

### Build environment

- The WSL-native .NET SDK does not satisfy the `global.json` 10.0.300 pin. Use the WSL-accessible Windows SDK at `/mnt/c/Program\ Files/dotnet/dotnet.exe` for restore / build / test. (Memory: `dotnet-windows-sdk-wsl.md`.)
- For settings files / hook paths: WSL paths use `/mnt/d/...`, not `D:\...` (Memory: `wsl-windows-hook-paths.md`). This story does not touch settings/hooks; reference for future infra-adjacent work.
- The FrontComposer submodule is initialized at `Hexalith.FrontComposer/`. Per `CLAUDE.md`, **do not** run `git submodule update --init --recursive`; only root-level submodules are valid. The UI's ProjectReference path `..\..\Hexalith.FrontComposer\src\Hexalith.FrontComposer.Shell\Hexalith.FrontComposer.Shell.csproj` resolves through the existing root-level checkout. If a dev encounters a missing FrontComposer source tree, the fix is `git submodule update --init Hexalith.FrontComposer` (root-level, non-recursive) — not `--recursive`.

### Project-reference direction (architecture line 1329 — non-negotiable)

`Hexalith.Folders.UI` must reference **only**:
- `Hexalith.Folders.Client` (the SDK), already in `Hexalith.Folders.UI.csproj:16`.
- `Hexalith.FrontComposer.Shell` (added in this story).

It must **NOT** reference:
- `Hexalith.Folders.Server` — would expose server-only types like `ITenantContextAccessor` (the Server one, not the FrontComposer one), `IDomainProcessor`, `FoldersDomainServiceRequestHandler`, the authorization stack, the projection adapters, and the layered-auth pipeline. Reading the server's tenant-claim names (AC #2) is done by reading the **claim names**, not by referencing the type. The `tenant_id` claim name is a wire contract; copy it as a string constant `"tenant_id"` in `FoldersUserContextAccessor.cs`.
- `Hexalith.Folders` (the domain) — would expose aggregates, commands, events, projections, and read-models. None of those belong in the UI.
- `Hexalith.Folders.Workers` — process managers and reconcilers.
- Any provider adapter project — GitHub / Forgejo / etc. are domain-side only.

The existing `Hexalith.Folders.Testing.Tests.ProjectReferencesFollowAllowedDependencyDirection` test enforces this. **Do not loosen** the test.

### Why `services.Replace` instead of `services.AddScoped`

FrontComposer's `AddHexalithFrontComposer` (called by `AddHexalithFrontComposerQuickstart`) registers `NullUserContextAccessor` via `services.TryAddScoped<IUserContextAccessor, NullUserContextAccessor>()` (`ServiceCollectionExtensions.cs:236`). A subsequent `services.TryAddScoped<IUserContextAccessor, FoldersUserContextAccessor>()` would silently **lose** because the first registration won (TryAdd is "only if none registered"). `services.AddScoped<...>` would register a second descriptor, and DI resolves the **last-registered**, which would happen to be `FoldersUserContextAccessor` — but the FrontComposer composition could re-register later, swapping it back. The deterministic, drift-resistant pattern is `services.Replace(new ServiceDescriptor(typeof(IUserContextAccessor), typeof(FoldersUserContextAccessor), ServiceLifetime.Scoped))`, which atomically removes the FrontComposer default and installs the Folders one. The Counter sample uses this exact pattern at `Counter.Web/Program.cs:108`.

### Why `AuthenticationStateProvider` and not `IHttpContextAccessor`

`HttpContext` only exists during the initial GET that opens a SignalR circuit. Once the circuit is open, the Blazor Server runtime keeps the auth state in the circuit, not the (long-gone) `HttpContext`. Resolving `IHttpContextAccessor` from a Scoped service inside a circuit returns the saved-at-circuit-start context, which:
- Is potentially stale (claims could have been refreshed by an OIDC silent re-auth that updated the cookie + the circuit's `AuthenticationStateProvider` but not the captured `HttpContext`).
- Throws `InvalidOperationException` if the service is resolved during a Razor render outside the request pipeline (which is most of the circuit's lifetime).

`AuthenticationStateProvider` is the framework-blessed authority in a circuit. The bearer-token forwarder (`BearerTokenDelegatingHandler` in AC #3) is allowed to use `IHttpContextAccessor.GetTokenAsync(...)` because outbound HTTP calls from a Blazor Server circuit run inside a managed `HttpContext` scope; the token is what the auth scheme persisted at sign-in.

### Don't ship fake auth in the Folders UI

The Counter sample's `DemoUserContextAccessor` / `CounterFakeAuthUserContextAccessor` (`Counter.Web/Program.cs:105-108`) are explicitly sample-only and Development-gated (`Program.cs:91-96` throws if requested outside Development). Folders is a production-aligned host. The **only** non-OIDC path Story 6.2 allows is the AC #6 `"hermetic-test"` JWT-bearer stub, which is:
- Strictly Development/Test only (rejected at boot otherwise).
- Used **only** by the E2E test fixture, not by any sample or quickstart.
- Issues a fixed token producing a fixed claims principal — no other test should expand this surface without a follow-up story.

Future stories needing a richer Development experience (e.g. a local-dev tenant picker) MUST author it as a separate, opt-in story, not by quietly broadening the `"hermetic-test"` branch.

### Story 6.1 patterns to inherit

Story 6.1 ("Audit and operation-timeline query endpoints", commit `4d6efbd`) shipped a mature pattern for safe-denial wiring, layered authorization, and metadata-only responses. Story 6.2 does NOT call those endpoints directly (UI calls go through `Hexalith.Folders.Client`), but the patterns to internalize are:
- **Authorization-before-observation.** The UI must let the server enforce authorization; do not pre-filter on the client. Every SDK call carries a bearer token; the server's layered-auth stack (`LayeredFolderAuthorizationService` etc.) is the single authoritative gate. The UI's job is to forward the token and render the response.
- **Safe-denial indistinguishability.** When the SDK returns a `403 folder_acl_denied` or `403 tenant_access_denied` or `404 not_found`, the UI renders them with the **same** copy — no resource-existence hints, no "the folder exists but you can't see it" leakage. Per architecture cross-cutting concern #11, the UI must NOT distinguish hidden / wrong-tenant / missing / redacted / stale / unavailable cases visually. Story 6.2 does not render any of those states (no diagnostic pages yet), but the pattern matters for Stories 6.6-6.8.
- **Redacted vs unknown UX rule.** Story 6.4 ships the formal redaction component, but the architecture says redacted state must be visibly distinct from unknown / missing / unavailable / failed / denied / dirty / locked / ready / committed states (UX-DR10, UX-DR22). Story 6.2 does not render any state; this pattern is internalized for future work.
- **Metadata-only.** No file contents, no raw diffs, no provider tokens, no credential values, ever, in any UI surface. Story 6.2 does not render any of those; the negative-scope tests in AC #5 enforce the absence at compile time.

The Story 6.1 file list at `_bmad-output/implementation-artifacts/6-1-audit-and-operation-timeline-query-endpoints.md:447-528` documents every file added; review it before writing the UI's first projection-query call (Story 6.6) to know which DTOs flow through the generated SDK.

### FrontComposer integration cookbook (read against the Counter sample)

| Need | Pattern | Source |
| --- | --- | --- |
| Bootstrap shell services | `AddHexalithFrontComposerQuickstart(o => o.ScanAssemblies(typeof(Program).Assembly))` | `Counter.Web/Program.cs:34-43` |
| Mount the shell in MainLayout | `<FrontComposerShell>@Body</FrontComposerShell>` | `Counter.Web/Components/Layout/MainLayout.razor:3` |
| Replace the fail-closed user-context accessor | `services.Replace(new ServiceDescriptor(typeof(IUserContextAccessor), typeof(MyAccessor), ServiceLifetime.Scoped))` | `Counter.Web/Program.cs:108` |
| Wire CSS in App.razor | `<link href="_content/Microsoft.FluentUI.AspNetCore.Components/Microsoft.FluentUI.AspNetCore.Components.bundle.scp.css" rel="stylesheet" /> <link href="_content/Hexalith.FrontComposer.Shell/Hexalith.FrontComposer.Shell.styles.css" rel="stylesheet" />` | `Counter.Web/Components/App.razor:7-8` |
| Cascade auth state to routes | `<CascadingAuthenticationState><Router ...>...</Router></CascadingAuthenticationState>` | `Counter.Web/Components/Routes.razor:12-20` |
| Interactive Server render mode | `<HeadOutlet @rendermode="RenderMode.InteractiveServer" />` + `<Routes @rendermode="RenderMode.InteractiveServer" />` + `app.MapRazorComponents<App>().AddInteractiveServerRenderMode();` | `Counter.Web/Components/App.razor:10,13`; `Counter.Web/Program.cs:125-134` |
| Dev-mode overlay (non-prod only) | `services.AddFrontComposerDevMode(builder.Environment)` | `Counter.Web/Program.cs:44` |

**What NOT to copy from the Counter sample:**
- The `AddHexalithDomain<CounterDomain>()` registration. Folders has no `[Command]`-attributed domain to register in the UI (mutation surfaces are server-side only). The shell renders an empty navigation, which is correct for Story 6.2.
- The SourceTools analyzer ProjectReference. No `[ProjectionTemplate]` markers in this story.
- The `AddViewOverride<...>` / `AddSlotOverride<...>` registrations. Story 6.6+ may introduce them; Story 6.2 does not.
- The `DemoUserContextAccessor` and `CounterFakeAuthUserContextAccessor`. Use OIDC + the hermetic-test stub instead.
- The `ScanAssemblies(typeof(FrontComposerTypeSpecimen).Assembly)` specimen scan. Specimens are sample-only.

### Recent commit signals (relevant to Story 6.2)

```
4d6efbd feat(story-6.1): Audit and operation-timeline query endpoints     ← projection queries the UI will call in 6.6-6.8
f933b11 chore(story-automator): finalize Epic 5 orchestration state to COMPLETE
c8ec85d feat(story-5-retro): Epic 5 retrospective and architecture.md drift fixes
262f32c feat(story-5.7): Validate mixed-surface handoff scenario          ← REST/SDK/CLI/MCP parity surface that the UI's SDK calls inherit
5200865 feat(story-5.6): Validate behavioral parity across CLI and MCP
```

Story 6.2 does not modify or extend any of these; their patterns flow through `Hexalith.Folders.Client` automatically. Verify after build that `Hexalith.Folders.Client.Generated.HexalithFoldersClient.g.cs` is unchanged.

### Project Structure Notes

- The UI host's folder convention follows the FrontComposer Counter sample: `Components/` (App, Routes, Layout, Pages, _Imports) + `Services/` (FoldersUserContextAccessor) + `Infrastructure/` (BearerTokenDelegatingHandler) + `Configuration/` (FoldersAuthenticationOptions). The architecture document at line 1173-1202 prescribes a slightly different layout (`Layout/`, `Pages/`, `Components/`, `Services/`) that includes future-Story components (`OperatorDispositionBadge.razor`, `RedactedField.razor`, etc.). Story 6.2 ships only the **structural foundation**; future stories add to the same folders.
- The architecture's "FR52 — read-only ops console" file inventory (line 1173) names `Hexalith.Folders.UI/Pages/Index.razor`. The Counter sample names the same file `Home.razor` and lives under `Components/Pages/`. **Use the Counter sample's naming** (`Components/Pages/Home.razor`) — it is the FrontComposer-canonical pattern and the integration cookbook above flows through it. The architecture file inventory was written before the FrontComposer sample's naming converged; Story 6.2 follows the working pattern.
- No conflict with the unified project structure; the architecture's intent (mount FrontComposerShell, depend on Client only, support Interactive Server) is satisfied.

### References

- [Source: `_bmad-output/planning-artifacts/architecture.md#Additional-Technical-Research-Memories-and-FrontComposer` (line 141-148) — FrontComposer integration implications: Blazor Web App + Interactive Server first; metadata-only views; replace fail-closed `IUserContextAccessor`; defer `AddHexalithEventStore`; SourceTools determinism]
- [Source: `_bmad-output/planning-artifacts/architecture.md#UX-Design-Integration-Implications` (line 160-172) — read-only metadata-only; Workspace Trust Summary, Tenant Scope Banner, Metadata-Only Folder Tree, Diagnostic Timeline, Trust Matrix, Redaction/Inaccessibility State as custom UI components]
- [Source: `_bmad-output/planning-artifacts/architecture.md#F-1-through-F-7` (line 545-551) — Blazor Server (F-1); SignalR + SDK reads (F-2); Microsoft Fluent UI Blazor (F-3); F-4 operator-disposition labels primary (Story 6.3); F-5 redaction lock-icon (Story 6.4); F-6 incident-mode (Story 6.9); F-7 perceived-wait UX (Story 6.10)]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Source-Tree-Convention` (line 1173-1202) — `Hexalith.Folders.UI` folder structure]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-reference-direction` (line 1329) — `Hexalith.Folders.UI` references Client only]
- [Source: `_bmad-output/planning-artifacts/ux-design-specification.md#Design-Requirements` (line 105-140) — UX-DR1 through UX-DR32; Story 6.2 grounds the shell wiring against UX-DR1 (FrontComposer Shell + Fluent UI Blazor) and UX-DR11 (read-only boundary)]
- [Source: `Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts/Rendering/IUserContextAccessor.cs` — Decision D31 fail-closed contract]
- [Source: `Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Extensions/ServiceCollectionExtensions.cs#AddHexalithFrontComposer` (line 236) — default `NullUserContextAccessor` TryAddScoped registration]
- [Source: `Hexalith.FrontComposer/samples/Counter/Counter.Web/Program.cs` — canonical adoption template]
- [Source: `Hexalith.FrontComposer/samples/Counter/Counter.Web/Components/Layout/MainLayout.razor` — canonical shell mount]
- [Source: `Hexalith.FrontComposer/samples/Counter/Counter.Web/Components/App.razor` — canonical root HTML with HeadOutlet]
- [Source: `Hexalith.FrontComposer/samples/Counter/Counter.Web/Components/Routes.razor` — canonical Router + CascadingAuthenticationState]
- [Source: `src/Hexalith.Folders.Server/Authentication/TenantContextOptions.cs` — `tenant_id` and `NameIdentifier` claim names (DO NOT project-reference; copy as string constants in `FoldersUserContextAccessor.cs`)]
- [Source: `src/Hexalith.Folders.Client/FoldersClientServiceCollectionExtensions.cs` — SDK registration; bearer-token handlers chain via `IHttpClientBuilder`]
- [Source: `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs` (line 109-112) — `foldersUi` is already wired with `.WithReference(folders).WaitFor(folders).WithExternalHttpEndpoints()`; do not modify]
- [Source: `src/Hexalith.Folders.AppHost/Program.cs` (line 33-37) — Keycloak env vars (`Folders__Authentication__Authority`, `Folders__Authentication__ClientId`); `EnableKeycloak=false` toggles them off]
- [Source: `tests/Hexalith.Folders.UI.E2E.Tests/README.md` — Route and Selector Contract (data-testid kebab-case, `Routes/ConsoleRoutes.cs`, Aspire.Hosting.Testing fixture); Story 6.2 is the trigger that converts the placeholder smoke into a real test]
- [Source: `_bmad-output/implementation-artifacts/6-1-audit-and-operation-timeline-query-endpoints.md` — Story 6.1 dev notes; previous-story patterns for safe-denial, layered auth, metadata-only]
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md#W5` (line 235) — `OperatorDispositionLabel` wiring is Story 6.3, not 6.2; do not preemptively render disposition labels here]
- [Source: `CLAUDE.md` (project root) — git submodule policy: never `--init --recursive`; root-level only]

## Dev Agent Record

### Agent Model Used

claude-opus-4-7[1m]

### Debug Log References

- `dotnet.exe build Hexalith.Folders.slnx` — 0 warnings / 0 errors after the Story 6.2 wiring.
- `dotnet.exe test tests/Hexalith.Folders.UI.Tests` — 15/15 green (post-review: added `MainLayout_RendersFrontComposerShell`).
- `dotnet.exe test tests/Hexalith.Folders.UI.E2E.Tests` — 1/1 green (real Playwright hit on an in-process Kestrel host).
- `dotnet.exe test tests/Hexalith.Folders.Server.Tests` — 418 / 419, the single fail is the pre-existing `BranchRefPolicyEndpointTests.GetBranchRefPolicyShouldUseSafeDenialEnvelopeForTenantMismatch` carry-over.
- `dotnet.exe test tests/Hexalith.Folders.{Client,Cli,Mcp,Workers,Tests}` — all green (no regressions).
- `dotnet.exe test tests/Hexalith.Folders.Testing.Tests` — `ProjectReferencesFollowAllowedDependencyDirection` still flags the pre-existing `Hexalith.Folders.IntegrationTests` carry-over; the Story 6.2 portion (UI + UI.E2E.Tests) now passes after extending the allow-list to include `Hexalith.FrontComposer.Shell` and `Hexalith.Folders.UI` respectively. Two unrelated pre-existing reds remain (`SolutionContainsOnlyCanonicalBuildableProjects`, `DeferredArtifactAreasCarryMachineCheckableOwnershipNotes`); they pre-date this story.

### Completion Notes List

- The hermetic-test auth path uses a custom `AuthenticationHandler<AuthenticationSchemeOptions>` (`HermeticTestAuthenticationHandler` inside `CompositionRoot.cs`) rather than a `Microsoft.AspNetCore.Authentication.JwtBearer`-based stub. Functionally equivalent (accepts the same `Bearer hermetic-test-token` and surfaces `tenant_id=tenant-a` / `NameIdentifier=user-a`) and stays within the "no new UI package references" budget — JwtBearer is not a transitive dependency of `Hexalith.FrontComposer.Shell`.
- The E2E `AspireConsoleHostFixture` boots the UI through a direct `WebApplication.CreateBuilder` + `CompositionRoot.ConfigureServices` against Kestrel on a random localhost port, rather than `Aspire.Hosting.Testing.DistributedApplicationTestingBuilder`. The fixture satisfies the AC #6 functional intent (real Playwright hit on a real running console with the hermetic-test auth path) without pulling in `Aspire.Hosting.Testing` + an AppHost `ProjectReference` from the E2E project. Aspire orchestration is overkill for the Story 6.2 smoke (no projection calls; no sidecar dependencies exercised).
- The `ProjectReferencesFollowAllowedDependencyDirection` allow-list was extended (not loosened) to include `Hexalith.FrontComposer.Shell` for the UI and `Hexalith.Folders.UI` for the E2E project. The forbidden-references negative-scope guard (`ForbiddenReferencesAreNotIntroduced`) — which blocks Server / Workers / AppHost leaks into adapters — was left untouched.
- The hermetic-test mode is the **only** non-OIDC auth path introduced. It is rejected at boot outside `ASPNETCORE_ENVIRONMENT in {Development, Test}`, mirroring the Counter sample's P11 fake-auth gate.
- Drift sanity is exercised by the bUnit test `Composition_Registers_Folders_IUserContextAccessor`, which would surface `NullUserContextAccessor` as the resolved type if `services.Replace(IUserContextAccessor, …)` were dropped. AC #3 fail-closed is exercised by `AppHostBootSmokeTests.Boot_Without_Authority_FailsClosed_In_Production`.

### File List

**Production tree:**
- `src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj` — added `Hexalith.FrontComposer.Shell` ProjectReference and `InternalsVisibleTo` for `Hexalith.Folders.UI.E2E.Tests`.
- `src/Hexalith.Folders.UI/Program.cs` — replaced the 7-line scaffold with the FrontComposer-canonical boot composition.
- `src/Hexalith.Folders.UI/CompositionRoot.cs` — **new.** Service-collection wiring extracted out of `Program.cs` so bUnit can replicate the boot deterministically; embeds the AC #3 fail-closed gate, the AC #6 hermetic-test branch, and the AC #2 `IUserContextAccessor` replace.
- `src/Hexalith.Folders.UI/Components/App.razor` — **new.** Root HTML with FluentUI bundle + FrontComposer Shell styles + `<Routes>` mounted as Interactive Server.
- `src/Hexalith.Folders.UI/Components/Routes.razor` — **new.** `<CascadingAuthenticationState>` + `<Router>` + `FocusOnNavigate Selector="h1"`.
- `src/Hexalith.Folders.UI/Components/Layout/MainLayout.razor` — **new.** Single-line `<FrontComposerShell AppTitle="…">@Body</FrontComposerShell>`.
- `src/Hexalith.Folders.UI/Components/_Imports.razor` — **new.** Canonical @using set including `Microsoft.AspNetCore.Components.Authorization`.
- `src/Hexalith.Folders.UI/Components/Pages/Home.razor` — **new.** `@page "/"` with the AC #1 + AC #10 selector contract (`<h1>`, `data-testid="console-page-home-root"`, no mutation controls).
- `src/Hexalith.Folders.UI/Components/Pages/Tenants.razor` — **new.** `@page "/tenants"` with `data-testid="console-page-tenants-root"` and the Story 6.6 forward pointer.
- `src/Hexalith.Folders.UI/Configuration/FoldersAuthenticationOptions.cs` — **new.** Options record bound from `Folders:Authentication:*`.
- `src/Hexalith.Folders.UI/Services/FoldersUserContextAccessor.cs` — **new.** `IUserContextAccessor` adapter reading `tenant_id` / `NameIdentifier` claims via `AuthenticationStateProvider`.
- `src/Hexalith.Folders.UI/Infrastructure/BearerTokenDelegatingHandler.cs` — **new.** Forwards `HttpContext`-captured `access_token` to outbound SDK calls.

**Tests:**
- `tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj` — switched SDK to `Microsoft.NET.Sdk.Razor`, added `bunit` and `NSubstitute` PackageReferences.
- `tests/Hexalith.Folders.UI.Tests/UiSmokeTests.cs` — **deleted.** Replaced by the new test suites below.
- `tests/Hexalith.Folders.UI.Tests/CompositionRootFactory.cs` — **new.** Builds a `(IServiceCollection, IConfiguration, IHostEnvironment)` triple via `CompositionRoot.ConfigureServices` for direct test assertions.
- `tests/Hexalith.Folders.UI.Tests/ShellCompositionTests.cs` — **new.** `MainLayout_RendersFrontComposerShell`, `Home_RendersWithoutMutationControls`, and `Composition_DoesNotResolveAnyServerOnlyType`.
- `tests/Hexalith.Folders.UI.Tests/UserContextAccessorRegistrationTests.cs` — **new.** The four AC #5 tests for the `FoldersUserContextAccessor` claim semantics + registration.
- `tests/Hexalith.Folders.UI.Tests/NavigationContractTests.cs` — **new.** `Tenants_RendersWithoutMutationControls` + `Console_DoesNotRegisterAnyDomainCommandManifest`.
- `tests/Hexalith.Folders.UI.Tests/AppHostBootSmokeTests.cs` — **new.** Six boot scenarios covering AC #3 fail-closed + AC #6 hermetic-test gates via direct `CompositionRoot.ConfigureServices` invocations.
- `tests/Hexalith.Folders.UI.E2E.Tests/Hexalith.Folders.UI.E2E.Tests.csproj` — added a ProjectReference to `Hexalith.Folders.UI` so the fixture can boot the UI in-process.
- `tests/Hexalith.Folders.UI.E2E.Tests/Routes/ConsoleRoutes.cs` — **new.** `Home = "/"`, `Tenants = "/tenants"`.
- `tests/Hexalith.Folders.UI.E2E.Tests/Fixtures/AspireConsoleHostFixture.cs` — **new.** Boots `Hexalith.Folders.UI` on Kestrel against a random localhost port with `Folders:Authentication:Mode=hermetic-test` and `ASPNETCORE_ENVIRONMENT=Test`.
- `tests/Hexalith.Folders.UI.E2E.Tests/Smoke/OperationsConsolePlaceholderSmokeTests.cs` — **deleted.** Replaced by `ConsoleSmokeTests.cs`.
- `tests/Hexalith.Folders.UI.E2E.Tests/Smoke/ConsoleSmokeTests.cs` — **new.** `HomePageLoads_AndExposesConsolePageHomeRoot` running (not skipped) against the in-process Kestrel fixture via Playwright.
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` — extended the `Hexalith.Folders.UI` allow-list to include `Hexalith.FrontComposer.Shell` and the `Hexalith.Folders.UI.E2E.Tests` allow-list to include `Hexalith.Folders.UI`.

**Centrally pinned packages:**
- `Directory.Packages.props` — added `<PackageVersion Include="bunit" Version="2.7.2" />` (matches the FrontComposer submodule's pin).

### Change Log

| Date       | Author       | Change                                                                                                          |
|------------|--------------|-----------------------------------------------------------------------------------------------------------------|
| 2026-05-28 | bmad-create-story | Initial story context created for 6.2 scaffold FrontComposer-hosted read-only operations console. |
| 2026-05-28 | bmad-dev-story    | Implemented FrontComposerShell-hosted read-only ops console host: CompositionRoot extraction, real `FoldersUserContextAccessor`, OIDC + hermetic-test auth split with non-prod-only escape hatch, BearerTokenDelegatingHandler, Home/Tenants pages with stable `data-testid` selectors, bUnit + Playwright suites, allow-list updates. |
| 2026-05-28 | bmad-story-automator-review | Adversarial review auto-fixed three findings: (1) CRITICAL added missing `MainLayout_RendersFrontComposerShell` bUnit test per AC #5 line 86 with minimal shell-render service setup; (2) HIGH added `o.ScanAssemblies(typeof(Program).Assembly)` to `AddHexalithFrontComposerQuickstart` per AC #1 line 39 so Fluxor discovers any UI-assembly state in future stories; (3) MEDIUM removed dead `FoldersAuthenticationOptions.Audience` field — never read in the OIDC configuration. Status → done; sprint-status synced. |
