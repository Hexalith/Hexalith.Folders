# Reviewer Gate — Security & Authorization Lens (re-gate, pass 2)

- **Artifact under review:** `_bmad-output/planning-artifacts/architecture.md` (1907 lines, working tree, pass-2 state)
- **Baseline:** HEAD `de281e7` + the uncommitted pass-2 amendment (`+115/-65` on architecture.md)
- **Prior reports:** `review-security-2026-09-16.md` (FAIL, 1C/8H/8M/3L) → `review-security-update-2026-09-16.md` (FAIL, 0C/5H/8M/3L)
- **Mode:** read-only apart from this report. **Every pass-2 assertion cited below was re-verified against the working tree with file:line**, per the instruction that pass 1 introduced eight false claims. Where pass 2 asserts something about the repo, I re-ran the check rather than accepting the sentence.
- **Date:** 2026-09-16

---

## Verdict

**FAIL — narrowed again, and this is now a close call. 0 critical, 4 high, 11 medium, 3 low.**

Pass 2 is the best of the three states of this document. Three of my five highs are genuinely closed, one of them (SU-4) completely and exactly as prescribed. The S-7 row stopped making a false enforcement claim and started making an honest owed-work statement, which is the correct direction and the harder one to write. The SSRF item adopted every substantive correction I gave it, including the one I expected to be resisted (that `safe-denial-404` is the wrong envelope). The deny-by-default item replaced a structurally blind gate with the right one.

It still fails, for three reasons, and only the first is a carry-over:

1. **SU-3 is not closed — and pass 2 closed off the only available fix.** The guard no longer *names* the header, but "the task of the re-acquiring command … server-resolved" has no definition anywhere in this document or in any fixture, every task-identity source in both is caller-supplied, no durable task→principal binding exists — and pass 2 newly declares `stagedByPrincipal` to be "**evidence, not a guard input** … no transition reads it." The one field that would have closed the intra-tenant staged-work takeover is now affirmatively forbidden from closing it.
2. **A live NFR76 condition has no canonical outcome.** NFR76 is deny-by-default on *absent, stale, **malformed**, or unavailable* authority. S-7's three outcomes serve absent-authentication (401), denied/absent-resource (404), and stale/unavailable (503). `LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed` is a live code that today routes to **403 `authorization_denied`** — the status PD10 retires — and neither S-7 nor the PD10 work list mentions it. The mapper's `_` default arm is also 403, so a denial outcome added without a mapper arm silently re-emits the retired status. *(This corrects my own prior reports, which wrongly treated "malformed" as absorbed by the restored 401.)*
3. **Pass 2 introduced two new false claims of exactly the class the instruction warned about** — both in security decision rows, both citing a named repo artifact that says the opposite. One of them (`withheld` "does not collide with the shipped meaning of `redacted`") is falsified by *both* artifacts it cites in the same sentence.

The scoreboard moved a long way. It has not moved past the point where a security sign-off is available.

---

## What pass 2 got right — verified, not accepted

I re-checked each claimed fix at the level of the repo, not the sentence.

**SU-4 — CLOSED, both limbs, exactly as prescribed.**
- `architecture.md:656` now carries: *"**This headline is qualified, not absolute, until the open item below is settled:** the operational carve-out re-admits durable cleartext for the lock identity and the provider executor, so read this sentence as scoped to the evidence and observability surfaces."* The falsified headline no longer publishes unmarked.
- `architecture.md:356` (C9 verification method) now reads *"asserting no durable cleartext in the **Dapr state store** (explicitly in scope — the PD8 carve-out's only candidate home, so a gate that omits it certifies green over the exact hole) or in any event, projection, audit record, log, trace, or export."* The gate can now fail on the risk it exists to cover, and the parenthetical states *why*, which is better than the clause I asked for.

**SU-1 — substantially surfaced (obligation still missing; see SR-1).** S-7 now states the exit-65/72 mapping, the exit-72 `reconciliation_required` contradiction, the surviving `not_found`→73 rows, and the `auth_outcome_class` values that reproduce the oracle. All four are true:
- `parity-contract.yaml`: `authentication_failure`→65 (49 rows), `read_model_unavailable`→72 (37), `projection_unavailable`→72 (35), `not_found`→73 (22), `tenant_access_denied`→66 (49), `folder_acl_denied`→66 (39), `cross_tenant_access_denied`→66 (6), `audit_access_denied`→66 (4).
- `architecture.md:730`: `72 | reconciliation_required | … not retryable until cleared` — the contradiction against the 503's `retryable: true` is real and is now in the document.
- `auth_outcome_class` per-row values: `folder_acl_denied` ×35, `tenant_access_denied` ×10, `audit_access_denied` ×4.

**SU-2 half two — CLOSED.** The false "CLI and MCP cannot disagree" sentence is gone and replaced with *"The exit-code/failure-kind derivation is **owed, not achieved**"* plus *"Until those rows exist, CLI and MCP **can** disagree about an authority outage."* That is the honest sentence I asked for. (Half one was replaced by a *different* false claim — SR-1.)

**SEC-7 / SU-8 / SU-9 — closed in substance.** `architecture.md:682` now says *"**The sink is wider than the probe.** The same authorized base URL is reused by every subsequent Forgejo call with the tenant's bearer attached, so a destination accepted once at readiness is trusted for the life of the binding"*; names **resolve-then-pin** (*"whether it resolves then pins the address it validated, so a rebind between check and call cannot move the request"*); and explicitly rejects the wrong envelope: *"**It is not the S-7 `safe-denial-404`** — that envelope … carries `no_action`, which mis-advises a caller who can fix a genuinely invalid endpoint."* Verified against `ForgejoHttpApiClientFactory.cs:45-52` (sets `client.BaseAddress = request.BaseUri` and attaches `ForgejoAuthorizationHeader.FromBearerToken(credential)`) — the doc's description of the sink is accurate. Residual is SR-11.

