# Sprint Change Proposal — Use Hexalith.McpCli instead of a Folders-specific CLI/MCP (2026-11-10)

**Status:** Accepted 2026-11-10 by Jerome and applied (uncommitted). Edit proposals 1–7 were approved individually (incremental review), followed by final approval of the complete proposal and of the FR48/FR49 epics mirror (Section 6).
**Trigger:** Jerome's direction of 2026-11-10: "use common Hexalith McpCli module instead of a mcp/cli specific to folders".
**Mode:** Incremental. **Scope class:** Moderate (re-scoped and new backlog stories, two external dependency nodes, cross-repository handoff; no production code).
**Baseline:** Folders `ff915ec8` (main, clean). McpCli reference `c1313a6ab4a35ffd3fc6d76d8ec4f86f77270e62`. Builds catalog `HexalithMcpCliVersion` 2.0.1.

## Decisions recorded

| ID | Question | Decision |
| --- | --- | --- |
| D1 | Disposition of `Hexalith.Folders.Cli` and `Hexalith.Folders.Mcp` | **Freeze, then retire.** Both stay frozen compatibility binaries. They receive safety and continuity fixes only, including the Story 1.17 v2 migration. A Folders story deletes them after the McpCli replacement passes parity. |
| D2 | How the MVP CLI and MCP cells count | **McpCli is required for MVP.** The four-surface MVP stays. CLI and MCP cells count only when McpCli serves them after Folders enrollment. MVP release therefore depends on McpCli. |
| D3 | Story structure | **Re-scope inside Epic 5.** Stories 5.8–5.11 keep their identities and ranks and are re-scoped to McpCli enrollment slices. Foundation Stories 5.12–5.14 and retirement Story 5.15 are added. |

## 1. Issue summary

The Hexalith baseline (`references/Hexalith.AI.Tools/hexalith-llm-instructions.md`, "Hexalith MCP and CLI ownership") makes `Hexalith.McpCli` the target Hexalith-owned MCP server and CLI. It forbids new per-module MCP servers and CLIs. Migration plan 1.1.0 (`hexalith-mcpcli-migration-instructions.md`) names `Hexalith.Folders.Mcp` and `Hexalith.Folders.Cli` as frozen compatibility implementations, limited to safety and continuity fixes.

The 2026-09-27 correction (`fb551174`) added banners to the PRD, architecture and epics, re-pointed FR48/FR49 and relabelled decisions A-4/A-5. It changed no executable backlog. The plan still works against the frozen adapters:

| Evidence | Location |
| --- | --- |
| Stories 5.8–5.11 are deployed-evidence work on the Folders CLI and MCP server, at ranks 42/43 | `epics.md` Stories 5.8–5.11; `planning-story-manifest.yaml` rows 5.8–5.11 |
| A draft spec for Story 5.8 targets the frozen CLI | `_bmad-output/implementation-artifacts/spec-5-8-cli-workspace-preparation-and-lock-lifecycle.md` (`status: draft`) |
| The Epic 12 action item HXF-MCP-001 asks for new features on the frozen MCP server | `sprint-status.yaml` action item "Verification honesty (Murat)" |
| No Folders story owns the McpCli prerequisites G1–G6 (decorated Contracts, `/process` admission, Gateway authorization, query handlers, metadata-only output, provider egress) | McpCli Folders inventory 1.0.0, "Gateway prerequisites and owners" |
| McpCli plans no Folders enrollment, parity or retirement story; only Story 5.8 (inventory re-disposition) exists | `references/Hexalith.McpCli/_bmad-output/planning-artifacts/epics.md` Epic 5 |
| The PRD and architecture still say "CLI and MCP wrap the SDK". McpCli instead dispatches decorated Contracts through the EventStore Gateway, which bypasses Folders REST authorization. | `prd.md:238`, `:665`, `:1031`; `architecture.md:615`, `:1644–1645` |
| `Hexalith.Folders.Contracts` has no package reference and no decorated type | `src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj` |
| Story 11.6's spec is `done` (`4403ef17`), but the tracker and manifest still show `backlog`/`held` behind 1.17 | `spec-11-6-…md`; `sprint-status.yaml`; manifest row 11.6 |

**Category:** Strategic pivot (platform consolidation). No story failed.

## 2. Impact analysis

| Area | Impact |
| --- | --- |
| Epic 5 | Cannot complete as planned. Stories 5.8–5.11 are re-scoped. Stories 5.12–5.15 are added. Stories 5.1–5.7 stay `done` and are preserved as historical compatibility evidence. |
| Epic 1 | No change. Story 1.17's CLI/MCP v2 migration counts as continuity work under the freeze, and its bound bytes stay untouched. Story 5.12 waits for 1.17. |
| Epic 4 | No change. Stories 4.19–4.21 still supply the durable paths that 5.8 and 5.9 prove. |
| Epics 7 and 8 | No change. Story 7.13 (consumer references) and Story 8.3 (four-surface claim) stay done as history. Story 5.15 updates the consumer references at retirement. |
| Epic 11 | Story 11.6 gets a frozen-adapter scope note; its ACs are unchanged. Story 11.10 becomes a prerequisite of 5.13. Story 11.13 is deliberately not used for the retirement. |
| Epic 12 | The HXF-MCP-001 action item is redirected to McpCli and Stories 5.12/5.14. Stories 12.1, 12.2, 12.3 and 12.6 become prerequisites of the new stories. |
| Epic 13 | No change. Legacy CLI credential-file hardening (HXF-SEC-004) remains a permitted safety fix. |
| PRD | A banner and four clarifying sentences (E1–E6). **No FR bullet is edited**, so `epics.md` mirroring holds. **No hash-pinned NFR1–NFR73 bullet is edited**: the banner states how the NFR bullets that name CLI/MCP are read. MVP scope is unchanged, and its dependency on McpCli is now explicit. |
| Architecture | Banner, new decision A-12, Contracts boundary, API boundaries, Adapter Parity Contract scope, C13 `mcpcli_operation`, FR48/FR49 mapping (A1–A7). |
| UX | Not applicable. The console is unaffected. |
| Manifest | Four rows retitled and re-edged; four rows added; two pending external nodes; new waves; FR owners; counts 159→163; V2-003; a 2026-11-10 amendment. |
| Tracker | Four keys renamed and four added (all `backlog`); one action item reworded. Frozen evidence that embeds the old keys is not rewritten. |
| Code, CI, packages | No change now. Story 5.15 later removes the legacy projects, tests, shared adapter core, `.slnx` entries, NuGet release/trusted-publishing entries, CI references and pinned inventory gates in one lockstep change. |
| Cross-repository | McpCli must add Folders enrollment, parity and retirement-verification work, and Story 5.2 must cover the Folders non-gateway classes. See Section 5. |

