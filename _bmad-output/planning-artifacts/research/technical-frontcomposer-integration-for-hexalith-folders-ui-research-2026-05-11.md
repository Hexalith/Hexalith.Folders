---
stepsCompleted: [1, 2, 3, 4, 5, 6]
inputDocuments:
  - D:/Hexalith.Folders/_bmad-output/project-context.md
  - D:/Hexalith.Folders/Hexalith.FrontComposer/_bmad-output/project-context.md
workflowType: 'research'
lastStep: 6
research_type: 'technical'
research_topic: 'FrontComposer integration for Hexalith.Folders UI'
research_goals: 'Find how to integrate Hexalith.FrontComposer into the Hexalith.Folders folder service UI.'
user_name: 'Jerome'
date: '2026-05-11'
web_research_enabled: true
source_verification: true
---

# Research Report: technical

**Date:** 2026-05-11
**Author:** Jerome
**Research Type:** technical

---

## Research Overview

This research identifies the practical integration path for using Hexalith.FrontComposer inside the Hexalith.Folders UI, combining local repository analysis with current official documentation for version-sensitive platform behavior.

---

## Technical Research Scope Confirmation

**Research Topic:** FrontComposer integration for Hexalith.Folders UI
**Research Goals:** Find how to integrate Hexalith.FrontComposer into the Hexalith.Folders folder service UI.

**Technical Research Scope:**

- Architecture Analysis - design patterns, frameworks, system architecture
- Implementation Approaches - development methodologies, coding patterns
- Technology Stack - languages, frameworks, tools, platforms
- Integration Patterns - APIs, protocols, interoperability
- Performance Considerations - scalability, optimization, patterns

**Research Methodology:**

- Current web data with rigorous source verification
- Multi-source validation for critical technical claims
- Confidence level framework for uncertain information
- Comprehensive technical coverage with architecture-specific insights

**Scope Confirmed:** 2026-05-11

---

# FrontComposer Integration for Hexalith.Folders UI: Technical Research

## Executive Summary

Hexalith.Folders should integrate Hexalith.FrontComposer by turning `src/Hexalith.Folders.UI` from its current minimal HTTP scaffold into a Blazor Web App host that renders `FrontComposerShell` as the primary layout. The local FrontComposer Counter sample is the strongest implementation reference: it registers Razor components, Fluent UI, FrontComposer quickstart services, a bounded-context domain assembly, optional projection templates, then maps the app root component with Interactive Server rendering.

For the Folders MVP, the safest integration is read-only and projection-backed. Folders should first define metadata-only FrontComposer projection models for folder/workspace/operation-console views, scan those assemblies with the FrontComposer source generator, and supply a real `IUserContextAccessor` that bridges authenticated tenant/user claims. Mutation commands can be modeled later, but the current Folders project context explicitly says the Blazor operations console is read-only for MVP and must not expose mutation, file browsing, file editing, raw diffs, credential reveal, or repair actions.

The EventStore-backed FrontComposer adapter already expects REST command and query endpoints plus a SignalR projection-change hub. Folders can either defer `AddHexalithEventStore` until those endpoints exist, or add a Folders-specific read-only `IQueryService` adapter that wraps `Hexalith.Folders.Client` and returns `QueryResult<T>` for generated projection pages. Do not wire commands through FrontComposer until the Folders server owns the `/api/v1/commands`, `/api/v1/queries`, and `/hubs/projection-changes` contract expected by `Hexalith.FrontComposer.Shell`.

## Table of Contents

1. Technology Stack Analysis
2. Integration Pattern
3. Architecture Decisions
4. Implementation Roadmap
5. Risks and Guardrails
6. Source Verification

## 1. Technology Stack Analysis

### Current Folders UI State

`Hexalith.Folders.UI` is currently a minimal ASP.NET Core web project using `Microsoft.NET.Sdk.Web`. It references only `Hexalith.Folders.Client` and maps `/` to a scaffold string. There are no Razor components, layouts, routes, Fluent UI services, Fluxor setup, or FrontComposer references yet.

Local evidence:

- `D:/Hexalith.Folders/src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj`
- `D:/Hexalith.Folders/src/Hexalith.Folders.UI/Program.cs`
- `D:/Hexalith.Folders/tests/Hexalith.Folders.UI.Tests/UiSmokeTests.cs`

### FrontComposer Host Stack

FrontComposer Shell is a Razor class library targeting `net10.0` with `Microsoft.NET.Sdk.Razor`. It depends on:

- `Hexalith.FrontComposer.Contracts`
- `Fluxor.Blazor.Web`
- `Microsoft.FluentUI.AspNetCore.Components`
- `Microsoft.AspNetCore.SignalR.Client`
- `Microsoft.AspNetCore.Authentication.OpenIdConnect`
- `System.Reactive`
- `NUlid`

