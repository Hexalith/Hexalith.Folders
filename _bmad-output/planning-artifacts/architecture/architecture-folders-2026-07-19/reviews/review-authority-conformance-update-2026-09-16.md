# Authority Conformance + Closure Verification — Update Run, 2026-09-16

**Lens:** Authority conformance / closure verification
**Target:** `_bmad-output/planning-artifacts/architecture.md` (working tree, `updated: '2026-09-16'`, 1902 lines; +91/−46 against `de281e7`)
**Verifying closure against:** `validation-report-2026-09-16.md` (gate **FAIL**, 96 findings: 12 critical / 37 high / 34 medium / 13 low)
**Also in the change set:** `docs/exit-criteria/c6-transition-matrix-mapping.md` (co-normative artifact, modified), `.memlog.md`
**Method:** every claim re-derived from the repository at the working tree. Two test suites executed. No file was modified by this run.

---

## Lens verdict — **FAIL**

Two residual **CRITICAL** findings, both on the Tier-1 item, both mechanically fixable. Everything else about this remediation is strong: it is the most effective pass this document has received, it did not undo a single thing the prior gate marked as strong, and it closed 8 of 12 criticals outright.

The FAIL is narrow and specific: **the S-7 row itself is now byte-exact and correct, but (a) it still names one token — `withheld` — that exists in no enum anywhere, which is the exact AUTH-1 defect shape surviving inside the sentence that was supposed to fix it, and (b) the two in-document tables that S-7 declares are *derived from* the matrix were not propagated, so the document's own canonical CLI exit-code table routes two of the three S-7 outcomes to `internal_error` / exit 1 while the as-built C13 oracle maps them to 65 and 72.** An implementer performing the PD10 regeneration against this document would regenerate CLI/MCP mappings that contradict the shipped oracle. That is what blocks A6b.

| Severity | Count |
| --- | ---: |
| Critical | **2** |
| High | **5** |
| Medium | **5** |
| Low | **2** |
| **Total** | **14** |

---

# HALF 1 — The Tier-1 fix (AUTH-1 / CV-1)

## 1.1 Byte-exact reproduction — S-7 vs the approved OQ3 matrix

Source of authority: `docs/contract/authorization-matrix.md` §"Canonical Outcomes", lines **143–147**.

| Outcome | Column | `authorization-matrix.md` | `architecture.md:656` (S-7) | Match |
| --- | --- | --- | --- | :---: |
| `authentication-failure-401` | status | `401` | `401` | ✅ |
| | category | `authentication_failure` | `authentication_failure` | ✅ |
| | code | `authentication_required` | `authentication_required` | ✅ |
| | retryable | `false` | `retryable: false` | ✅ |
| | client action | `check_credentials` | `check_credentials` | ✅ |
| | details visibility | `redacted` | `details.visibility: redacted` | ✅ |
| `safe-denial-404` | status | `404` | `404` | ✅ |
| | category | `tenant_access_denied` | `tenant_access_denied` | ✅ |
| | code | `resource_unavailable` | `resource_unavailable` | ✅ |
| | retryable | `false` | `retryable: false` | ✅ |
| | client action | `no_action` | `no_action` | ✅ |
| | details visibility | `redacted` | `details.visibility: redacted` | ✅ |
| `authority-unavailable-503` | status | `503` | `503` | ✅ |
| | category | `read_model_unavailable` | `read_model_unavailable` | ✅ |
| | code | `projection_unavailable` | `projection_unavailable` | ✅ |
| | retryable | `true` | `retryable: true` | ✅ |
| | client action | `retry` | `retry` | ✅ |
| | details visibility | `redacted` | `details.visibility: redacted` | ✅ |

**18 of 18 cells reproduce byte-exact. The prior critical AUTH-1 is closed on this axis.**

The four invented tokens the prior gate found are gone from the document entirely:

```
grep -n "authority_unavailable\|retry_after_backoff\|category \`authorization\`\|\`availability\`" architecture.md
→ no matches (exit 1)
```

Three further improvements in the amended row that were not asked for and are correct:

- The row now carries an explicit **precedence-of-authority clause**: *"The wire values are owned by `docs/contract/authorization-matrix.md` §"Canonical Outcomes" and transcribed here, never restated independently — … on any disagreement the matrix wins and this row is the defect."* This is the right structural fix: it converts a transcription into a subordinate copy, so the next drift is a defect by construction rather than a competing authority.
- **SEC-5 is answered in full.** Envelope identity is extended past `category` to *status, category, code, message, `type` URI, detail keys, `retryReasonCode`, `layer`, `timingBucket`*, with the explicit warning that *"a remediation scoped to `category` alone leaves the oracle intact in the fields underneath."*
- **SEC-2 and SEC-4 are answered.** The 404 is now *"a single code, chosen by no predicate"*, and the 401 is outcome (1) *"evaluated before every other conjunct, so an unauthenticated caller is never routed to the outage envelope"* — which is precisely the pre-auth retry-amplification inversion SEC-4 identified.

## 1.2 Token vocabulary membership

S-7 asserts: *"Every token above is a member of the closed canonical vocabulary in `tests/fixtures/parity-contract.schema.json`."*

Reproduced against `tests/fixtures/parity-contract.schema.json` (`$defs` = `adapter_name`, `canonical_error_category`, `cli_exit_code`, `mcp_failure_kind`, `ownership`) and against `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`:

| Token | Class | In `parity-contract.schema.json`? | In generated client? | Verdict |
| --- | --- | :---: | --- | :---: |
| `authentication_failure` | category | ✅ `$defs/canonical_error_category` | ✅ `CanonicalErrorCategory:13324` | ✅ |
| `tenant_access_denied` | category | ✅ | ✅ | ✅ |
| `read_model_unavailable` | category | ✅ | ✅ | ✅ |
| `authentication_required` | **code** | ❌ no code vocabulary exists in that file | ✅ spine `hexalith.folders.v1.yaml:5780` | ⚠️ real, wrong citation |
| `resource_unavailable` | **code** | ❌ | ✅ `FileSafeResourceUnavailableProblemCode:13903` | ⚠️ real, wrong citation |
| `projection_unavailable` | **code** | ⚠️ present, but as a *category*, not a code | ✅ (as category) | ⚠️ real, wrong citation |
| `check_credentials` | client action | ❌ no client-action vocabulary in that file | ✅ `ProblemDetailsClientAction:13825` | ⚠️ real, wrong citation |
| `no_action` | client action | ❌ | ✅ | ⚠️ real, wrong citation |
| `retry` | client action | ❌ | ✅ | ⚠️ real, wrong citation |
| `redacted` | visibility | ❌ no visibility vocabulary in that file | ✅ `DetailsVisibility:15167` | ⚠️ real, wrong citation |
| `metadata_only` | visibility | ❌ | ✅ `DetailsVisibility:15173` | ⚠️ real, wrong citation |
| **`withheld`** | **visibility** | ❌ | ❌ **not in `DetailsVisibility`; 0 hits in the entire OpenAPI spine directory** | ❌ **exists nowhere** |

---

### **U-C1 (CRITICAL) — `withheld` is named in the amended S-7 row as a member of a closed enumeration, and it exists in no vocabulary anywhere**

**Anchor:** `architecture.md:656` — *"`visibility` is a required field on every error and is enumerated — `metadata_only`, `redacted`, `withheld` — not free text, so two surfaces cannot invent different vocabularies."*

**Reproduction:**

```
grep -rn "withheld" src/Hexalith.Folders.Contracts/openapi/        → 0 matches
enum DetailsVisibility (HexalithFoldersClient.g.cs:15167)          → { redacted, metadata_only }   (2 members)
enum Details2Visibility … Details7Visibility                       → { redacted, metadata_only }   (all 2 members)
tests/fixtures/parity-contract.schema.json                          → no visibility vocabulary at all
```

**Why this is the AUTH-1 defect and not a new one.** AUTH-1 was *"S-7 names a denial vocabulary contradicting the approved matrix; the invented tokens are in no enum."* The remediation removed four invented tokens and left a fifth standing in the same sentence, under a clause that explicitly warrants all of them (*"Every token above is a member of the closed canonical vocabulary…"*). The sentence's stated purpose — *"so two surfaces cannot invent different vocabularies"* — is defeated by the sentence itself: it publishes a third member that no surface can emit and no schema will accept.

**Consequence for A6b.** `visibility` is a **required** field on every error (A-8, `:692`). An implementer regenerating the spine from this document adds `withheld` to the required-field enum. `withheld` is a **PD8** construct — the `confidential` tier and its render state — and PD8 is explicitly unapproved and unowned (`:277`, `:281`). The PD10 regeneration would therefore smuggle an unapproved PD8 wire value into the artifact A6b reapproves.

**Mitigating context (read it before grading the author).** The document is *elsewhere* honest about this: `:243` states plainly *"No `confidential` tier, no tokenizer, and no `withheld` render state exists"*, and `:281` routes the `withheld` render state to the unowned PD8 story. This is a one-sentence inconsistency inside an otherwise scrupulous pass — but it is on the Tier-1 line, so it is graded as instructed.

**Fix (one edit).** Either enumerate the shipped closure — `metadata_only`, `redacted` — and add *"`withheld` is a PD8 addition and is not in the vocabulary until PD8 is approved and the spine regenerated"*; or drop the enumeration from S-7 entirely and cite `DetailsVisibility` / the spine as its owner, matching the precedence clause the row already establishes for the other values.

---

### **U-C2 (CRITICAL) — the two derivation tables S-7 declares "derived from the matrix" were not propagated, and the document's own canonical table routes two of the three S-7 outcomes to `internal_error` / exit 1**

**Anchors:** `architecture.md:656` (the claim) vs `:713–731` (CLI exit-code table) and `:733` (MCP failure-kind set).

S-7 claims: *"the C13 `cli_exit_code` / `mcp_failure_kind` columns are **derived from the matrix**, so CLI and MCP cannot disagree on whether an authority outage is retryable."*

The document's own table, headed **"CLI exit-code mapping (canonical, asserted by C13 oracle)"**, contains **no row** for `authentication_failure`, `read_model_unavailable`, or `projection_unavailable`. Its final row is:

```
| 1 | `internal_error` | Catch-all for unmapped server exceptions; correlation ID always emitted to stderr |
```

So by the table's own rule, both new S-7 outcomes are *unmapped* and derive **exit 1 / `internal_error`**. The MCP failure-kind set at `:733` enumerates 14 kinds and likewise omits all three.

**The as-built oracle disagrees.** Reproduced from `tests/fixtures/parity-contract.yaml` (171 KB, 46 distinct outcome mappings):

| Canonical category | `cli_exit_code` (oracle) | `mcp_failure_kind` (oracle) | Rows | `architecture.md` table |
| --- | ---: | --- | ---: | --- |
| `authentication_failure` | **65** | `authentication_failure` | 49 | **absent** → derives 1 |
| `tenant_access_denied` | 66 | `tenant_access_denied` | 49 | ✅ 66 |
| `read_model_unavailable` | **72** | `read_model_unavailable` | 37 | **absent** → derives 1 |
| `projection_unavailable` | **72** | `projection_unavailable` | 35 | **absent** → derives 1 |

The oracle's own vocabularies confirm the room exists: `$defs/mcp_failure_kind` has **49** members including all three; `$defs/cli_exit_code` = `[0,1,64,65,66,67,68,69,70,71,72,73,74,75,76]`.

**Why this is the gate-blocking shape.** This is precisely the failure mode the prior gate named as dominant — *"landed in one paragraph and left its contradiction standing elsewhere in the same document"* — and here the contradicting paragraph is the **derivation target of the very claim S-7 makes**. `RUB-5` ("S-7's envelopes are not named anywhere the C13 columns are derived from") and the derivability half of `SEC-3` and `ADV-4` are therefore **not closed**. Prior CV-1's stated consequence — *"CLI derives exit 1 / non-retryable while MCP derives retryable for the same authority outage"* — still reproduces from this document, now for a different reason (omission rather than invention).