## 3. Recommended approach — Direct adjustment

- **Option 1, direct adjustment: viable (selected).** Effort medium, risk medium. The main risk is the cross-repository dependency: MVP now waits on McpCli enrolling Folders.
- **Option 2, rollback: not viable.** The completed adapter Stories 5.2–5.7 are valid compatibility evidence. Deleting the adapters now is the "withdraw now" option that D1 rejected.
- **Option 3, MVP review: not needed.** D2 keeps the four-surface MVP and changes only which binaries qualify.

Rationale:
- Re-scoping 5.8–5.11 keeps their ranks and durable-path prerequisites, so the execution graph barely moves.
- The new foundation stories turn McpCli's Folders-owned gaps G1–G5 into Folders backlog items. Before this, neither repository owned them.
- Story 5.12 can start as soon as Story 1.17 closes, so McpCli Story 5.8 can re-disposition rows against named decorated types. McpCli counts a candidate as a replacement only once its type is named.

Risks and mitigations:
- **MVP slip from McpCli work:** both McpCli dependencies are graph nodes (EXT-MCPCLI-FOLDERS-ENROLLMENT, EXT-MCPCLI-FOLDERS-RETIREMENT-GATE), not prose, so readiness checks surface them.
- **A core-lifecycle variant McpCli cannot carry,** for example staged content above 262144 bytes: E4 and Story 5.9 require an inventory `defer` plus a product-approved C13 declaration. That deliberately blocks MVP rather than falling back to a legacy binary.
- **Authorization bypass through the Gateway:** E6, A-12 and Story 5.13 make domain-service enforcement equivalent to REST a precondition of any enrollment.
- **Two contract definitions drifting (OpenAPI Spine vs decorated types):** Story 5.12 adds a drift gate.

## 4. Detailed change proposals

### 4.1 PRD (`_bmad-output/planning-artifacts/prd.md`) — approved

**E1 — Banner, line 123 (replace).**

OLD:
> **Approved McpCli course correction (2026-09-27):** `Hexalith.McpCli` is the target Hexalith-owned CLI/MCP surface for Folders operations that pass contract enrollment and authorization. Any proprietary module CLI, MCP host, plug-in, or planned adapter described below is an obsolete migration source or historical design, not a new target. The module retains its domain, UI, and security semantics; replacement or approved withdrawal and parity evidence precede retirement. External development CLIs are unaffected.

NEW:
> **Approved McpCli course correction (2026-09-27, extended 2026-11-10):** `Hexalith.McpCli` is the only target Hexalith-owned CLI/MCP surface for Folders. Its CLI and MCP heads dispatch decorated `Hexalith.Folders.Contracts` operations through the EventStore Gateway; they do not wrap the Folders SDK or REST API. Wherever this PRD requires CLI or MCP, the release-qualifying surface is the corresponding McpCli head for operations enrolled under an owner-approved Folders inventory. `Hexalith.Folders.Cli` and `Hexalith.Folders.Mcp` are frozen compatibility binaries: they receive only safety and continuity fixes (including the Story 1.17 v2 migration), never new operations or release evidence, and retire after an approved replacement or withdrawal plus parity evidence. Hash-pinned NFR bullets that name CLI and MCP are read the same way and are not edited. The module retains its domain, UI, and security semantics. External development CLIs are unaffected.

**E2 — Project Type, line 147.**
OLD: `**Project Type:** API/backend service module with a generated .NET (C#) SDK and supporting CLI and MCP surfaces.`
NEW: `**Project Type:** API/backend service module with a generated .NET (C#) SDK, decorated McpCli Contracts, and CLI/MCP access through Hexalith.McpCli.`

**E3 — MVP scope, line 238.**
OLD: `The MVP includes the Contract Spine, REST transport, generated SDK, CLI, and MCP for the core lifecycle, plus the diagnostic console. CLI and MCP must preserve the generated SDK and Contract Spine semantics rather than creating independent models.`
NEW: `The MVP includes the Contract Spine, REST transport, generated SDK, and the Hexalith.McpCli CLI and MCP heads over enrolled Folders Contracts for the core lifecycle, plus the diagnostic console. MVP release therefore depends on McpCli enrolling the Folders Contracts. Decorated Contracts must preserve Contract Spine semantics rather than creating independent models; the frozen Folders CLI and MCP binaries do not satisfy MVP CLI or MCP cells.`

**E4 — C13 paragraph, line 433 (append).**
ADD: `CLI and MCP cells are satisfied only by the McpCli heads, for an operation whose owner-approved Folders inventory row is replace / include with a named decorated Contracts type; an operation McpCli cannot carry needs an inventory withdraw or defer decision plus a product-approved C13 declaration, never a legacy-binary fallback.`
The existing rule "core lifecycle and tenant-administration contract operations are required on all four surfaces" stays, so deferring a core operation blocks MVP.

**E5 — SDK section, line 665.**
OLD: `CLI and MCP consume this SDK and may not implement independent lifecycle behavior.`
NEW: `The McpCli heads do not consume this SDK; they dispatch decorated Folders Contracts through the EventStore Gateway, and those Contracts may not define lifecycle behavior independent of the Contract Spine. The frozen Folders CLI and MCP binaries keep consuming this SDK until they retire.`

**E6 — Implementation constraint, line 713.**
OLD: `Implementation must avoid separate business-logic paths for API, CLI, MCP, and SDK. Shared application services or generated client contracts define canonical behavior; each surface adapts transport and presentation only.`
NEW: `Implementation must avoid separate business-logic paths for API, CLI, MCP, and SDK. Shared application services, generated client contracts, and the Folders domain service behind the EventStore Gateway define canonical behavior; authorization, idempotency, and metadata-only rules hold identically for REST requests and Gateway-dispatched McpCli operations. Each surface adapts transport and presentation only.`