The source generator package is `Hexalith.FrontComposer.SourceTools`, referenced as an analyzer with `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`.

Local evidence:

- `D:/Hexalith.Folders/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Hexalith.FrontComposer.Shell.csproj`
- `D:/Hexalith.Folders/Hexalith.FrontComposer/src/Hexalith.FrontComposer.SourceTools/Hexalith.FrontComposer.SourceTools.csproj`
- `D:/Hexalith.Folders/Hexalith.FrontComposer/samples/Counter/Counter.Web/Counter.Web.csproj`

External verification:

- Microsoft Blazor docs confirm that a Blazor Web App must call `AddRazorComponents().AddInteractiveServerComponents()` and map the root component with `MapRazorComponents<App>().AddInteractiveServerRenderMode()` for Interactive Server rendering: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0
- Microsoft Fluent UI Blazor docs/README confirm `builder.Services.AddFluentUIComponents()` registration and provider components for dialogs, tooltips, messages, and menus: https://github.com/microsoft/fluentui-blazor
- Microsoft RCL docs confirm `_content/{PACKAGE ID}/{PATH}` static asset paths and router `AdditionalAssemblies` for RCL routable components: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/class-libraries?view=aspnetcore-10.0

## 2. Integration Pattern

### Recommended Pattern

Use `Hexalith.Folders.UI` as the host application and `Hexalith.FrontComposer.Shell` as the UI framework. Keep folder domain behavior in the existing Folders domain/client/server projects, and expose UI-specific metadata through FrontComposer projection contracts.

Recommended project references for `Hexalith.Folders.UI`:

```xml
<ProjectReference Include="..\Hexalith.Folders.Client\Hexalith.Folders.Client.csproj" />
<ProjectReference Include="..\..\Hexalith.FrontComposer\src\Hexalith.FrontComposer.Shell\Hexalith.FrontComposer.Shell.csproj" />
<ProjectReference Include="..\..\Hexalith.FrontComposer\src\Hexalith.FrontComposer.SourceTools\Hexalith.FrontComposer.SourceTools.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false"
                  SetTargetFramework="TargetFramework=netstandard2.0" />
```

If the projection DTOs live outside the UI project, reference that assembly too and include it in Fluxor scanning and Razor `AdditionalAssemblies`.

### Program.cs Host Shape

The Counter sample provides the closest working pattern:

```csharp
using Hexalith.FrontComposer.Contracts;
using Hexalith.FrontComposer.Contracts.Rendering;
using Hexalith.FrontComposer.Shell.Extensions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FluentUI.AspNetCore.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider(o => o.ValidateScopes = true);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient();
builder.Services.AddFluentUIComponents();

builder.Services.AddHexalithFrontComposerQuickstart(o =>
{
    o.ScanAssemblies(typeof(Program).Assembly /*, typeof(FoldersUiDomain).Assembly */);
});

builder.Services.AddFrontComposerDevMode(builder.Environment);
builder.Services.AddHexalithDomain<Program>(); // replace Program with a bounded-context marker type
builder.Services.Configure<FcShellOptions>(builder.Configuration.GetSection("Hexalith:Shell"));

builder.Services.Replace(ServiceDescriptor.Scoped<IUserContextAccessor, FoldersUserContextAccessor>());

WebApplication app = builder.Build();

app.MapStaticAssets();
app.UseStaticFiles();
app.UseRequestLocalization();
app.UseAntiforgery();

app.MapRazorComponents<Hexalith.Folders.UI.Components.App>()
    .AddAdditionalAssemblies(typeof(Program).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
```

The `AddHexalithEventStore(...)` call should be added only when Folders server implements the required FrontComposer EventStore protocol, or when a compatible adapter is available.

### Razor Component Shape

Create the standard Blazor component structure under `src/Hexalith.Folders.UI/Components`:

```text
Components/
  App.razor
  Routes.razor
  _Imports.razor
  Layout/
    MainLayout.razor
  Pages/
    Home.razor
```

`MainLayout.razor` should be the same minimal shell pattern as the FrontComposer sample:

```razor
@inherits LayoutComponentBase
@using Hexalith.FrontComposer.Shell.Components.Layout

<FrontComposerShell>@Body</FrontComposerShell>
```

`App.razor` should host `HeadOutlet` and `Routes` with `InteractiveServer` render mode. That matches Microsoft Blazor guidance that the root `App` component itself is not made interactive; the render mode is applied to the routed component tree and head outlet.

## 3. Architecture Decisions

### Projection-First MVP

Use `[Projection]` and `[BoundedContext("Folders")]` types for read-only surfaces. Start with metadata-only read models such as:

