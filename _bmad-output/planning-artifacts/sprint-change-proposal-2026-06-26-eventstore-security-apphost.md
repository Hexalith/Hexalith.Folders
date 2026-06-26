# Sprint Change Proposal - Initialize Folders AppHost Security Through EventStore Aspire Extension

_Workflow: bmad-correct-course. Date: 2026-06-26. Mode: Batch. Author: Codex. Status: Approved by direct user request and implemented._

> Trigger: `HexalithEventStoreSecurityExtensions to initialize the security service in aspire host`.
> `Hexalith.Folders.AppHost` still hand-wired the local Keycloak resource and JWT environment variables even though `Hexalith.EventStore.Aspire` now owns the reusable `HexalithEventStoreSecurityExtensions` API used by the EventStore and Tenants AppHosts.

## 1. Issue Summary

The Folders AppHost created Keycloak directly with `builder.AddKeycloak("keycloak", 8180)`, manually built the realm URL, and locally set the JWT bearer settings for `eventstore`, `tenants`, `folders`, and `folders-workers`. This duplicated platform security bootstrap logic that now lives in `Hexalith.EventStore.Aspire`.

Evidence:

| Location | Finding |
| --- | --- |
| `src/Hexalith.Folders.AppHost/Program.cs` | Manual Keycloak creation and manual `Authentication__JwtBearer__*` wiring. |
| `Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs` | Shared `AddHexalithEventStoreSecurity`, `WithJwtBearerSecurity`, `WithSecurityDependency`, and related helpers. |
| `Hexalith.Tenants/src/Hexalith.Tenants.AppHost/Program.cs` | Existing precedent using the shared EventStore security helper. |

Issue type: technical AppHost composition drift. This is not product-scope change.

## 2. Impact Analysis

Epic impact: Epic 9 remains valid. The change strengthens Story 9.1 platform-helper alignment by extending it to the security resource.

Story impact: no new story is required. The change is a minor implementation correction to AppHost composition and its conformance guards.

Artifact impact:

| Artifact | Impact | Action |
| --- | --- | --- |
| PRD | No conflict. Tenant isolation and auth expectations are reinforced. | No edit. |
| Epics | Epic 9 wording remains valid; this is an implementation detail under platform helper alignment. | No edit. |
| Architecture | Existing AppHost composition and security invariants already support shared EventStore/Tenants/Aspire helpers. | No edit. |
| UX | No console behavior or UI scope change. | No edit. |
| Code/tests | Directly impacted. | Update AppHost and conformance tests. |

Technical impact:

- Initialize local security via `builder.AddHexalithEventStoreSecurity()`.
- Use `WithJwtBearerSecurity` for EventStore, Tenants, Folders Server, and Folders Workers.
- Keep Folders UI on its existing `Folders:Authentication` configuration contract while making it depend on the shared security resource.
- Remove the direct `Aspire.Hosting.Keycloak` package reference from Folders AppHost if the build remains clean.
- Preserve `EnableKeycloak=false` behavior through the helper returning `null`.

## 3. Recommended Approach

Selected path: Direct Adjustment.

Effort: Low.
Risk: Low to Medium.

Rationale: the backlog already calls for platform helper alignment; duplicating Keycloak/security setup would let EventStore/Tenants/Folders local hosts drift. A rollback or MVP review is not justified.

Primary risk: the visible Aspire resource name changes from `keycloak` to the shared helper default `security`. This aligns Folders with EventStore/Tenants and enables the helper's persistent Keycloak reuse knobs.

## 4. Detailed Change Proposals

### Code

`src/Hexalith.Folders.AppHost/Program.cs`

OLD:

```csharp
IResourceBuilder<KeycloakResource>? keycloak = null;
ReferenceExpression? realmUrl = null;
if (!string.Equals(builder.Configuration["EnableKeycloak"], "false", StringComparison.OrdinalIgnoreCase))
{
    keycloak = builder.AddKeycloak("keycloak", 8180)
        .WithRealmImport("./KeycloakRealms");
    EndpointReference keycloakEndpoint = keycloak.GetEndpoint("http");
    realmUrl = ReferenceExpression.Create($"{keycloakEndpoint}/realms/hexalith");
}
```

