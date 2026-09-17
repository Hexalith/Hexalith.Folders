# Reviewer Gate — Security & Authorization Lens (update review)

- **Artifact under review:** `_bmad-output/planning-artifacts/architecture.md` (1902 lines, working tree, amended 2026-09-16)
- **Baseline:** HEAD `de281e7` + the uncommitted 2026-09-16 amendment (`+125/-52` across architecture.md, `.memlog.md`, `docs/exit-criteria/c6-transition-matrix-mapping.md`)
- **Prior report:** `review-security-2026-09-16.md` — **FAIL**, 1 critical / 8 high / 8 medium / 3 low
- **Ratified scope acknowledged:** architecture.md + co-normative docs only. No production code, no `epics.md`. Security work that needs code is correctly recorded rather than implemented; where the *recording* is too weak for the risk, I say so and treat that as the finding.
- **Mode:** read-only apart from this report. Every claim below was verified against the working tree with file:line.
- **Date:** 2026-09-16

---

## Verdict

**FAIL — materially narrowed. 0 critical, 5 high, 8 medium, 3 low.**

This is a good amendment and I want to be precise about how good. The prior gate's single highest-priority item — S-7 specifying a denial contract that contradicted the approved, digest-bound, test-pinned `authorization-matrix.md` on five dimensions while omitting the 401 outcome entirely — is **closed, correctly, and verbatim**. That was the A6b blocker. It is gone. The critical finding is downgraded to high on the strength of an honest, owned, decision-shaped recording. Four other prior findings are closed or substantially closed. Nothing in the amendment made anything worse.

It still fails, for a reason that is the same *class* as the prior failure one layer down: **the S-7 row now closes the existence oracle in the HTTP response body and asserts, in a new sentence, that a named artifact enforces this — and that artifact does not exist in the form claimed, while the surface it governs still routes the retired distinctions to distinct caller-visible outcomes.** `tests/fixtures/parity-contract.yaml` today assigns `auth_outcome_class: folder_acl_denied` to 35 operations, `tenant_access_denied` to 10, and `audit_access_denied` to 4; it routes `not_found` to CLI exit 73 on 22 rows and `tenant_access_denied` to exit 66 on 49. A CLI caller distinguishes absent from denied by exit code. The oracle S-7 removes from the body is intact one surface down, and the document's new claim that "CLI and MCP cannot disagree" is falsified by the same file it cites.

Three of the five highs are doc-only edits that fit inside the ratified scope. One (SU-1) needs the PD10 regeneration the document already obliges. One (SU-4) is the open item the sponsor deliberately left open, and my judgment on that is below.

---

## Direct answers to the questions put to this gate

### 1. SEC-2 / SEC-3 / SEC-4 / SEC-5 — the S-7 rewrite. Verified against the matrix and the real vocabulary.

**Transcription: exact.** I diffed `architecture.md:656` against `docs/contract/authorization-matrix.md:144-146` token by token:

| Outcome | Status | Category | Code | Retryable | Client action | Visibility |
| --- | --- | --- | --- | --- | --- | --- |
| `authentication-failure-401` | 401 ✓ | `authentication_failure` ✓ | `authentication_required` ✓ | false ✓ | `check_credentials` ✓ | `redacted` ✓ |
| `safe-denial-404` | 404 ✓ | `tenant_access_denied` ✓ | `resource_unavailable` ✓ | false ✓ | `no_action` ✓ | `redacted` ✓ |
| `authority-unavailable-503` | 503 ✓ | `read_model_unavailable` ✓ | `projection_unavailable` ✓ | true ✓ | `retry` ✓ | `redacted` ✓ |

All eighteen cells match. The invented categories `authorization` and `availability` and the invented code `authority_unavailable` are gone. The three canonical categories are confirmed members of `canonical_error_category` in `tests/fixtures/parity-contract.schema.json` (50 members). Naming the matrix as owner-on-disagreement — "on any disagreement the matrix wins and this row is the defect" — is the right governance construction and I would keep that sentence pattern for every transcribed value in this document.