### 4.2 Architecture (`_bmad-output/planning-artifacts/architecture.md`) — approved

**A1 — Banner, line 51 (replace).**
> **Approved McpCli course correction (2026-09-27, extended 2026-11-10):** `Hexalith.McpCli` is the only target Hexalith-owned CLI/MCP surface for Folders; decision A-12 defines the enrollment architecture. Any proprietary module CLI, MCP host, plug-in, or planned adapter described below is a frozen compatibility surface or historical design. That includes `Hexalith.Folders.Cli`, `Hexalith.Folders.Mcp`, `src/shared/AdapterCore`, and the CLI/MCP columns of §"Adapter Parity Contract". These receive safety and continuity fixes only and are never a template or a release-qualifying surface. Replacement or approved withdrawal plus parity evidence precede retirement (Story 5.15). External development CLIs are unaffected.

**A2 — New decision row after A-11 (API & Communication Patterns table).**
> `| A-12 | Hexalith CLI/MCP surface (2026-11-10) | **Hexalith.McpCli CLI and MCP heads over decorated Hexalith.Folders.Contracts.** Contracts declares [assembly: HexalithModule("folders", …)] and one decorated public Command or Query type per owner-approved inventory candidate, referencing only Hexalith.McpCli.Abstractions (versionless; HexalithMcpCliVersion from the Builds catalog). Each Command maps explicitly to its existing Hexalith.Folders.Commands.* wire name. Each Query routes to a registered Gateway query handler (IDomainQueryHandler). McpCli sends operations to the EventStore Gateway, which routes them to the Folders domain service. The domain service therefore enforces the same authority, layered ACL, deny-before-access, A-9 idempotency, and metadata-only rules as REST (inventory gaps G1–G6). McpCli owns CLI exit codes, MCP result shapes, error documents, and profiles; Folders owns operation semantics and the C13 mapping. A drift gate fails when a decorated type diverges from the Contract Spine operation it maps to. | One generic Hexalith CLI/MCP (AI.Tools ownership rule, migration plan 1.1.0); removes per-module adapter upkeep and the SDK-wrapping parity layer | Keep and extend Hexalith.Folders.Cli/.Mcp (forbidden for new work by the ownership rule); a Folders-specific McpCli adapter (forbidden by the plan's no-module-adapter rule) |`

**A3 — Component boundary, `Hexalith.Folders.Contracts`.**
OLD: `**Hexalith.Folders.Contracts** owns the C0 Contract Spine (OpenAPI 3.1 + extension vocabulary) and all DTOs. NO behavior; no Hexalith.EventStore reference.`
NEW: `**Hexalith.Folders.Contracts** owns the C0 Contract Spine (OpenAPI 3.1 + extension vocabulary) and all DTOs, including the decorated McpCli Command and Query types (A-12). NO behavior; no Hexalith.EventStore reference; its only package reference is Hexalith.McpCli.Abstractions, which has no consumer-visible dependencies.`

**A4 — API Boundaries (replace the "MCP tools surface" and "CLI surface" bullets).**
> - **CLI and MCP surface (target):** the `Hexalith.McpCli` heads list and run the decorated `Hexalith.Folders.Contracts` operations through the EventStore Gateway (A-12); Folders ships no CLI or MCP host for them.
> - **Legacy compatibility surfaces (frozen):** `Hexalith.Folders.Mcp.Tools.*` and `Hexalith.Folders.Cli.Commands.*` wrap `Hexalith.Folders.Client`; safety and continuity fixes only until Story 5.15 retires them.

**A5 — §"Adapter Parity Contract" (append after the intro paragraph).**
> **Scope (2026-11-10):** the CLI and MCP columns describe the frozen legacy adapters only. For McpCli heads, behavioral parity is proven two ways. McpCli's enrollment gates provide loopback request/result parity plus live persisted semantic proof for each included operation. The C13 mapping links each Folders canonical category, code, retryable flag and client action to the McpCli error document. McpCli owns its exit codes and result shapes.

**A6 — C13 row, line 382 (append).**
> **McpCli mapping (2026-11-10):** each row also records `mcpcli_operation` (canonical name such as `folders.create-folder`, decorated CLR type, inventory disposition). CLI/MCP cells are satisfied only by McpCli heads. `cli_exit_code` and `mcp_failure_kind` stay legacy-adapter columns until Story 5.15 removes them.

**A7 — FR mapping rows.**
- FR48 NEW: `| **FR48** CLI parity | decorated types in src/Hexalith.Folders.Contracts/ (A-12); McpCli CLI-head enrollment and parity evidence; legacy src/Hexalith.Folders.Cli/ frozen until Story 5.15 |`
- FR49 NEW: `| **FR49** MCP parity | decorated types in src/Hexalith.Folders.Contracts/ (A-12); McpCli MCP-head enrollment and parity evidence; legacy src/Hexalith.Folders.Mcp/ frozen until Story 5.15 |`

The source tree, Phase 7 and bootstrap sections are covered by A1 and stay as historical text.

### 4.3 Epics (`_bmad-output/planning-artifacts/epics.md`) — approved

**S1 — Banner, line 24 (replace).**
> **Approved McpCli course correction (2026-09-27, extended 2026-11-10).** Stories 5.2–5.7, 7.13, and 8.3 that name Folders CLI/MCP adapters stay done as historical compatibility evidence. Story 11.6 is safety work on the frozen adapters. Stories 5.8–5.11 are re-scoped to McpCli enrollment slices, and Stories 5.12–5.15 are added (`sprint-change-proposal-2026-11-10.md`). Preserve C13 lifecycle, authorization, idempotency, status, error, and audit behavior. McpCli owns the generic heads, the Folders inventory re-disposition, and its own enrollment gates. Folders owns the decorated Contracts, domain-service enforcement, and retirement of its legacy binaries.