**SEC-9 / SU-7 — closed on the gate prescription.** `architecture.md:684`: *"**The gate must be the framework default, not a test that enumerates the generated surface** … Enforcement belongs in the routing pipeline (a deny-by-default fallback policy applied to every endpoint, with an explicit opt-out that a test *can* enumerate and review)."* That is the correct inversion — the enumerable set is now the opt-out, not the protected surface. Residual is SR-12 (two dropped dimensions).

**Degraded mode — correctly opened, and the matrix contradiction is real.** `authorization-matrix.md:76` routes `stale` to `safe-denial-404`; `:146` routes stale authority evidence to `authority-unavailable-503`. Both verified verbatim. The as-built third answer is also real: `TenantAccessAuthorizer.cs:18,82,87` implements `AuthorizeDiagnosticReadAsync(allowBoundedStale: true)` with a `DiagnosticStalenessBudget`. Refusing to claim the matrix arbitrates is the right call.

**S-4 evaluation order — authentication first, verbatim at `:654`**, with the reason stated (*"an unauthenticated caller must receive the 401 envelope and must never be routed to the retryable authority-outage 503"*). Closed.

**SU-5 — surfaced inline on `GetTaskStatus` at `:658`**, with two candidate resolutions named. Downgraded to MEDIUM (SR-7), not closed.

**Bridge arbitration — stronger than claimed, and pinned by a test.** `FoldersServerServiceCollectionExtensions.cs:130` documents "Server is read-only — no `ISemanticIndexingBridgeWriter` registration", and `tests/Hexalith.Folders.Server.Tests/FoldersContextSearchFacadeRegistrationTests.cs:102` asserts `GetService<ISemanticIndexingBridgeWriter>().ShouldBeNull()`. The claim is not just prose; it has a gate. Credit.

**`previous-spine.yaml` known_omissions — exact.** `tests/fixtures/previous-spine.yaml:7-9` carries `No request/response schema fingerprints` and `No status-code surface` verbatim. The "C13 symmetric-drift gate will not catch this" paragraph is accurate.

---

## Findings

### SR-1 — HIGH — S-7's new "vocabulary ownership, honestly scoped" sentence is false in the opposite direction, and the two axes it writes off are exactly the two carrying the surviving existence oracle

`architecture.md:657`:

> **Vocabulary ownership, honestly scoped:** `tests/fixtures/parity-contract.schema.json` **closes exactly two axes** — `canonical_error_category` and `mcp_failure_kind` — so of the tokens above only the three *categories* have a home there…

Verified against the schema. It closes **four** required, `$ref`-bound enums, not two:

| Axis | Location | Members | Required? |
| --- | --- | --- | --- |
| `auth_outcome_class` | `properties/transport_parity/properties/auth_outcome_class` | 6 | yes (`:62`) |
| `canonical_error_category` | `$defs/canonical_error_category` | 50 | yes (`:198`, `:204`) |
| `cli_exit_code` | `$defs/cli_exit_code` | 15 | yes (`:129`, `:199`, `:207`) |
| `mcp_failure_kind` | `$defs/mcp_failure_kind` | 49 | yes (`:130`, `:200`, `:210`) |

(Plus `operation_family`, `read_consistency_class`, `idempotency_key_rule`, `task_id_sourcing`, `credential_sourcing`, `correlation_id_sourcing`, `pre_sdk_error_class`, `idempotency_key_sourcing`, `adapter_name` — the file is closed-vocabulary throughout.)

Pass 1's defect was a *false enforcement* claim ("CLI and MCP cannot disagree"). Pass 2 replaced it with a *false scoping* claim, and the scoping error is not neutral: **the two axes S-7 declares to have no closed vocabulary are the two that publish the existence oracle it is retiring.** `cli_exit_code` is the enum whose members include 66 and 73 — the denied-versus-absent split a caller reads with a shell conditional. `auth_outcome_class` is a closed six-member enum whose live members are `tenant_authorized, tenant_access_denied, folder_acl_denied, audit_access_denied, credential_missing, safe_not_found` — i.e. **four of its six members are distinctions S-7 retires**, and it is a *required* field on every one of the 49 rows.

The practical consequence is the SU-1 obligation gap, now formalised. S-7 correctly says the *codes* `code`/`clientAction`/`visibility` have no closed vocabulary and that *"giving them one is part of this correction."* But by declaring the schema to close "exactly two axes", it removes `cli_exit_code` and `auth_outcome_class` from the set of vocabularies the correction owns — the two that *do* have closed vocabularies and *do* need narrowing. A change set executed exactly as written closes the HTTP envelope, closes two category enums, and leaves the scriptable oracle standing in a required, schema-validated field.

One further omission in the same sentence's owed-work list: it names retiring `not_found`→73 but not **`authorization_revocation_detected`→73 (×6)**. "Revoked" is an explicit `safe-denial-404` cause at `authorization-matrix.md:145`, so a CLI caller currently distinguishes *your access was revoked* (73) from *you were denied* (66) from *it does not exist* (73) — a three-way oracle, and the revocation limb is not on the retirement list.

**Attack class:** authenticated resource enumeration and revocation-detection via `cli_exit_code` / `mcp_failure_kind` / `auth_outcome_class`, on 49 operations, scriptable, surviving a fully conformant PD10 change set.