NEW:

```csharp
HexalithEventStoreSecurityResources? security = builder.AddHexalithEventStoreSecurity();
```

Replace the manual `ConfigureJwt` helper and local JWT environment wiring with the shared `WithJwtBearerSecurity` helper. Preserve Folders UI's existing configuration keys:

```csharp
if (security is not null)
{
    _ = eventStore.WithJwtBearerSecurity(security);
    _ = tenants.WithJwtBearerSecurity(security);
    _ = folders.WithJwtBearerSecurity(security);
    _ = foldersWorkers.WithJwtBearerSecurity(security);

    _ = foldersUi
        .WithSecurityDependency(security)
        .WithEnvironment("Folders__Authentication__Authority", security.RealmUrl)
        .WithEnvironment("Folders__Authentication__ClientId", "hexalith-folders")
        .WithEnvironment("Folders__Authentication__RequireHttpsMetadata", security.RequireHttpsMetadata ? "true" : "false");
}
```

`src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj`

Remove the direct `Aspire.Hosting.Keycloak` reference if no AppHost code consumes `KeycloakResource` directly after the refactor.

### Tests

Add source-level conformance assertions that `Program.cs`:

- contains `AddHexalithEventStoreSecurity`;
- does not contain `builder.AddKeycloak(`;
- does not manually set `Authentication__JwtBearer__Authority`;
- retains Folders UI's `Folders__Authentication__Authority` wiring.

## 5. Implementation Handoff

Scope classification: Minor.

Route to: Developer agent for direct implementation.

Success criteria:

- Folders AppHost initializes security through `HexalithEventStoreSecurityExtensions`.
- EventStore, Tenants, Folders Server, and Folders Workers receive equivalent JWT bearer settings through shared helpers.
- Folders UI still receives `Folders:Authentication` settings.
- Direct Keycloak package/reference is removed from the AppHost project.
- Focused tests pass; AppHost build passes once the checkout has the required nested Memories dependency.

Validation note: focused contract tests pass. AppHost build is blocked in this checkout before this change by the missing nested `Hexalith.Memories/Hexalith.PolymorphicSerializations` submodule; nested submodule initialization was not performed because the repository instructions require explicit user approval.

## Checklist Status

- 1.1 Triggering story: N/A. Direct AppHost composition correction requested by user.
- 1.2 Core problem: Done. Manual security initialization drift.
- 1.3 Evidence: Done. Program.cs manual wiring, EventStore helper, Tenants precedent.
- 2.1 Current epic impact: Done. Epic 9 reinforced.
- 2.2 Epic-level changes: N/A.
- 2.3 Future epic review: Done. No future epic invalidated.
- 2.4 New epic need: N/A.
- 2.5 Epic order/priority: N/A.
- 3.1 PRD conflict: N/A.
- 3.2 Architecture conflict: Done. No required architecture edit.
- 3.3 UI/UX conflict: N/A.
- 3.4 Other artifacts: Done. AppHost project/test artifacts affected.
- 4.1 Direct Adjustment: Viable, selected.
- 4.2 Rollback: Not viable / not applicable.
- 4.3 PRD MVP Review: Not viable / not applicable.
- 4.4 Recommended path: Done.
- 5.1 Issue summary: Done.
- 5.2 Impact and artifact needs: Done.
- 5.3 Path forward: Done.
- 5.4 MVP impact/action plan: Done. MVP scope unaffected.
- 5.5 Handoff plan: Done. Minor direct implementation.
- 6.1 Checklist completion: Done.
- 6.2 Proposal accuracy: Done.
- 6.3 User approval: Done by direct implementation request.
- 6.4 Sprint-status update: N/A. No epic/story inventory changes.
- 6.5 Next steps/handoff: Done. Routed to Developer implementation.
