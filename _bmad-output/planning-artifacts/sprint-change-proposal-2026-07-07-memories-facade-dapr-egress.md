# Sprint Change Proposal — Memories Read-Facade Egress Policy

- **Date:** 2026-07-07 19:28 CEST
- **Author:** Amelia (Developer) via `bmad-correct-course`, with Jerome
- **Trigger story:** 10.5 — Expose an authorized Folders query facade over Memories
- **Deferred-work ledger item resolved:** `sprint-status.yaml:305-309` (epic 10, owner *Winston / Platform*, priority *medium*, status *open*)
- **Chosen branch:** **Route `Memories:BaseAddress` through the Dapr sidecar invoke** (make the enforcement real) — *not* the "revise evidence to weaken the claim" branch
- **Review mode:** Incremental (all 5 edit proposals approved)
- **Scope classification:** **Minor** (direct Developer-agent implementation) with Architect (Winston) sign-off on the architecture edit

---

## Section 1 — Issue Summary

The Story 10.5 context-search facade reads the Memories search index over `GET /api/search` on Dapr app-id `memories`. Three governance artifacts present **Dapr deny-by-default + mTLS** as the operative egress control for that read path:

- **architecture I-3** (line 578): "`folders`/`folders-workers` may invoke `memories`" under production deny-by-default + mTLS, validated by a merge-blocking `dapr-policy-conformance` CI job.
- **architecture S-4** (line 497): the authorization layering ends in "production Dapr deny-by-default policies + mTLS".
- **Shipped policy + tests:** `deploy/dapr/production/accesscontrol.yaml` carries the `folders → memories` `GET /api/search` invoke allow-rule (deny-by-default otherwise); `tests/fixtures/dapr-policy-conformance.yaml` + `DaprPolicyConformanceTests` assert the rule + negative controls.

**But the live client bypasses Dapr entirely.** `AddFoldersContextSearchFacade` (`FoldersServerServiceCollectionExtensions.cs:87-112`) configures `AddMemoriesClient` as a **direct base-address `HttpClient`** (`httpClient.BaseAddress = Memories:BaseAddress`; `MemoriesClient.SearchAsync` → `GetAsync("api/search")`). Nothing routes that base address through the sidecar. The §Query Facade section (line 172) quietly caveated this: "…the operative control **only when that base address routes through the Dapr sidecar**" — a condition nothing in the topology satisfies. The Folders AppHost `folders` resource sets **no** `Memories__BaseAddress` at all, and the copied precedent (`Hexalith.Tenants.AppHost`) sets it to the *direct* Memories HTTP endpoint (`memoriesService.GetEndpoint("http")`).

**Net effect:** the deny-by-default envelope that governs every other Folders egress (`eventstore`, `tenants`) does **not** cover the memories read path, while the architecture and the shipped conformance suite imply that it does. This is an over-claim of Dapr enforcement, and an inconsistency between §Query Facade and I-3/S-4.

### Evidence

- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:87-112` — direct base-address wiring + the conditional XML-doc claim.
- `_bmad-output/planning-artifacts/architecture.md:172` — the never-satisfied caveat.
- `10-5-…md:247` — the completion note that admits "not Dapr-invoke … operative only when that base address routes via the sidecar."
- `references/Hexalith.Tenants.AppHost/Program.cs:141-143` — the precedent Folders copied itself does direct HTTP.
- `deploy/dapr/production/accesscontrol.yaml:119-160` — the allow-rule that never fires for a direct-HTTP client.

---

## Section 2 — Impact Analysis

| Area | Impact | Detail |
|------|--------|--------|
| **Epic** | None | Epic 10 unchanged; no new/removed/resequenced epics. Corrective refinement inside Story 10.5. |
| **PRD** | None | FR58 behavior is unchanged — only the transport path changes. |
| **Architecture** | **Yes** | §Query Facade "Read mechanism (Option B)" (line 172) drops the caveat and aligns with I-3/S-4 (which already claim Dapr governs this egress). |
| **UX** | None | Indexing-status console projection unaffected. |
| **Story 10.5** | Corrective note | Completion note (line 247) gets a dated correction; task-guidance lines 96/209 already anticipated the Dapr-invoke option. |
| **Code** | **Yes** | Server facade composes the sidecar-invoke base address (no client-code change; `MemoriesClient` composes `BaseAddress + api/search`). |
| **IaC / AppHost / deploy** | **None required** | Dapr injects `DAPR_HTTP_PORT`/`DAPR_HTTP_ENDPOINT` into every daprized app in dev + prod; the `folders` Server already has a sidecar (EventStore + pub/sub via `AddHexalithFolders`). Production `accesscontrol.yaml` already carries the allow-rule. |
| **Tests** | **Yes** | One new hermetic config-resolution test class pins the composed endpoint; the existing `DaprPolicyConformanceTests` now validate a policy the live client actually exercises. |
| **Governance** | Ledger close | `sprint-status.yaml:305-309` deferred-work item → resolved (route-through-sidecar branch). |

### Technical basis (why this is a small change)

The codebase's canonical idiom for routing a typed `HttpClient` through Dapr service invocation is **path-based**: `BaseAddress = {DAPR_HTTP_ENDPOINT}/v1.0/invoke/{appId}/method/`, calling relative paths. `Hexalith.Memories.Server` does exactly this for its own `eventstore` invoke (`MemoriesServerServiceCollectionExtensions.cs:85-87`); `Hexalith.Tenants.Api` composes the same `DAPR_HTTP_ENDPOINT ?? http://localhost:{DAPR_HTTP_PORT ?? 3500}`. Because `MemoriesClient` already issues `GetAsync("api/search")` relative to `BaseAddress`, pointing the base at `…/v1.0/invoke/memories/method/` yields `…/v1.0/invoke/memories/method/api/search` — a genuine Dapr invoke matched by the allow-rule's `operation: /api/search`. **Zero client-code change.**

---

## Section 3 — Recommended Approach

**Direct Adjustment (Option 1)** — modify Story 10.5's implementation + evidence in place. Rollback and MVP-review were not viable/warranted (nothing to revert; MVP scope untouched).

**Rationale for the route-through-sidecar branch over the revise-evidence branch:**

1. **Preserves a stated security invariant.** I-3/S-4 commit to Dapr deny-by-default + mTLS over the memories egress as a merge-blocked production control. The revise-evidence branch would *weaken* that invariant and reclassify a shipped conformance suite as testing-a-dormant-policy — a real reduction in the promised posture. The route-through-sidecar branch instead *realizes* the stated architecture; the direct-HTTP precedent (Tenants) is the outlier vs I-3, not the reference truth.
2. **Cheap and localized.** Server-side base-address composition + doc corrections + one hermetic test. No client rewrite, no AppHost/deploy base-address wiring (Dapr injects the port everywhere).
3. **Consistency.** Restores the memories read egress to the same Dapr envelope as `eventstore`/`tenants`.

**Effort:** Low. **Risk:** Low–Medium (security-invariant touch → explicit Architect review of the doc edit + the graceful-degradation nuance below).

**Trade-off accepted:** Folders' memories client now differs from the Tenants precedent (which stays direct-HTTP). If desired, a follow-up can push the same idiom upstream to `Hexalith.Tenants.UI` for parity — **out of scope here.**

**Residual (unchanged):** live `aspire run` proof of the through-sidecar invoke stays **BLOCKED-PENDING** the Epic 9 DCP/`--tls-cert-file` boot lane — the same blocker as 10.5's live round-trip and the epic-10 DCP ledger item (`sprint-status.yaml:285-289`). Local bar = build + the new hermetic test + the existing Dapr policy-conformance suite.

---

## Section 4 — Detailed Change Proposals (all approved, incremental mode)

### Edit 1 — Code (load-bearing): `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`

Compose the `MemoriesClientOptions.Endpoint` as the sidecar service-invocation endpoint; rewrite the XML-doc. An explicit absolute `Memories:BaseAddress` remains a direct-URL override (hermetic tests / non-Dapr hosting).