**Correction (doc-only).** Replace "closes exactly two axes" with the true statement — the file closes `auth_outcome_class`, `canonical_error_category`, `cli_exit_code` and `mcp_failure_kind`, and the correction owns narrowing all four. Extend S-7's identity obligation from "the envelope" to "every caller-observable outcome". Add to the PD10 work list: narrow `auth_outcome_class` to the post-correction set; assert that no protected-operation row carries `not_found`, `folder_acl_denied`, `cross_tenant_access_denied`, `audit_access_denied`, or `authorization_revocation_detected`.

---

### SR-2 — HIGH — SU-3 is not closed: "server-resolved task" is undefined, every task-identity source in the document and the fixtures is caller-supplied, and pass 2 newly forbids the field that would close it

`architecture.md:447`, pass-2 text:

> `dirty` + `WorkspaceLocked` resolves to `changes_staged` when **the workspace still holds staged changes AND `stagedByTaskId` equals the task of the re-acquiring command** — both conjuncts, never one. … **"The task of the re-acquiring command" is server-resolved, not the `X-Hexalith-Task-Id` header.** … the comparison is against the task identity the server already authorized for this command under S-8's derivation rule. … `stagedByPrincipal` is **evidence, not a guard input** — it records who staged the work for audit and operator display, **and no transition reads it**.

Two of my three sub-findings are addressed: the staged-content conjunct is now explicit ("both conjuncts, never one"), and the orphaned-workspace obligation is named rather than assumed away. The load-bearing one is not.

**"Server-resolved" is asserted, never defined, and nothing in the repo supplies it.** Re-verified at every source:

| Source | Says | Verified |
| --- | --- | --- |
| A-10, `architecture.md:699` | "`X-Correlation-Id` and `X-Hexalith-Task-Id` headers carry across REST, SDK, CLI, MCP" | yes |
| Adapter Parity Contract, `:710` | "**Caller-provided** via `X-Hexalith-Task-Id` header. SDK does not generate; required for task-scoped operations (lock, file mutation, commit)" | yes |
| Header table, `:918` | "`X-Hexalith-Task-Id` \| request + response \| yes for task-scoped ops \| ULID" | yes |
| `parity-contract.schema.json` `task_id_sourcing` | `[not_task_scoped, caller_provided, cli_flag, mcp_tool_input]` | yes — **every member is caller-supplied** |
| grep for a durable task→principal binding across architecture.md | zero hits (`task.principal`, `principal who created`, `bound at creation`, `task→principal`, `task-to-principal`) | yes |

So "the task identity the server already authorized for this command" resolves to: *the caller-presented task id, after the server checked it against S-8's conjuncts.* S-8 at `:658` states those conjuncts for the nearest analogue — `GetTaskStatus` "requires the current tenant, bound-folder read authority, and task scope" — and **there is no principal conjunct**. Server-*validated* is not server-*resolved*. A task id validated against tenant + bound-folder authority is validated against authority that a second principal in the same tenant, on the same folder, also holds.

**Pass 2 made this strictly harder to fix.** The one field that carries the missing conjunct is now declared out of bounds: `stagedByPrincipal` is "evidence, not a guard input … no transition reads it." Pass 1 merely left it unused; pass 2 forbids its use. The document now simultaneously (a) states the failure mode — *"trust the header and any principal who names another principal's task id resumes and commits their staged changes"* — (b) declares the guard is not the header, (c) supplies no non-header comparand, and (d) prohibits the field that would supply one.

**Attack class — intra-tenant workspace takeover.** Principal B holds write authority on folder F (a normal collaborator, not an attacker who has escalated). Principal A stages a change set under task `T`; `stagedByTaskId = T` becomes durable. `T` is a ULID, time-ordered, carried on **request *and* response** per `:918`, echoed in audit evidence, and — per pass 2's own sentence — displayed to operators as `stagedByPrincipal`'s companion. B presents `T`, the server validates it against tenant + folder authority (which B holds), `stagedByTaskId == T` passes, B re-acquires the lock and commits A's staged changes into the customer's repository. The commit is attributed to the folder, the staging evidence says A. This is the document's own enumerated failure, reached through the door it left open.

**Correction (doc-only, and narrower than what I asked last round).** State the guard as a conjunction whose authoritative limb is the principal — `stagedByPrincipal == the authenticated principal (S-2 `sub`)` **and** `stagedByTaskId == the presented task id` **and** staged content present — and delete the "no transition reads it" clause, which is the sentence blocking the fix. If the sponsor wants the task-only form, then the missing declaration must be written instead: *a task id is bound at creation to the principal who created it; that binding is durable and server-owned; S-8's task-scope conjunct is evaluated against it; a presented task id not bound to the caller yields the S-7 `safe-denial-404` before the lock guard is reached.* Either is acceptable. Neither exists today, and without one of them the header is authority by the back door regardless of how the guard is worded.

---

### SR-3 — HIGH — malformed authority evidence has no canonical outcome; the live code for it routes to the retired 403, and the denial mapper's default arm does too

This corrects my own two prior reports, which treated NFR76's "malformed" limb as absorbed by the restored 401. It is not. The 401 covers absent or malformed **authentication**. Malformed **authority evidence** — a tenant-access or folder-ACL record that is present but unparseable — is a distinct condition, it is named in NFR76, and it has a live outcome code.

`src/Hexalith.Folders/Authorization/LayeredAuthorizationOutcomeCodes.cs` carries 13 codes. `FolderAuthorizationDenialMapper.StatusAndCategory` maps them:

| Outcome code | Live status | Live category |
| --- | --- | --- |
| `AuthenticationDenied` | 401 | `authentication_failure` |
| `SafeNotFound`, `FolderAclDenied` | 404 | `not_found_to_caller` |
| `TenantProjectionUnavailable/Stale`, `FolderAclUnavailable/Stale` | 503 | `read_model_unavailable` |
| `DaprPolicyDenied` when retryable | 503 | `policy_evidence_unavailable` |
| `DaprPolicyDenied`, `ClaimTransformDenied`, `EventStoreValidatorDenied`, **`AuthorizationEvidenceMalformed`**, `TenantAccessDenied` | **403** | `authorization_denied` / `policy_denied` / `tenant_access_denied` |
| `_` (default arm) | **403** | `authorization_denied` |

