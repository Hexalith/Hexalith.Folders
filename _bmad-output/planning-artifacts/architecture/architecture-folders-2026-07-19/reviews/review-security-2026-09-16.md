# Reviewer Gate — Security & Authorization Lens

- **Artifact under review:** `_bmad-output/planning-artifacts/architecture.md` (1857 lines, HEAD `de281e7`, working tree clean)
- **Lens:** security and authorization sign-off for a multi-tenant control plane that brokers access to customer Git repositories on behalf of AI agents
- **Focus:** S-7 (PD10 denial envelopes), S-8 (derived scope), S-6 (PD8 confidential tokenization), F-6 (incident authorization), A-8, A-11, credential/SSRF surface, multi-tenant isolation, AuthN
- **Mode:** read-only. No file outside this report was modified.
- **Prior reports read (to avoid re-reporting):** `review-adversarial-2026-09-15.md`, `review-authority-conformance-2026-09-15.md`, `review-rubric-2026-09-15.md`, `review-technology-2026-09-15.md`
- **Date:** 2026-09-16

---

## Verdict

**FAIL** — 1 critical, 8 high, 7 medium, 3 low.

The 2026-09-15 amendment fixed several things the prior gate flagged (the S-6 token derivation is now pinned to keyed HMAC; the `visibility` value set is now enumerated; `withheld` vs `redacted` is now reconciled; the honesty labels and the "current reality" table at lines 238–243 now exist; A-11 migration is now an explicit open item). Credit where due.

But the flagship security decision, **S-7, is wrong in three independent ways against the approved artifact it is supposed to implement**, and one of them re-creates by construction the exact existence oracle S-7 exists to remove. Separately, the one durable-cleartext carve-out the document records as an open assumption (line 643) is, as written, an unbounded sanction to persist confidential-tier cleartext — which is the document's own definition of the failure it forbids.

Three notes on method, because they bound what I called a finding:

1. The document explicitly labels PD8/PD10/PD11 as **target state** (line 236) and carries a verified current-reality table (lines 238–243). I did **not** report "the code does not do this yet" as a defect for those three. I *did* report where the target state contradicts an **approved, digest-bound, test-pinned** artifact, and where the stated remediation scope is too narrow to actually remove the oracle.
2. Every code claim below was verified against the working tree with file:line.
3. Where I could not verify, I say "unverified".

---

## Findings

### SEC-1 — CRITICAL — The PD8 "operational path" carve-out sanctions durable cleartext of confidential-tier values in an unnamed store, with no boundary, no encryption requirement, and no export prohibition

**architecture.md:643:**

> "The architecture's assumption, pending confirmation, is that **tokenization applies to the evidence and observability surfaces** (audit records, projections, logs, traces, diagnostics, search index, exports) and **not** to the operational fields the lock identity and provider executor consume, which stay cleartext inside the tenant's own execution boundary and are never emitted."

**Against S-6's own headline guarantee, architecture.md:639:**

> "Cleartext confidential values are never made durable — not in an event, projection, audit record, log, trace, diagnostic, or generated artifact — so confidentiality never depends on read-time redaction."

**And S-6's own rationale for why read-time redaction is unacceptable, architecture.md:639:**

> "Durable cleartext plus read-time redaction is a persistence/redaction contradiction: every later reader — replay, export, backup, incident dump — becomes a disclosure path the redaction rule never reaches"