- **SEC-2 (two codes, one predicate) — CLOSED.** The parenthetical is gone; the row now says "one exact envelope for every authenticated absent, hidden, wrong-tenant, revoked, disabled, unknown, or insufficient-scope cause — **a single code, chosen by no predicate**." That last clause is stronger than the matrix's own wording and is exactly right.
- **SEC-3 (non-existent categories) — CLOSED.** Verified against the schema enum, not just the matrix.
- **SEC-4 (missing 401; absent/malformed authority routed to a retryable 503) — CLOSED in substance.** The 401 is restored and is explicitly "evaluated before every other conjunct, so an unauthenticated caller is never routed to the outage envelope." That single clause closes two of my three sub-findings: the token-refresh signal is back, and the pre-authentication authority-health probe is gone (a prober without credentials now gets 401, uniformly). The residual is SU-13 (no `Retry-After`, no retry ceiling, no ingress-keying rule) and SU-12 (NFR76's own four-condition wording).
- **SEC-5 (remediation scoped to `category`) — CLOSED for the HTTP body, NOT closed for the caller-observable surface.** The new "Envelope identity extends past `category`" clause plus the closer "only the correlation identity and the per-request instance identifier may differ" is the right shape and does cover the six disclosing fields I found in `FolderAuthorizationDenialMapper.cs`. But S-7's obligation stops at the envelope; see **SU-1**, where the same distinction is still published through `cli_exit_code` and `mcp_failure_kind`.

**Does any oracle survive in a field the remediation missed?** Yes, two of them, one inside the body and one outside it — SU-10 and SU-1 below.

### 2. SEC-1 — is the sharpened open item an adequate interim disposition?

**Mostly yes, with one concrete inadequacy that is in scope and was not fixed.**

What the amendment does at `architecture.md:661,663` is close to a model recording. It states plainly that the carve-out *relocates* rather than removes the durable cleartext; identifies the store (D-1/D-2/D-3, shared Dapr state store); says no separate store, key, encryption-at-rest requirement or export prohibition is named; kills the "execution boundary" euphemism ("a tenant-prefixed key in a shared Redis is a naming convention, not an execution boundary"); notes that "never emitted" constrains egress and says nothing about snapshots, backups, replicas, dumps, or operator access; and — the part that matters most for a security sign-off — **states the interim reading: "Until this is settled, S-6's guarantee should be read as scoped to the evidence and observability surfaces, not as the unqualified claim the row makes."** It then names three candidate control sets, states a preference (HMAC the lock identity + re-derive the executor's ref at call time), and marks (c) as a fallback that "must be written down as a bounded exception with an owner, never as an unqualified guarantee plus a parenthetical."

That is an adequate disposition for an undecided architectural question. Leaving it open is **not** itself unacceptable, on three conditions, and the amendment meets two of them: the falsification is stated in the document rather than in a review; the decision is shaped, not merely routed (a reviewer can now say yes or no to a named option set); and PD8 is not implemented, so nothing is shipping against the false headline today.

**The third condition is not met, and it is a doc-only fix that was in scope.** Two things:

1. **The S-6 row still publishes the falsified sentence verbatim, unmarked.** `architecture.md:655` still reads "Cleartext confidential values are never made durable — not in an event, projection, audit record, log, trace, diagnostic, or generated artifact." A reader of the decision table — which is what a story author opens — reads a security guarantee the same document disowns six lines later. The amendment gave `NOT BUILT` markers to `TenantPrefixedCacheKey.cs`, an as-built banner to the project tree, and a "Target (not built)" label to I-3 and I-8. S-6's headline got none. One clause — "*(scoped to evidence and observability surfaces pending the open item below)*" — would have closed it.
2. **C9's verification method is scoped around the hole.** `architecture.md:355` requires "event-write token-substitution tests asserting no durable cleartext in any **event, projection, audit record, log, trace, or export**." That enumeration omits the operational state store, which is precisely and only where the carve-out puts the cleartext. The gate that exists to prove the guarantee is, by construction, incapable of detecting the residual risk, and will go green with confidential-tier cleartext sitting in Redis. See **SU-4** — this is the part I hold at HIGH.

### 3. SEC-7 (destination policy / SSRF) and SEC-9 (deny-by-default binding) — adequacy of the routed open items.

**SEC-7 → `architecture.md:677`. Adequate in form; under-scoped in substance, and it prescribes a wrong answer.**

Right: it concedes that S-8's derive-never-caller-supplied rule "structurally cannot cover" `ValidateProviderReadiness`, which is the concession S-8 needed. It names the mechanism-location question, the DNS-rebinding question, redirect policy, and who may register a privately-resolving destination. It makes the denial-envelope question explicit. Those are largely the right questions.

Wrong or missing, three ways — SU-8 and SU-9:

- **The sink is scoped to one operation and it isn't one operation.** `ForgejoProvider` takes an `IForgejoApiClientFactory` and uses it for every Forgejo call, not only readiness validation (`src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs:10,78`; `ForgejoHttpApiClientFactory.cs:45-52` sets `BaseAddress` and attaches the tenant's bearer). A destination policy scoped to `ValidateProviderReadiness` leaves every post-binding call unguarded against the same base URL. The open item must say "the authorized base URL, everywhere it is dereferenced," not "this operation."
- **"Re-resolves at call time" is not the control.** Re-resolution without **pinning the connection to the validated address** still loses the race; the control is resolve-then-connect-to-that-IP. The open item also omits connection timeouts, response-size caps, and an allowlist escape with a named approver. And it never states the impact that makes this worse than ordinary SSRF: **the tenant's provider bearer credential is attached to the request to the caller-named host.**
- **It prescribes the wrong envelope.** "its denial is the S-7 `safe-denial-404`, or the readiness probe becomes a network-existence oracle." The non-disclosure instinct is right; the chosen envelope is not. The approved matrix defines `safe-denial-404` as an *authenticated absent, hidden, wrong-tenant, revoked, disabled, unknown, or insufficient-scope* state. A prohibited-destination result is none of those, and its `clientAction: no_action` actively mis-advises the tenant admin who typo'd a hostname — the correct action is "fix the endpoint." The non-disclosure property that is actually needed is *constant shape within the readiness vocabulary*: one `provider_readiness_failed` envelope, byte-identical for prohibited-destination, unreachable, and timed-out. That preserves both properties; collapsing into the authorization 404 sacrifices actionability for a property it does not need.

**SEC-9 → `architecture.md:679`. Correct diagnosis; the remediation it prescribes cannot detect the threat it names.**

The framing is exactly right — "This is the difference between 'every operation we listed is protected' and 'an operation cannot be added unprotected', and only the second survives a new contributor." Naming the 13.2-vs-unowned-PD10 collision in the same paragraph is good.

But: "plus a conformance test that **enumerates the generated surface** and fails on any operation carrying neither." One sentence earlier the threat is "an endpoint added without the attribute is reachable." An endpoint added without the attribute is, by construction, **not in the generated surface** — it is in the ASP.NET routing table and nowhere else. The prescribed gate is blind to its own threat model. The test must enumerate `EndpointDataSource` / endpoint metadata at runtime and fail on any endpoint that carries neither an authorization requirement nor an entry on a reviewed anonymous allowlist. Two further omissions: the **sidecar-only app-port** half of HXF-SEC-003 is dropped entirely, and the environment-discriminator downgrade is not mentioned — `FoldersOidcOptionsValidator` and `FoldersAuthSchemeValidator` both short-circuit to success outside Production, and I-3's local profile is `defaultAction: allow`, so a host mis-labelled `Development` has neither JWT enforcement nor Dapr access control. See **SU-7**.

Neither open item changed `docs/exit-criteria/nfr-traceability.md:121-122`, so NFR75 and NFR76 still point at backlog stories rather than at decisions in this document. That file is co-normative and was in scope; the c6 mapping doc was updated in lockstep and this one was not.

### 4. SEC-8 (tokenizer width / rotation) and SEC-6 (S-7 ⊥ S-8) — closed, or at least surfaced?

**SEC-8 — NOT closed; half-surfaced.** `architecture.md:655` still says the token is "truncated to **a fixed width**." Grep confirms no bit length, byte length, or character count appears anywhere in the document. The new unowned-corrections bullet at `:281` does surface that PD8 "needs a tokenizer component, its per-tenant key management and rotation policy," and adds the genuinely valuable observation that the tokenizer has **no owning component at all** — which is a stronger version of my divergent-writers concern. But two load-bearing properties are surfaced nowhere: the **truncation width** (the entire collision-resistance budget; a collision silently merges two fields' evidence into one audit join) and the fact that **key rotation is a one-way destruction of audit-joinability** (cleartext is gone by construction, so old tokens can never be translated). See **SU-6**.

**SEC-6 — NOT closed, NOT surfaced, and now sharper.** S-8 at `:657` is byte-identical to the pre-amendment text; `GetTaskStatus` still "requires the current tenant, bound-folder read authority, and task scope," and the caller names only a task id. Meanwhile S-7's ordering language was *strengthened* to "**each** evaluated before **any** protected-resource lookup," and the 503 clause repeats it. Grep for "parent resolution" / "tenant-partitioned" returns nothing. The two rules remain jointly unsatisfiable for any operation whose authorizing parent is discovered from the caller-named child, and the amendment tightened the half that makes the contradiction bite. See **SU-5**.

### 5. `stagedByTaskId` / `stagedByPrincipal` — is the rule airtight, and does any other guard read caller input?

**No other guard reads caller input** — verified. The other three discriminated pairs read server-owned state: `changes_staged`+`CommitFailed` reads the shared provider-outcome classifier's retryable/non-retryable verdict (and the row correctly forbids per-adapter re-decision); `inaccessible`+`ProviderReadinessValidated` reads staged-content presence against the C3 window; `dirty`+`LockLeaseBecameStale` reads clean-vs-staged. Declaring the durable fields at all, stating they survive the lock instance, fixing their set/clear points, and recording "`stagedBy*` has zero occurrences in `src/` today" is good, honest work.

**But the `dirty`+`WorkspaceLocked` rule is not airtight — it binds one side of an equality and leaves the other side fully caller-controlled.** `architecture.md:446`:

> The `dirty` + `WorkspaceLocked` guard is `stagedByTaskId == the re-acquiring task`, and **not** the `X-Hexalith-Task-Id` request header, which is caller-supplied input and never authority.

The negation pins which side is durable. It never defines **"the re-acquiring task."** The only task-identity source this document defines is caller-supplied, at three places and with no server-side alternative: `A-10:694` ("`X-Hexalith-Task-Id` headers carry across REST, SDK, CLI, MCP"), the Adapter Parity Contract at `:705` ("Caller-provided via `X-Hexalith-Task-Id` header. SDK does not generate"), and the header table at `:913`. `tests/fixtures/parity-contract.schema.json` `task_id_sourcing` is `[not_task_scoped, caller_provided, cli_flag, mcp_tool_input]` — every member is caller-supplied. No durable task→principal binding is declared anywhere.

So the equality resolves to `stagedByTaskId == X-Hexalith-Task-Id`, which is the failure the paragraph says it prevents, in its own words: "trust the header and any principal who names another principal's task id resumes and commits their staged changes." And `stagedByPrincipal` — the one field that would close it — is declared and then **read by no guard**. It appears exactly twice in the document, in its own declaration and in the story bullet at `:280`. See **SU-3**.

---

## Findings

### SU-1 — HIGH — S-7 makes the HTTP envelope uniform; the C13 parity oracle still routes the retired distinctions to distinct CLI exit codes and MCP kinds, so the existence oracle survives one surface down

S-7's identity obligation is written about the error envelope. The C13 surface is not an error envelope, and it is caller-visible. Verified from `tests/fixtures/parity-contract.yaml`:

| Channel | Live values | S-7 status |
| --- | --- | --- |
| `auth_outcome_class` | `folder_acl_denied` ×35, `tenant_access_denied` ×10, `audit_access_denied` ×4 | two of the three are distinctions S-7 **retires** |
| `(category, cli_exit_code, mcp_failure_kind)` | `not_found` → 73 / `not_found` ×22 | retired from protected responses |
| | `folder_acl_denied` → 66 ×39, `cross_tenant_access_denied` → 66 ×6, `audit_access_denied` → 66 ×4 | retired |
| | `tenant_access_denied` → 66 ×49 | the surviving outcome |
| | `authorization_revocation_detected` → 73 / `not_found` ×6 | "revoked" is an explicit `safe-denial-404` cause in the matrix |

A CLI caller today distinguishes **absent** (exit 73, `not_found`) from **denied** (exit 66) with a shell conditional, and an MCP caller reads it straight off `kind`. `architecture.md:719` documents exit 66 as "Authorization failed at tenant or folder ACL boundary" and `:726` documents 73 as "Resource not found within tenant scope" — the oracle in prose, in the same document that removes it from the wire.

The PD10 regeneration list at `:665` names "CLI/MCP parity fixtures," so the `.yaml` is in scope for regeneration. What is **not** stated anywhere is the requirement that makes regeneration correct: on a protected operation, the CLI exit code and MCP failure kind must also be constant across every `safe-denial-404` cause, and `not_found` must be unreachable on protected operations. Without that sentence, a regeneration that collapses the HTTP body while preserving per-operation exit codes is conformant to S-7 as written and still ships the oracle.

**Attack class:** authenticated resource enumeration via CLI exit code / MCP `kind`, on 49 operations, scriptable.

**Correction.** Extend the S-7 identity clause from "the envelope" to "every caller-observable outcome": HTTP envelope, `cli_exit_code`, `mcp_failure_kind`, and `auth_outcome_class`. Add to the PD10 work list: narrow the `auth_outcome_class` enum in `parity-contract.schema.json` to the post-correction set, and assert in the C13 gate that no protected-operation row carries `not_found`, `folder_acl_denied`, `cross_tenant_access_denied`, or `audit_access_denied`.

---

### SU-2 — HIGH — S-7's new enforcement claim is false against the artifact it names, and the retryability guarantee in the same sentence is inverted by this document's own exit-code table

`architecture.md:656`, new text:

> Every token above is a member of the closed canonical vocabulary in `tests/fixtures/parity-contract.schema.json`; the C13 `cli_exit_code` / `mcp_failure_kind` columns are **derived from the matrix**, so CLI and MCP cannot disagree on whether an authority outage is retryable

Both halves fail.

**Half one — the vocabulary claim.** The schema carries `canonical_error_category` (50), `cli_exit_code` (15), `mcp_failure_kind` (49), `auth_outcome_class` (6) and several sourcing enums. It carries **no** `code` vocabulary, **no** `clientAction` vocabulary, and **no** `visibility` vocabulary. Grep count in that file: `authentication_required` 0, `resource_unavailable` 0, `check_credentials` 0, `no_action` 0, `"retry"` 0, `clientAction`/`client_action` 0, `visibility` 0. Nine of the eighteen transcribed tokens have no home in the cited artifact. And the one enum in it that *does* speak to authorization outcomes — `auth_outcome_class` — still enumerates `folder_acl_denied`, `audit_access_denied` and `safe_not_found`, i.e. the distinctions S-7 retires. The row cites as its guarantee a file that contradicts it.

**Half two — the retryability inversion.** The oracle maps `read_model_unavailable` → CLI exit **72** (37 rows) and `projection_unavailable` → 72 (35 rows). `architecture.md:725` documents exit 72 as: "Workspace in `reconciliation_required` state; **not retryable** until cleared." The matrix says the 503 is `retryable: true`, clientAction `retry`. So a script keyed on the CLI exit code is told *not* to retry exactly the outcome the matrix says to retry — in the same sentence that promises "CLI and MCP cannot disagree on whether an authority outage is retryable." (`authentication_failure` similarly lands on exit 65, documented as `credential_missing`; tolerable, but it is a many-to-one collapse the document nowhere acknowledges.)

**Why this is a security finding and not a documentation nit.** The row's claim is the *only* stated mechanism preventing two surfaces from inventing different denial vocabularies, and it is the justification offered for not restating the values independently. A false enforcement claim is worse than a silent gap, because it terminates the search.

**Correction.** Either make the claim true — add closed `code`, `clientAction` and `visibility` vocabularies to `parity-contract.schema.json`, narrow `auth_outcome_class`, and add an explicit `authentication_failure` and `read_model_unavailable` row to the CLI exit-code table with retryability that matches the matrix — or replace the sentence with the honest one: "the code, clientAction and visibility vocabularies are not yet closed in any fixture; closing them is part of the PD10 change set." Do not ship the current sentence.

---

### SU-3 — HIGH — the `stagedByTaskId` guard binds only the durable side of its equality; the comparand has no server-side definition and `stagedByPrincipal` is read by no guard

Full analysis in §5 above. In short: `architecture.md:446` forbids the guard from *being* the `X-Hexalith-Task-Id` header, but the guard is an equality and the document defines no non-caller source for the other operand. `A-10:694`, `:705`, `:913` and `parity-contract.schema.json`'s `task_id_sourcing` enum all make task identity caller-supplied without exception. `stagedByPrincipal` is declared at `:446`, listed as owed work at `:280`, and never appears in a guard predicate.

**Attack class:** workspace takeover — a principal inside the same tenant who learns or guesses another principal's task id (ULIDs are time-ordered and echoed in responses and audit evidence) presents it in `X-Hexalith-Task-Id`, the durable `stagedByTaskId` matches, the guard passes, and they re-acquire the lock and commit another principal's staged changes to the customer's repository. This is the exact scenario the paragraph enumerates as the failure it prevents.

**Correction, doc-only and in scope.** State the guard as a conjunction whose authoritative limb is the principal: `stagedByPrincipal == the authenticated principal (from `sub`, per S-2)` **and** `stagedByTaskId == the presented task id`. Then add the missing declaration: a task id is bound at creation to the principal who created it, that binding is durable, and the task-scope conjunct of S-8 is evaluated against it — so a presented task id that is not bound to the caller yields the S-7 `safe-denial-404` before the lock guard is reached. Without the task→principal binding the header remains authority by the back door regardless of how the guard is worded.

---

### SU-4 — HIGH — SEC-1 stays open, the S-6 row still publishes the falsified headline unmarked, and C9's verification method is scoped around the hole it is meant to close

Judgment on the disposition is in §2 above: the recording is adequate as a routed decision, and leaving it open is defensible. Two things keep it at HIGH rather than resolved-as-recorded.

1. **`architecture.md:655` still reads "Cleartext confidential values are never made durable"** with no qualifier, no target-state marker, and no pointer to the paragraph six lines below that disowns it. Every other stratum of this document received honesty labelling in this same amendment — `NOT BUILT` on `TenantPrefixedCacheKey.cs` and the whole `RateLimiting/` subtree, "Target (not built)" on I-8, the as-built banner on the project tree, the de-checkmarked coverage section. The one sentence the document itself calls false is the one that kept its unqualified present tense.
2. **`architecture.md:355` (C9 verification method)** requires tests "asserting no durable cleartext in any **event, projection, audit record, log, trace, or export**." The carve-out puts the cleartext in the Dapr **state store**, which is not on that list. The gate cannot fail on the risk. If the fallback control set (c) is ever chosen, C9 will certify "no durable cleartext" over a system that has durable cleartext, and the certificate will be technically accurate against its own enumeration. That is the failure mode the prior review named for read-time redaction, reproduced in the evidence layer.

**Correction, both doc-only and in scope.** Add one clause to the S-6 row: "*(scoped to evidence and observability surfaces pending the open item below)*". Add "state store and any other durable operational store" to C9's enumeration at `:355`, so that choosing option (c) automatically obliges a sentinel over a state-store export.

---

### SU-5 — HIGH — S-7 and S-8 remain jointly unsatisfiable for task-derived scope, and the amendment tightened the half that makes it bite

Carried from SEC-6, unaddressed and unsurfaced. `GetTaskStatus` is `/tasks/{taskId}/status`; the caller names only a task id; S-8 (`:657`, unchanged) requires **bound-folder read authority**; learning which folder bounds the task requires reading the task record; S-7 (`:656`) now says each of the three outcomes is "evaluated before **any** protected-resource lookup." The pre-amendment wording was "evaluated before lookup"; the new wording is universally quantified and repeated twice, so the contradiction is strictly sharper than when I first reported it.

The approved matrix flags a related divergence as `G10` (`authorization-matrix.md:350`) but frames it as token comparison, not as an ordering impossibility, so the gap is not covered there either. An implementer forced to choose will look up first and let the lookup result influence the denial — restoring the oracle in the one operation family where it is easiest to reach.

**Correction, doc-only.** Add a fourth clause to the S-4 evaluation order and to S-8: a **tenant-partitioned parent resolution** is not a protected-resource lookup for S-7 purposes, provided (a) it is constrained to the caller's authoritative tenant partition at the storage-key level so it cannot cross tenants by construction, (b) it returns only the parent identifier and no resource state, and (c) its miss and its hit produce the identical `safe-denial-404`. This is also the seam that SU-3's task→principal binding needs, so the two corrections land together.

---

### SU-6 — MEDIUM — the tokenizer truncation width is still unspecified, and key rotation as a one-way destruction of audit joins is stated nowhere

`architecture.md:655`: "truncated to **a fixed width** and carried with its key version." No width appears anywhere in the document. Truncation length *is* the collision-resistance budget; at 64 bits, two distinct confidential values within one tenant collide around 2³² values, and a collision **merges two different fields' evidence into one audit join** — a silent integrity failure in the audit trail this product sells. Two implementers will pick different widths, which is the same divergence class the amendment correctly worries about at `:281` ("two implementations that produce different tokens for the same value") one level down.

Separately, "carried with its key version" implies rotation, and rotation produces a different token for the same value, so every evidence join across a rotation boundary returns empty. Re-tokenization is impossible by construction — the cleartext is gone, which is the point. **Key rotation is therefore a one-way destruction of audit-joinability**, including emergency rotation after suspected key compromise, which makes it an incident-response failure mode. The document says none of this. `:281` surfaces that a "rotation policy" is owed; it does not surface what rotation costs.

**Correction.** Pin the width to a stated bit length (128 bits / 22 Base64Url characters is the defensible floor) as a constant in the single shared write-path implementation. State the rotation consequence and pick: joins are scoped to a key epoch and cross-epoch joins are impossible (making rotation an audit-continuity event requiring approval), or a long-lived key with a documented compromise-response plan. Silence is the one option not available.

---

### SU-7 — MEDIUM — the deny-by-default open item prescribes a conformance test that cannot detect the threat it names, and drops two dimensions of HXF-SEC-003

`architecture.md:679` names the threat correctly ("an endpoint added without the attribute is reachable") and then prescribes "a conformance test that enumerates **the generated surface**." An endpoint added without the attribute is by definition absent from the generated surface. The gate must enumerate runtime endpoint metadata (`EndpointDataSource`), not the Contract Spine inventory, and fail on any endpoint carrying neither an authorization requirement nor membership in a reviewed, enumerated anonymous allowlist (health probes being the expected set).

Two dimensions dropped from the prior finding: the **sidecar-only app-port** boundary (the other half of HXF-SEC-003, expressed in no manifest and no decision), and the **environment-discriminator downgrade** — `FoldersOidcOptionsValidator` and `FoldersAuthSchemeValidator` both short-circuit to success outside Production, and I-3's local profile is `defaultAction: allow`, so a host mis-configured as `Development` has neither JWT enforcement nor Dapr access control, and no decision requires the environment discriminator itself to be a protected, verified input.

---

### SU-8 — MEDIUM — the destination-policy open item under-scopes the sink to one operation and omits the controls that actually stop the attack

`architecture.md:677` scopes the problem to `ValidateProviderReadiness`. The authorized base URL is consumed by `ForgejoProvider` for every Forgejo operation via `IForgejoApiClientFactory` (`ForgejoProvider.cs:10,78`), and `ForgejoHttpApiClientFactory.cs:45-52` sets it as `BaseAddress` **and attaches the tenant's Forgejo bearer credential**. `ConnectCallback`, `IPAddress`, `Dns.`, `IsLoopback`, and `169.254` have zero occurrences in `src/` or `tests/`.

Missing from the recorded questions: **resolve-then-pin** (re-resolution alone still races; the control is connecting to the validated address, not re-validating a name), connection timeout and response-size caps, an explicit per-deployment allowlist escape with a named approver, and — the impact sentence that changes how this gets prioritised — **the tenant's provider credential is transmitted to whatever host the tenant names**. The open item reads as an SSRF question; it is also a credential-exfiltration question.

Note Epic 13 prose at `:271` does name `ConnectCallback` for HXF-SEC-002, so the open item's "this document decides none" is slightly overstated — but substantively right: a backlog scope note is not a decision row, and it names no resolve-then-pin, no caps, and no coverage beyond readiness.

---

### SU-9 — MEDIUM — the destination-policy open item prescribes `safe-denial-404`, which conflicts with the approved matrix's own definition and mis-advises the caller

`architecture.md:677`: "what the denial envelope is — it must be the S-7 `safe-denial-404`, or the readiness probe becomes a network-existence oracle for the tenant's private ranges."

The non-disclosure instinct is correct and the oracle risk is real. The prescribed envelope is not. `authorization-matrix.md:145` defines `safe-denial-404` as "Any authenticated **absent, hidden, wrong-tenant, revoked, disabled, unknown, or insufficient-scope** state." A prohibited-destination result is none of those; routing it there widens an approved, digest-bound outcome definition by prose in a different document — the exact governance failure the amendment's own owner-on-disagreement rule exists to prevent. It also hands a tenant admin who mistyped a hostname `clientAction: no_action`, which is actively wrong guidance.

**Correction.** The property needed is *constant shape within the readiness vocabulary*, not collapse into the authorization vocabulary: one `provider_readiness_failed` envelope, byte-identical across prohibited-destination, unreachable, DNS-failure and timeout, with no field distinguishing them. That removes the network-existence oracle and keeps the response actionable.

---

### SU-10 — MEDIUM — the byte-identity enumeration omits three fields the live mapper emits, and the omitted one is the variable one

`architecture.md:656` enumerates "status, category, code, message, `type` URI, detail keys, `retryReasonCode`, `layer`, and `timingBucket`." `FolderAuthorizationDenialMapper.cs:21-44` emits, inside `details`: `visibility`, `retryReasonCode`, `reasonCategory`, `evidenceSource`, `layer`, `policyClass`, `freshnessClass`, `timingBucket` — plus a top-level `taskId`.

Three are not on the list: `reasonCategory` (mirrors `category`, safe), `policyClass` (per-operation constant, safe), and **`freshnessClass`** — which varies with which read model was consulted and how fresh it was, i.e. the one field in the set whose value is a function of the authorization path taken. The universal closer ("only the correlation identity and the per-request instance identifier may differ") does cover it, so the rule is *correct*. But a remediator working an explicit list of eight next to a general clause patches the eight. Note also that the matrix's own wording is "detail **keys** are identical," which constrains key names and not values; the architecture's closer is stronger and should be the one quoted.

**Correction.** Either add `freshnessClass` (and drop the others as derived-constant) or delete the enumeration and keep only the universal closer with an explicit "including but not limited to" and a pointer to the mapper as the current field inventory.

---

### SU-11 — MEDIUM — `visibility` is still specified in two wire positions, and OQ2 is still absent from the superseded-approval table

Partially improved: the 404's value is now stated (`redacted`, matching the matrix) which closes the third oracle channel I reported. Two halves remain.

1. **Two positions.** `architecture.md:656` and A-8 (`:692`) both say "`visibility` is a required field on every error" (top-level), while all three transcribed outcomes specify `details.visibility: redacted` (nested), and the matrix column header is "Details visibility". Verified: `ProblemDetails.required` at `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:7635-7645` does not include `visibility`; all five required-`visibility` sites are nested. The new owner-on-disagreement rule arguably resolves this in favour of `details.visibility`, but A-8 was not updated and still asserts the top-level form, so the document contradicts itself in two rows of the same section.
2. **OQ2 supersession.** `ExactFileProblem.details` is `additionalProperties: false, required: [visibility], enum: [redacted, metadata_only]` (`hexalith.folders.v1.yaml:7738-7748`), with `RedactionMetadata` (`:10307-10318`), four `allOf` invariants, the runtime enum `RedactionVisibility.cs:7-11` (two members), and the exact-member check in `Oq2WireObjectConverter.cs:458`. Adding `withheld` breaks a closed, OQ2-approved contract. The superseded-approval table at `:229-234` still lists OQ3, C3, C6 and C9 — not OQ2 — and the PD10 regeneration list at `:665` still does not name the OQ2 problem schemas. `RedactionDisclosureMapper.cs:22-27` throws on an unrecognised visibility value, so shipping `withheld` to the wire ahead of the mapper turns a new confidential field into a UI exception.

---

### SU-12 — MEDIUM — NFR76's own wording is now half-contradicted by the ratified 503, and the co-normative row was not updated in lockstep

`docs/exit-criteria/nfr-traceability.md:122` states NFR76 as "fail-safe deny-by-default on **absent, stale, malformed, or unavailable** authority." The amendment's degraded-mode reconciliation at `architecture.md:673` ratifies that stale-or-unavailable authority evidence returns the **retryable 503**, not a denial. The absent/malformed limb is now correctly served by the 401; the stale/unavailable limb is served by an availability answer.

This is defensible — the 503 grants nothing, so it is fail-closed in the sense that matters — but the document never says so, and the four-condition NFR wording is what Story 13.2 will build against. The c6 mapping doc received a full lockstep edit in this same amendment; the NFR traceability rows did not, although they are equally co-normative and equally in scope.

**Correction.** State the binding explicitly in S-7 or the degraded-mode paragraph: deny-by-default is satisfied by the 401 and 404 envelopes for the absent/malformed/denied conditions, and by the 503 for the unavailable condition, which grants nothing and is therefore fail-closed rather than a degradation. Then re-word NFR76's row to match, so 13.2 cannot read the four-condition sentence as an instruction to build a second shape.

---

### SU-13 — MEDIUM — the now-ratified retryable 503 has no `Retry-After`, no retry ceiling, and no ingress rate-limit keying rule

`Retry-After` has zero occurrences in architecture.md. The degraded-mode paragraph at `:673` explicitly ratifies that "while Tenants is unavailable beyond the staleness bound, protected reads return a retryable 503" and tells `OQ12`/`OQ13` to accept that envelope — so sustained, fleet-wide 503s during an authority outage are now a *designed* state, and every conforming client is instructed to `retry`. With no `Retry-After` and no stated ceiling, conforming clients become a self-inflicted load amplifier against the authority path during exactly the incident the 503 signals.

Ingress rate limiting remains HXF-SEC-005 (`:271`, Epic 13, backlog); I-8 is provider *egress* only and is now correctly labelled not-built. The document still states no keying rule, so if ingress buckets are ever keyed per-resource rather than per-tenant, a 429 becomes an existence signal that S-7's envelope identity does not reach.

Severity reduced from the prior report because the 401 now absorbs the pre-authentication case, which removes the unauthenticated amplification vector.

---

### SU-14 — LOW — S-7 still hard-codes "49" in the same amendment that removed the hard-coded denominator two hundred lines above

`architecture.md:656`: "All **49** protected Contract Spine operations evaluate authority before resource lookup." The current-reality table at `:240` was corrected in this amendment to "every protected operation in the current generated Contract Spine inventory … the denominator is the generated inventory, never a number transcribed here" — which also fixed the phantom "49 of 50" (SEC-18 closed). The same fix was not applied to S-7, which the document's own rules at `:732` and `:1739` forbid. Replace with "every protected Contract Spine operation in the current generated inventory."

---

### SU-15 — LOW — three protected operations cannot express the safe-denial envelope today, and that is still not in the PD10 work list

`authorization-matrix.md:341` (`G1`): 404 is declared on **46 of 49** operations. Three protected operations therefore cannot emit `safe-denial-404` without a spine change. The PD10 regeneration list at `:665`/`:279` now enumerates six artifacts plus the missing status-code/error-vocabulary drift surface — a real improvement — but still does not name adding the 404 response to those three operations. A change set executed exactly as listed leaves three operations unable to express the only permitted denial.

---

### SU-16 — LOW — S-6, S-7 and S-8 are the only strata in the document that did not receive the amendment's own honesty labelling

Carried from SEC-20. This amendment added `NOT BUILT` markers, as-built banners, "Target (not built)" labels on I-3 and I-8, a corrected C10 row, and a de-checkmarked coverage section — a consistent and genuinely valuable convention. The three PD rows still read as unqualified present-tense mechanism ("Cleartext confidential values **are never** made durable", "All 49 protected Contract Spine operations **evaluate** authority before resource lookup"), with the target-state statement 400 lines above at `:236`. A story author opening §"Authentication & Security" directly reads them as current guarantees. Prefix each with the same marker the rest of the document now uses.

---

## Prior-finding closure tally

| Prior finding | Severity | Status after the 2026-09-16 amendment |
| --- | --- | --- |
| SEC-1 — PD8 carve-out sanctions durable cleartext with no boundary | CRITICAL | **OPEN, downgraded to HIGH (SU-4).** Honestly recorded, falsification stated, interim reading given, three candidate control sets named with a stated preference and an A7b routing. Not closed: S-6's row still publishes the unqualified headline, and C9's evidence enumeration omits the state store. |
| SEC-2 — two codes in the "one exact envelope" | HIGH | **CLOSED.** Single code, "chosen by no predicate." |
| SEC-3 — invented categories contradicting the matrix on five dimensions | HIGH | **CLOSED.** All 18 cells verified against `authorization-matrix.md:144-146` and the schema enum. |
| SEC-4 — 401 dropped; absent/malformed authority → retryable 503 | HIGH | **CLOSED in substance.** 401 restored and evaluated first; pre-auth probe removed. Residual → SU-12, SU-13. |
| SEC-5 — remediation scoped to `category`, six fields still disclosing | HIGH | **CLOSED for the HTTP body** (envelope identity + universal closer). **Not closed for caller-observable outcomes** → SU-1; enumeration gap → SU-10. |
| SEC-6 — S-7 ⊥ S-8 for task-derived scope | HIGH | **NOT CLOSED, NOT SURFACED, and sharpened** by S-7's stronger ordering quantifier → SU-5. |
| SEC-7 — destination policy / SSRF sink has no owning decision | HIGH | **ROUTED (scope-appropriate), partially adequate** → SU-8 (under-scoped to one operation; missing resolve-then-pin, caps, credential-exfil framing), SU-9 (prescribes the wrong envelope). |
| SEC-8 — truncation width, key management, rotation destroying joins | HIGH | **PARTIALLY SURFACED.** Key management + rotation policy named as owed work at `:281`, plus the valuable new finding that the tokenizer has no owning component. Width and the rotation→join-destruction property surfaced nowhere → SU-6. |
| SEC-9 — no deny-by-default binding for the HTTP surface | HIGH | **ROUTED (scope-appropriate), diagnosis right, prescribed gate wrong** → SU-7. |
| SEC-10 — timing / ingress rate limits / freshness header / cursors | MEDIUM | **NOT CLOSED.** Cursors still absent from S-8's dimension list; ingress keying still unstated → partially SU-13. |
| SEC-11 — F-6 incident path outside the authorization model | MEDIUM | **NOT CLOSED, unchanged.** `G4`/`G6` still open; UI-boundary contradiction and the availability paradox both stand. |
| SEC-12 — C10/C9 artifact locations wrong; audit sanitizer weakest of nine | MEDIUM | **HALF CLOSED.** C10 corrected to the real as-built gate at `:355`, with `NOT BUILT` markers at `:1330` and concerns #1/#13 — good work. The nine divergent sensitive-value predicates and the C9-named sanitizer lacking the `ghp_`/JWT/PEM regexes are unchanged. |
| SEC-13 — S-2 missing `ValidAlgorithms`/`typ`, lifetime ceiling, revocation | MEDIUM | **NOT CLOSED, unchanged.** |
| SEC-14 — shared `folders-index` tenant; PD8 tokens break and weaken it | MEDIUM | **NOT CLOSED.** The new bridge-arbitration rule at `:675` is a genuine improvement to a different risk (two-writer replay), but the cross-tenant trim negative test, the Memories bearer's absence from S-5, and the tokenize-vs-exclude decision are unchanged. |
| SEC-15 — `visibility` in two positions; 404 value unstated; OQ2 unsuperseded | MEDIUM | **ONE THIRD CLOSED.** The 404's value is now `redacted`. Two positions and OQ2 both stand → SU-11. |
| SEC-16 — no conformance gate for the flagship correction; stale ✅ coverage | MEDIUM | **HALF CLOSED.** The ✅ heading is removed and the NFR74–84 hole is stated honestly at `:1748` — a real fix. No denial-envelope conformance gate was added to the `:1095` gate list, and the Security bullet at `:1750` still omits S-7 and S-8. |
| SEC-17 — denial contract undefined on the 202-Accepted async path | MEDIUM | **NOT CLOSED, unchanged.** |
| SEC-18 — "49 of 50"; hard-coded denominator | LOW | **HALF CLOSED.** The phantom 50th operation is gone from `:240`. S-7 still hard-codes 49 → SU-14. The 46-of-49 gap → SU-15. |
| SEC-19 — canonical example missing `visibility`; `withheld` unmapped | LOW | **NOT CLOSED, unchanged.** |
| SEC-20 — S-6/S-7/S-8 read as current mechanism | LOW | **NOT CLOSED** — and now conspicuous, because every other stratum got labelled → SU-16. |

**Tally:** 1 critical downgraded (not closed) · 4 high closed · 2 high routed as scope-appropriate open items with adequacy findings · 2 high not closed · 1 high partially surfaced · 2 medium half-closed · 1 medium one-third closed · 1 low half-closed · 8 not closed and unchanged.

---

## Coverage table — focus areas and verdicts

| Focus area | Prior | Now | Findings |
| --- | --- | --- | --- |
| S-7 wire values vs the approved matrix | FAIL | **PASS** | All 18 cells verified exact; owner-on-disagreement rule is the right construction |
| S-7 — 401 restored; deny-by-default inversion | FAIL | **PASS with residual** | SU-12 (NFR76 wording), SU-13 (`Retry-After` / ceiling) |
| S-7 — residual oracles in the HTTP envelope | FAIL | **PASS with residual** | SU-10 (`freshnessClass` off the enumeration) |
| S-7 — residual oracles outside the envelope | FAIL | **FAIL** | SU-1 (`cli_exit_code` 73 vs 66; `auth_outcome_class`; `mcp_failure_kind`) |
| S-7 — enforcement claim vs the cited artifact | n/a (new claim) | **FAIL** | SU-2 (no code/clientAction/visibility vocabulary; retryability inverted at exit 72) |
| S-7 ⊥ S-8 ordering for task-derived scope | FAIL | **FAIL (worse)** | SU-5 |
| S-7 — async 202 path, timing, cursors | FAIL | **FAIL (unchanged)** | prior SEC-17, SEC-10 |
| S-8 — caller-supplied locator / SSRF sink | FAIL | **ROUTED, partially adequate** | SU-8, SU-9 |
| S-6 — no durable cleartext vs the operational carve-out | FAIL (critical) | **OPEN, adequately recorded, inadequately published** | SU-4 |
| S-6 — truncation width / key management / rotation | FAIL | **PARTIAL** | SU-6 |
| PD11 guards — do any read caller input? | n/a (new) | **FAIL for one of four** | SU-3 (`dirty`+`WorkspaceLocked` comparand; `stagedByPrincipal` unused) |
| PD11 guards — the other three | n/a (new) | **PASS** | provider-outcome classifier, staged-content presence, clean-vs-staged: all server-owned |
| Deny-by-default binding at the HTTP surface | FAIL | **ROUTED, gate prescription wrong** | SU-7 |
| Honesty labelling of the security strata | FAIL (SEC-20) | **FAIL (now anomalous)** | SU-16 |
| Degraded-mode reconciliation (concern #20 vs S-4/S-7) | not reported | **PASS** | resolved in favour of the approved matrix, consequence priced — good work |
| Bridge single-writer arbitration (10.7 vs 12.5) | not reported | **PASS** | `:675`, closes a replay-empties-search hole |
| C10 / as-built artifact accuracy | FAIL (SEC-12) | **PASS** | corrected at `:355`, `:1330`, concerns #1/#13 |
| NFR coverage honesty | FAIL (SEC-16) | **PASS** | ✅ removed; NFR74–84 hole stated |
| Multi-tenant isolation — identity, streams, topics | PASS | **PASS** | unchanged |
| Multi-tenant isolation — shared search index | FAIL | **FAIL (unchanged)** | prior SEC-14 |
| Credential and secret handling | FAIL | **FAIL (unchanged)** | prior SEC-12, SEC-14; SU-8 adds the credential-exfil framing |
| AuthN — OIDC validation parameters | PARTIAL FAIL | **PARTIAL FAIL (unchanged)** | prior SEC-13 |
| Webhook / callback authenticity | PASS | **PASS** | unchanged |

---

## Minimum set to reach PASS-WITH-FINDINGS

Four of these five are doc-only and fit inside the ratified scope.

1. **SU-2** — delete or make true the sentence "Every token above is a member of the closed canonical vocabulary in `tests/fixtures/parity-contract.schema.json` … so CLI and MCP cannot disagree on whether an authority outage is retryable." As written it is false in both halves and it is the row's only stated enforcement mechanism. *(doc-only)*
2. **SU-1** — extend S-7's identity obligation from "the envelope" to "every caller-observable outcome, including `cli_exit_code`, `mcp_failure_kind` and `auth_outcome_class`", and add the enum narrowing + the "no `not_found` on protected operations" assertion to the PD10 work list. *(doc-only; the regeneration itself is already obliged)*
3. **SU-3** — restate the `dirty`+`WorkspaceLocked` guard as `stagedByPrincipal == authenticated principal` **and** `stagedByTaskId == presented task id`, and declare the durable task→principal binding that S-8's task-scope conjunct evaluates against. Without it the header is authority by the back door. *(doc-only)*
4. **SU-5** — add the tenant-partitioned parent-resolution carve-out to S-4/S-8 so S-7's ordering rule is satisfiable as stated. *(doc-only)*
5. **SU-4** — qualify S-6's headline in the row itself, and add "state store and any other durable operational store" to C9's verification enumeration so the fallback control set cannot certify green over the hole. *(doc-only)*

SU-6 (truncation width) should land in the same pass; it is one number and it is the difference between a joinable audit trail and a silently merged one.

---

## Credit where due

The following are verified improvements and a later pass should not undo them:

- The S-7 rewrite is exact against the approved matrix, and naming that matrix as owner-on-disagreement — "on any disagreement the matrix wins and **this row is the defect**" — is a governance pattern worth copying to every transcribed value in the document.
- The SEC-1 recording is the right shape for an undecided question: it states the falsification, gives the interim reading, names three candidate control sets, states a preference, and routes to named reviewers under A7b. That is a decision brief, not a deferral.
- The degraded-mode reconciliation at `:673` resolves a genuine two-way contradiction in favour of the approved artifact and **prices the availability consequence** rather than hiding it.
- The bridge single-writer arbitration at `:675` closes a two-writer hole between Stories 10.7 and 12.5 that would have manifested as a replay silently emptying every search result.
- The PD11 triple-keying propagation, with the explicit default rule that an unenumerated guard branch **rejects and is never routed to the sibling branch's outcome**, is exactly right, and the co-normative lockstep into `c6-transition-matrix-mapping.md` was done rather than promised.
- The current-reality table was corrected in the **strict** direction (three-of-five rejected, two accepted onto the unguarded branch so the guarded outcome is unreachable rather than absent) — a correction that makes the document look worse and is therefore the most trustworthy kind.
- The as-built/target strata labelling, the de-checkmarked coverage section, the C10 correction, and the removal rather than refresh of the version pins all reduce the document's capacity to mislead a release reviewer.