Six of thirteen codes, plus the default, land on the status PD10 retires. S-7 enumerates three canonical outcomes and says 403 is "removed from protected-operation responses" — but it never states **which canonical outcome each retired condition maps to**, and for `AuthorizationEvidenceMalformed` there is no obvious answer: it is neither a denial (404 would be a lie about a condition the operator must fix) nor an availability outage (503 invites a retry that will fail identically forever). It needs a decision, and the document does not record that one is needed.

The default arm is the structural half of the same gap. The deny-by-default open item at `:684` argues, correctly, that *"an operation cannot be added unprotected"* must be a framework property rather than an enumeration. The identical argument applies one layer in and is not made: **a denial outcome cannot be added un-canonicalized.** Today, adding a 14th outcome code with no mapper arm silently produces a 403 `authorization_denied` — the retired status, reachable by omission, after the correction ships. S-7's "a single code, chosen by no predicate" has no total-function rule behind it.

**Correction (doc-only).** Add to S-7: (a) an explicit mapping of `authorization_evidence_malformed` to one of the three canonical outcomes, with the reason; and (b) the totality rule — *the denial mapper's fallback for any unrecognised outcome is the `safe-denial-404`, never 403, and a conformance test asserts every member of `LayeredAuthorizationOutcomeCodes` has an explicit canonical mapping.* Add both to the PD10 work list. Relatedly, S-7 should state that the 503's two live categories (`read_model_unavailable` and `policy_evidence_unavailable`) collapse to one, or the "byte-identical" property fails on the availability outcome for the same reason it failed on the denial.

---

### SR-4 — HIGH — S-6's `withheld` non-collision claim is falsified by both artifacts it cites, and S-7's "zero occurrences anywhere" is false in `src/`

`architecture.md:656`, new in pass 2:

> `withheld` is a **new** render state: it **does not collide with the shipped meaning of `redacted`** in `docs/.../safety-invariant-ci-gates.md` and `ConsoleStatusText.cs`, which stays "a value exists and is suppressed for this viewer"…

Both cited artifacts define `redacted` **using the word `withheld`**:

- `docs/contract/safety-invariant-ci-gates.md:59` — *"`redacted`: a value exists for an authorized audience but is deliberately **withheld**."*
- `src/Hexalith.Folders.UI/Services/ConsoleStatusText.cs:45` — `["redacted"] = "The requested evidence is **withheld** by tenant policy."`

And `architecture.md:657` asserts *"`withheld` has zero occurrences anywhere, as this document's own reality table records."* Grep of `src/` returns four: `ConsoleStatusText.cs:45`, `TenantAccessState.cs:25` ("The effective-access evidence is itself **withheld** by tenant policy (F-5)"), `TrustDimensionState.cs:28`, `MetadataOnlyFolderTree.razor:49`. The narrow claim ("zero occurrences *as an enum member*") is true; the sentence as written is not, and it is the sentence offered as evidence for the non-collision.

**Why this is a security finding.** S-6 is introducing `withheld` to mean *"no cleartext value exists to show anyone, here is its correlation token"* — the opposite of the shipped meaning, which is *"a value exists and you may not see it."* The distinction between **hidden-from-you** and **does-not-exist** is precisely the redacted-vs-unknown rule that cross-cutting concern #11 and F-5 make a safety invariant, with a CI gate behind it. Shipping a state named `withheld` whose meaning inverts the word's shipped meaning in the gate document and in the console's own string table is a guaranteed operator-facing existence-disclosure confusion — an operator seeing "withheld" cannot tell which of the two things it means, in an incident, which is when this UI is used. It is also the same defect class the instruction flagged: a security row citing a named artifact as support when the artifact says the opposite.

**Correction (doc-only).** Either rename the new state (`tokenized` reads correctly and collides with nothing), or state plainly that adopting `withheld` requires a lockstep amendment of `safety-invariant-ci-gates.md:59` and every `withheld`-as-`redacted` string in `src/Hexalith.Folders.UI/`, and put that amendment on the PD8 work list. Delete "zero occurrences anywhere" or scope it to "zero occurrences as a contract or enum value."

---

### SR-5 — MEDIUM — A-8 still publishes as current contract exactly what S-7 now marks "proposed, not approved", and the `withheld` blast radius is understated

Pass 2's honesty marker at `:657` is good: *"PD10's promotion of `visibility` to a **required top-level field on every error** and PD8's addition of **`withheld`** are *proposed*, not yet in the matrix, the spine, or any enum … and **must not be cited as current contract**."*

Three problems remain, all in scope:

1. **A-8 cites it as current contract, twenty rows later.** `architecture.md:698`: *"**`visibility` is a required field on every error (PD10, 2026-09-15);**"* — unqualified, no marker, no pointer. Verified against the spine: `ProblemDetails.required` at `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:7635-7645` is `[type, title, status, category, code, message, correlationId, retryable, clientAction, details]` — no `visibility`. The document now marks a claim "must not be cited as current contract" and then cites it as current contract in the same section.
2. **The blast radius is larger than "the shipped `DetailsVisibility` set is `{redacted, metadata_only}`".** The generated client carries **seven** visibility enums, five of them closed to a *single* member: `DetailsVisibility` `{redacted, metadata_only}`, `Details2Visibility` `{redacted}`, `Details3Visibility` `{metadata_only}`, `Details4Visibility` `{redacted}`, `Details5/6/7Visibility` `{metadata_only}` (`HexalithFoldersClient.g.cs:15167-15230`). Adding `withheld` widens seven closed enums, five of which currently admit one value. Also `DetailsVisibility` is a *generated-client* name; the contracts type is `RedactionVisibility` (`src/Hexalith.Folders.Contracts/Projections/Audit/RedactionVisibility.cs:5-11`). Naming the generated artifact as "the shipped set" is the kind of mis-citation the document's own owner-on-disagreement rule exists to catch.
3. **OQ2 is still absent from the superseded-approval table** at `:229-234` (OQ3, C3, C6, C9 only), and the PD10 regeneration list at `:666` still does not name the OQ2 problem schemas, whose `details` objects are `additionalProperties: false` with `required: [visibility]`.