**S2 — Epic 5 intro (add after "Parity is verified…"; the FR line is unchanged).**
> **McpCli target (2026-11-10).** CLI and MCP users reach Folders through the `Hexalith.McpCli` heads.
> - **Stories 5.12–5.14** build the Folders side of enrollment: decorated Contracts, Gateway admission and authorization, and Gateway query handlers.
> - **Stories 5.8–5.11** prove the enrolled lifecycle through both McpCli heads.
> - **Story 5.15** retires `Hexalith.Folders.Cli` and `Hexalith.Folders.Mcp`.
>
> Evidence from the legacy binaries never satisfies a story in this epic after 2026-11-10.

**S3 — Story 5.8 (replace whole story).**

> ### Story 5.8: Enroll workspace preparation and lock lifecycle through McpCli
>
> **Requirements:** FR41–FR44, FR48, FR49, FR51
>
> As a CLI user or AI agent using `Hexalith.McpCli`,
> I want to prepare a workspace and inspect, acquire, and release its task-scoped lock through either McpCli head,
> So that scripted and agent work uses the same durable lifecycle and lock semantics as REST and SDK.
>
> **Acceptance Criteria:**
>
> **Given** Story 4.19's durable prepare/lock path is deployed, McpCli has enrolled the Folders Contracts (EXT-MCPCLI-FOLDERS-ENROLLMENT), and the caller has current tenant/folder authorization
> **When** `folders.prepare-workspace`, `folders.lock-workspace`, `folders.release-workspace-lock` and the workspace lock, status, retry-eligibility, transition-evidence and cleanup-status queries run through the McpCli CLI and MCP heads
> **Then** operation identity, exact lock states, C6 lifecycle, idempotency, canonical category/code/retryable/clientAction, and metadata-only output match the C13 oracle and the REST/SDK results for the same inputs
> **And** success, denial/wrong-tenant, equivalent/conflicting replay, lock collision/expiry, failure, timeout/unknown, and input boundaries are verified against real deployed composition with asserted state-store end-state
> **And** fake, seed-only, unavailable, safe-empty, in-memory, or legacy Folders CLI/MCP behavior cannot prove completion.

**S4 — Story 5.9 (replace whole story).**

> ### Story 5.9: Enroll file, context, commit, status, error, and audit operations through McpCli
>
> **Requirements:** FR41–FR44, FR48, FR49, FR51
>
> As a CLI user or AI agent using `Hexalith.McpCli`,
> I want to mutate files, query bounded context, commit, inspect status, and review safe audit evidence through either McpCli head,
> So that the complete agent workflow keeps canonical behavior and security without a Folders-specific tool.
>
> **Acceptance Criteria:**
>
> **Given** Stories 4.20, 4.21, and 12.6 production paths are available and McpCli has enrolled the Folders Contracts
> **When** `folders.mutate-files`, `folders.commit-workspace`, and the context, commit-evidence, provider-outcome, reconciliation, task-status and audit queries run through both McpCli heads
> **Then** C4 bounds, lock and all-mutation idempotency rules, canonical errors, terminal/recovery states, retry eligibility, metadata-only output, and parity-oracle semantics are preserved without exposing content or secrets outside authorized response rules
> **And** inline content up to the inclusive 262144-byte decoded boundary is proven. Larger content uses the staging contract approved by McpCli Story 5.2, or stays an inventory `defer` with a product-approved C13 declaration.
> **And** deployed success plus denial, conflict, provider failure, timeout/unknown reconciliation, cancellation/size/path boundaries, tenant isolation, and metadata-only audit are proven
> **And** NoOp, mock-only, seed, unavailable, safe-empty, fake, or legacy Folders CLI/MCP evidence cannot satisfy completion.

**S5 — Story 5.10 (replace whole story).**

> ### Story 5.10: Enroll tenant, folder, ACL, provider-readiness, and binding operations through McpCli
>
> **Requirements:** FR48, FR49, FR51
>
> As an operator or AI agent using `Hexalith.McpCli`,
> I want to create, bind, archive, and inspect folders, manage folder ACL entries, and configure and check provider bindings and branch policy through either McpCli head,
> So that setup work needs no Folders-specific tool.
>
> **Acceptance Criteria:**
>
> **Given** the durable folder repository (Story 12.1) is deployed and McpCli has enrolled the Folders Contracts
> **When** the create-folder, create-repository-backed-folder, bind-repository, archive-folder, grant/revoke-folder-access, configure-provider-binding and configure-branch-ref-policy operations and their read queries run through both McpCli heads
> **Then** results, canonical errors, idempotency, and metadata-only output match the C13 oracle and REST/SDK for the same inputs, and grant and revoke stay distinct operations (inventory V08)
> **And** provider binding accepts only credential references, never raw secrets. It preserves reserved `ProviderBindingActorId` routing and tenant/namespace separation, and authorizes before any provider egress (inventory G1/C05, G6).
> **And** cross-tenant, unauthorized, revoked, and replayed requests fail closed with zero mutation, proven by persisted state-store end-state.

**S6 — Story 5.11 (replace whole story).**

> ### Story 5.11: Prove four-surface parity and mixed-surface handoff through McpCli
>
> **Requirements:** FR47–FR51
>
> As a release owner,
> I want the C13 oracle to report zero material deltas across REST, SDK, and both McpCli heads, and a task handed between REST and McpCli to keep one identity,
> So that the four-surface MVP claim rests on McpCli evidence alone.
>
> **Acceptance Criteria:**
>
> **Given** Stories 5.8–5.10 are done
> **When** the C13 conformance suite and the SM1 calibration scenarios run for GitHub and Forgejo
> **Then** every core-lifecycle and tenant-administration cell passes on REST, SDK, McpCli CLI, and McpCli MCP, and every other cell carries an inventory `withdraw` or `defer` plus a product-approved C13 declaration
> **And** a task started through REST or SDK and continued through either McpCli head, or the reverse, keeps the same task identity, lock ownership, idempotency outcomes, and audit trail (UJ7)
> **And** the evidence binds the exact McpCli version, Folders Contracts package version, Catalog digest, environment, and vectors by hash. It passes the secret scan, and the Folders owner approves it as input to McpCli's Folders retirement gate.
> **And** legacy Folders CLI/MCP results are not counted.

**S7 — New Story 5.12 (append to Epic 5).**