**Secondary reproduction defects in the same table**, found while verifying it:

- `:718` maps exit **65 → `credential_missing`**. The oracle maps 65 → `{authentication_failure, credential_reference_invalid}`. `credential_missing` appears in the oracle only as a `pre_sdk_error_class`, never as a post-SDK `canonical_error_category`.
- `:724` maps exit **72 → `reconciliation_required`** only. The oracle maps 72 → 8 categories.
- The table is one-category-per-code throughout; the oracle is many-to-one. The header **"canonical, asserted by C13 oracle"** is a false provenance claim: no test asserts this table against `parity-contract.yaml`.

**Fix.** Either (a) replace the table body with the four S-7-relevant rows reproduced from the oracle and state that `tests/fixtures/parity-contract.yaml` owns the full mapping, or (b) delete both in-document tables and point at the oracle — consistent with the `Directory.Packages.props` delegation pattern this same remediation adopted for versions.

---

### **U-H1 (HIGH) — S-7's provenance sentence cites a file that cannot validate 8 of its 12 tokens**

`tests/fixtures/parity-contract.schema.json` defines exactly five vocabularies (`adapter_name`, `canonical_error_category`, `cli_exit_code`, `mcp_failure_kind`, `ownership`). It contains **no** error-code vocabulary, **no** client-action vocabulary, and **no** visibility vocabulary. Only the three *categories* are members of it.