---

### SR-6 — MEDIUM — S-7 contradicts itself on whether the parity oracle has rows for the three new categories, and the contradiction changes what the change set must do

`architecture.md:657`:

> Today the oracle and this document's own canonical table have **no row** for `authentication_failure`, `read_model_unavailable`, or `projection_unavailable`; the shipped `parity-contract.yaml` maps them to exit **65** and **72** …

Both halves cannot be true, and the first is the false one. `parity-contract.yaml` carries **49** rows for `authentication_failure`→65, **37** for `read_model_unavailable`→72, and **35** for `projection_unavailable`→72. What has no row for them is **this document's exit-code table** at `:722-734`, where 65 is `credential_missing`, 73 is `not_found`, and there is no entry for either availability category — they collapse onto 72 `reconciliation_required`.

This matters operationally, not just editorially: "no row exists" instructs an implementer to *add* rows; the truth is that 121 rows exist with the wrong mappings and must be *changed*, and the document's own exit-code table must gain entries that do not collide with 72. A change set that adds rows beside the existing ones ships both.

Two collapses worth naming in the same fix: exit **65** currently serves both `authentication_failure` and `credential_reference_invalid` (a provider-credential fault, ×2), and exit **72** serves `read_model_unavailable`, `projection_unavailable`, `projection_stale`, `reconciliation_required`, `dirty_workspace`, `file_policy_unavailable`, `workspace_not_ready` and `workspace_preparation_failed` — retryable and non-retryable conditions on one code.

---

### SR-7 — MEDIUM — the S-7 ⊥ S-8 ordering contradiction is surfaced in S-8 and still asserted as settled in S-7, and the resolving seam is unwritten

Pass 2 adds, at `:658`: *"**and this is in unresolved tension with S-7's 'before any protected-resource lookup' ordering**, because the bound folder whose authority gates the call is discovered *from* the caller-named task, which is itself the protected resource; the correction must say whether task-derived scope is resolved by an authority-only lookup that leaks nothing, or whether these operations take an explicit folder scope."*

That is the right surfacing and names both candidate resolutions. But S-7 at `:657` is unchanged: *"**Three canonical outcomes, each evaluated before any protected-resource lookup**"*, repeated for the 503. The document now ratifies a property in the row a story author opens and disputes it in the row twenty lines down. An implementer working from S-7 will look up first and let the lookup result influence the denial.

The unwritten seam is the same one SR-2 needs: a **tenant-partitioned, state-free parent resolution** that is not a protected-resource lookup for S-7 purposes, provided it is constrained to the caller's authoritative tenant partition at the storage-key level, returns only the parent identifier, and produces the identical `safe-denial-404` on hit-without-authority and on miss. Writing it once resolves SR-7 and supplies SR-2's task→principal lookup. `authorization-matrix.md:350` records the related divergence as `G10` but frames it as token comparison, not as an ordering impossibility, so the matrix does not cover it either.

---

### SR-8 — MEDIUM — the tokenizer truncation width is still unspecified and rotation-destroys-audit-joins is still unstated

`architecture.md:656` still reads *"truncated to **a fixed width** and carried with its key version."* Grep: `128 bits` zero hits; no bit length, byte length, or character count anywhere in the document. Truncation length is the entire collision-resistance budget, and a collision **merges two different fields' evidence into one audit join** — a silent integrity failure in the audit trail this product sells. The document's own worry at `:281` (two implementations producing different tokens for the same value) applies one level down: two implementers will pick different widths.

Separately, "carried with its key version" implies rotation; rotation produces a different token for the same value; re-tokenization is impossible by construction because the cleartext is gone. **Key rotation is therefore a one-way destruction of audit-joinability**, including emergency rotation after suspected key compromise — an incident-response failure mode. `:281` surfaces that a rotation policy is owed; nothing surfaces what rotation costs.

Pin the width (128 bits / 22 Base64Url characters is the defensible floor) as a constant in the single shared write-path implementation, and state the rotation consequence explicitly with a chosen disposition.

---

### SR-9 — MEDIUM — the ratified retryable 503 still has no `Retry-After`, no retry ceiling, and no ingress rate-limit keying rule

`Retry-After` has zero occurrences in architecture.md. The degraded-mode paragraph ratifies that protected reads return a retryable 503 while Tenants is unavailable beyond the staleness bound, and tells `OQ12`/`OQ13` to accept that envelope — so fleet-wide 503s during an authority outage are a *designed* state and every conforming client is instructed to `retry`. With no `Retry-After` and no ceiling, conforming clients become a load amplifier against the authority path during exactly the incident the 503 signals. Ingress rate limiting remains HXF-SEC-005 (Epic 13, backlog); no keying rule is stated, so per-resource ingress buckets would make a 429 an existence signal that S-7's envelope identity does not reach.

---

### SR-10 — MEDIUM — NFR75/NFR76 were not updated in lockstep, and the NFR text is hash-pinned, so the reconciliation must be recorded architecture-side