> ### Story 5.12: Declare Folders operations in decorated McpCli Contracts
>
> **Requirements:** FR48, FR49 (enabler); architecture A-12; McpCli Folders inventory G1
>
> As a Folders Contracts maintainer,
> I want each owner-approved Folders operation declared as a decorated public Command or Query type in `Hexalith.Folders.Contracts`,
> So that McpCli can discover and validate Folders operations without importing Folders code or adding an adapter.
>
> **Acceptance Criteria:**
>
> **Given** the McpCli Folders inventory 1.0.0 candidates C01–C13 and Q01–Q35 and the v2 Contract Spine (Story 1.17)
> **When** the Folders owner records agent eligibility for each candidate
> **Then** each eligible candidate has exactly one decorated public type, one type per file, under `Hexalith.Folders.Contracts.Commands` or `.Queries`. Each type has a nonblank description, `Domain = "folders"`, aggregate routing, and `[HexalithIdentifier]` properties.
> **And** the eligibility decisions and every accepted behavior loss are recorded as the Folders owner's input to McpCli Story 5.8. The losses to decide are V04 local-file input, V07 `--allow-auto-key`, V09 credential precedence, and the R01/R02 resources.
>
> **Given** `[assembly: HexalithModule("folders", …)]`
> **When** its identifier kind is chosen
> **Then** existing folder, workspace, task, and provider-binding identifiers are proven to round-trip under it with no silent conversion, and any format it cannot accept is a named gap.
>
> **Given** each decorated Command
> **When** its wire type is resolved
> **Then** it maps explicitly to the existing `Hexalith.Folders.Commands.*` wire name, never a derived `Contracts.Commands.*` name, and a test pins every mapping.
>
> **Given** a Release build with TreatWarningsAsErrors
> **When** the Contracts project builds and packs
> **Then** `Hexalith.McpCli.Abstractions` (versionless, Builds `HexalithMcpCliVersion`) is its only package reference, MCPCLI001 reports nothing, and the packed Contracts closure passes McpCli's AD-15 package/closure gate
> **And** a drift gate fails when a decorated type's fields, required flags, or bounds diverge from the Contract Spine operation it maps to
> **And** no Folders CLI, MCP host, or McpCli adapter code is added.

**S8 — New Story 5.13.**

> ### Story 5.13: Admit and authorize Gateway-dispatched Folders commands
>
> **Requirements:** FR48, FR49 (enabler); architecture A-9, A-12; McpCli Folders inventory G2, G3, V03, V07
>
> As a Folders security maintainer,
> I want commands that reach the domain service through the EventStore Gateway from McpCli to pass the same admission and authorization as REST,
> So that McpCli callers cannot reach data or effects that REST would deny.
>
> **Acceptance Criteria:**
>
> **Given** McpCli envelopes carrying canonical uppercase ULID message and correlation IDs
> **When** `/process` admits a decorated Folders command
> **Then** admission accepts them without lowercasing aggregate IDs or weakening McpCli rules, and keeps the caller idempotency key independent of the generated message ID
> **And** replay, changed-input conflict, expiry, and task association behave exactly as A-9 specifies, through admission and persisted effects.
>
> **Given** task and workspace context carried as Payload properties or allowed Envelope extensions
> **When** a mutation or task-required read runs
> **Then** the context is enforced as REST enforces it, and a missing or mismatched task fails closed.
>
> **Given** envelope tenant and actor values
> **When** a command is authorized
> **Then** they serve only as comparison inputs against verified authority. Layered folder/action ACL, revocation, task/workspace/lease ownership, and deny-before-access give the same PD10 v2 safe-denial outcomes as REST.
>
> **Given** cross-tenant, unauthorized, revoked, or forged-tenant commands dispatched through the Gateway
> **When** they are processed
> **Then** they disclose no protected data and cause no mutation, proven by state-store end-state assertions.

**S9 — New Story 5.14.**

> ### Story 5.14: Serve Folders reads through Gateway query handlers
>
> **Requirements:** FR48, FR49 (enabler); architecture A-12; McpCli Folders inventory G4, G5, V01, V02, V06
>
> As a Folders query maintainer,
> I want every agent-eligible Folders read served by a registered Gateway query handler with metadata-only results,
> So that McpCli reads return the same authorized, bounded data as REST.
>
> **Acceptance Criteria:**
>
> **Given** each eligible decorated Query sent by McpCli through the Gateway
> **When** it is dispatched
> **Then** a registered `IDomainQueryHandler` serves it from durable projections, and `/project` is never used as a query dispatcher.
>
> **Given** the legacy per-operation freshness (V01) and paging/filter inputs (V02)
> **When** a query arrives
> **Then** each operation applies its single freshness value, payload-versus-envelope paging precedence is defined, and conflicting or unsupported combinations are rejected rather than silently dropped.
>
> **Given** any result or error leaving the domain service
> **When** it is serialized
> **Then** content bytes, inline content, stream descriptors, secrets, and unauthorized paths are absent, and authorization ran before any read.
>
> **Given** an authorized content read-back request (Epic 12 action item HXF-MCP-001)
> **When** it is requested
> **Then** it is served as a decorated Query within C4 bounds, or stays an inventory `defer` with a product-approved C13 declaration
> **And** cross-tenant and unauthorized reads return the safe denial and no protected data.

**S10 — New Story 5.15.**