- `FolderSummaryProjection`
- `WorkspaceStatusProjection`
- `ProviderConnectionProjection`
- `FolderOperationProjection`
- `TenantFolderAccessProjection`

The FrontComposer docs say projection types are partial C# types marked with `[Projection]`, usually grouped by `[BoundedContext]`, and display metadata should use framework-supported attributes so SourceTools emits the manifest and UI. Local examples live in the Counter domain.

Local evidence:

- `D:/Hexalith.Folders/Hexalith.FrontComposer/docs/skills/frontcomposer/domain/projections.md`
- `D:/Hexalith.Folders/Hexalith.FrontComposer/samples/Counter/Counter.Domain/CounterProjection.cs`
- `D:/Hexalith.Folders/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts/Attributes/ProjectionAttribute.cs`

### Commands Deferred

FrontComposer supports `[Command]` models and generated command forms, but Folders MVP policy says the operations console is read-only. Do not generate or route folder mutation forms yet. If commands are introduced later, they need:

- `[Command]` DTOs with `MessageId` and an aggregate identifier property such as `AggregateId`, `Id`, or `Name`
- `[BoundedContext("Folders")]`
- `[RequiresPolicy]` where policy metadata is needed
- EventStore command endpoint compatibility
- authorization gate coverage before dispatch

Local evidence:

- `D:/Hexalith.Folders/Hexalith.FrontComposer/docs/skills/frontcomposer/domain/commands.md`
- `D:/Hexalith.Folders/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Infrastructure/EventStore/EventStoreCommandClient.cs`
- `D:/Hexalith.Folders/_bmad-output/project-context.md`

### Tenant Context Bridge Is Mandatory

`AddHexalithFrontComposer` installs a fail-closed `NullUserContextAccessor`. Folders must replace it with an authenticated implementation that returns the current tenant and user IDs from the Folders/Tenants auth context. Returning null/blank means no authenticated context and causes tenant-scoped FrontComposer behavior to fail closed or no-op.

Local evidence:

- `D:/Hexalith.Folders/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts/Rendering/IUserContextAccessor.cs`
- `D:/Hexalith.Folders/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Infrastructure/Tenancy/FrontComposerTenantContextAccessor.cs`
- `D:/Hexalith.Folders/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts/FcShellOptions.cs`

### EventStore Adapter Contract

`AddHexalithEventStore` registers FrontComposer command, query, and SignalR projection subscription services. Its default options are:

- command endpoint: `/api/v1/commands`
- query endpoint: `/api/v1/queries`
- projection changes hub: `/hubs/projection-changes`
- access tokens required by default

Folders server currently exposes only `/`, so direct `AddHexalithEventStore` integration is premature unless Folders implements these endpoints or adapts the options to compatible server endpoints.

Local evidence:

- `D:/Hexalith.Folders/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Infrastructure/EventStore/EventStoreOptions.cs`
- `D:/Hexalith.Folders/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Extensions/EventStoreServiceExtensions.cs`
- `D:/Hexalith.Folders/src/Hexalith.Folders.Server/Program.cs`

## 4. Implementation Roadmap

### Phase 1: Host FrontComposer Shell in Folders UI

1. Add FrontComposer Shell and SourceTools references to `Hexalith.Folders.UI`.
2. Add package references already centrally versioned in `Directory.Packages.props`: `Microsoft.FluentUI.AspNetCore.Components` and `Fluxor.Blazor.Web` only if the transitive references are not enough for generated code compilation.
3. Convert `Program.cs` to a Blazor Web App host using `AddRazorComponents`, `AddInteractiveServerComponents`, `AddFluentUIComponents`, `AddHexalithFrontComposerQuickstart`, and `MapRazorComponents`.
4. Add `Components/App.razor`, `Routes.razor`, `_Imports.razor`, and `Layout/MainLayout.razor`.
5. Keep the first screen read-only and projection-backed.

### Phase 2: Add Folders Projection Models

1. Create a bounded-context marker, for example `FoldersFrontComposerDomain`, annotated `[BoundedContext("Folders")]`.
2. Add partial projection classes annotated `[Projection]`.
3. Scan the projection assembly in `AddHexalithFrontComposerQuickstart`.
4. Register the bounded context with `AddHexalithDomain<FoldersFrontComposerDomain>()`.
5. Build and inspect generated `.g.cs` output only for verification; never hand-edit generated files.

### Phase 3: Wire Data

Choose one of two paths:

- Preferred for eventual parity: implement FrontComposer-compatible `/api/v1/queries` and `/hubs/projection-changes` on `Hexalith.Folders.Server`, then call `AddHexalithEventStore`.
- Faster read-only bridge: implement a Folders-specific `IQueryService` backed by `Hexalith.Folders.Client`, returning `QueryResult<T>` for the projection DTOs. This avoids pretending the EventStore protocol exists before the server supports it.