`git diff docs/exit-criteria/nfr-traceability.md` is **empty** — the file was untouched by both fix passes. `:122` still states NFR76 as *"fail-safe deny-by-default on absent, stale, malformed, or unavailable authority"*, while the ratified S-7 serves the stale/unavailable limb with a retryable 503 and (per SR-3) serves the malformed limb with nothing at all.

Correcting my prior report's prescription: **the NFR row text cannot simply be re-worded.** `nfr-traceability.md:23-26` states that the PRD bullet is referenced by a stable 12-character SHA-256 hash and that `NfrTraceabilityConformanceTests` re-derives each hash from `prd.md` and asserts the PRD bullet and the `epics.md` `NFRn` text are identical. Editing NFR76's wording reddens that gate unless `prd.md` and `epics.md` move in the same commit. So the binding belongs in architecture.md: state in S-7 or the degraded-mode paragraph that deny-by-default is satisfied by the 401 for absent/malformed authentication, by the 404 for denied/absent/hidden, by the *(to be decided, SR-3)* outcome for malformed authority evidence, and by the 503 for the unavailable condition — which grants nothing and is therefore fail-closed rather than a degradation. Otherwise Story 13.2 builds a second shape from the four-condition sentence.

---

### SR-11 — MEDIUM — the destination-policy open item still omits the controls that bound the blast radius, and no SSRF primitive exists in the tree

The item now names the right sink, resolve-then-pin, the redirect question, the registration question, and its own envelope. Still absent: **connection and total timeouts**, **response-size caps**, an explicit **per-deployment allowlist escape with a named approver**, and any statement of what a *rejected* destination does to an already-established binding (a destination that becomes privately-resolving after binding is the persistence case the "trusted for the life of the binding" sentence identifies but does not resolve). Verified: `ConnectCallback`, `IsLoopback`, `IPAddress`, `Dns.` and `169.254` have **zero** occurrences across `src/` and `tests/`. Epic 13 prose at `:271` names `ConnectCallback` for HXF-SEC-002, so a scope note exists; it is not a decision row and names none of the above.

---

### SR-12 — MEDIUM — the deny-by-default item still drops the sidecar-only app-port boundary and the environment-discriminator downgrade

`architecture.md:684` fixed the gate prescription and did not re-admit the other two dimensions of HXF-SEC-003.

- **Sidecar-only app port**: expressed in no manifest and no decision row. A deny-by-default fallback policy in the routing pipeline protects the HTTP surface; it says nothing about whether the app port is reachable from outside the sidecar.
- **Environment-discriminator downgrade** — re-verified, with corrected citations (my prior reports named a non-existent `FoldersOidcOptionsValidator.cs`): `src/Hexalith.Folders.Server/Authentication/FoldersAuthSchemeValidator.cs:20` returns success when `environment.IsDevelopment() || environment.IsEnvironment("Test")`, and `src/Hexalith.Folders.Server/Authentication/FoldersAuthenticationServiceCollectionExtensions.cs:122` does the same before any `Audience`/`ValidIssuer` requirement is applied. Combined with I-3's local Dapr profile `defaultAction: allow`, a host mis-labelled `Development` has neither JWT configuration enforcement nor Dapr access control. No decision requires the environment discriminator itself to be a protected, verified input — which makes `ASPNETCORE_ENVIRONMENT` a single string that disables two authorization layers.

---

### SR-13 — MEDIUM — S-2 still has no `ValidAlgorithms`, no `typ` check, no lifetime ceiling, and no revocation path

Unchanged from SEC-13 across both passes. `architecture.md:652` freezes seven `TokenValidationParameters` and the JWKS refresh intervals — good — but `ValidAlgorithms` has zero occurrences in the document, so algorithm confusion is unconstrained by the frozen set; no `typ` header check is required; no maximum token lifetime ceiling is imposed on the issuer's `exp`; and "Token introspection: JWT-only (no introspection round-trip)" is stated without the consequence — a compromised or revoked token stays valid until `exp`, which is the issuer's choice, which no decision bounds. For a product whose safe-denial design assumes revocation is detectable (`authorization_revocation_detected` is a live code), an unbounded token lifetime with no introspection is the weakest link in the chain.

---

### SR-14 — MEDIUM — the denial contract on the 202-Accepted async path is still undefined

Unchanged from SEC-17 across both passes. S-7 fixes the synchronous envelope. Task-scoped mutations are accepted with 202 and complete asynchronously; nothing states what a caller observes when authority is revoked, becomes stale, or is re-evaluated *after* acceptance and before the side effect. The matrix's `revoked` row says re-evaluation happens "again at every pre-side-effect revalidation" (`authorization-matrix.md:75`), so the condition is real and its caller-visible outcome is unspecified — a status polling surface that distinguishes "denied after acceptance" from "not found" would rebuild the oracle on the async path.

---

### SR-15 — MEDIUM — the PD10 work list says "the tests" while a shipped test actively pins the oracle it must retire

`architecture.md:665` obliges regeneration of "the OpenAPI spine, the generated client, the CLI/MCP parity fixtures, the `previous-spine.yaml` snapshot, the C13 parity-oracle inventory, the published docs, and **the tests**."

"The tests" is doing too much work. `tests/Hexalith.Folders.Server.Tests/SafeAuthorizationDenialMappingTests.cs:51` asserts `problem.ProblemDetails.Extensions["code"].ShouldBe(outcomeCode)` — that is, it **pins `code` to the per-cause outcome code**, which is exactly the byte-identity S-7 requires it to stop doing. Within the single 404 branch, `SafeNotFound` and `FolderAclDenied` produce different `code` values, different `type` URIs (`https://hexalith.dev/errors/folders/{OutcomeCode}`), different `details.retryReasonCode`, and different `Message()` strings — four caller-visible fields varying by cause, with a green test holding them in place.