**NEW (config binding, replacing lines 92-97 + preceding XML-doc + a new const):**
```csharp
/// Registers the Story 10.5 authorized context-search facade (Option B). Adds the typed Memories search client
/// and routes its egress through THIS app's Dapr sidecar service-invocation API, so the production deny-by-default
/// <c>folders -&gt; memories</c> <c>GET /api/search</c> invoke allow-rule + mTLS are the operative network-layer
/// control (consistent with architecture I-3/S-4). Wires the live <see cref="MemoriesFolderSearchSource"/> as the
/// <see cref="IFolderSearchSource"/>; it degrades safely when the sidecar/Memories is unreachable. ...

// Stable Dapr app-id contract for the Memories search-index server (project-context #71).
private const string MemoriesDaprAppId = "memories";

// ... inside AddFoldersContextSearchFacade's Configure<IConfiguration> callback:
// Compose the base address as this sidecar's service-invocation endpoint; MemoriesClient issues
// `GET api/search` relative to it, yielding `.../v1.0/invoke/memories/method/api/search` — a genuine
// Dapr invoke governed by the deny-by-default allow-rule + mTLS. Mirrors Hexalith.Memories.Server's
// eventstore-invoke idiom. An explicit absolute `Memories:BaseAddress` still wins as a direct-URL
// override (hermetic tests / non-Dapr hosting).
if (Uri.TryCreate(configuration["Memories:BaseAddress"], UriKind.Absolute, out Uri? endpoint))
{
    options.Endpoint = endpoint;
}
else
{
    string daprHttpEndpoint = configuration["DAPR_HTTP_ENDPOINT"]
        ?? $"http://localhost:{configuration["DAPR_HTTP_PORT"] ?? "3500"}";
    options.Endpoint = new Uri(
        $"{daprHttpEndpoint.TrimEnd('/')}/v1.0/invoke/{MemoriesDaprAppId}/method/");
}
```

**Dev caveat (must-record for handoff):** graceful degradation still holds (unreachable sidecar → `HttpRequestException` → `Unavailable`), but the fail-safe trigger shifts from "no base address" to "connection refused." Any test wiring the *live* facade must inject a fake `HttpMessageHandler` behind `MemoriesFolderSearchSource` (as the AC11 no-mock-gateway test already does) rather than relying on an unset base address.

### Edit 2 — Code comment: `src/Hexalith.Folders.Server/ContextSearch/MemoriesFolderSearchSource.cs:170`

**NEW:**
```csharp
// Defensive: a malformed/misconfigured invoke endpoint surfaces as InvalidOperationException;
// degrade to unavailable rather than 500. Post-10.5 egress correction the base address is always
// composed (the Dapr `.../v1.0/invoke/memories/method/` sidecar URL, or an explicit
// `Memories:BaseAddress` direct-URL override), so "unset base address" is no longer the trigger.
InvalidOperationException => true,
```

### Edit 3 — Architecture: `_bmad-output/planning-artifacts/architecture.md:172` (§Query Facade → "Read mechanism (Option B)")

Replace the final sentence.

**NEW:**
> The `MemoriesClient` `HttpClient` base address is composed as the Folders **Dapr sidecar service-invocation endpoint** (`{DAPR_HTTP_ENDPOINT}/v1.0/invoke/memories/method/`, from the sidecar-injected `DAPR_HTTP_PORT`/`DAPR_HTTP_ENDPOINT`; `HEXALITH_MEMORIES_API_TOKEN` supplies the bearer, and an explicit absolute `Memories:BaseAddress` is a direct-URL override for hermetic tests / non-Dapr hosting). The search egress therefore traverses Dapr service invocation, so the production deny-by-default `folders → memories` `GET /api/search` invoke allow-rule + mTLS (§Egress policy, and I-3/S-4) are the **operative network-layer control** — not conditional. Folders-side layered authorization + security-trim remain the load-bearing *per-tenant* controls; Dapr is the coarse app-to-app + transport envelope, consistent with every other Folders egress (`eventstore`, `tenants`).