> ### Story 5.15: Retire Hexalith.Folders.Cli and Hexalith.Folders.Mcp
>
> **Requirements:** FR48, FR49, FR51 (retirement clauses of FR48/FR49)
>
> As the Folders owner,
> I want the legacy Folders CLI and MCP server removed from source, packages, and documentation once McpCli replaces them,
> So that McpCli is the only supported Folders CLI/MCP path and no stale binary is mistaken for a supported surface.
>
> **Acceptance Criteria:**
>
> **Given** an owner-approved McpCli Folders inventory version in which every legacy tool, resource, CLI leaf, and variant is `replace` (with passing Story 5.11 evidence) or `withdraw` (EXT-MCPCLI-FOLDERS-RETIREMENT-GATE)
> **When** the legacy surfaces retire
> **Then** one lockstep change removes or updates all of the following:
> - `src/Hexalith.Folders.Cli`, `src/Hexalith.Folders.Mcp` and `src/shared/AdapterCore`, plus their test projects;
> - their entries in both `.slnx` files, `deploy/nuget/release-packages.yaml`, `deploy/nuget/trusted-publishing-policy.yaml` and the CI workflows;
> - the pinned inventory gates: the `ScaffoldContractTests` solution pins and the PD10 conformance-set path bindings.
>
> **And** the C13 `cli_exit_code`/`mcp_failure_kind` columns and the parity-oracle CLI/MCP consumers are removed, while C13 keeps `mcpcli_operation`.
>
> **Given** the README, the Story 7.13 consumer references, and the operator documentation
> **When** they are updated
> **Then** they direct CLI/MCP users to McpCli profile setup, and the README "Legacy compatibility surfaces" section records the retirement, the last published legacy package versions, and the rollback path to them
> **And** the published `Hexalith.Folders.Cli` and `Hexalith.Folders.Mcp` package IDs are marked deprecated on NuGet with McpCli as the alternative.
>
> **Given** a release candidate that still builds, packs, or documents a removed surface
> **When** retirement is verified
> **Then** verification fails and names that surface
> **And** completed Story 5.2–5.7 evidence and lifecycle history are preserved, not rewritten.

**S11 — Story 11.6 (append after its ACs; ACs unchanged).**
> **Frozen-adapter scope (2026-11-10).** This story is safety and continuity work on the frozen legacy adapters. The bearer-transport guard (HXF-SEC-001) and the shared adapter core landed in `4403ef17`; no further adapter-core work is planned, and McpCli carries its own transport safety. Closure stays gated on Story 1.17, as the 11.6 spec records. Story 5.15 deletes the shared adapter core together with the legacy binaries.

### 4.4 Execution manifest (`_bmad-output/planning-artifacts/planning-story-manifest.yaml`) — approved

**M1 — Story rows.** Every row is classification `product`. New rows are `backlog`, `decision_status: not-applicable`, `execution_authorization_status: held`, `delivery_evidence_status: open`, with `authoritative_definition: reconciled-epics-anchor:_bmad-output/planning-artifacts/epics.md#Story-5.x`.

| Story | Title | Prerequisites (exact) | Rank | Requirement clauses | Evidence posture |
| --- | --- | --- | --- | --- | --- |
| 5.8 | Enroll workspace preparation and lock lifecycle through McpCli | DEC-A8-HOLD, 4.19, EXT-MCPCLI-FOLDERS-ENROLLMENT | 42 | unchanged | unchanged |
| 5.9 | Enroll file, context, commit, status, error, and audit operations through McpCli | DEC-A8-HOLD, 4.20, 4.21, 12.6, EXT-MCPCLI-FOLDERS-ENROLLMENT | 43 | unchanged | unchanged |
| 5.10 | Enroll tenant, folder, ACL, provider-readiness, and binding operations through McpCli | DEC-A8-HOLD, 12.1, EXT-MCPCLI-FOLDERS-ENROLLMENT | 42 | unchanged | unchanged |
| 5.11 | Prove four-surface parity and mixed-surface handoff through McpCli | DEC-A8-HOLD, 5.8, 5.9, 5.10 | 44 | unchanged | unchanged |
| 5.12 | Declare Folders operations in decorated McpCli Contracts | DEC-A8-HOLD, 1.17 | 11 | FR48, FR49 | open-enabler-or-release-evidence-required |
| 5.13 | Admit and authorize Gateway-dispatched Folders commands | DEC-A8-HOLD, 5.12, 11.10, 12.1, 12.6 | 22 | FR48, FR49 | open-real-production-evidence-required |
| 5.14 | Serve Folders reads through Gateway query handlers | DEC-A8-HOLD, 5.12, 5.13, 12.2, 12.3 | 23 | FR48, FR49 | open-real-production-evidence-required |
| 5.15 | Retire Hexalith.Folders.Cli and Hexalith.Folders.Mcp | DEC-A8-HOLD, 5.11, EXT-MCPCLI-FOLDERS-RETIREMENT-GATE | 46 | FR48, FR49, FR51 | open-enabler-or-release-evidence-required |

Story 5.10 drops 4.19: its setup operations need no workspace lifecycle. Story 5.11 replaces 4.20, 4.21 and 12.6 with 5.8–5.10, which still carry them transitively.

**M2 — External nodes (new).** Both nodes share these settings:
- `node_type: external-platform-release`
- `repository: references/Hexalith.McpCli`
- `repository_owner: Hexalith.McpCli maintainers`
- `accountable_role: McpCli maintainer (single-maintainer model)`
- `observed_repository_commit: c1313a6ab4a35ffd3fc6d76d8ec4f86f77270e62`
- `release_version_or_commit: pending-platform-publication`
- `acceptance_status: pending`, `decision_status: pending`, `execution_authorization_status: external-owner-controlled`, `delivery_evidence_status: pending`
- `placeholder_acceptance_forbidden: true`

- **EXT-MCPCLI-FOLDERS-ENROLLMENT.**
  - Rank 24; prerequisites 5.12, 5.13, 5.14.
  - `release_identifier: hexalith-mcpcli-folders-enrollment-v1`.
  - Source work:
    - McpCli Story 5.2 (Decide Generic Non-Gateway Capability Contracts);
    - McpCli Story 5.8 (Refresh and Re-disposition the Folders Inventory);
    - a McpCli Folders enrollment story still to be added (request R3).
  - Required evidence:
    - an owner-approved Folders inventory version naming a decorated type for every `replace` row;
    - a McpCli release that enrolls the exact Folders Contracts package version and passes the AD-15 package/closure and AD-21 Catalog set/type gates;
    - one approved execution vector per included operation, with loopback request/result parity.
- **EXT-MCPCLI-FOLDERS-RETIREMENT-GATE.**
  - Rank 45; prerequisite 5.11.
  - `release_identifier: hexalith-mcpcli-folders-retirement-gate-v1`.
  - Source work: McpCli Folders shared-head parity and retirement-verification stories still to be added (request R3); migration plan 1.1.0 "Retirement and release prerequisites".
  - Required evidence:
    - an owner-approved Folders inventory version in which every legacy tool, resource, CLI leaf and variant is `replace` with passing evidence or `withdraw`;
    - McpCli selected-environment shared-head parity, safety and rollback evidence for Folders.