A related shipped defect in the same family, worth naming because a gate claims otherwise: `ConsoleStatusText.cs:47-52` carries the comment *"Kept existence-neutral so `*_denied` and not-found read identically"*, and then maps `not_found_to_caller` → "No matching diagnostic evidence is available for this scope." while `authorization_denied` → "Your effective permissions do not allow this view." and `policy_denied` → "Access is denied by tenant policy…". They do not read identically. The console is a third caller-observable surface outside S-7's "envelope" obligation, and it publishes the oracle in prose under a comment asserting it does not.

**Correction.** Name `SafeAuthorizationDenialMappingTests`, `LayeredAuthorizationOutcomeCodes`, `FolderAuthorizationDenialMapper` and `ConsoleStatusText` explicitly in the PD10 work list, so the correction cannot land against a green gate that encodes the retired behaviour.

---

### SR-16 — LOW — "49" is still hard-coded in the deny-by-default open item, and the sentence forbidding transcribed numbers transcribes three

`architecture.md:684`: *"The **49** protected operations are protected because each one opts in."* The same fix applied to S-7 and to the reality table was not applied here. And `:243` reads *"403 on 49 of 49, 404 on 46 of 49 — the denominator is the generated inventory, never a number transcribed here"* — transcribing three numbers in the clause that forbids transcribing numbers. Use the generated-inventory phrasing in both places, or drop the prohibition.

---

### SR-17 — LOW — the 46-of-49 404 gap is still not on the PD10 work list

`authorization-matrix.md:341` (`G1`) declares 404 on **46 of 49** operations. Three protected operations therefore cannot emit `safe-denial-404` without a spine change. The regeneration list enumerates seven artifacts plus the status-code/error-vocabulary drift surface, and still does not name adding the 404 response to those three. A change set executed exactly as listed leaves three operations unable to express the only permitted denial — which, under S-7, means they fall through to whatever the mapper's default arm does (SR-3: 403).

---

### SR-18 — LOW — S-7 and S-8 are now the only strata in the document without the honesty marker

Half closed. S-6 received its qualifier in pass 2 (SU-4), which was the important one. S-7 (*"Every protected operation … **evaluates** authority before resource lookup"*) and S-8 (*"Provider, repository, ref, and task dimensions **are derived** from an already-authorized folder"*) still read as current mechanism, while `NOT BUILT` markers, "Target (not built)" labels and the as-built banner carry the convention everywhere else. A story author opening §"Authentication & Security" reads two target-state rows as guarantees, with the target-state statement 400 lines above at `:236`.

---

## Closure tally

### The five pass-1 highs

| # | Finding | Status after pass 2 |
| --- | --- | --- |
| **SU-1** | Existence oracle survives in `cli_exit_code` / `mcp_failure_kind` / `auth_outcome_class` | **PARTIALLY CLOSED — surfaced, not obliged.** S-7 now names the 65/72 mapping, the exit-72 contradiction, the surviving `not_found`→73 rows, and the `auth_outcome_class` values; all verified true. The *obligation* to extend envelope identity to every caller-observable outcome, and the enum narrowing, are still absent — and SR-1's false "two axes" scoping now formally excludes them. Revocation→73 also unnamed. |
| **SU-2** | False enforcement claim; retryability inverted at exit 72 | **HALF CLOSED, HALF REOPENED.** The retryability inversion is now stated explicitly and honestly — fully closed, well done. The vocabulary half swapped a false *enforcement* claim for a false *scoping* claim (SR-1), plus a self-contradiction about whether the oracle rows exist (SR-6). |
| **SU-3** | `stagedByTaskId` guard's comparand is caller-controlled | **NOT CLOSED — and the fix direction was closed off.** Staged-content conjunct and orphan obligation: closed. The comparand is now "server-resolved", a term with no definition in the document or the fixtures, where every task-identity source remains caller-supplied and no task→principal binding exists; `stagedByPrincipal` is newly declared unreadable by any guard → **SR-2**. |
| **SU-4** | S-6 headline unmarked; C9 scoped around the hole | **CLOSED — both limbs, exactly.** `:656` qualifier and `:356` Dapr-state-store inclusion verified verbatim. The strongest fix in this pass. |
| **SU-5** | S-7 ⊥ S-8 for task-derived scope | **SURFACED, NOT RESOLVED → downgraded to MEDIUM (SR-7).** Named inline on `GetTaskStatus` with two candidate resolutions; S-7 still asserts the ordering as settled and the resolving seam is unwritten. |

### The prior SEC series