### Edit 4 — Story record: `10-5-…md:247` (Completion Notes → "Server gateway" bullet)

Replace the final "Note:" with a dated correction (10.5 is `done`; preserve the audit trail).

**NEW:**
> Note: `AddMemoriesClient`'s `HttpClient` base address is composed as the Folders **Dapr sidecar service-invocation endpoint** (`{DAPR_HTTP_ENDPOINT}/v1.0/invoke/memories/method/`, from the sidecar-injected `DAPR_HTTP_PORT`/`DAPR_HTTP_ENDPOINT`), so the search egress traverses Dapr and the production `folders → memories` invoke allow-rule + mTLS are the operative network-layer control (an explicit absolute `Memories:BaseAddress` remains a direct-URL override for hermetic tests). Recorded in architecture §Query Facade / I-3 / S-4. **[Corrected 2026-07-07 via bmad-correct-course: the original 10.5 impl used a direct base-address HttpClient that bypassed the sidecar, over-claiming Dapr enforcement; the facade now routes through Dapr service invocation so the shipped allow-rule + conformance suite govern a path the live client actually exercises.]**

### Edit 5 — New test: `tests/Hexalith.Folders.Server.Tests/FoldersContextSearchFacadeRegistrationTests.cs`

Four hermetic assertions (no network, no sidecar): composes from `DAPR_HTTP_PORT`; prefers `DAPR_HTTP_ENDPOINT`; safe default `:3500`; explicit `Memories:BaseAddress` override wins. Resolves only `IOptions<MemoriesClientOptions>`. Full body approved during incremental review (see the correct-course transcript / handoff). This is the regression guard: if the egress reverts to a Dapr-bypassing direct URL, tests 1–3 go red.

---

## Section 5 — Implementation Handoff

**Scope class: Minor.** Route to the **Developer agent** (`bmad-dev-story` / Amelia) for direct implementation, with **Winston (Architect)** signing off the architecture edit (Edit 3).

### Action items (ordered)

1. Apply **Edit 1** (`FoldersServerServiceCollectionExtensions.cs`) — base-address composition + XML-doc + `MemoriesDaprAppId` const.
2. Apply **Edit 2** (`MemoriesFolderSearchSource.cs:170` comment).
3. Apply **Edit 3** (`architecture.md:172`) — Winston sign-off.
4. Apply **Edit 4** (`10-5-…md:247` dated correction).
5. Add **Edit 5** (new `FoldersContextSearchFacadeRegistrationTests.cs`).
6. Mark the deferred-work ledger item resolved: `sprint-status.yaml:305-309` → `status: done` with a comment referencing this proposal and the route-through-sidecar branch.

### Verification (local bar — all hermetic)

- `dotnet build Hexalith.Folders.slnx` — clean (warnings-as-errors).
- `dotnet test tests/Hexalith.Folders.Server.Tests` — incl. the new `FoldersContextSearchFacadeRegistrationTests` (4 green) and unchanged `MemoriesFolderSearchSourceTests`.
- `dotnet test tests/Hexalith.Folders.Contracts.Tests` — `DaprPolicyConformanceTests` still green (allow-rule + negative controls; no fixture change needed — the rule already exists).
- `dotnet format` whitespace/analyzers — clean.

### Success criteria

- Facade egress composes to `…/v1.0/invoke/memories/method/api/search` (pinned by Edit 5).
- Architecture §Query Facade is internally consistent with I-3/S-4 (no conditional caveat).
- Graceful degradation preserved (unreachable sidecar → `Unavailable`, never 500).
- Deferred-work ledger item closed.

### Known blocker (carried, not introduced)

Live `aspire run` end-to-end proof of the through-sidecar invoke remains **BLOCKED-PENDING** the DCP-capable AppHost lane (Epic 9 residual / `sprint-status.yaml:285-289`, `320-324`). This proposal does not add a new blocker class — it extends the same blocked-pending scope already owned by 10.5's live round-trip.