The three codes and three client actions *are* real — they live in `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and in the generated client (`FileSafeResourceUnavailableProblemCode`, `ProblemDetailsClientAction`, `DetailsVisibility`). The defect is the citation, not the values: an implementer or a future gate-writer who takes the sentence at face value and tries to validate the codes against that schema will find nothing to validate against and may reasonably conclude the tokens are unconstrained. The file also carries its own disclaimer — *"This is not the public product contract and does not define final OpenAPI semantics"* — which makes it the wrong authority to name in a row whose whole purpose is to fix wire values.

**Fix.** Name the real owners per token class: categories → `parity-contract.schema.json` `$defs/canonical_error_category` **and** the generated `CanonicalErrorCategory`; codes and client actions → the OpenAPI spine + `ProblemDetailsClientAction`; visibility → `DetailsVisibility`.

## 1.3 Self-contradiction sweep on the S-7 values

| Check | Result |
| --- | --- |
| Invented categories/codes/actions anywhere in the document | **clean** — 0 hits |
| Exit 66 → `tenant_access_denied` (the prior gate's cited contradiction at old `:691`) | **now consistent** — `safe-denial-404` carries `tenant_access_denied`, which the table maps to 66 |
| CLI exit-code table covers the 401 and 503 outcomes | ❌ **U-C2** |
| MCP failure-kind set covers the 401 and 503 outcomes | ❌ **U-C2** |
| `not_found` still listed (exit 73, and in the MCP kind set) after S-7 retires it from protected-operation responses | ⚠️ dead-but-not-contradictory: all 49 spine operations are protected, so the row is unreachable. Worth a note, not a finding. |
| A-8 (`:692`) agrees with S-7 | ✅ — `visibility` required, defers to "the S-7 envelopes" |
| S-4 (`:653`) evaluation order agrees with S-7 | ✅ — "authority-unavailability is evaluated first, then authority, and only then any protected-resource lookup" |
| Concern #20 bounded-staleness vs S-7 deny-on-stale (prior `RUB-7`) | ✅ **closed** — `:673` "Degraded mode is decided once, by the approved matrix", and `:131` re-labelled with a forward pointer |
| `AuthorizationMatrixContractTests` reddening risk (prior SEC-3) | ✅ **verified green** — `Contracts.Tests` **314 total, 0 failed** |

---

# HALF 2 — Closure audit

## Dispositions used

**CLOSED** — fixed correctly, verified against the repo. **PARTIAL** — fixed in one place, a contradiction still stands elsewhere. **ROUTED** — deliberately recorded as an owned open item rather than decided (acceptable under the ratified scope). **OUT-OF-SCOPE** — needs `epics.md` or production code, both explicitly excluded. **NOT CLOSED**.

## Tally

| | Critical (12) | High (37) | **Total (49)** |
| --- | ---: | ---: | ---: |
| CLOSED | 8 | 20 | **28** |
| PARTIAL | 1 | 5 | **6** |
| ROUTED | 3 | 9 | **12** |
| NOT CLOSED | 0 | 3 | **3** |
| OUT-OF-SCOPE (as a whole item) | 0 | 0 | **0** |

Two items carry an out-of-scope *half*: `RUB-9` and `ADV-6` both require `epics.md` edits, which the ratified scope excludes. In both cases the architecture-side half was handled correctly (`RUB-9`) or not at all (`ADV-6`).

## The 12 criticals

| ID | Disposition | Verification |
| --- | --- | --- |
| **AUTH-1** S-7 vocabulary | **CLOSED** *(with U-C1/U-C2 residuals)* | 18/18 cells byte-exact; 4 invented tokens = 0 hits. Residuals are `withheld` and the underived tables. |
| **ADV-1** `(state,event,guard)` in one paragraph only | **PARTIAL** | **Fixed:** `:365` (matrix intro now "keyed on `(currentState, event, guard)`"), `:445`, `:447` (gate re-keyed, with the one-branch rationale), `:1097` (CI-gate bullet), **and the co-normative `c6-transition-matrix-mapping.md`** — which gained a full *Guard Discriminators* table with all four pairs, both branches, the durable field each reads, the "never `X-Hexalith-Task-Id`" rule, and `approval-pending under A7b` annotations on three mapping rows. That is a genuinely good propagation. **Still pair-keyed:** `:325` — the **C6 exit-criterion definition itself** ("every (state, event) pair → outcome"), which is the artifact A7b reapproves — and `:1874` Implementation Guidance ("unlisted (state, event) pairs MUST reject"). See **U-H3**. The live code divergence is now honestly recorded at `:429` → that half is ROUTED. |
| **ADV-2** one-branch guard, no durable field | **CLOSED** | `:446` declares `stagedByTaskId` / `stagedByPrincipal` on the workspace aggregate, set at first `FileMutated`, surviving the lock instance, cleared only on commit/discard/C3 cleanup; states the guard is `stagedByTaskId == the re-acquiring task` and **"not the `X-Hexalith-Task-Id` request header, which is caller-supplied input and never authority (S-3, S-8)"**; names both failure modes ADV-2 identified; and closes with *"`stagedBy*` has zero occurrences in `src/` today — declaring the fields is part of the owning PD11 story, not a description of what exists."* **Verified: `grep -rc "stagedBy" src/` → 0.** The mapping doc carries the same. |
| **ADV-3** two cleanup triggers | **CLOSED** | `:439` and `:1015` now both read *"terminal task closure with no active task"*, and `:1015` explicitly says *"the single trigger fixed by C3 (see §"C3 is the cleanup authority"), stated here in the same vocabulary."* The follow-on contradiction ADV-3 exposed (the staged-content window may never start) is ROUTED at `:441`. |
| **SEC-1** PD8 durable cleartext | **ROUTED** | `:657`–`:661` state the carve-out *"relocates the durable cleartext rather than removing it, and that falsifies S-6's headline"*, then `:661` **"The decision this needs (route to Security + Architecture, with A7b)"** names three concrete control options including the keyed-HMAC lock identity SEC-1 recommended. Not decided — correct under the ratified scope. Note the document now holds both the original assumption (`:659`) and its refutation (`:657`); they are adjacent and labelled, so the reader is not misled. |
| **REAL-1** `Hexalith.Folders.EventStore` absent | **CLOSED** | 6 occurrences. Added to the project tree with `AS-BUILT` markers, to the app-ID list (`eventstore` now resolves to it, not to the sibling), and given a dedicated bullet describing it as composing `Hexalith.EventStore.Gateway` + `.DomainService` and hosting the A-9 intent adapters. **Verified:** `src/Hexalith.Folders.EventStore/` exists and is in the `.slnx` inventory. |
| **REAL-2** I-3 merge-blocking policy gate | **CLOSED** | I-3 now says *"Policy validation — as-built: a static-fixture xUnit suite driven by `tests/tools/run-dapr-policy-conformance-gates.ps1`, surfaced through `.github/workflows/policy-conformance.yml`, which is `schedule:` + `workflow_dispatch:` only and therefore cannot block a m[erge]"*. **Verified byte-exact:** the workflow's `'on':` block is `schedule: cron '43 2 * * *'` + `workflow_dispatch`, and the script exists. |
| **REAL-3** I-8 token buckets | **CLOSED** | I-8 now opens **"Target (not built — this whole row is design intent, not shipped behavior)."** Tree entries `RateLimiting/`, `PerTenantTokenBucket.cs`, `GlobalReconciliationBucket.cs` each marked `NOT BUILT`. The validation section was demoted in lockstep: Performance now reads *"The I-8 provider rate-limit chaos test is **target, not built**"*, Scalability *"I-8 per-tenant token buckets are **target, not built**"*. **Verified:** no `RateLimiting/` directory. |
| **REAL-4** C10 pinned artifact + ci.yml lint job | **CLOSED** | Corrected in **four** places in lockstep: the C10 ops-plan row, cross-cutting concern #1, concern #13, and the project-tree entry — all now name `GovernanceCompletenessGateTests` over `tests/fixtures/cache-key-exceptions.yaml`, with the 2026-05 target explicitly marked never built. **Verified:** `tests/fixtures/cache-key-exceptions.yaml` exists; `src/Hexalith.Folders/Caching/` does not. |
| **RUB-1** `✅` NFR coverage certification | **CLOSED** | Heading changed from *"Requirements Coverage Validation ✅"* to **"Requirements Coverage Validation — nine of eleven NFR categories"**. Body now reads *"Non-Functional Requirements Coverage — partial, and the gap is named"*, followed by a dedicated paragraph *"The two categories admitted 2026-09-15 are not"* which names `NFR74`–`NFR78` and `NFR79`–`NFR84` and reconciles to the eleven at `:59`. "Verification Expectations" narrowed to *"every NFR **in the nine original categories**"*. This is the cleanest fix in the set. |
| **RUB-2** disaster recovery silent | **ROUTED** | `:769` — routed to Architecture + Operations with Epic 13. Names RTO/RPO per store, backup mechanism, restore runbook, restore cadence, and — crucially — the hazard RUB-2 identified: *"a restore from before a deletion can resurrect data whose deletion was a compliance obligation, including admission records under the P7Y retention regime."* |
| **RUB-3** event schema evolution silent | **ROUTED** | `:644` — routed to Architecture with Epic 12. Names the version marker, the upcasting seam and where it runs, the additive-vs-breaking rule, and grounds it in the live case: *"it is currently adding an event to the published vocabulary (`LockLeaseBecameStale`)"*. |

## The 37 highs

### Adversarial (5)

| ID | Disposition | Verification |
| --- | --- | --- |
| **ADV-4** S-7 tokens outside canonical vocabulary | **PARTIAL** | Categories closed. Residual = **U-C1** (`withheld`) + **U-H1** (provenance) + **U-C2** (derivation). |
| **ADV-5** `visibility` closed twice at two closures | **CLOSED** | `visibility` is now enumerated exactly once in the document, at `:656`. A-8 (`:692`) declares it required and defers. No second closure. |
| **ADV-6** D-9 413 retry header collides with a reserved request-side name | **NOT CLOSED** (doc) / **OUT-OF-SCOPE** (epics half) | `:914`–`:915` unchanged by this pass. `X-Hexalith-Retry-Transport` (response) and `X-Hexalith-Retry-As` (request) still coexist with only prose disambiguation. |
| **ADV-7** three "current deployed behaviour" anchors false at HEAD | **NOT CLOSED** — see **U-H2** | The remediation edited the *tail* of `:182` (the Story 10.9 clause, correctly) and left the false present-tense code claim in the same sentence. |
| **ADV-8** bridge has two writers, no arbitration | **CLOSED** | `:675` **"One writer owns the semantic-indexing bridge (arbitration, 2026-09-16)"** — new normative paragraph resolving 10.7 vs 12.5. |

### Technology (6) — all six closed

| ID | Disposition | Verification |
| --- | --- | --- |
| **TECH-1** MCP SDK `1.3.0` ×4 | **CLOSED** | 0 hits. A-5 now reads *"version owned by `Directory.Packages.props`"*; the tree comment is de-versioned. **Actual: `ModelContextProtocol` 2.2.0.** |
| **TECH-2** Aspire `13.4.6` | **CLOSED** | 0 hits. I-1 and the stack list delegate: *"versions are owned by `references/Hexalith.Builds/Props/Directory.Packages.props`, never restated here"*. **Actual: `Aspire.Hosting` 13.5.3; `CommunityToolkit.Aspire.Hosting.Dapr` 13.5.1-beta.752.** |
| **TECH-3** EventStore/Tenants `3.15.1` | **CLOSED** | 0 hits. The *"verified on NuGet 2026-05-09"* stamp is gone from Decision Compatibility. **Actual: `HexalithEventStoreVersion` 3.104.0, `HexalithTenantsVersion` 5.7.0.** |
| **TECH-4** package-management decision inverted | **CLOSED** | `:535` now records **both** consumption paths, *"selected by the `UseNuGetDeps` property (verified as-built — the 2026-05 draft recorded only the first and forbade the second, which is what the release lane actually does)"*. |
| **TECH-5** Testcontainers mandated, unused | **CLOSED** | Removed from the non-negotiable stack, the testing stack list, and the sibling-convention list. One residual mention survives at `:81` and is *self-labelling*: *"`Testcontainers` was named here in the 2026-05 draft, has zero usage in Folders, and is not a constraint."* |
| **TECH-6** `oasdiff` in six places, absent from repo | **CLOSED** | Removed from C12, Phase 5, the tree (`tests/tools/oasdiff/` → `forgejo-drift/`), the Forgejo contract-tests bullet, and the CI-gate summary. One residual at `:691`, again self-labelling: *"the 2026-05 draft named `oasdiff` as the classifier, which was never adopted and exists nowhere in the repository."* **Verified:** `tests/tools/oasdiff` absent; `tests/tools/forgejo-drift/` and `tests/tools/run-nightly-drift-gates.ps1` present. |

### Rubric (9)

| ID | Disposition | Verification |
| --- | --- | --- |
| **RUB-4** C6 lockstep has no gate; named gate doesn't read the compared document | **PARTIAL** | `:427` now names **five** co-normative artifacts (adds `docs/diagrams/workspace-lifecycle.md`). A real gate does exist and is green: `ConsumerDocsConformanceTests` parses the architecture C6 matrix and asserts it edge-for-edge against the diagram (**41 edges / 24 events / 11 states**, executed this run). But **no gate compares the mapping document** to the matrix, and the document still does not say which gate enforces which pair. Adding the diagram to the co-normative set without saying what enforces it widens the claim slightly ahead of the evidence. |
| **RUB-5** S-7 envelopes absent where the C13 columns are derived | **NOT CLOSED** | = **U-C2**. |
| **RUB-6** `withheld` / `confidential` never reach the UX contract or F-5 | **ROUTED** | `:243` and `:281` both record the tier and render state as non-existent and assign them to the unowned PD8 story. F-5 unchanged. Correct disposition under the ratified scope; interacts with **U-C1**. |
| **RUB-7** degraded mode decided twice in opposite directions | **CLOSED** | `:673` decides it once, in favour of the approved matrix, and prices the consequence: *"while Tenants is unavailable beyond the staleness bound, protected reads return a retryable 503 rather than degrading. `OQ12`/`OQ13` must accept that envelope."* Concern #20 re-labelled with a pointer. Model closure for a contradiction fix. |
| **RUB-8** S-6 tokenizer has no owner or key management | **ROUTED** | `:281` — *"The tokenizer has no owning component in this architecture"*, `FolderAuditSanitizer.cs` explicitly *not* it, and per-tenant key management + rotation named as owed PD8 work. |
| **RUB-9** PD8/PD11 code-landing unowned at every rank | **ROUTED** (architecture half) / **OUT-OF-SCOPE** (epics half) | `:277` generalised from "no story owns PD10" to **"None of the three PRD corrections has an owning story (open — route to PM + Delivery)"**, with a per-correction work breakdown at `:279`–`:281`. **Verified:** `grep -c "PD8\|PD10\|PD11" epics.md` → **0**. |
| **RUB-10** structure mapping presented as complete | **CLOSED** | Two banners added, which is exactly the prior gate's structural recommendation: the tree gets *"This tree is the TARGET layout, authored 2026-05 and never re-derived from the repository… `Hexalith.Folders.slnx` is the authoritative as-built project inventory"*; the requirements-to-structure table gets *"Target routing, not an as-built index (authored 2026-05, partially stale)"*. |
| **RUB-11** write concurrency undecided | **ROUTED** | `:642` — names three candidate mechanisms and requires the losing writer's canonical error, and states the stakes: *"Two units reading this document today could reasonably pick different ones and both believe they complied."* |
| **RUB-12** no deployment profile | **ROUTED** | `:767` — routed with OQ12/OQ13, listing the minimum record: supported profile, environment list, per-app-ID replica/scaling rule, configuration precedence. Correctly notes it blocks RUB-11. |

### Security (8)

| ID | Disposition | Verification |
| --- | --- | --- |
| **SEC-2** "single 404" is two codes chosen by a predicate | **CLOSED** | *"a single code, chosen by no predicate."* |
| **SEC-3** invented categories break derivability; would redden `AuthorizationMatrixContractTests` | **PARTIAL** | Categories closed; **`Contracts.Tests` verified green, 314/314**. Derivability half = **U-C2**. |
| **SEC-4** absent/malformed authority → retryable 503; 401 dropped | **CLOSED** | 401 is outcome (1), evaluated before every other conjunct. |
| **SEC-5** remediation scoped to `category` only | **CLOSED** | Nine fields pinned byte-identical; the failure mode is named in the row. |
| **SEC-6** S-7 and S-8 jointly unsatisfiable for task-derived scope | **PARTIAL** | S-8 (`:657`) is **unchanged**. The new `:446` paragraph does resolve the caller-supplied-task-id problem for the PD11 lifecycle guard (`stagedByTaskId`, never the header), which is most of SEC-6's substance — but S-8 itself still asserts unqualified derivation while `GetTaskStatus` takes a caller-named task. |
| **SEC-7** SSRF destination policy | **ROUTED** | `:677` — routed with OQ12. Requires the check location, re-resolution policy, and the denial envelope, with the right constraint: *"it must be the S-7 `safe-denial-404`, or the readiness probe becomes a network-existence oracle for the tenant's private ranges."* |
| **SEC-8** tokenizer truncation width; rotation destroys evidence joins | **ROUTED** | Rotation policy named as owed PD8 work at `:281`. Truncation width still unspecified — the narrower half of the finding survives inside the routed item. |
| **SEC-9** no deny-by-default HTTP binding | **ROUTED** | `:679` — routed with Story 13.2, requiring a fallback policy plus a conformance test over the generated surface, and naming the collision: *"if 13.2 lands first it will build a second deny-by-default shape beside S-7."* |

### Code reality (4)

| ID | Disposition | Verification |
| --- | --- | --- |
| **REAL-5** reality table understates PD11 in the dangerous direction | **CLOSED** | `:241` corrected from *"rejects four of the five"* to **"rejects three of the five transitions this correction adds"**, naming them explicitly (`changes_staged → inaccessible` on the revocation events, `dirty → changes_staged`, `dirty → ready`); `:429` restates *"two of the five new transitions are accepted onto the unguarded branch."* Corrected in the strict direction, as recommended. |
| **REAL-6** Story 12.4 pointed at a non-existent `NotImplementedException` | **CLOSED** | `:263` **"Corrected anchor (2026-09-16): there are no `NotImplementedException` workspace-executor methods — no workspace-executor type exists, and the single `NotImplementedException` in `src/` is the deliberate live-readiness seam in `OctokitGitHubApiClient.cs:60`, which is a *readiness probe*, not a write path."* **Verified byte-exact:** exactly one `NotImplementedException` in `src/`, at that file and line. |
| **REAL-7** `FolderWorkspaceDirtyResolution` discriminates a different pair | **PARTIAL** — see **U-H4** | |
| **REAL-8** five FR blocks and three concerns route to non-existent paths | **CLOSED** | Two rows corrected outright (#1, #13) and the rest covered by the `:1621` banner. This is the disposition the prior gate recommended. |

### Authority (5)

| ID | Disposition | Verification |
| --- | --- | --- |
| **AUTH-2** "49 of 50" | **CLOSED** | `grep "49 of 50"` → 0 hits. `:242` now reads *"403 on 49 of 49, 404 on 46 of 49 — the denominator is the generated inventory, never a number transcribed here."* **Verified byte-exact against `authorization-matrix.md:341` (`G1`):** *"403 on 49 of 49 operations and 404 on 46 of 49."* Note the fix went further than asked: it removed the hard-coded denominator *and* stated the rule that forbids reintroducing one. |
| **AUTH-3** Story 10.9 framed as the abolished body-content follow-up | **CLOSED** | `:182` rewritten: *"**Story 10.9** is the **metadata-only safety guard** (A4 / PD5, 2026-09-15) — it is *not* a body-content follow-up and *not* a forward capability dependency… The earlier "C9-gated body-content follow-up" framing is superseded."* Now consistent with `:226`. |
| **AUTH-4** `unknown_provider_outcome` disposition diverges across three artifacts | **ROUTED** | `:429` names it as open divergence (2), gives both readings (`auto-recovering` here vs `awaiting-human` in the mapping doc and diagram), anchors the code at `FolderStateTransitions.cs:157`, and assigns it to the owning PD11 story *"in the same change set as `DispositionLabelMapper.cs`"*. |
| **AUTH-5** nine-category coverage claim after the 84-row relock | **CLOSED** | See RUB-1. |
| **AUTH-6** NFR74 credited to Epic 13, no story owns it | **CLOSED (as recorded)** | `:283` — a dedicated paragraph stating the fact and reciting the full per-row assignment from `nfr-traceability.md`. |

---

# New findings raised by this run

### **U-C1 (CRITICAL)** — `withheld` named in S-7 exists in no vocabulary. *Detail in §1.2.*

### **U-C2 (CRITICAL)** — CLI exit-code and MCP failure-kind tables not propagated; contradict the as-built oracle. *Detail in §1.2.*

### **U-H1 (HIGH)** — S-7's schema provenance citation covers only 3 of its 12 tokens. *Detail in §1.2.*

### **U-H2 (HIGH)** — A false present-tense code claim survived in the same sentence the remediation edited

`architecture.md:182` and `:148`–`:152` both still assert:

> *"the deployed `Hexalith.Folders.Server` composition does **not** register an EventStore-backed bridge read model: `AddFoldersContextSearchFacade` leaves the fail-safe `UnavailableSemanticIndexingBridgeReadModel` default in place (`FoldersServerServiceCollectionExtensions.cs:84-86`)"*

**Reality at the working tree** — `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:129-134`:

```csharp
// Override after AddFoldersContextSearchQueries (TryAddScoped Unavailable). Mirror Workers: RemoveAll then
services.RemoveAll<ISemanticIndexingBridgeReadModel>();
services.TryAddSingleton<EventStoreSemanticIndexingBridgeStore>();
services.TryAddSingleton<ISemanticIndexingBridgeReadModel>(static sp => sp.GetRequiredService<EventStoreSemanticIndexingBridgeStore>());
```

The `Unavailable` default is gone, the cited lines 84–86 no longer say what the document says they say, and Stories 10.7/10.8 are `done`.

This is the sharpest instance of the failure shape the prior gate named: **the remediation edited the tail of this exact sentence** (correctly fixing the Story 10.9 clause per AUTH-3) and left the false claim in front of it untouched. A reviewer checking AUTH-3 sees a freshly-dated, carefully-reasoned correction and reasonably concludes the sentence was verified end to end.

**Consequence (unchanged from ADV-7).** A Story 12.5 implementer reading `:182` has explicit documentary licence to re-register the `Unavailable` default; Story 6.14 has licence to build deployed-host console journeys asserting `ReadModelUnavailable` as the correct deployed contract. Two units, one endpoint, two incompatible contracts, both citing a declared authority.

**Fix.** Rewrite `:148`–`:152` and the head of `:182` to past tense with the landing evidence, or replace the `file.cs:line` assertion with a pointer plus a conformance test — the prior gate's own structural recommendation, which this pass applied everywhere *except* here.

### **U-H3 (HIGH)** — Two normative statements are still `(state, event)`-keyed, and one of them is the C6 criterion A7b reapproves

- **`:325`** — the C6 exit-criterion definition: *"Total workspace state-transition matrix (every (state, event) pair → outcome, including reconciliation paths and terminal states)"*. This is the **definition of the criterion being reapproved**, so a pair-keyed definition and a triple-keyed matrix are the same class of authority conflict AUTH-1 was.
- **`:1874`** — Implementation Guidance: *"unlisted (state, event) pairs MUST reject with `state_transition_invalid`."* This is the section an implementer reads last and trusts most.

Related, lower-weight: `:1842` still describes C6 as *"~30 transitions"* against the pinned 41; and `docs/diagrams/workspace-lifecycle.md` was newly promoted to co-normative at `:427` but was not opened by this change set.

**Note in mitigation:** the two sites the prior gate cited *by line* (old `:358`, `:1065`) were both fixed, as was the mapping document. The misses are sites the prior report did not enumerate.

### **U-H4 (HIGH)** — `FolderWorkspaceDirtyResolution` cannot carry the four PD11 guards, and `:445` still presents it as the guard discriminator

`:445`: *"implements this matrix as a switch expression over **`(currentState, eventType, resolution)`** … where `resolution` is the `FolderWorkspaceDirtyResolution` discriminator"* — then, in the same bullet, *"PD11 makes **four pairs** guard-discriminated."*

**Reality** — `src/Hexalith.Folders/Aggregates/Folder/FolderWorkspaceDirtyResolution.cs`:

```csharp
public enum FolderWorkspaceDirtyResolution
{
    [JsonStringEnumMemberName("commit_confirmed")] CommitConfirmed,
    [JsonStringEnumMemberName("commit_rejected")]  CommitRejected,
}
```

Two members, both about commit outcome. It cannot discriminate *staged-content presence* (needed by `inaccessible` + `ProviderReadinessValidated` and `dirty` + `LockLeaseBecameStale`) or *originating-task identity* (needed by `dirty` + `WorkspaceLocked`). The bullet reads as though the existing discriminator already carries all four guards, which is REAL-7's original point. The immediately following bullet (`:446`) does the honest work of declaring the missing durable fields — so the fix is small: state that `resolution` covers only the commit-outcome guard and that the other three need new discriminators, sized as part of the owning PD11 story.

### **U-M1 (MEDIUM)** — Rank-30 row `:222` still schedules a `done` story behind four `backlog` ones

*"10.8 follows 12.1–12.3, 12.5, completed 10.7, the Story 11.15 DCP lane…"* while 10.8 is `done` and 12.1/12.2/12.5/11.15 are `backlog`. The `:215` open item covers the *rank-rule* defect but not this specific unsatisfiable edge. Either apply the `:212` terminal-state escape hatch explicitly or fold it into the routed item.

### **U-M2 (MEDIUM)** — S-8 unchanged against SEC-6. *See SEC-6 above.*

### **U-M3 (MEDIUM)** — `:1842` C6 summary says "~30 transitions" against the pinned 41 edges.

### **U-M4 (MEDIUM)** — `Octokit 14.0.0` restated at `:1311`, `:1545`, `:1654` against the new *"versions are owned by `Directory.Packages.props`, never restated here"* rule the same pass introduced. The value is **correct** (props = 14.0.0), so this is a consistency defect, not a currency one — but it is the mechanism by which TECH-1–3 arose in the first place.

### **U-M5 (MEDIUM)** — Two different C6 event pins live in two test suites

`ConsumerDocsConformanceTests` pins the architecture vocabulary at **24** events; `ExitCriteriaDecisionArtifactTests.C6Events` carries **23** and does not include `LockLeaseBecameStale`. The second is a `foreach … ShouldContain` subset assertion, so it passes and will keep passing — which means the PD11 story can add the event to the architecture and the mapping doc without either pin catching an omission on the mapping side. Worth naming in the PD11 lockstep list at `:437`, which already correctly warns that *"the gate will not flag the gap for you"* for the aggregate gate.

### **U-L1 (LOW)** — `Testing.Tests` is 3-red, and it is **pre-existing, not caused by this change set**

Executed this run:

| Suite | Result |
| --- | --- |
| `Hexalith.Folders.Contracts.Tests` | **314 total, 0 errors, 0 failed, 0 skipped** ✅ |
| `Hexalith.Folders.Testing.Tests` | 68 total, **3 failed** |

All three failures are in `ScaffoldContractTests` (`SolutionContainsOnlyCanonicalBuildableProjects`, `ProjectReferencesFollowAllowedDependencyDirection`, `RootBuildConfigurationOwnsTargetFrameworkAndPackageVersions`) and read `.slnx` / `.csproj` files — none of which this change set touched. This is the known `.slnx`-inventory red carried on `main`. **Do not attribute it to this remediation.**

Importantly: **editing `c6-transition-matrix-mapping.md` did not redden any doc gate.** `ExitCriteriaDecisionArtifactTests` validates approval-state cells by substring (`row.Contains(state)`), so the new `"approved; guard key approval-pending under A7b"` cells still satisfy it; the placeholder denylist (`TBD`/`TODO`/…) is not tripped; and the `C6States`/`C6Events` assertions are additive-safe. Verified by execution.

### **U-L2 (LOW)** — The new `validationRemediation:` front-matter key is unpinned by any gate, so it can silently go stale like the `reconciledTo` claim did.

---

# Preservation audit — did the remediation undo anything the prior gate verified as strong?

**No. All seven preserved items reproduce.**

| Prior "what is strong" item | Status | Evidence |
| --- | --- | --- |
| **1.** Honesty convention + *Current reality* table | ✅ **preserved and improved** | Table intact at `:239`–`:243`. The **PD11** row was corrected in the *strict* direction (four → three rejected, with the three named) and the **PD10** row was rewritten to remove the hard-coded denominator and cite the generated inventory. The planning-consistency invariant (`:253`) and *"admission is not implementation evidence"* (`:285`) are untouched. The convention was then **extended** to the 2026-05 strata — `as-built`, `NOT BUILT`, `Target (not built)`, `Corrected anchor (2026-09-16)` — which is the strongest thing about this pass. |
| **2.** Five routed `Open —` items | ✅ **preserved; now nine** | All five survive: `:215` (rank rule), `:277` (unowned corrections — generalised from PD10 to all three), `:441` (staged-content window), `:659` (PD8 boundary), and the C3/A7b routing. Four new: `:642` write concurrency, `:644` schema evolution, `:677` SSRF destination policy, `:679` deny-by-default binding, `:767` deployment profile, `:769` disaster recovery. Every one names a route target. |
| **3.** Byte-exact digests and counts | ✅ **all reproduce** | OQ3 `5ffabd71faea234d884b3c45506fd7c19db562b3353072c396aca206378c52d7` (`:232`) · C12 `5799e090a005addebb8361ba42f36a075ecafd60350837cdd5e29a1d6bad228a` (`:336`) · NFR **84/11** (`:59`, `:1753`) · C6 **11 states** (`:367`) with **41 edges / 24 events** re-verified *by execution* against `ConsumerDocsConformanceTests` (green) · all eleven NFR74–84 owner cells recited at `:275` and `:283`. C7's `30/15/60/60` is not carried as a literal in this document and was not carried at `de281e7` either — the prior gate verified it against the C7 artifact, not against `architecture.md`. |
| **4.** The guard dimension itself | ✅ **preserved and propagated** | `:445`–`:447` intact and now propagated into the mapping document with a full four-row *Guard Discriminators* table. Exactly the *"needs propagating, not rewriting"* disposition the prior gate asked for. |
| **5.** A-9 / D-7 idempotency | ✅ **untouched** | Not in the diff. |
| **6.** `{C3, C4, C7, C12}` hard-pin, NFR row statuses, governance YAML statuses | ✅ **untouched** | `:336` and `:338` byte-identical to `de281e7`, including *"the `{C3, C4, C7, C12}` reference-pending hard-pin is untouched"* and the cascade rule. No governance YAML or `nfr-traceability.md` file was modified by this change set. |
| **7.** Repo currency | ✅ **strengthened** | Version literals replaced by delegation to `Directory.Packages.props`, which removes the doc-vs-repo drift class rather than re-pinning it. |

---

# Recommended disposition

**Before A6b (two edits, both single-sentence / single-table):**

1. **U-C1** — remove `withheld` from S-7's `visibility` closure, or mark it explicitly as a PD8 addition that is not in the vocabulary until PD8 is approved.
2. **U-C2** — reconcile the CLI exit-code table and MCP failure-kind set with `tests/fixtures/parity-contract.yaml` for the three S-7 outcomes (`authentication_failure` → 65, `read_model_unavailable`/`projection_unavailable` → 72), or replace both tables with a pointer to the oracle. Fix **U-H1** in the same edit by naming the real owner per token class.

With those two applied, the Tier-1 item is fully closed and the authority-conformance lens flips to **PASS-WITH-FINDINGS**.

**Before any Epic 4 / Epic 12 lifecycle story:** **U-H3** (`:325`, `:1874`, `:1842`, and the diagram), **U-H4** (`resolution` vs the four guards).

**Before Story 12.5 or 6.14 is picked up:** **U-H2** — the false `Unavailable`-default claim at `:148`–`:152` / `:182` is the one residual that can cause a *regression in shipped code* rather than a documentation defect.

**Mechanical:** U-M1 through U-M5, U-L2.

**Not this run:** everything requiring `epics.md` (the PD8/PD10/PD11 ownership, ADV-6's colliding header spelling) or production code.

---

## Run record

- Target read in full; diff against `de281e7` read in full (137 changed lines).
- Authority artifacts re-derived: `docs/contract/authorization-matrix.md`, `tests/fixtures/parity-contract.schema.json`, `tests/fixtures/parity-contract.yaml` (46 outcome mappings extracted programmatically), `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`, `src/Hexalith.Folders.Contracts/openapi/`, `references/Hexalith.Builds/Props/Directory.Packages.props`, `docs/exit-criteria/c6-transition-matrix-mapping.md`, `.github/workflows/policy-conformance.yml`, `_bmad-output/planning-artifacts/epics.md`.
- Code anchors re-derived: `FoldersServerServiceCollectionExtensions.cs`, `FolderWorkspaceDirtyResolution.cs`, `OctokitGitHubApiClient.cs`, `src/Hexalith.Folders.EventStore/`, absence of `src/Hexalith.Folders/Caching/`, `RateLimiting/`, `tests/tools/oasdiff/`, and `stagedBy*` in `src/`.
- Tests executed: `Hexalith.Folders.Contracts.Tests` (**314/314 green**), `Hexalith.Folders.Testing.Tests` (68, 3 red — pre-existing `ScaffoldContractTests`).
- **No file under `src/`, `tests/`, `docs/`, or `_bmad-output/planning-artifacts/architecture.md` was modified by this run.**