| Prior | Status |
| --- | --- |
| SEC-1 (durable cleartext carve-out) | **CLOSED as a recording** (the decision stays correctly open and routed). |
| SEC-2, SEC-3, SEC-4 | **CLOSED** (unchanged from pass 1). |
| SEC-5 (remediation scoped to `category`) | **CLOSED for the HTTP body**; caller-observable surfaces → SR-1; live mapper field variance → SR-15. |
| SEC-6 (S-7 ⊥ S-8) | **SURFACED** → SR-7. |
| SEC-7 (SSRF / destination policy) | **CLOSED in substance** — sink widened, resolve-then-pin added, wrong envelope rejected. Residual → SR-11. |
| SEC-8 (tokenizer width / rotation) | **NOT CLOSED** → SR-8. |
| SEC-9 (deny-by-default binding) | **CLOSED on the gate prescription** (routing-pipeline fallback + enumerable opt-out). Two dropped dimensions → SR-12. |
| SEC-10 (timing / ingress / cursors) | **NOT CLOSED** → SR-9. |
| SEC-11 (F-6 incident path) | **NOT CLOSED, unchanged.** |
| SEC-12 (C10 locations; audit sanitizer) | **HALF CLOSED** (C10 corrected in pass 1); the nine divergent sensitive-value predicates and the missing `ghp_`/JWT/PEM regexes are unchanged — `ghp_` still zero occurrences in the document. |
| SEC-13 (S-2 validation parameters) | **NOT CLOSED, unchanged** → SR-13. |
| SEC-14 (shared `folders-index` tenant) | **NOT CLOSED, unchanged.** |
| SEC-15 (`visibility` two positions; OQ2) | **PARTIALLY CLOSED** — S-7 now marks the top-level promotion "proposed"; A-8 still contradicts it and OQ2 is still unsuperseded → SR-5. |
| SEC-16 (no conformance gate for the correction) | **HALF CLOSED, unchanged from pass 1.** |
| SEC-17 (202 async denial path) | **NOT CLOSED, unchanged** → SR-14. |
| SEC-18 ("49 of 50"; hard-coded denominator) | **MOSTLY CLOSED** — S-7 and `:243` fixed; `:684` residual → SR-16; 46-of-49 → SR-17. |
| SEC-19 (`withheld` unmapped) | **NOT CLOSED, and now worse** → SR-4. |
| SEC-20 (strata read as current mechanism) | **HALF CLOSED** — S-6 marked; S-7/S-8 not → SR-18. |

**Tally: 8 closed · 3 closed-in-substance · 5 partially closed or surfaced · 12 not closed · 2 new errors introduced.**

---

## New security-relevant errors introduced by pass 2

1. **SR-1 — "`parity-contract.schema.json` closes exactly two axes."** False: it closes four required `$ref`-bound enums including `cli_exit_code` (15 members) and `auth_outcome_class` (6 members, four of which are distinctions PD10 retires). The two written off are the two that publish the surviving existence oracle, so the error removes the oracle surfaces from the correction's ownership scope.
2. **SR-4 — "`withheld` … does not collide with the shipped meaning of `redacted` in `safety-invariant-ci-gates.md` and `ConsoleStatusText.cs`."** Falsified by both cited artifacts, which define `redacted` *as* withheld. The companion claim "`withheld` has zero occurrences anywhere" is false in `src/` at four sites.
3. **SR-6 — internal contradiction** in one sentence: "the oracle … ha[s] no row for `authentication_failure`, `read_model_unavailable`, `projection_unavailable`" immediately followed by the mapping those 121 rows carry. Not a false claim about the repo so much as a false instruction about the work.

Everything else pass 2 asserts about the repo, I checked and it is true: the 65/72 mappings, the exit-72 wording, the `not_found`→73 survivals, the `auth_outcome_class` values, the matrix `:76`-vs-`:146` contradiction, the absence of `cli_exit_code`/`mcp_failure_kind` columns in the matrix, `AuthorizeDiagnosticReadAsync(allowBoundedStale: true)`, the Server's absent bridge writer (with a test pinning it), `previous-spine.yaml`'s `known_omissions`, and the Forgejo base-URL-plus-bearer sink. That is a markedly better verification record than pass 1.

---

## Minimum set to reach PASS-WITH-FINDINGS

All five are doc-only and sit inside the ratified scope.

1. **SR-2** — give the `dirty`+`WorkspaceLocked` guard a comparand that exists. Either restore `stagedByPrincipal` as the authoritative limb (delete "no transition reads it"), or declare the durable task→principal binding that S-8's task-scope conjunct evaluates against. Until one lands, intra-tenant staged-work takeover is reachable.
2. **SR-3** — assign `authorization_evidence_malformed` a canonical outcome, and state the totality rule that an unmapped denial outcome falls to the `safe-denial-404`, never 403.
3. **SR-1** — correct "closes exactly two axes" to the four the schema actually closes, and extend S-7's identity obligation from "the envelope" to every caller-observable outcome, with the `auth_outcome_class` narrowing and the revocation→73 retirement on the work list.
4. **SR-4** — rename `withheld`, or put the lockstep amendment of `safety-invariant-ci-gates.md:59` and the console string table on the PD8 work list; drop "zero occurrences anywhere".
5. **SR-5** — mark or correct A-8's unqualified `visibility` claim so the document stops citing as current contract what S-7 says must not be cited as current contract.

SR-7 (the tenant-partitioned parent-resolution seam) should land with SR-2, because the same paragraph resolves both.

---

## Credit where due

Pass 2 earned most of what it claimed, which was not true of pass 1. Specifically:

- **SU-4 is a model fix**: the S-6 qualifier and the C9 state-store inclusion were applied exactly, and the C9 parenthetical explains *why* the enumeration matters — a gate that omits it "certifies green over the exact hole." That sentence teaches the next reviewer something.
- **The S-7 "owed, not achieved" paragraph** replaces a false guarantee with a precise liability, naming the exit codes, the contradiction, and the surviving rows. Writing down that your own surfaces disagree is the hardest edit in a document like this and it was made without hedging.
- **The SSRF item took every correction, including the unflattering one** — that `safe-denial-404` was the wrong envelope because `no_action` mis-advises a caller who can fix the endpoint. It also adopted the credential framing ("with the tenant's bearer attached"), which is the sentence that changes how this gets prioritised.
- **The deny-by-default inversion** — moving the enumerable set from the protected surface to the opt-out set — is the correct structural answer and is now stated in one sentence a reviewer can approve.
- **The degraded-mode item refuses to claim the matrix arbitrates** when the matrix contradicts itself, and names the as-built third answer with its real method signature. Recording that the shipped behaviour is none of the three ratified options is the trustworthy kind of correction.
- **The bridge field-split arbitration is better than the document claims**: it is not only structural by interface, it is pinned by `FoldersContextSearchFacadeRegistrationTests.cs:102`.