For Aspire, keep `folders-ui` separate from `folders` and add a reference from UI to server once service discovery is needed:

```csharp
IResourceBuilder<ProjectResource> folders =
    builder.AddProject<Projects.Hexalith_Folders_Server>(FoldersAspireModule.FoldersAppId);

builder.AddProject<Projects.Hexalith_Folders_UI>(FoldersAspireModule.FoldersUiAppId)
    .WithReference(folders)
    .WaitFor(folders);
```

External verification: Aspire AppHost docs describe `AddProject` as the code-first application model, `WithReference` as dependency/configuration wiring, and service discovery through references: https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/app-host-overview and https://learn.microsoft.com/en-us/dotnet/aspire/service-discovery/overview

### Phase 4: Add Tests

1. Update `Hexalith.Folders.UI.Tests` to assert FrontComposer registrations exist in DI.
2. Add bUnit only when rendering actual FrontComposer pages becomes part of the acceptance gate.
3. Add smoke coverage that `MainLayout` renders `FrontComposerShell`.
4. Add fail-closed tests for missing tenant/user context.
5. Keep tests credential-free and independent of Dapr, Keycloak, provider credentials, or nested submodule initialization.

## 5. Risks and Guardrails

### Security

- Do not expose mutation UI in the MVP.
- Do not include tenant IDs as user-editable command fields.
- Do not show file contents, diffs, provider tokens, credential material, or unauthorized resource existence.
- Replace `IUserContextAccessor` with a real auth bridge before enabling tenant-scoped queries.
- Leave `AllowDemoTenantContext` disabled outside development/test.

### Build and Package Governance

- Do not add inline package versions. Use `Directory.Packages.props`.
- Do not initialize nested submodules.
- Treat `Hexalith.FrontComposer` as a root-level sibling module.
- Do not edit generated `.g.cs` files.
- Keep Folders contracts behavior-free; if UI-only projection annotations would pollute the canonical contract spine, place FrontComposer projection DTOs in a UI/domain companion assembly.

### Blazor Runtime

- Prefer Interactive Server first. FrontComposer local context says Blazor Auto is a first-class constraint, but Folders has no `.Client` project today. Interactive Auto would require additional client-project layout and asset decisions.
- Browser storage must go through FrontComposer storage abstractions because prerender and circuit lifetime matter.
- `FrontComposerShell` owns the Fluxor `StoreInitializer`; do not mount a second one.

## 6. Source Verification

### Local Primary Sources

- `D:/Hexalith.Folders/src/Hexalith.Folders.UI/Program.cs`
- `D:/Hexalith.Folders/src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj`
- `D:/Hexalith.Folders/src/Hexalith.Folders.AppHost/Program.cs`
- `D:/Hexalith.Folders/_bmad-output/project-context.md`
- `D:/Hexalith.Folders/Hexalith.FrontComposer/_bmad-output/project-context.md`
- `D:/Hexalith.Folders/Hexalith.FrontComposer/samples/Counter/Counter.Web/Program.cs`
- `D:/Hexalith.Folders/Hexalith.FrontComposer/samples/Counter/Counter.Web/Components/Layout/MainLayout.razor`
- `D:/Hexalith.Folders/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Extensions/ServiceCollectionExtensions.cs`
- `D:/Hexalith.Folders/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Extensions/EventStoreServiceExtensions.cs`
- `D:/Hexalith.Folders/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Infrastructure/EventStore/EventStoreOptions.cs`

### External Primary Sources

- ASP.NET Core Blazor render modes, .NET 10: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0
- ASP.NET Core Razor class libraries, .NET 10: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/class-libraries?view=aspnetcore-10.0
- ASP.NET Core Blazor static files: https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/static-files?view=aspnetcore-10.0
- .NET Aspire AppHost overview: https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/app-host-overview
- .NET Aspire service discovery: https://learn.microsoft.com/en-us/dotnet/aspire/service-discovery/overview
- Microsoft Fluent UI Blazor repository/README: https://github.com/microsoft/fluentui-blazor

## Conclusion

The integration path is straightforward but should be staged. First make `Hexalith.Folders.UI` a real Blazor host and render `FrontComposerShell`. Then add Folders read-only projection models and tenant-aware user context. Only after the Folders server supports the FrontComposer EventStore query/command/subscription contract should `AddHexalithEventStore` become the production data path.

The most important design choice is to preserve the Folders MVP safety boundary: projection-backed, metadata-only, tenant-scoped, and read-only. FrontComposer gives the UI shell, generated projection views, Fluxor state, Fluent UI surface, and eventual command/query lifecycle integration, but Folders still owns the domain semantics, authorization, server endpoints, and no-leakage policy.