**Why the stated assumption is not safe.** The carve-out names three consumers — the canonical serializing lock identity (concern #4, line 106), the Story 12.4 Git commit executor, and restart recovery under NFR79. All three require the value to be **durable**, not merely in-process:

- The lock is durable operational state that must survive host/sidecar restart and be readable by multiple replicas (concern #21, line 123: "Locks, idempotency admission records/tombstones, fencing tokens, in-flight checkpoints, and reconciliation tasks are durable and fail closed when unavailable or unreadable").
- "Restart recovery under NFR79" is, by definition, reading the value back from durable storage.
- That durable storage is the same Dapr state store as everything else (D-1/D-2/D-3, lines 619–621: Redis-compatible state store, Postgres escalation path). No separate store, no separate credential, no separate key is named anywhere in the document.

So the carve-out does not remove the contradiction — **it relocates it into a store that the document never subjects to any of S-6's constraints.** A Redis RDB snapshot, an operator `KEYS`/`SCAN` dump during an incident, a Postgres backup, or a state-store restore into a lower environment are all "later readers" in exactly S-6's own sense, and the redaction rule never reaches them. The phrase "never emitted" constrains egress; it says nothing about persistence, backup, replication, or operator access to the store — which is the disclosure class S-6 was written to close.

Additionally, "inside the tenant's own execution boundary" is undefined: the Dapr state store is **shared** across tenants (D-2: one Redis, keys tenant-prefixed by convention per C10 — and see SEC-12, the helper that convention names does not exist). A tenant-prefixed key in a shared Redis is not an execution boundary.

**Attack class:** durable confidential-field disclosure via backup/snapshot/restore, state-store operator access, cross-environment restore, and replica reads — the full set S-6's rationale enumerates and claims to have closed.

**Concrete correction.** The C9 relock must not simply "name the boundary". It must decide three things:

1. **The lock identity does not need cleartext.** A serializing key needs to be *stable and collision-free*, not *readable*. Specify the durable lock key as `HMAC-SHA-256(per-tenant key, canonical provider/repository identity ‖ normalized target ref)` using the same S-6 derivation. Every writer computes the same key from the same inputs, so single-active-writer holds exactly as today, and no cleartext ref is persisted for locking. This removes the largest of the three consumers outright and resolves the adversarial-review F2 collision rather than deferring it.
2. **The Git executor needs cleartext only at call time, not at rest.** Specify that the executor re-derives the cleartext ref from the authoritative provider binding at execution time, under the same authorization that authorized the task, and that it is held in memory only for the duration of the provider call. Name that as the one permitted cleartext locus.
3. **If any durable cleartext survives that reduction**, it needs a named store with a named control set: separate state store or separate key namespace, encryption at rest with a key distinct from the tokenization key, explicit exclusion from backups/exports/dumps that leave the tenant boundary, and a sentinel test that fails when a confidential-tier cleartext value appears in a state-store export. Until that set is written down, PD8's headline sentence is false and must not be published as a security guarantee.

---

### SEC-2 — HIGH — The "single non-disclosing 404" specifies two codes selected by an unspecified predicate, re-creating the existence oracle inside the envelope

**architecture.md:640 (S-7):**

> "every post-authorization denial returns one HTTP 404 `tenant_access_denied` / `resource_unavailable` shape"

and later in the same row:

> "the 404 carries category `authorization`, code `tenant_access_denied` (**resource-shaped denials use code `resource_unavailable` under the same category**), `retryable: false`, client action `verify_tenant_context_and_authorization`"

**The approved artifact says the opposite.** `docs/contract/authorization-matrix.md:145` defines **one** outcome row:

> `| safe-denial-404 | 404 | tenant_access_denied | resource_unavailable | false | no_action | redacted | Any authenticated absent, hidden, wrong-tenant, revoked, disabled, unknown, or insufficient-scope state. **One exact envelope for all causes.** |`

Pinned by `tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuthorizationMatrixContractTests.cs:202-205` (category `tenant_access_denied`, code `resource_unavailable`, retryable `false`, visibility `redacted`).

**The defect.** "Resource-shaped denials" is not defined anywhere in the document. There are only two plausible readings, and both are bad:

- If "resource-shaped" means *the locator named a resource that does not exist*, then the code field directly discriminates absent-vs-denied — **the oracle, verbatim, moved from the status line into the `code` field**.
- If it means *the operation's shape* (path-parameterised vs collection), then the selection is deterministic per operation and harmless — but the document does not say so, and an implementer reading "resource-shaped denial" while holding a resource-lookup result will implement the first reading, because it is the natural one.

Either way the document as written **contradicts "one exact envelope for all causes"** and hands the implementer a second code with no rule for choosing it.

**Attack class:** authenticated resource enumeration across folders (and, depending on where tenant scoping terminates, across managed tenants — unverified) via the `code` field of a uniform 404.

**Concrete correction.** Delete the parenthetical. State the envelope as exactly one tuple, matching the approved matrix: status `404`, category `tenant_access_denied`, code `resource_unavailable`, `retryable: false`, clientAction `no_action`, `visibility: redacted` — **independent of whether the resource exists, whether the locator was well-formed, and which authorization layer terminated the decision**. If a second code is genuinely wanted, it must come with a predicate that is provably independent of resource existence, and it must be re-approved into the matrix first (A6b), not asserted here.

---

### SEC-3 — HIGH — The S-7 envelopes are named with categories that do not exist, contradicting the approved matrix, the CLI exit-code table, and the MCP kind set — in the same sentence that claims derivability

**architecture.md:640:**

> "**Both envelopes are fully named so the C13 `cli_exit_code` / `mcp_failure_kind` columns stay derivable:** the 404 carries category `authorization`, code `tenant_access_denied` … the 503 carries category `availability`, code `authority_unavailable`, `retryable: true`, client action `retry_after_backoff`"

**Four-way contradiction, all verified:**

| Dimension | S-7 says | Approved matrix / canonical vocabulary says |
| --- | --- | --- |
| 404 category | `authorization` | `tenant_access_denied` (`docs/contract/authorization-matrix.md:145`, pinned `AuthorizationMatrixContractTests.cs:202`) |
| 404 code | `tenant_access_denied` | `resource_unavailable` (same) — S-7 has swapped the category and the code |
| 503 category | `availability` | `read_model_unavailable` (`authorization-matrix.md:146`, pinned `AuthorizationMatrixContractTests.cs:210`) |
| 503 code | `authority_unavailable` | `projection_unavailable` (same) |
| 503 clientAction | `retry_after_backoff` | `retry` (pinned `AuthorizationMatrixContractTests.cs:212`) |

Neither `authorization` nor `availability` is a member of `CanonicalErrorCategory` — the 49-member enum at `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:11054-11105`, mirrored at `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs:13324`. `resource_unavailable` exists in the spine only as a **code**, never a category (e.g. `hexalith.folders.v1.yaml:5794`). `authority_unavailable` appears **nowhere in the repository**.

The derivation claim therefore fails on its own terms. The CLI exit-code table (architecture.md:686-702) is keyed on canonical category and has no row for `authorization` or `availability`; the MCP `kind` set is declared one-to-one with the canonical category set (architecture.md:704). An implementer following S-7 literally produces two categories that no oracle row, no exit code, and no MCP kind can map — and reddens `AuthorizationMatrixContractTests` on three assertions.

**Attack class:** not a direct disclosure, but a divergence-generator. Two surfaces implementing "the same" corrected spine will produce different denial vocabularies, and the parity oracle that is supposed to catch that is itself defined in terms of the categories S-7 just invented. Divergent denial vocabularies are how one surface ends up with a distinguishable denial the others do not have.

**Concrete correction.** Replace the naming clause with the three approved outcome tuples verbatim from `docs/contract/authorization-matrix.md:144-147`, including the 401 row (see SEC-4). If PD10 genuinely intends to rename the categories, that is a change to the matrix and must go through A6b re-approval **before** it is written here as mechanism — the document's own supersession rule (line 227) applies to its own edits.

---

### SEC-4 — HIGH — S-7 routes absent and malformed authority to a retryable 503, dropping the 401 outcome and inverting NFR76's deny-by-default

**architecture.md:640:**

> "Authority that is **absent, stale, malformed, or unavailable** returns one non-disclosing HTTP 503 envelope carrying `details.visibility: redacted`, available to every protected operation and evaluated before lookup." … "the 503 carries category `availability`, code `authority_unavailable`, **`retryable: true`**, client action `retry_after_backoff`"

**Against NFR76 as the document itself states it, architecture.md:272:**

> "**deny-by-default** at protected endpoints and internal service boundaries when authority is **absent, stale, malformed, or unavailable** (NFR76)"

**And architecture.md:274:**

> "NFR76 and NFR84 additionally exercise the S-7 denial envelopes, whose contract is owned here in §'Authentication & Security'. Story 13.2 must consume that one shape rather than build a second deny-by-default behaviour beside it."

The four conditions are word-for-word identical. NFR76 says those four conditions **deny**. S-7 says they return a **retryable availability response**. Story 13.2 is then instructed to implement NFR76 by consuming S-7's shape — so the deny-by-default requirement is discharged by a mechanism that does not deny.

Three concrete consequences:

1. **The 401 outcome disappears.** The approved matrix has exactly three outcomes, the first being `authentication-failure-401` → 401 / `authentication_failure` / `authentication_required` / `retryable: false` / `check_credentials` (`docs/contract/authorization-matrix.md:144`, pinned `AuthorizationMatrixContractTests.cs:196,198`). S-7 defines two envelopes and never mentions 401. "Authority absent or malformed" is precisely the 401 case. An implementer reading S-7 as the authoritative denial contract emits 503 where 401 is required, breaking the `outcomes.Length.ShouldBe(3)` and `statuses.ShouldBe(["401","404","503"])` pins, and removing the only signal a client has that it should refresh its token.
2. **Retry amplification, reachable pre-authentication.** The envelope is "available to every protected operation and evaluated before lookup", carries `retryable: true`, and instructs `retry_after_backoff`. A caller with a malformed or expired token is therefore told, on every protected endpoint, to retry forever. The document sets no `Retry-After`, no retry ceiling, and no ingress rate limit (I-8 is *provider egress* rate limiting only; ingress rate limiting is HXF-SEC-005, Epic 13 `backlog`). Conforming clients become a self-inflicted DoS against the authority path during exactly the incident the 503 signals.
3. **The 503 is a probe surface.** Because it is evaluated first, pre-lookup, and reachable by any caller, it is a free health signal for the tenant-access projection and folder-ACL read models. Combined with S-4's fail-closed-on-stale policy, an unauthenticated prober can distinguish "authority subsystem healthy" from "authority subsystem degraded" on any protected path.

**Attack class:** authentication-state confusion, retry-amplification DoS against the authorization substrate, and an availability side-channel on the authority layer.

**Concrete correction.** Split the condition. **Absent or malformed authority is a client fault → 401 `authentication_failure` / `authentication_required` / `retryable: false` / `check_credentials`**, per the approved matrix row that S-7 omits. **Only genuine unavailability of the authority substrate (projection unreachable/stale beyond the C8 bound) → 503**, with the matrix's `read_model_unavailable` / `projection_unavailable` / `retry`, plus a mandatory `Retry-After` and a stated client retry ceiling. Then restate NFR76's binding: deny-by-default is satisfied by the 401/404 envelopes; the 503 is an availability answer that grants nothing and is not the deny mechanism.

---

### SEC-5 — HIGH — The PD10 remediation is scoped to the `category` field, leaving six other fields of the live 404 discriminating existence

**architecture.md:646:**

> "`FolderAuthorizationDenialMapper` currently emits denial categories outside the canonical set; the correction collapses **them** into the S-7 envelopes rather than leaving non-canonical categories reachable behind a corrected spine."

**What the mapper actually emits.** `src/Hexalith.Folders.Server/FolderAuthorizationDenialMapper.cs:53-54` routes two *different* outcomes to the same 404 and the same category:

```csharp
LayeredAuthorizationOutcomeCodes.SafeNotFound or LayeredAuthorizationOutcomeCodes.FolderAclDenied =>
    (StatusCodes.Status404NotFound, "not_found_to_caller"),
```

but the envelope it then builds (`FolderAuthorizationDenialMapper.cs:21-44`) carries the raw outcome code in **six** places:

```csharp
type: $"https://hexalith.dev/errors/folders/{decision.OutcomeCode}",   // :22
["code"] = decision.OutcomeCode,                                       // :28
["details"]["retryReasonCode"] = decision.OutcomeCode,                 // :37
["details"]["layer"] = decision.TerminalLayer.ToString(),              // :40
["details"]["freshnessClass"] = decision.FreshnessClass,               // :42
["details"]["timingBucket"] = decision.TimingBucket,                   // :43
```

The outcome codes are `safe_not_found` and `folder_acl_denied` (`src/Hexalith.Folders/Authorization/LayeredAuthorizationOutcomeCodes.cs:11,16`). So a caller receiving a 404 today learns, from four independent fields plus the `type` URI, whether the resource **does not exist** or **exists and you are ACL-denied**. `details.timingBucket` additionally publishes a timing classification of the denial as a response field.

**Why this is an architecture finding and not just a code bug.** The current-reality table (line 241) records only that 403 is live and that three enum members are live. The approved gap inventory that drives the correction records only 403 counts (`G1`), enumeration-leaking *categories* (`G2`), and 503 *visibility* (`G3`) — `docs/contract/authorization-matrix.md:341-343`. **No gap, and no sentence in PD10, covers the `code` / `type` / `details.layer` / `details.retryReasonCode` / `details.freshnessClass` / `details.timingBucket` channels.** A team that executes PD10 exactly as specified — retire 403, remove three categories, add the two envelopes — ships a "corrected" spine with the oracle fully intact, because the oracle does not live in the category field.

**Attack class:** authenticated resource enumeration (folder existence) through a uniform 404; a denial-timing side-channel published as a first-class response field. Whether `safe_not_found` vs `folder_acl_denied` is reachable across managed-tenant boundaries is **unverified** — if it is, this escalates to critical.

**Concrete correction.** Extend S-7 from "one status and one category" to **"one byte-identical envelope"**: every field of a protected-operation denial — `type`, `title`, `category`, `code`, `message`, `retryable`, `clientAction`, and the entire `details` map — must be a constant, with the sole exception of `correlationId`. Explicitly forbid `details.layer`, `details.retryReasonCode`, `details.freshnessClass`, `details.timingBucket`, and the outcome-code-bearing `type` URI on protected-operation denials; those belong in the server-side audit record, not the response. Then add the missing gap rows to `docs/contract/authorization-matrix.md` (as `G12`+) so the remediation is scoped correctly and re-approved under A6b, and add a conformance test asserting envelope-field equality across the `SafeNotFound` and `FolderAclDenied` outcomes.

---

### SEC-6 — HIGH — S-7 and S-8 are jointly unsatisfiable for task-derived scope

**S-7, architecture.md:637 (S-4 evaluation order) and :640:** "authority-unavailability is evaluated first, then authority, and only then any protected-resource lookup — so a denial never depends on whether the resource exists".

**S-8, architecture.md:641:** "Provider, repository, ref, and task dimensions are **derived from an already-authorized folder, task, or binding**" … "`GetTaskStatus` requires the current tenant, **bound-folder read authority**, and task scope".

`GetTaskStatus` is `/tasks/{taskId}/status` — the caller names only a task id (verified in the prior technology report against `hexalith.folders.v1.yaml:3874`). To evaluate "bound-folder read authority" the server must first learn **which folder** the task belongs to, which requires reading the task record. That read is a protected-resource lookup, and S-7 forbids it before authority. The two rules cannot both be obeyed for this operation. The same shape applies to any operation whose authorizing parent is discovered from the caller-named child.

The approved matrix already flags a related divergence for this operation (`G10`, `docs/contract/authorization-matrix.md:350`: "`GetTaskStatus` and `ValidateProviderReadiness` diverge"), but frames it as a token-comparison problem, not an ordering impossibility.

**Attack class:** an implementer forced to choose will pick one of two bad resolutions — look up first and let the lookup result influence the denial (restores the oracle), or reject the operation as unauthorizable (breaks a shipped capability). Under schedule pressure the first is chosen.

**Concrete correction.** Name the resolution explicitly in S-8: a **tenant-partitioned parent resolution** is not a "protected-resource lookup" for S-7 purposes, provided (a) it is constrained to the caller's authoritative tenant partition at the storage-key level so it cannot cross tenants by construction, (b) it returns only the parent identifier and no resource state, and (c) its miss and its hit produce the **identical** S-7 envelope. Add that as a fourth clause to the S-4 evaluation order so the ordering rule is satisfiable as stated.

---

### SEC-7 — HIGH — S-8's universal "never caller-supplied" rule is violated by its own listed operation, which is the known SSRF sink — and the architecture's security spine carries no destination-denial decision

**architecture.md:641 (S-8):** "**Structured, derived, never caller-supplied.** … a raw locator submitted by the caller is input, never authority" … "`ValidateProviderReadiness` stays tenant-level under folder-create authority and does not invent an ACL for a folder that does not yet exist".

`ValidateProviderReadiness` exists precisely to validate a **caller-supplied provider endpoint** before any folder exists to derive from. There is no authorized parent. S-8's rule cannot hold for it, and S-8 does not say so — it only addresses the ACL dimension.

**What the caller-supplied locator reaches, verified.** `src/Hexalith.Folders/Providers/Forgejo/ForgejoAuthorizedBaseUrl.cs:25-47` validates scheme (HTTPS), non-empty host, no `UserInfo`, and no `token`/`access_token` query parameter. That is the entire destination policy. The value flows to `src/Hexalith.Folders/Providers/Forgejo/ForgejoHttpApiClientFactory.cs:45-52`, which sets it as `client.BaseAddress` **and attaches the tenant's Forgejo bearer credential** before the call. `ConnectCallback`, `IPAddress`, `Dns.`, `IsLoopback`, and `169.254` have **zero occurrences anywhere in `src/` or `tests/`**. Redirects are disabled (`ForgejoHttpApiClientFactory.cs:10`) and cross-origin redirect is detected by textual same-origin comparison (`ForgejoAuthorizedBaseUrl.cs:69-71`), so redirect pivoting is blocked — but direct targeting and DNS rebinding are not.

**Where the control lives in the document.** Nowhere in the decision tables. Not in S-1…S-8, not in I-1…I-9. It appears only as Epic 13 scope prose (line 270, "HXF-SEC-002 … block RFC-1918/link-local/loopback/metadata IPs via `ConnectCallback`") and as NFR75 (line 272, `reference-pending`, owner `13-1`, `docs/exit-criteria/nfr-traceability.md:121`). Per the Release Authority Overlay (line 204) **this document owns the security spine** — so the spine is missing its outbound-destination control, and a backlog story is the only place it exists.

**Attack class:** server-side request forgery from a tenant-administrator-controlled value into cluster-internal services and cloud metadata endpoints, with a bearer credential attached; DNS-rebinding bypass of the textual same-origin check. Note this is a **tenant-admin-authorized** input, so the cross-tenant dimension is the internal network, not another tenant's data — but the credential is transmitted to whatever host the tenant names.

**Concrete correction.** Two edits. (1) Add an explicit exception clause to S-8: `ValidateProviderReadiness` (and any future pre-creation validation operation) takes an unavoidable caller-supplied locator; the compensating controls are named in S-*n* and are mandatory before the locator is dereferenced. (2) Add a new decision row, **S-9 provider-endpoint destination policy**, owning: resolve-then-pin (validate the resolved IP and connect to that address, defeating rebinding), denial of RFC-1918/RFC-4193/loopback/link-local/`169.254.169.254`/`fd00:ec2::254`/CGNAT and any non-global-unicast destination, an explicit per-deployment allowlist escape with an approver, a connection timeout and response-size cap, and a negative-test suite. Then bind NFR75 to that decision rather than to a backlog story.

---

### SEC-8 — HIGH — S-6 leaves the truncation width unspecified and has no key-management, rotation, or re-tokenization decision — and rotation silently destroys the evidence joins the token exists to serve

**architecture.md:639 (S-6):**

> "The token is `HMAC-SHA-256(key = per-managed-tenant confidential-token key, message = versioned classification tag ‖ canonical field identity ‖ NFC-normalized cleartext)`, **truncated to a fixed width** and carried with its key version."

Three gaps, all load-bearing:

1. **No width.** "A fixed width" is not a value. Truncation length is the entire collision-resistance budget. At 64 bits, two distinct confidential values inside one tenant collide at roughly 2³² values — and a collision **merges two different fields' evidence into one join**, which is a silent integrity failure in exactly the audit trail this product sells. Two implementers will pick different widths (the prior adversarial review already demonstrated two writers producing different tokens from two legitimate readings of the earlier text; pinning HMAC but not the width reproduces the same class of divergence one level down). Specify a value — 128 bits / 22 Base64Url characters is the defensible floor — and pin it as a constant in the shared write-path implementation.
2. **No key management.** "Per-managed-tenant confidential-token key" has no generation rule, no storage decision, no access-control rule, and no rotation cadence. S-5 (line 638) covers *provider* credentials and mandates "credential references only (Hexalith.Tenants OR a Dapr secret store)" — it does not cover this key, and the production secret-store scoping in `deploy/dapr/production/accesscontrol.yaml:29-36` allows folders exactly two named secret refs. A key with no home is a key that ends up in configuration.
3. **Rotation breaks the joins.** The design "carries its key version", which implies rotation. But a rotated key produces a **different token for the same value**, so every join across the rotation boundary returns empty — the same failure the prior gate flagged for two divergent writers, now reintroduced along the time axis. The document states no re-tokenization strategy, and re-tokenization is impossible by construction: the cleartext is gone (that is the whole point), so old tokens can never be translated to new ones. This is not a detail — it means **key rotation is a one-way destruction of audit-joinability**, and nothing in the document says so.

**Attack class:** (a) collision-induced evidence merging under-specified into existence; (b) key sprawl / key-in-config; (c) an operational action (routine key rotation, or emergency rotation after a suspected key compromise) silently destroying audit correlation, which is itself an incident-response failure mode.

**Concrete correction.** Pin the width to a stated bit length. Add key storage/rotation to S-5 or a new row: generation, secret-store location, per-tenant isolation, access restricted to the single shared write-path implementation, and a rotation cadence. Then make the rotation consequence explicit and choose: either (i) tokens are key-version-tagged and joins are **scoped to a key epoch**, with the document stating that cross-epoch joins are not possible and that rotation is therefore an audit-continuity event requiring approval; or (ii) accept a long-lived key with a documented compromise-response plan. Silence is the one option that is not available.

---

### SEC-9 — HIGH — The architecture's security spine has no deny-by-default binding for the HTTP surface, and S-7 is the only thing it points at

`src/Hexalith.Folders.Server/Program.cs:14` is a bare `builder.Services.AddAuthorization();`. Across the whole server project there are **zero** occurrences of `RequireAuthorization`, `[Authorize]`, `FallbackPolicy`, `RequireAssertion`, or `AllowAnonymous`. ASP.NET Core's default `FallbackPolicy` is null, so `UseAuthorization()` (`Program.cs:45-48`) is a pass-through for every endpoint that carries no authorization metadata — and none do. Tenant authority is read from claims and **returns null rather than denying** when there is no principal (`src/Hexalith.Folders.Server/Authentication/HttpContextTenantContextAccessor.cs:21-31`).

Whether a given endpoint ultimately denies therefore depends entirely on each handler's downstream treatment of a null tenant. That is per-endpoint, not deny-by-default, and a newly added endpoint is unprotected until someone remembers.

**This is an architecture finding, not only a code one.** S-4 (line 637) specifies the *order* of the authorization layers but never specifies the *binding mechanism* that makes the chain mandatory for every endpoint. The one place the document addresses it is Epic 13 prose (line 270, "HXF-SEC-003 (fail-safe fallback authorization policy + sidecar-only app port)") and NFR76 — and NFR76 is explicitly bound to S-7 (line 274), whose answer for absent authority is a retryable 503 (SEC-4). So the deny-by-default requirement points at a decision that does not deny, and the mechanism that would enforce it is owned by a `backlog` story (`_bmad-output/implementation-artifacts/sprint-status.yaml:241-247`, `epic-13: backlog`).

Relatedly, the two startup guards that *do* exist short-circuit to success outside production: `FoldersOidcOptionsValidator` (`src/Hexalith.Folders.Server/Authentication/FoldersAuthenticationServiceCollectionExtensions.cs:122-125`) and `FoldersAuthSchemeValidator` (`src/Hexalith.Folders.Server/Authentication/FoldersAuthSchemeValidator.cs:20-23`). With I-3's local `defaultAction: allow` (line 731), a host mis-configured as `Development` has neither JWT enforcement nor Dapr access control. The document has no decision requiring that the environment discriminator itself be a protected, verified input.

**Attack class:** unauthenticated access to any endpoint whose handler does not independently reject a null tenant; environment-discriminator downgrade.

**Concrete correction.** Add a decision row (extend S-4) stating: the server registers a **deny-by-default authorization fallback policy**; every endpoint is authorization-required unless it carries an explicit, enumerated, reviewed anonymous exception (health probes being the expected set); a conformance test enumerates endpoint metadata and fails when an endpoint is reachable anonymously without being on the list; and the app port is reachable only through the sidecar. Bind NFR76 to *that* decision and to the corrected 401 envelope, not to the 503.

---

### SEC-10 — MEDIUM — S-7 constrains only the response envelope; timing, ingress rate limiting, freshness headers, and pagination cursors are unconstrained side channels, and cursors are an unowned S-8 hole

S-7's removal of the oracle is expressed entirely in terms of status code and error fields. The document names no rule for any other observable:

- **Timing.** No constant-time or uniform-latency requirement anywhere (`grep -i "constant.time|timing"` over architecture.md returns only `details.timingBucket`-adjacent text and provider-failure prose). The evaluation-order rule makes *denied* responses uniformly fast, which is good, but nothing prevents an implementation from short-circuiting differently per layer.
- **Ingress rate limiting.** I-8 (line 736) is *provider egress* rate limiting. Ingress rate limiting is HXF-SEC-005 (line 270), Epic 13, `backlog`. If ingress buckets are ever keyed per-resource rather than per-tenant, a 429 becomes an existence signal. The document states no keying rule.
- **`X-Hexalith-Freshness`** (line 884) is a conditional response header tied to read-consistency class. Whether it appears on a denial — and whether its presence or value varies with whether a read model was consulted — is unspecified.
- **Pagination cursors.** §Component Boundaries (line 1539) names "query cursors" as a shared platform capability Folders consumes. The code already has cursor-tamper detection (`src/Hexalith.Folders.Server/AuditEndpoints.cs:302-304`, `cursor_tampered`), so cursors are real and are caller-supplied. **A cursor is a caller-supplied locator that carries scope** — precisely the thing S-8 forbids — yet S-8's dimension list is "provider, repository, ref, and task" and never mentions cursors or continuation tokens. Nothing in the document requires a cursor to be bound to the issuing tenant/folder/principal, integrity-protected, or expiring.

**Attack class:** existence disclosure via side channels S-7 does not cover; scope widening or cross-tenant read by replaying a cursor issued under different authority (reachability **unverified** — the tamper check exists, but the document states no binding requirement).

**Concrete correction.** Add to S-7 an explicit statement that the non-disclosure obligation covers *every* caller-observable difference, not only the error body, and enumerate the channels that must be invariant across denied-vs-absent: response timing bucket, presence and value of all response headers, rate-limit accounting and `Retry-After`, and audit/idempotency side effects. Add cursors/continuation tokens to S-8's derived-scope dimension list, with the requirement that a cursor is integrity-protected, bound to the issuing managed tenant and principal scope, expiring, and that a cursor presented under different authority produces the S-7 envelope.

---

### SEC-11 — MEDIUM — F-6's incident path has no Contract Spine operation, no permission in the catalog, and contradicts the UI boundary — so the highest-privilege read in the system is outside everything S-7/S-8/C13 govern

**architecture.md:722 (F-6):** "Separate dual-authorization event-stream view at `/_admin/incident-stream`" … "the same actor must hold incident-admin permission (`eventstore:permission=admin`) **and** fresh current tenant/folder authorization before any stream lookup, event counting, checkpoint lookup, filtering, or shaping".

Verified against the approved artifacts:

- The `incident-evidence` operation family has **zero** Contract Spine operations (`docs/contract/authorization-matrix.md:88-100`; gap `G6` at `:346`: "has no current public Contract Spine operation, so its operation count is zero and its runtime evidence is not claimed"; asserted at `AuthorizationMatrixContractTests.cs:442`).
- The effective-permission action catalog "carries **no** audit-reviewer role, operator permission, **incident-admin permission**, or incident-evidence action" (gap `G4`, `docs/contract/authorization-matrix.md:344`, evidence `src/Hexalith.Folders/Authorization/EffectivePermissionsActionCatalog.cs`).

So the permission F-6 requires does not exist in the authorization model, and the operation it guards is not in the 49-operation protected inventory that S-7 covers, not in the 8 capability groups (architecture.md:1531), and has no C13 parity row.

**Two further design problems inside F-6 itself:**

1. **Boundary contradiction.** §Component Boundaries:1543 says "`Hexalith.Folders.UI` is the read-only ops console. **References Client only.**" and Integration Points:1607 says the UI "reads only from projection endpoints". F-6 places a raw **event-stream** view in that UI (`Hexalith.Folders.UI/Pages/_Admin/IncidentStream.razor`, architecture.md:1588). Either the UI reaches EventStore directly — bypassing the entire Folders authorization spine, which lives in `Hexalith.Folders.Server` — or the Server must expose a stream endpoint that does not exist in the spine. The document does not say which, and the difference is the whole security property.
2. **The precondition is unavailable in the trigger condition.** F-6 exists for "when projections are degraded" and requires "**fresh** current tenant/folder authorization" — which S-4 sources from the local tenant-access projection and the folder ACL read model. When those are degraded, S-7's own rule says the answer is the 503 authority-unavailable envelope. F-6 is therefore unusable in precisely the scenario it is built for, which is how the dual-authorization requirement gets relaxed in practice.

**Attack class:** an unmodelled privileged read path; and, if the dual-authorization precondition is relaxed under incident pressure, unscoped raw event-stream access across folders.

**Concrete correction.** Either declare F-6 out of MVP explicitly, or give it a Contract Spine operation in the `incident-evidence` family, a real permission in `EffectivePermissionsActionCatalog`, a C13 parity row, and an S-7-conformant denial envelope. Resolve the boundary by routing it through `Hexalith.Folders.Server` like every other read. Resolve the availability paradox by naming the authorization source that stays available when projections are degraded (a synchronous Tenants query, per concern #20's mutation path) and stating that if even that is unavailable, F-6 denies rather than degrades.

---

### SEC-12 — MEDIUM — C9's and C10's named security artifacts are inaccurate, and the audit sanitizer the document cites is the weakest of nine divergent copies

Two artifact-location claims in the exit-criteria plan are wrong in ways that matter for a security sign-off:

**C10 (architecture.md:349, also :1586, :1589, :1298):** Artifact Location is "`.github/workflows/ci.yml` (lint job) + `Hexalith.Folders/Caching/TenantPrefixedCacheKey.cs`". `TenantPrefixedCacheKey` has **zero occurrences in any `.cs` file**; there is no `Caching` directory. The gate does exist, but elsewhere and in a different form: `.github/workflows/ci.yml:110-113` → `tests/tools/run-security-redaction-ci-gates.ps1:56-59` (category `tenant-cache-key-lint`) → `tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs:1383,1432,1453`, with an exception manifest at `tests/fixtures/cache-key-exceptions.yaml`. Note the document's own `exit-criteria-presence` gate (line 354) is meant to fail the release pipeline "when an artifact link is missing" — it evidently does not check that the link resolves. (No runtime impact today: `MemoryCache`/`IDistributedCache`/`CacheKey` have zero hits in `src/`, so no cache layer exists yet.)

**C9 (architecture.md:348):** the artifact is "this document §'S-6' + `Hexalith.Folders/Observability/FolderAuditSanitizer.cs`". The file exists, and S-6 correctly says it is the read/emit-path sanitizer and not the tokenizer. But cross-cutting concern #6 (line 108) says redaction must be "designed once, not per surface", and there are **nine** independent sensitive-value predicates in `src/` with three different rule sets:

| Copy | Location | Rules |
| --- | --- | --- |
| 1 | `src/Hexalith.Folders.Server/FolderSensitiveDiagnosticDetector.cs:15-45` | 14 substrings + 3 regexes (`gh[pousr]_`, JWT, PEM) — strongest; shared by OpsConsole and ProviderReadiness endpoints |
| 2 | `src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessValidationService.cs:768-793` | byte-for-byte duplicate of #1 in another assembly |
| **3** | **`src/Hexalith.Folders/Observability/FolderAuditSanitizer.cs:110-128`** | **17 substrings, no regexes — omits all three token/JWT/PEM patterns** |
| 4 | `src/Hexalith.Folders/Providers/Abstractions/ProviderCapabilityProfileFactory.cs:332-352` | 10 substrings + 2 regexes (drops PEM) |
| 5–9 | `InMemoryProviderCapabilityEvidenceStore.cs:48-65`, `ForgejoSafeTargetFingerprint.cs:491-497`, `GitHubSafeTargetFingerprint.cs:416-423`, `RepositoryBindingService.cs:342-361`, `OrganizationProviderBindingSecretDetector.cs:7-33` | 5–9 rules, mostly no regexes |

The C9-named sanitizer, sitting on the audit-observation path, is the one that **lacks the `ghp_`/JWT/PEM regexes**. A raw GitHub token, a bare JWT, or a PEM block is rejected at the HTTP edge and accepted by the audit sanitizer. HXF-SEC-006 ("converge the four sensitive-value filter copies", line 270) understates the count and is `backlog`.

**Attack class:** credential material reaching durable audit records through the weakest of nine divergent filters — the exact class cross-cutting concern #6 and the `audit-leakage-corpus.json` sentinel gate exist to prevent.

**Concrete correction.** Correct the C10 artifact location to the real gate path and teach the `exit-criteria-presence` gate to assert that each linked path resolves. For C9, add a decision sentence making one predicate normative and requiring every other site to delegate to it, with a conformance test that fails when a second predicate is defined; and state the minimum rule set (substrings **and** the token/JWT/PEM regexes) so convergence cannot happen downward onto the weakest copy.

---

### SEC-13 — MEDIUM — S-2 freezes eight validation parameters but omits algorithm pinning, a token-lifetime ceiling, and any principal-level revocation mechanism

**architecture.md:635 (S-2)** freezes `ClockSkew`, `RequireExpirationTime`, `RequireSignedTokens`, `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey`, JWKS refresh intervals, and "**Token introspection:** JWT-only (no introspection round-trip)". `docs/exit-criteria/s2-oidc-validation.md` carries the same ten rows. The code matches exactly (`src/Hexalith.Folders.Server/Authentication/FoldersAuthenticationServiceCollectionExtensions.cs:98-110`, plus `MapInboundClaims = false` at `:95` and `RequireHttpsMetadata` defaulting true at `Authentication/FoldersOidcOptions.cs:15`) — so this is a completeness gap in the frozen set, not a drift.

Three omissions:

1. **No `ValidAlgorithms` / `ValidTypes`.** The standard control against algorithm substitution and cross-token-type confusion is pinning the accepted `alg` set and the `typ` header. `RequireSignedTokens` + `ValidateIssuerSigningKey` mitigate the classic `alg:none`/HMAC-with-public-key cases, but an IdP that publishes multiple key types, or a deployment that later adds a symmetric key, has no constraint. Pin `ValidAlgorithms` to the intended asymmetric set and pin `typ` to `at+jwt` (or the provider's access-token type).
2. **No maximum acceptable token lifetime.** `RequireExpirationTime` + `ValidateLifetime` mean a token must *have* an expiry and be within it. They do not bound it. A compliant IdP issuing a one-year access token passes every frozen parameter. For a control plane holding Git write authority, state a maximum accepted `exp - iat` and reject beyond it.
3. **No principal-level revocation.** "JWT-only (no introspection round-trip)" means a stolen or administratively revoked token remains valid until `exp`. C7's 60-second revocation-effect SLO (line 319) and concern #16's 15-second revalidation (line 118) cover **tenant-access** revocation — they re-check the *tenant projection*, not whether the *token* or the *principal* is still valid. So "user disabled at the IdP" has no mechanism and no SLO anywhere in the document, while "tenant access revoked" has both. Given that a held lock can mutate a customer repository, this asymmetry should be a stated decision, not an omission.

**Attack class:** algorithm/type confusion (mitigated but not pinned); long-lived-token replay after principal revocation.

**Concrete correction.** Add the three parameters to the S-2 frozen set and to `docs/exit-criteria/s2-oidc-validation.md`, and either accept the revocation window explicitly (stating the maximum exposure as `min(token lifetime, …)` and why it is acceptable) or add a revocation mechanism — a `jti` denylist checked on the same 15-second revalidation tick already mandated for held locks is the cheap option, since the tick exists.

---

### SEC-14 — MEDIUM — The shared `folders-index` Memories tenant is a cross-tenant aggregation point defended only at the application layer, with no named negative test — and PD8 tokens both weaken and break it there

**architecture.md:177:** "All Folders content lives under one physical Memories tenant (`folders-index`), so Memories' own tenant scoping does **NOT** isolate Folders managed tenants." … "The Folders-side trim — not the attribute filter — is the load-bearing tenant-isolation control; the attribute filter is defense-in-depth."

The risk is stated honestly, which is good. What is missing is any decision that mitigates it:

- Every managed tenant's indexed metadata sits in one physical index. The only per-tenant control is a Folders-side re-check in one code path. A bug in that path, or any other caller reaching `memories` with the shared `HEXALITH_MEMORIES_API_TOKEN`, reads every tenant's indexed metadata. That token is a **single, non-tenant-scoped, static bearer** for the whole index (`src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:117-121`) with no storage, rotation, or scoping decision in S-5 — S-5 covers *provider* credentials only. (To the credit of the implementation: the token is not logged; `MemoriesFolderSearchSource.cs:64,72,82` log only timeout duration, exception type, and a fixed string.)
- The network control is real and verified — `deploy/dapr/production/accesscontrol.yaml:122-141` carries exactly the `folders → memories` `GET /api/search` allow-rule with `defaultAction: deny`, `folders-workers` correctly has no invoke rule, and mTLS is on (`deploy/dapr/production/daprsystem.yaml:9-12`). But that is app-to-app, not per-tenant.
- **No cross-tenant negative test is named for the trim.** The sentinel corpus (`tests/fixtures/audit-leakage-corpus.json`) tests *leakage patterns*, not *cross-tenant hits*. The `dapr-policy-conformance` suite tests app-ID triples. Neither asserts "a hit belonging to tenant B never survives a query authorized for tenant A".

**And PD8 interacts badly here.** If a path or repository name is `confidential`-tier, S-6 requires the token in place of cleartext — which means the search index stores tokens. Two consequences the document does not address: (a) **the capability breaks** — FR58's metadata-token recall cannot match a user's search term against an HMAC, so search silently returns nothing for confidential-tier tenants; (b) **the index becomes a per-tenant correlation set** — an equal-token join over a shared physical index is exactly the correlation the token is designed to enable, now sitting in the one datastore whose tenant isolation the document says does not work.

There is also a chosen-plaintext concern with S-6's claim that "the tenant key enables correlation, never recovery": within a tenant, a principal who can *create* a value (a branch, a folder name) and *observe* its token can build a lookup table for that tenant's low-entropy value space and recover other principals' withheld values by token equality. The claim is true against an outsider and false against a tenant insider; the document should say which adversary it holds against.

**Attack class:** cross-tenant metadata disclosure through a single application-layer trim; intra-tenant confidential-value recovery by chosen-plaintext token comparison; silent capability loss.

**Concrete correction.** Add a decision stating the trim's required negative-test gate (a cross-tenant hit must be dropped, asserted with seeded foreign-tenant documents). Decide and record whether confidential-tier fields are **excluded from the index entirely** rather than tokenized into it — exclusion is the honest answer given (a). Add the Memories bearer to S-5's scope with storage and rotation. Restate S-6's non-recovery claim with its adversary model.

---

### SEC-15 — MEDIUM — `visibility` is still specified in two incompatible wire positions, the 404's value is unspecified, and adding `withheld` silently supersedes the OQ2-approved closed schemas

Carry-over from `review-technology-2026-09-15.md` F7 and `review-adversarial-2026-09-15.md` F6 — **partially fixed, and the unfixed half now has security teeth.**

Fixed: the value set is now enumerated (architecture.md:640, "`visibility` is an enumerated field — `metadata_only`, `redacted`, `withheld` — not free text"), and the `withheld`-vs-`redacted` collision is explicitly reconciled (architecture.md:639).

Not fixed, and consequential:

1. **Two positions.** Architecture.md:640 and A-8 (`:664`) both say "`visibility` is a **required field on every error**" (top-level), while the same S-7 sentence specifies the 503 as carrying "`details.visibility: redacted`" (nested). The current-reality row (`:241`) frames the gap as top-level-vs-`details`, then S-7 prescribes `details`. Verified: `ProblemDetails.required` at `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:7635-7645` does **not** include `visibility`; all five required-`visibility` sites are nested.
2. **The 404's `visibility` value is never stated.** The 503's is `redacted`; the 404's is left to the implementer. The runtime hard-codes `metadata_only` at five sites (`FolderAuthorizationDenialMapper.cs:36`, `FolderProblemDetailsFactory.cs:210`, `FoldersDomainServiceEndpoints.cs:1218`, `FoldersDomainServiceRequestHandler.cs:43`, plus the client projection). The approved matrix requires `redacted` on **all three** outcomes (`docs/contract/authorization-matrix.md:144-146`), and gap `G3` (`:343`) exists precisely because the shared example carries `metadata_only`. If an implementer emits `metadata_only` for a genuine absence and `redacted` for an authorization denial — a natural reading — **the required field becomes the oracle.** Given SEC-2 and SEC-5, this is the third independent channel through which the same distinction survives.
3. **Adding `withheld` breaks an OQ2-approved closed contract that is not listed as superseded.** `ExactFileProblem.details` is `additionalProperties: false, required: [visibility], enum: [redacted, metadata_only]` (`hexalith.folders.v1.yaml:7738-7748`), plus `RedactionMetadata` (`:10307-10318`) and four `allOf` invariants (`:10342, 10370, 10396, 10434`), plus the runtime enum `src/Hexalith.Folders.Contracts/Projections/Audit/RedactionVisibility.cs:7-11` (two members), plus the exact-member check in `src/Hexalith.Folders.Client/Serialization/Oq2WireObjectConverter.cs:458`. These are bound to the **OQ2** approval (`docs/contract/file-context-contract-groups.md`, `docs/contract/oq2-file-policy-evidence.yaml`, version `1.1.0`, architecture.md:266). The superseded-digest table (architecture.md:229-234) lists OQ3, C3, C6, and C9 — **not OQ2**, and the PD10 regeneration list (`:645`) does not name the OQ2 problem schemas.

**Attack class:** existence disclosure via the required `visibility` field; and a governance failure in which an approved, digest-bound contract is changed without supersession.

**Concrete correction.** Pick one wire position and say it once — top-level is the right choice given A-8's wording; then state that `details.visibility` is retired and list the five schema sites plus `RedactionVisibility.cs` in the regeneration set. Fix the 404's value to `redacted`, matching the matrix, and state explicitly that `visibility` on a protected-operation denial is a constant. Add OQ2 to the superseded-approval table with its required re-approvers.

---

### SEC-16 — MEDIUM — The flagship security correction ships with no conformance gate, while the validation section still certifies gate coverage for every NFR

The CI gate inventory (architecture.md:1065) and the "Key Strengths" defense-in-depth list (`:1799`) were **not** updated by the PD10 amendment. There is no denial-envelope conformance gate, no gate asserting 403 is absent from protected operations, and no gate asserting envelope-field equality. The document self-flags part of this at `:647` ("the drift fixture must gain a status-code and error-vocabulary surface, or the removals ship unguarded") — verified: `tests/fixtures/previous-spine.yaml:7-9` declares `known_omissions: [No request/response schema fingerprints, No status-code surface]`, so the symmetric-drift gate is blind to exactly these changes. The only live tripwire is `AuthorizationMatrixContractTests.AuthorizationMatrixGapCountsTrackTheContractSpineDeclarations` (`AuthorizationMatrixContractTests.cs:496-521`), which recomputes G1/G2/G3 counts from the OpenAPI file — and which SEC-3 would redden and SEC-5 would not reach.

Meanwhile §"Requirements Coverage Validation ✅" is stale in a way that matters for sign-off:

- `:1708` "Every NFR category … is bound to specific architectural decisions and at least one CI gate or runbook" and `:1718` "every NFR has at least one CI gate, lint, codegen rule, or release-validation evidence path" — contradicted by `:274`/`:278` and by `docs/exit-criteria/nfr-traceability.md:120-130`, where **all eleven** NFR74–NFR84 rows are `reference-pending` and release-blocking with an empty gate column.
- `:1710`, the Security & Tenant Isolation bullet, lists S-2/S-6/S-3/S-4/C10/I-3 and **never mentions S-7 or S-8** — the two decisions that now carry the denial contract and the scope model.

A green "✅ Requirements Coverage Validation" heading over an eleven-row release-blocking hole is the kind of thing a release reviewer takes at face value.

**Concrete correction.** Add the denial-envelope conformance gate to the `:1065` gate list and to the C13 obligations: (a) no protected operation declares 403; (b) `not_found`/`cross_tenant_access_denied`/`audit_access_denied` are absent from protected-operation responses; (c) the denial envelope is field-identical across `SafeNotFound` and `FolderAclDenied`; (d) the drift fixture gains a status-code and error-vocabulary surface. Then correct `:1708`/`:1710`/`:1718` to state the NFR74–NFR84 exception, or drop the ✅.

---

### SEC-17 — MEDIUM — The denial contract is specified only for synchronous responses, while the canonical mutation transport is 202-Accepted with async status polling

The data-flow diagram (architecture.md:1618-1641) routes every command through `POST /api/v1/commands` → EventStore (`"EventStore validates auth + envelope"`, `:1623`) → Dapr invoke → Folders `/process`, and returns `"202 Accepted + correlationId for status polling"` (`:1641`).

S-7 specifies HTTP 404 and 503 envelopes. The document never says **where** on this path the envelope is produced, or what a denial looks like when the caller's next observation is a *status read* rather than a response. Two unresolved questions with security content:

1. If authorization terminates before admission (which A-9 `:953` requires — "No key disposition is observable before authorization and validation"), the 404 must be produced synchronously at the REST edge, before EventStore is involved. The document should say that, because the diagram implies otherwise.
2. If any denial is observable through the command-status projection instead, the S-7 non-disclosure obligation must extend to that projection's responses — and it currently does not, because S-7 is written about error envelopes only. A status read that distinguishes "no such command" from "command denied" reinstates the oracle one hop downstream.

**Concrete correction.** State the production point explicitly ("the S-7 envelope is produced at the REST edge before command admission; no protected-operation denial is ever observable through the 202 path"), and extend the non-disclosure obligation to command-status and audit reads for denied requests.

---

### SEC-18 — LOW — "49 of 50 protected operations" is factually wrong and contradicts S-7 four hundred lines later

**architecture.md:241:** "HTTP 403 is live on **49 of 50** protected operations."

The approved gap says 49 of 49: `docs/contract/authorization-matrix.md:341` (`G1`) — "403 on 49 of 49 operations and 404 on 46 of 49" — and the matrix denominator is 49 (`:15`, `:102`), asserted at `AuthorizationMatrixContractTests.cs:148`. S-7 itself says "All **49** protected Contract Spine operations" (`:640`).

This is low in isolation but it is a **security denominator**: as written, the document asserts that there exists a 50th protected operation to which the S-7 rule does not apply — which is precisely the shape of finding a reviewer should chase. Correct it to 49 of 49.

Related and worth folding in (carry-over, `review-adversarial-2026-09-15.md` F14, **not fixed**): S-7 hard-codes "49" in a document that twice forbids hard-coded surface denominators (`:704`, `:1698` — "Surface denominators … are always the **current generated Contract Spine inventory** — never hard-coded counts"). Replace both numbers with "every protected Contract Spine operation in the current generated inventory".

Also note from `G1`: 404 is declared on only **46 of 49** operations today, so three protected operations cannot express the safe-denial envelope at all without a spine change. That belongs in the PD10 work list and is not currently there.

---

### SEC-19 — LOW — The canonical error example omits the field A-8 declares required, and the `withheld` state has no CLI exit code or MCP kind

- The worked error example at `architecture.md:889-905` carries `category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, `details` — and **no `visibility`**, six lines before A-8 (`:664`) declares it "a required field on every error". The example is what implementers copy.
- The CLI exit-code table (`:686-702`) has exit 75 for category `redacted` and exit 73 for `not_found`. S-6 introduces `withheld` as a distinct render state; no exit code and no MCP `kind` is assigned to it, and `not_found` is retired from protected responses while its exit code remains listed without qualification.
- `FieldDisclosure` (`src/Hexalith.Folders.UI/Services/FieldDisclosure.cs:23-45`) has four members — `Visible`, `Redacted`, `Unknown`, `Missing` — and `RedactionDisclosureMapper.cs:22-27` **throws** on any visibility value it does not recognise. Adding `withheld` to the wire before adding it to the mapper turns a new confidential field into a UI exception. (Correctly noted as unimplemented in `ux-design-specification.md:39`.)

**Correction:** add `visibility` to the example; assign `withheld` an exit code and MCP kind, or state that it is a render state only and never a canonical category; list `FieldDisclosure` and `RedactionDisclosureMapper` in the PD8 change set.

---

### SEC-20 — LOW — S-6/S-7/S-8 read as current mechanism in the one section most likely to be read in isolation

The target-state label (`:236`, "PD8, PD10, and PD11 are target state, not current behavior") and the verified reality table (`:238-243`) are excellent and resolve `review-rubric-2026-09-15.md` CRITICAL-1. But they sit ~400 lines above the decision table, and the S-6/S-7/S-8 rows themselves are written in unqualified present tense — "Cleartext confidential values **are never** made durable", "All 49 protected Contract Spine operations **evaluate** authority before resource lookup". A story author opening §"Authentication & Security" directly reads them as the system's current guarantees.

**Correction:** prefix each of the three rows with a one-clause target-state marker, e.g. "**(target state — see §Release Authority Overlay; not implemented)**", as the transition matrix rows already do for the reserved operator events.

---

## Items claimed or appearing fixed since 2026-09-15 — verified

| Prior finding | Status | Evidence |
| --- | --- | --- |
| Adversarial F1 — S-6 fixes no token derivation; unkeyed digest is dictionary-recoverable and a cross-tenant correlation oracle | **FIXED** | `:639` now pins `HMAC-SHA-256(per-managed-tenant key, versioned classification tag ‖ canonical field identity ‖ NFC-normalized cleartext)`, one shared write-path implementation, and states the per-tenant keying rationale. Residual: width + key management (SEC-8) |
| Adversarial F6 / Technology F7 (part) — `visibility` value set unenumerated; `withheld` collides with `redacted` | **FIXED** | `:640` enumerates `metadata_only, redacted, withheld`; `:639` explicitly reconciles `withheld` vs the shipped `redacted` meaning |
| Adversarial F5 — the two S-7 envelopes are not fully named | **FIXED IN FORM, BROKEN IN SUBSTANCE** | Both are now fully named (`:640`) — with categories that do not exist and that contradict the approved matrix on five dimensions. See **SEC-3** |
| Rubric CRITICAL-1 — three normative sections describe systems that do not exist, with none of the honesty labels | **FIXED** | `:236` target-state statement + `:238-243` verified reality table. Residual wording only (SEC-20) |
| Rubric CRITICAL-2 / A-11 — migration and rollout dimension silent | **FIXED (raised as open)** | `:651` now names the rollout decision, the deprecation window, the client-rollout order, and the 403→404 mis-handling risk as an explicit open item. One dimension still missing: nothing forbids emitting both shapes concurrently during a deprecation window, which keeps the oracle live for its duration — the in-place amendment must be atomic per deployment, and the document should say so |
| Adversarial F2 / Rubric HIGH-1 — tokenizing repo/ref breaks the lock identity and the Git write path | **ACKNOWLEDGED, NOT RESOLVED** | `:643` records it as an open item with a stated assumption. Judged unsafe as stated — see **SEC-1** |
| Technology F7 (part) — OQ2 closed problem schemas missing from the regeneration list | **NOT FIXED** | `:645` regeneration list still omits them; OQ2 absent from the superseded-digest table. See **SEC-15** |
| Adversarial F13 — S-8 never specifies the derived-scope shape | **NOT FIXED** | `:641` lists dimensions and per-operation corrections but no structure. Compounded by the cursor gap in **SEC-10** |
| Adversarial F14 — S-7 hard-codes "49" against the document's own rule | **NOT FIXED**, and now contradicted by a second, wrong count at `:241`. See **SEC-18** |
| Authority F8 — S-7's 403 removal is not named by the approved §7.2 rule 1 | **STILL PRESENT** — governance dimension, owned by the authority lens; noted here only because SEC-3/SEC-4 change what the removal is replaced *with*, which makes re-approval unavoidable regardless |
| Technology F8 / Authority F5 — blank line orphaning S-7/S-8 from the decision table | **FIXED** | S-7 (`:640`) and S-8 (`:641`) are contiguous table rows |

Also verified as accurate and worth crediting: the production Dapr posture is real, not aspirational — `deploy/dapr/production/accesscontrol.yaml` carries `defaultAction: deny` on all six app configurations, the exact `folders → memories` `GET /api/search` allow-rule (`:122-141`) with `folders-workers` deliberately excluded, and secret-store `defaultAccess: deny` with two named refs; mTLS is enabled with a 24h workload cert TTL (`daprsystem.yaml:9-12`). The S-2 JWT parameter set matches the document and the exit-criteria artifact exactly (`FoldersAuthenticationServiceCollectionExtensions.cs:98-110`), with `MapInboundClaims = false` and `RequireHttpsMetadata` enforced outside Development. `HEXALITH_MEMORIES_API_TOKEN` is not logged.

---

## Coverage table — focus areas and verdicts

| Focus area | Verdict | Findings |
| --- | --- | --- |
| **S-7 — residual existence oracles** (timing, size, rate limits, correlation IDs, audit side effects, ETag/cursors) | **FAIL** | SEC-2 (two codes in the "one exact envelope"), SEC-5 (six disclosing fields the remediation does not scope), SEC-10 (timing/rate-limit/freshness/cursor channels unconstrained), SEC-15 (`visibility` as a third oracle channel), SEC-17 (async 202 path uncovered) |
| **S-7 — operations the rule misses** | **FAIL** | SEC-11 (F-6 incident path is outside the 49-operation inventory entirely), SEC-18 (the phantom 50th operation; 404 declared on only 46 of 49 today) |
| **S-7 — does removing 403 create a new oracle or break a legitimate distinction?** | **FAIL** | SEC-4 — yes: dropping 401 alongside 403 and routing absent/malformed authority to a retryable 503 destroys the token-refresh signal, inverts NFR76's deny-by-default, creates a retry-amplification path, and leaves a pre-authentication authority-health probe |
| **S-8 — caller-supplied locators that widen scope** | **FAIL** | SEC-7 (`ValidateProviderReadiness` is caller-supplied by construction and is the SSRF sink; no S-decision owns destination denial), SEC-10 (pagination cursors are an unlisted caller-supplied scope carrier) |
| **S-8 — the six per-operation scoping corrections** | **PARTIAL** | The six are individually reasonable and were verified to exist in the spine by the prior technology review. `GetTaskStatus` is unsatisfiable against S-7's ordering rule (SEC-6). `ListFolderAclEntries`/`GetEffectivePermissions` still diverge from the spine per matrix gaps `G8`/`G9`, unchanged by this amendment |
| **S-6 — truncation length** | **FAIL** | SEC-8 — "a fixed width" is not a value; collision budget unspecified |
| **S-6 — key management / rotation / re-tokenization** | **FAIL** | SEC-8 — no storage, no cadence, and rotation is a one-way destruction of the joins the token exists to serve |
| **S-6 — cross-tenant correlation property** | **PASS with caveat** | Per-tenant keying correctly defeats cross-tenant correlation. The non-recovery claim does not hold against a tenant insider with a write-and-observe oracle over low-entropy values (SEC-14); state the adversary model |
| **S-6 — does "no durable cleartext" survive the lock identity / Git executor / restart recovery?** | **FAIL (critical)** | SEC-1 — the stated assumption is **not safe**; it relocates durable cleartext into an unnamed, shared, unencrypted, unbounded store rather than removing it |
| **S-6 — does tokenization defeat the evidence-joining it exists to serve?** | **PARTIAL FAIL** | Within one key epoch and one write-path implementation, joins work. Across a key rotation they silently return empty (SEC-8). For search, tokenization breaks FR58 metadata-token recall outright (SEC-14) |
| **F-6 — incident authorization + removal of cross-tenant operator views** | **FAIL** | SEC-11 — dual authorization is well-conceived but the permission does not exist in the action catalog (`G4`), the operation family has zero Contract Spine operations (`G6`), the path contradicts the UI boundary, and the precondition is unavailable in the trigger condition |
| **Credential and secret handling (GitHub/Forgejo tokens, SSH)** | **FAIL** | SEC-7 (tenant credential transmitted to a tenant-named, unvalidated host), SEC-12 (the C9-named audit sanitizer is the only one of nine filters lacking the token/JWT/PEM regexes), SEC-14 (shared static Memories bearer with no S-5 coverage). Bearer-over-plaintext (CLI `Commands/CommandPipeline.cs:164-169`; MCP `Program.cs:26-28` explicitly accepts `http`) and credential-file permissions (`Cli/Credentials/CredentialStore.cs:67`, no mode check, no write path) are correctly owned by NFR74/NFR77 and are not re-reported as document defects |
| **SSRF surface of the user-supplied Forgejo base URL** | **FAIL** | SEC-7 — zero `ConnectCallback`/`IPAddress`/`Dns.` in the repository; DNS rebinding unmitigated; the control exists only as backlog prose and has no architecture decision |
| **Webhook / callback authenticity** | **PASS** | Explicit no-webhook posture, stated as an architectural invariant rather than an omission (`:97`, `:936-939`); would-be webhook routes return 404. Tenant-routing is correctly named as a prerequisite for post-MVP introduction |
| **Multi-tenant isolation — identity, streams, topics** | **PASS** | `{managedTenantId}:{domain}:{aggregateId}`, per-tenant topics `{tenantId}.{domain}.events`, `system` tenant reserved, aggregate-ID opacity — all coherent and consistently stated (`:78`, `:912-915`, `:1605`) |
| **Multi-tenant isolation — search index + security-trim attributes** | **FAIL** | SEC-14 — a single shared physical index as the cross-tenant aggregation point, defended only by an application-layer trim with no named negative test, made worse by PD8 tokens |
| **Multi-tenant isolation — read models, cache keys, audit, telemetry, logs** | **PARTIAL** | Rules are sound (`:1020-1031`, C10) and the cache-key gate is real, but the artifact the document names does not exist (SEC-12) and the audit filter is the weakest of nine (SEC-12) |
| **AuthN — OIDC validation** | **PARTIAL FAIL** | SEC-13 — the frozen eight match the code exactly, but `ValidAlgorithms`/`typ`, a lifetime ceiling, and principal-level revocation are all absent from the frozen set |
| **AuthN — service-to-service via Dapr** | **PASS with gap** | Production deny-by-default + mTLS + the memories allow-rule all verified present. The sidecar-only app-port boundary is expressed in no manifest and no decision (SEC-9) |
| **AuthN — deny-by-default binding at the HTTP surface** | **FAIL** | SEC-9 — no fallback policy, no endpoint authorization metadata anywhere in the server; NFR76 is bound to S-7, whose answer for absent authority does not deny |
| **Migration / rollout risk of the PD10 breaking correction** | **PARTIAL PASS** | `:651` raises it properly and names the 403→404 client-mishandling risk. Missing: a requirement that the removal be atomic per deployment — a deprecation window that serves both shapes keeps the oracle live for its duration, so the security-exception in-place amendment is the only correct option and should be recorded as such |

---

## Minimum set to reach PASS-WITH-FINDINGS

1. **SEC-1** — decide the PD8 operational boundary rather than assuming it: HMAC the lock key, re-derive the executor's ref at call time, and name the control set for whatever durable cleartext survives.
2. **SEC-2 / SEC-3 / SEC-4** — restate S-7's envelopes verbatim from `docs/contract/authorization-matrix.md:144-147`, all three outcomes including 401, one code for the 404, and no invented categories.
3. **SEC-5** — widen the remediation from "one category" to "one byte-identical envelope", and add the missing gap rows to the matrix so the correction is scoped correctly under A6b.
4. **SEC-6** — state the tenant-partitioned parent-resolution carve-out so S-7 and S-8 are jointly satisfiable.
5. **SEC-7 / SEC-9** — add the two missing spine decisions: provider-endpoint destination policy, and deny-by-default authorization binding. NFR75 and NFR76 must point at decisions in this document, not at backlog stories.
6. **SEC-8** — pin the truncation width and decide key storage and rotation, including the audit-continuity consequence.
7. **SEC-16** — add the denial-envelope conformance gate, and correct the ✅ coverage claims that certify gate coverage the NFR74–NFR84 band does not have.