**M3 — Graph bookkeeping.**
- `execution_waves`: add 5.12 to rank 11 and 5.13 to rank 22. Create rank 23 (5.14), rank 24 (EXT-MCPCLI-FOLDERS-ENROLLMENT), rank 45 (EXT-MCPCLI-FOLDERS-RETIREMENT-GATE) and rank 46 (5.15). Move 5.11 from rank 43 to a new rank 44.
- `exact_prerequisites`: update 5.8, 5.9, 5.10, 5.11; add 5.12, 5.13, 5.14, 5.15 and both EXT nodes.
- `requirement_inventory` `completion_owners`: FR48 adds 5.10, 5.11, 5.15. FR49 adds 5.8, 5.9, 5.15. FR51 adds 5.11 and 5.15.
- `generated_counts`:

  | Count | Before | After |
  | --- | --- | --- |
  | `canonical_story_identities` | 159 | 163 |
  | `inventory_rows_including_aliases` | 160 | 164 |
  | `stories_by_classification.product` | 87 | 91 |
  | `canonical_story_lifecycle_statuses.backlog` | 45 | 49 |
  | `inventory_lifecycle_statuses_including_aliases.backlog` | 45 | 49 |
  | `definition_authorities.reconciled-epics-anchor` | 48 | 52 |
  | `scheduled_nonterminal_stories` | 48 | 52 |

- `validation_rules` V2-003 text: "Require 163 canonical story identities and 164 inventory rows including aliases."
- `planning_amendments`: prepend a 2026-11-10 entry with the following fields.
  - `decision_maker: Jerome`, `decision_policy: docs/governance/approval-policy.md`, `proposal: _bmad-output/planning-artifacts/sprint-change-proposal-2026-11-10.md`.
  - The accepted scope.
  - The changes.
  - `changed_authority` SHA-256 before/after for `prd.md`, `architecture.md` and `epics.md`. The before values are b08c5bb5…, 7ee56ec9… and da39584e….
  - A `tracker_change` with `sha256_before: ffd34527…` and `regeneration: none`.
  - The validation result.
  - An `unchanged` list: `general_execution_hold`, `v2_exposure_authorized`, `sprint_status_regeneration_authorized`, `ordinary_story_execution_authorized`, the 2026-09-21 `validation_results` record, and every approval and provenance digest.

### 4.5 Tracker and implementation artifacts — approved

**T1 — `_bmad-output/implementation-artifacts/sprint-status.yaml`, Epic 5 block.** Exact line edits; `sprint_plan.py generate` is not used because it drops comments.

| Old key | New key |
| --- | --- |
| `5-8-cli-workspace-preparation-and-lock-lifecycle` | `5-8-enroll-workspace-preparation-and-lock-lifecycle-through-mcpcli` |
| `5-9-cli-file-context-commit-status-error-and-audit-behavior` | `5-9-enroll-file-context-commit-status-error-and-audit-operations` |
| `5-10-mcp-workspace-preparation-and-lock-lifecycle` | `5-10-enroll-tenant-folder-acl-provider-readiness-and-binding-op` |
| `5-11-mcp-file-context-commit-status-error-and-audit-behavior` | `5-11-prove-four-surface-parity-and-mixed-surface-handoff-through` |
| — | `5-12-declare-folders-operations-in-decorated-mcpcli-contracts` |
| — | `5-13-admit-and-authorize-gateway-dispatched-folders-commands` |
| — | `5-14-serve-folders-reads-through-gateway-query-handlers` |
| — | `5-15-retire-hexalith-folders-cli-and-hexalith-folders-mcp` |

All eight entries are `backlog`. `epic-5` stays `in-progress`, and the `last_updated` header records this correction. Frozen evidence files that embed the old keys are historical and are not rewritten: the Story 12.2 bootstrap snapshots under `evidence/story-12-2-bound-bootstrap-20261101T164412Z/` and `eng/story117-nss-writer-trace/…`.

**T2 — `sprint-status.yaml`, Epic 12 "Verification honesty" action item.**
OLD: `Add MCP tool annotations + typed request bodies + structured output + authorized content read-back (HXF-MCP-001);`
NEW: `HXF-MCP-001 redirected to McpCli (2026-11-10): generic tool annotations and structured output come from McpCli, typed request bodies from Story 5.12 decorated Contracts, authorized content read-back from Story 5.14; no new work on the frozen Folders MCP server;`

**T3 — `_bmad-output/implementation-artifacts/spec-5-8-cli-workspace-preparation-and-lock-lifecycle.md`.** Insert a note immediately after the frontmatter. Leave the frozen intent block and the `status` field untouched.
> **Superseded 2026-11-10** by `sprint-change-proposal-2026-11-10.md`: Story 5.8 now enrolls workspace preparation and the lock lifecycle through `Hexalith.McpCli`. This draft targets the frozen Folders CLI and must not be executed; a fresh spec is required for the re-scoped story.

## 5. Implementation handoff

**Scope class: Moderate.** The backlog is reorganized, stories are re-scoped and added, and a cross-repository dependency is created. No production code or CI changes in this pass.

| Recipient | Responsibility |
| --- | --- |
| Developer agent (this session, on approval) | Apply E1–E6, A1–A7, S1–S11, M1–M3 and T1–T3 exactly. Run the validation below. Leave every edit uncommitted for Jerome. |
| Product Owner (Jerome) | Accept the proposal. Commit when satisfied. Decide when to send the McpCli requests (R1–R4). |
| Folders Contracts / security / query maintainers (Jerome, single-maintainer model) | Specify and execute Stories 5.12 → 5.13 → 5.14 as ranks allow. 5.12 becomes eligible once Story 1.17 closes and DEC-A8-HOLD clears. |
| McpCli maintainers (cross-repo) | **R1:** Story 5.2 covers the Folders non-gateway classes: staged content above 262144 bytes (V04/V05 `PutFileStream`), the R01/R02 resources, provider egress (G6), and freshness/paging envelope mapping (V01/V02). **R2:** Story 5.8 consumes Folders Story 5.12's eligibility decisions and named decorated types. **R3:** add Folders enrollment, shared-head parity and safety, and retirement-verification stories, analogous to McpCli Stories 5.3–5.5 for Parties. Completing them satisfies EXT-MCPCLI-FOLDERS-ENROLLMENT and EXT-MCPCLI-FOLDERS-RETIREMENT-GATE. **R4:** after Story 5.15, record the Folders retirement in a new AI.Tools migration-plan version (McpCli Story 5.11 or its successor). |

**Validation for the documentation pass:**
1. `prd.md` and `epics.md` FR1–FR58 bullets stay byte-identical; no `- FRn:` bullet is edited.
2. No NFR1–NFR73 bullet is edited. Run `NfrTraceabilityConformanceTests` and `GovernanceCompletenessGateTests` from the prebuilt `Hexalith.Folders.Contracts.Tests` assembly.
3. Manifest graph check covering V2-002 to V2-011: 163 canonical identities and 164 rows with aliases, every scheduled nonterminal node with one integer rank, no missing or duplicate edge, acyclic, strictly lower prerequisite ranks, and DEC-A8-HOLD on every rank ≥ 10 story.
4. YAML parse of the manifest and `sprint-status.yaml`, with the tracker SHA-256 recorded before and after.

**Success criteria for the corrected plan:**
- No backlog story targets `Hexalith.Folders.Cli` or `Hexalith.Folders.Mcp` except safety and continuity work (Stories 1.17 and 11.6) and retirement (Story 5.15).
- Every Folders-owned McpCli gap (G1–G6) has exactly one owning story, and every McpCli-owned dependency is a pending external node.
- MVP readiness reports the McpCli dependency explicitly, never a legacy-binary fallback.

## Checklist record

| Item | Status | Note |
| --- | --- | --- |
| 1.1 Trigger | [x] | Jerome's 2026-11-10 direction; 2026-09-27 banner-only correction left an executable backlog on frozen adapters. |
| 1.2 Core problem | [x] | Strategic pivot (platform consolidation). |
| 1.3 Evidence | [x] | Section 1 table. |
| 2.1 Current epic | [!] | Epic 5 re-scoped (S2–S6). |
| 2.2 Epic-level changes | [!] | Four stories re-scoped and four added in Epic 5; no new epic. |
| 2.3 Remaining epics | [x] | Epics 1, 4, 7, 8, 13 unchanged. Epic 11 gets the 11.6 note and a new 11.10 edge. Epic 12 gets the action-item redirect and new edges. |
| 2.4 Obsolete or new epics | [x] | None obsolete. Two external nodes added. |
| 2.5 Order and priority | [x] | Story 5.12 at rank 11 directly after 1.17. |
| 3.1 PRD | [!] | E1–E6; FR and NFR bullets untouched. |
| 3.2 Architecture | [!] | A1–A7. |
| 3.3 UX | [N/A] | Console unaffected. |
| 3.4 Other artifacts | [!] | M1–M3, T1–T3; code, CI and packages deferred to Story 5.15. |
| 4.1 Direct adjustment | Viable | Medium effort, medium risk. |
| 4.2 Rollback | Not viable | Completed adapter evidence stays valid; D1 rejects early withdrawal. |
| 4.3 MVP review | Not needed | D2 keeps the four-surface MVP. |
| 4.4 Selected path | [x] | Option 1, direct adjustment. |
| 5.1–5.5 Proposal components | [x] | Sections 1–5. |
| 6.1–6.2 Review | [x] | All seven proposals approved individually. |
| 6.3 Final approval | [x] | Jerome, 2026-11-10: "Yes — apply"; FR48/FR49 mirror: "Mirror now". |
| 6.4 sprint-status update | [x] | T1–T2 applied; YAML validated. |
| 6.5 Handoff | [x] | Section 5; McpCli requests R1–R4 await Jerome's send decision. |

## 6. Application record (2026-11-10)

All edits were applied to the working tree and left uncommitted for Jerome.

**Added at final approval:** `epics.md` FR inventory bullets FR48 and FR49 now mirror `prd.md` byte for byte. Commit `fb551174` (2026-09-27) had rewritten them in the PRD only. FR1–FR58 are now identical across both files, and no other FR bullet changed.

| Artifact | SHA-256 after (prefix) |
| --- | --- |
| `prd.md` | `1b422c87b889…` |
| `architecture.md` | `54e18f3a6b26…` |
| `epics.md` | `ca08c03c0dfb…` |
| `planning-story-manifest.yaml` | `1d8c6be38d22…` (amendment 2026-11-10 records the full before/after digests) |
| `sprint-status.yaml` | `a83206320f1f…` |

**Validation:**
- FR1–FR58 are byte-identical in `prd.md` and `epics.md`. The PRD Non-Functional Requirements section and the `epics.md` NFR bullets are unchanged against HEAD.
- Manifest graph (V2-002 to V2-011):
  - 163 canonical rows plus 1 alias, which matches `generated_counts`;
  - 79 ranked nodes and 284 edges;
  - acyclic, with every prerequisite strictly lower-ranked;
  - DEC-A8-HOLD on every ordinary story at rank 10 or later;
  - row and exact-prerequisite lists identical.
- YAML parse passes for the manifest and the tracker.
- Planning-reading tests, run from prebuilt assemblies (no test source changed since they were built on 2026-11-04):
  - `Hexalith.Folders.Contracts.Tests` (Nfr, Governance, AdrRunbook, ConsumerDocs, AuthorizationMatrix classes): 263 of 273 pass.
  - The 10 failures (`NfrTraceabilityGatePreservesNativeFailureExitWithoutEchoingOutput` and nine `GovernanceReportPublicationPreservesExitCodesAndRejectsStaleResultsAsync` cases) are **pre-existing**. The failure set is identical with the HEAD baseline files restored. Each one is a child gate process exiting 134 instead of the expected exit code. That is an environment-level process abort, not document content.
  - `Hexalith.Folders.Testing.Tests` (ExitCriteriaDecisionArtifact, OpsConsoleWireflowNotes): 27 of 27 pass.
  - `Hexalith.Folders.UI.Tests` (NoMutationConsoleSweep): 19 of 19 pass.

