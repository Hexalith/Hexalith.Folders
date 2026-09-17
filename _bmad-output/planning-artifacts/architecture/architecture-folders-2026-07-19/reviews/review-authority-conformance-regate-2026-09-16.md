# Authority Conformance + Closure Verification — RE-GATE (pass 2), 2026-09-16

**Lens:** Authority conformance / closure verification
**Target:** `_bmad-output/planning-artifacts/architecture.md` (working tree, pass-2 state, `updated: '2026-09-16'`, 1907 lines; +115/−65 against `de281e7`)
**Prior lens report:** `reviews/review-authority-conformance-update-2026-09-16.md` (FAIL — 2 critical / 5 high / 5 medium / 2 low)
**Verifying closure against:** `validation-report-2026-09-16.md` (gate FAIL, 96 findings: 12 critical / 37 high / 34 medium / 13 low)
**Also in the change set:** `docs/exit-criteria/c6-transition-matrix-mapping.md` (co-normative, modified), `docs/adrs/0003-*`, `docs/runbooks/{index,provider-drift}.md`, `.memlog.md`
**Method:** every assertion in the pass-2 text re-derived from the repository. Two suites executed. No file was modified by this run.

---

## Lens verdict — **FAIL** (narrowly; three sentences and one word from PASS-WITH-FINDINGS)

Pass 2 is a real improvement and it did the hardest thing right: **the S-7 row is still byte-exact against the approved matrix, and the APPROVED-vs-PROPOSED split closes the prior `withheld` critical cleanly and honestly.** ADV-1 (triple keying) and ADV-7 (the stale deployed-Server claim) both flipped from open to closed, and the closure claim about the bridge registration — which I was asked to treat as suspect — **verifies exactly as written.**

The FAIL is caused by pass 2 itself. Instructed to state the exit-code derivation as *owed*, the author over-corrected three factual claims in the same paragraph, and the worst of them is load-bearing:

> **S-7 now tells an implementer that the shipped parity oracle has *no row* for `authentication_failure`, `read_model_unavailable`, or `projection_unavailable` — and then, in the same sentence, states the exit codes those rows carry.** The oracle has 49, 37 and 35 rows for them respectively. An implementer executing the PD10 regeneration against this instruction would write rows that already exist, or regenerate over a fixture they were told was empty.

Two further vocabulary sentences in the same paragraph are false and are contradicted by S-7's own text a few clauses earlier. And the one-word antecedent slip at `:148`–`:152` re-opens the exact class of defect the prior gate named: a correct closure statement attached to the *wrong* limitation.

| Severity | Count |
| --- | ---: |
| Critical | **1** |
| High | **5** |
| Medium | **7** |
| Low | **3** |
| **Total** | **16** |

**Of these, 4 are NEW false claims introduced by pass 2** (RG-C1, RG-H1, RG-H2, RG-H3). The remaining 12 are residuals, carried findings, or defects the earlier passes authored that this run found.

---

# HALF 1 — The Tier-1 item

## 1.1 Byte-exact reproduction — S-7 vs the approved OQ3 matrix (**still exact**)

Source of authority: `docs/contract/authorization-matrix.md` §"Canonical Outcomes", lines **143–147**. Target: `architecture.md:657`.

| Outcome | status | category | code | retryable | client action | details visibility |
| --- | :---: | :---: | :---: | :---: | :---: | :---: |
| `authentication-failure-401` | ✅ 401 | ✅ `authentication_failure` | ✅ `authentication_required` | ✅ false | ✅ `check_credentials` | ✅ `redacted` |
| `safe-denial-404` | ✅ 404 | ✅ `tenant_access_denied` | ✅ `resource_unavailable` | ✅ false | ✅ `no_action` | ✅ `redacted` |
| `authority-unavailable-503` | ✅ 503 | ✅ `read_model_unavailable` | ✅ `projection_unavailable` | ✅ true | ✅ `retry` | ✅ `redacted` |

**18 of 18 cells reproduce byte-exact.** The precedence-of-authority clause ("on any disagreement the matrix wins and this row is the defect") survives verbatim, as does the SEC-5 nine-field envelope-identity pin. The four invented tokens the first gate found remain at zero occurrences.

## 1.2 The APPROVED / PROPOSED split — **prior CRITICAL U-C1 is CLOSED**

Pass 2's new clause reads: the six cells per outcome are approved; `visibility` as a **required top-level field** and **`withheld`** are *proposed*, "not yet in the matrix, the spine, or any enum", A6b material, "must not be cited as current contract."

Verified:

| Claim | Evidence | Verdict |
| --- | --- | :---: |
| `visibility` today is a `details` field carrying `redacted` | matrix `:143`–`:147` details-visibility column; `A-8` (`:697`) | ✅ |
| shipped `DetailsVisibility` set is `{redacted, metadata_only}` | `HexalithFoldersClient.g.cs:15167-15174`; 8 `Details*Visibility` enums, all 2-member | ✅ |
| `withheld` is in no enum | 0 hits as a token/enum member/state anywhere in `src/`, `tests/`, `docs/contract/`, or the OpenAPI spine | ✅ |
| PD10's top-level promotion is not in the matrix | matrix has no top-level `visibility` field | ✅ |

**This is the right fix, and it is better than the one I proposed.** Separating the transcribed-and-approved cells from the proposed additions makes the row self-policing: a reader cannot mistake a PD8/PD10 proposal for shipped contract. U-C1 is closed.

## 1.3 Vocabulary ownership — **two new false claims (RG-H1, RG-H2)**

Pass 2 replaced the prior mis-citation with:

> "`tests/fixtures/parity-contract.schema.json` closes exactly two axes — `canonical_error_category` and `mcp_failure_kind` — so of the tokens above only the three *categories* have a home there … The `code`, `clientAction`, and `visibility` axes have no closed vocabulary anywhere."

Reproduced from `tests/fixtures/parity-contract.schema.json`:

```
$defs = adapter_name(5) | canonical_error_category(50) | cli_exit_code(15) | mcp_failure_kind(49) | ownership
```

### **RG-H1 (HIGH, NEW) — "closes exactly two axes" is false; the file closes five, three of them error-surface axes**

`cli_exit_code` is a closed 15-member enum `[0,1,64,65,66,67,68,69,70,71,72,73,74,75,76]` **in that same file**. This is not pedantry: the sentence that follows argues the exit-code derivation has no vocabulary home, three clauses before asserting the derivation is owed. The conclusion is right; the premise is wrong in a way that will mislead whoever implements it.

The *sub*-claim — that of the S-7 tokens only the three categories are members — is **correct**, and that was the substance of the prior U-H1. Fix: say "closes five vocabularies, of which only `canonical_error_category` covers any S-7 token."

### **RG-H2 (HIGH, NEW) — "`code`, `clientAction`, and `visibility` have no closed vocabulary anywhere" is false on all three, and S-7 contradicts itself two sentences earlier**

| Axis | Closed vocabulary that exists | Anchor |
| --- | --- | --- |
| `visibility` | `DetailsVisibility` `{redacted, metadata_only}` (+7 sibling enums) | `HexalithFoldersClient.g.cs:15167` |
| `clientAction` | `ProblemDetailsClientAction` `{retry, revise_request, check_credentials, wait_for_reconciliation, …}` + 7 sibling `*ClientAction` enums; spine enums at `hexalith.folders.v1.yaml:7675-7679`, `:7734-7737`, `:7774` | 8 enums |
| `code` | `FileSafeResourceUnavailableProblemCode`, `FileRangeUnsatisfiableProblemCode`, `FilePolicyUnavailableProblemCode`, +4 more | `:13903` ff. |

S-7 itself names `DetailsVisibility` as "the shipped … set" two sentences before declaring that `visibility` has no closed vocabulary anywhere. That is a self-contradiction inside one table cell, on the Tier-1 line.

**This is an over-correction of prior U-H1.** U-H1 said the *citation* was wrong and named the real owners per token class. Pass 2 deleted the owners instead of naming them. What is actually true, and is what the row should say: these three axes are closed **per response schema** in the spine and the generated client, but there is **no single global vocabulary** for them the way `canonical_error_category` is global — so PD10 must decide whether a global closure is created or the per-schema closures are amended in lockstep.

## 1.4 The exit-code / failure-kind derivation — **one CRITICAL**

### **RG-C1 (CRITICAL, NEW) — S-7 asserts the parity oracle has no rows for three categories; it has 121, and the same sentence quotes their values**

**Anchor:** `architecture.md:657`.

> "Today the oracle and this document's own canonical table have **no row** for `authentication_failure`, `read_model_unavailable`, or `projection_unavailable`; the shipped `parity-contract.yaml` maps them to exit **65** and **72** …"

**Reproduced programmatically from `tests/fixtures/parity-contract.yaml` (171 KB):**

| Canonical category | `cli_exit_code` | `mcp_failure_kind` | outcome-mapping rows |
| --- | ---: | --- | ---: |
| `authentication_failure` | 65 | `authentication_failure` | **49** |
| `read_model_unavailable` | 72 | `read_model_unavailable` | **37** |
| `projection_unavailable` | 72 | `projection_unavailable` | **35** |

The clause is false for the oracle and **self-refuting**: the oracle *is* `parity-contract.yaml`, and the same sentence states the values those rows carry. The half about *this document's own canonical table* is **true** and verified (`:719`–`:735` has no row for any of the three; the MCP `kind` set at `:737` enumerates 14 kinds and omits all three).

**Three consequences, in ascending order of harm.**

1. The derived conclusion — *"Until those rows exist, CLI and MCP **can** disagree about an authority outage"* — is false. The oracle's `cli_exit_code` and `mcp_failure_kind` columns **agree** for all three categories. The real disagreement is between *this document's table* and the oracle, which is a different defect with a different fix.
2. An implementer performing the PD10 regeneration is instructed to create rows that already exist. Best case they discover the contradiction and stop; worst case they regenerate over a fixture they were told was empty and silently drop the 121 existing rows.
3. This is the **exact shape the prior gate named as dominant** — a remediation that lands a correct headline and leaves a contradiction standing — except that here the contradiction is inside the same sentence as the headline.

**Fix (one deletion).** Strike "the oracle and" from that clause. The rest of the sentence is correct and useful as written.

### **RG-H4 (HIGH) — prior U-C2, downgraded: the two derivation tables are still unpropagated and still carry a false provenance header**

`:719`–`:735` (CLI exit-code table) and `:737` (MCP failure-kind set) are **byte-identical to pass 1**. Pass 2 documented the gap in S-7 rather than fixing the tables — which is a legitimate disposition and materially reduces the A6b risk, so this drops from CRITICAL to HIGH. What survives:

- The header **"CLI exit-code mapping (canonical, asserted by C13 oracle)"** is a false provenance claim. No test asserts this table against `parity-contract.yaml`; I looked.
- `:722` maps exit **65 → `credential_missing`**. The oracle maps 65 → `{authentication_failure, credential_reference_invalid}`. `credential_missing` appears in the oracle only as a `pre_sdk_error_class`, never as a post-SDK `canonical_error_category`.
- `:731` maps exit **72 → `reconciliation_required`** only. The oracle maps 72 → 8 categories (`dirty_workspace`, `file_policy_unavailable`, `projection_stale`, `projection_unavailable`, `read_model_unavailable`, `reconciliation_required`, `workspace_not_ready`, `workspace_preparation_failed`).
- The table is one-category-per-code throughout; the oracle is many-to-one.

**Correctly verified in the same paragraph, to pass 2's credit:**

| Pass-2 claim | Verdict |
| --- | :---: |
| "The matrix carries no `cli_exit_code` or `mcp_failure_kind` columns" | ✅ 0 hits |
| "this document defines 72 as `reconciliation_required`, *not retryable until cleared*" | ✅ byte-exact at `:731` |
| "the shipped `parity-contract.yaml` maps them to exit 65 and 72" | ✅ |
| "must retire `not_found` → exit 73, which `parity-contract.yaml` still carries" | ✅ 22 `not_found` rows at exit 73 |
| "alongside `auth_outcome_class` values (`folder_acl_denied`, `audit_access_denied`)" | ✅ `folder_acl_denied` ×35, `audit_access_denied` ×4, `tenant_access_denied` ×10 |
| "those enums still enumerate the three codes PD10 removes" | ✅ `not_found`, `cross_tenant_access_denied`, `audit_access_denied` are in both `canonical_error_category` and `mcp_failure_kind` |
| `auth_outcome_class` "reproduces the existence oracle one surface below the HTTP envelope" | ✅ sound |

## 1.5 S-4 evaluation order — **correct**

`:654`: "**authentication** is evaluated before every other conjunct, then authority-unavailability, then authority, and only then any protected-resource lookup."

Matches the matrix's declared conjunct order byte-for-byte in sequence:

```
authentication -> authority_evidence_availability -> tenant_access ->
principal_and_delegation_intersection -> folder_acl_allow -> family_grant ->
resource_scope_binding -> freshness_revalidation -> observation
```

SEC-4 stays closed. The rationale sentence ("an unauthenticated caller must receive the 401 envelope and must never be routed to the retryable authority-outage 503") is the right one.

## 1.6 The Deployed-Server closure claim — **the code half verifies; the antecedent does not**

I was asked to treat this claim as suspect. **The substantive claim is accurate.**

| Assertion | Evidence | Verdict |
| --- | --- | :---: |
| Stories 10.7 and 10.8 are `done` | `sprint-status.yaml:203-204` — `10-7-…: done`, `10-8-…: done` | ✅ |
| calls `AddEventStoreReadModelStore()` | `FoldersServerServiceCollectionExtensions.cs:131` | ✅ |
| removes the `Unavailable` registration | `:132` `services.RemoveAll<ISemanticIndexingBridgeReadModel>()` | ✅ |
| binds `EventStoreSemanticIndexingBridgeStore` | `:133-134` | ✅ |
| "read-only on the Server — no `ISemanticIndexingBridgeWriter` is registered there" | 0 registrations; the only hit in the project is the comment at `:130` saying exactly that | ✅ |
| the stale `…cs:84-86` anchor is gone | 0 hits | ✅ |

**Prior U-H2 / ADV-7 is CLOSED.** This was the residual that could have caused a regression in shipped code, and it is properly retired.

### **RG-H3 (HIGH, NEW) — at `:148`–`:152` the closure is attached to the wrong limitation, and the document contradicts itself 32 lines later**

```
… Release readiness still requires live DCP-capable AppHost evidence for topology boot,
index auto-provisioning, SearchIndexEntryChanged/SearchIndexEntryRemoved publication,
archive filtering, and query facade hydration. **That limitation is now closed (verified 2026-09-16).**
```

The antecedent of *"That limitation"* is the sentence immediately before it — the **live DCP-capable AppHost evidence** requirement. That is not closed:

- `:184`, unchanged in this same document: *"**Live verification** of the full `folders-index` round-trip inherits the Epic 9 `aspire run` DCP boot blocker."*
- `sprint-status.yaml:223` — `11-15-maintain-the-dcp-capable-cross-repository-verification-lane: **backlog**`.

The evidence offered (10.7/10.8 `done` + the registration) closes only the **bridge-registration** half — which is precisely how the Query Facade entry at `:186` scopes it, correctly and in its own heading: *"Deployed-Server **bridge registration** — limitation CLOSED"*. The bullet at `:148` inherited the closure sentence without inheriting the scope.

**Consequence.** `:148`–`:152` sits in the input-provenance section a reader hits before anything else. Read literally, it certifies that live DCP evidence is no longer a release-readiness prerequisite — retiring, on a two-story citation, an evidence obligation that four other places in the repo still carry.

**Fix (one clause).** "That **bridge-registration** limitation is now closed; the live DCP-capable AppHost evidence requirement above stands (Story 11.15, `backlog`)."

---

# HALF 2 — Closure audit over the original 12 criticals + 37 highs

## Tally

| | Critical (12) | High (37) | **Total (49)** |
| --- | ---: | ---: | ---: |
| CLOSED | **9** *(was 8)* | **22** *(was 20)* | **31** *(was 28)* |
| PARTIAL | **0** *(was 1)* | **5** | **5** *(was 6)* |
| ROUTED | 3 | 9 | **12** |
| NOT CLOSED | 0 | **1** *(was 3)* | **1** *(was 3)* |

**Net movement this pass: +3 closed, and the last two NOT-CLOSED highs both improved.** No item regressed.

## Items that moved

| ID | Pass 1 | Pass 2 | Verification |
| --- | --- | --- | --- |
| **ADV-1** `(state, event, guard)` keying | PARTIAL | **CLOSED** | Both remaining sites fixed: `:331` — the **C6 exit-criterion definition itself** now reads "every `(state, event, guard)` triple → outcome"; `:1879` Implementation Guidance now reads "unlisted `(state, event, guard)` triples MUST reject … **including an unenumerated branch of a guard-discriminated pair, which must never fall through to its sibling's outcome**". The **C6 measurement method** (`:353`) also moved: "enumerated state × event × guard matrix … with a separate assertion per branch of a guard-discriminated pair". The CI-gate bullet (`:1102`) and the PR-review rule both re-keyed. Residual is a *count*, not a keying — see RG-M5. |
| **ADV-7** false present-tense deployed-behaviour anchors | NOT CLOSED | **CLOSED** | §1.6 above. All three anchors now past-tense with landing evidence. |
| **SEC-6** S-7/S-8 jointly unsatisfiable for task-derived scope | PARTIAL | **CLOSED** (doc half) | S-8 (`:658`) gained: *"**and this is in unresolved tension with S-7's 'before any protected-resource lookup' ordering**, because the bound folder whose authority gates the call is discovered *from* the caller-named task, which is itself the protected resource; the correction must say whether task-derived scope is resolved by an authority-only lookup that leaks nothing, or whether these operations take an explicit folder scope."* That is the finding's full substance, recorded as an owned open item. |
| **RUB-5** S-7 envelopes absent where the C13 columns are derived | NOT CLOSED | **PARTIAL** | The gap is now named in S-7 with the correct target values (65, 72) and the exit-72 retryability contradiction called out. Tables still unfixed (RG-H4) and the naming carries RG-C1's false premise. |
| **AUTH-1** S-7 vocabulary | CLOSED *(residuals)* | **CLOSED** *(new residuals)* | 18/18 byte-exact; U-C1 `withheld` closed by the APPROVED/PROPOSED split. New residuals RG-C1 / RG-H1 / RG-H2. |
| **AUTH-6** NFR74 ownership | CLOSED *(as recorded)* | **CLOSED** *(verified byte-exact)* | The false "no Epic 13 story owns NFR74" framing is gone. The recitation at `:276` reproduces `nfr-traceability.md:120-130` exactly: 74→`13-2`, 75→`13-1`, 76→`13-2`, 77→`13-3`, 78→`13-6`, 79→`12-1`, 80→`12-2`, 81→`13-4`, 82→`13-5`, 83→`7-16`, 84→`13-6`; all eleven `reference-pending`. |

## Items unchanged from pass 1

- **ADV-6** (D-9 `X-Hexalith-Retry-Transport` / `X-Hexalith-Retry-As` collision, `:914`–`:915`) — **the sole remaining NOT CLOSED high.** Untouched by this pass. Its epics half stays out of scope.
- **PARTIAL (5):** ADV-4 and SEC-3 (derivability half = RG-H4), REAL-7 (= RG-H5), RUB-4 (no gate compares the mapping document), RUB-5.
- **ROUTED (12):** all twelve reproduce, and the routed set grew again this pass. Every one names a route target and an owner.

## Spot-checks of pass-2 assertions outside the Tier-1 row (all verified true)

| Claim | Anchor | Verdict |
| --- | --- | :---: |
| PD11 reality: `(changes_staged, CommitFailed)` maps unconditionally to `failed`; `(inaccessible, ProviderReadinessValidated)` unconditionally to `ready`; the other three reject | `FolderStateTransitions.cs:90-91`, `:106-107`; no `changes_staged`→`inaccessible`, `dirty`→`changes_staged`, or `dirty`→`ready` rows | ✅ |
| `FolderStateTransitions.cs:157` "still maps `Dirty` to `AwaitingHuman` unconditionally with a test pinning it" | line 157 is exactly `Dirty => AwaitingHuman`; pin at `FolderStateTransitionsTests.cs:193` | ✅ byte-exact — **and a correction of a pass-1 mis-anchor** |
| 12.4 corrected anchor: `FoldersServerServiceCollectionExtensions.cs:67` registers `UnavailableWorkspaceCommitExecutor` | line 67 is `TryAddScoped<IWorkspaceCommitExecutor, UnavailableWorkspaceCommitExecutor>()` | ✅ byte-exact |
| single `NotImplementedException` in `src/`, at `OctokitGitHubApiClient.cs:60` | 1 hit, that file, that line | ✅ |
| `Hexalith.Folders.EventStore` hosts "13 today" intent adapters | 13 `*IdempotencyIntentAdapter.cs` files | ✅ |
| slnx is "14 src / 17 test at 2026-09-16" | 14 / 17 | ✅ |
| "12 of 22 concerns carry a structure-mapping row" | 12 rows | ✅ |
| `LibGit2Sharp` in the technology set | `Directory.Packages.props:15`, referenced by `Hexalith.Folders.csproj:17` | ✅ |
| `policy-conformance.yml` is "`schedule:` + `workflow_dispatch:` only" | `'on': schedule(cron '43 2 * * *') + workflow_dispatch` | ✅ |
| C10 as-built: `GovernanceCompletenessGateTests` over `tests/fixtures/cache-key-exceptions.yaml`, recorded in the governance YAML | class at `Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs:11`; `c0-c13-governance-evidence.yaml:192` `artifact_path: tests/fixtures/cache-key-exceptions.yaml` | ✅ |
| C12 as-built artifacts | `tests/contracts/forgejo/supported-versions.json`, `tests/tools/run-nightly-drift-gates.ps1`, `tests/tools/forgejo-drift/` all present | ✅ |
| `Testcontainers` has zero usage in Folders | 0 hits in `src/`, `tests/` | ✅ |
| `epics.md` has zero PD8/PD10/PD11 references, "verified again 2026-09-16" | 0 | ✅ |
| concern #20 is the shipped behaviour via `AuthorizeDiagnosticReadAsync(allowBoundedStale: true)` | `TenantAccessAuthorizer.cs:18`, `:82`, `:87` | ✅ |
| the matrix "routes stale two different ways — `:76` → `safe-denial-404`, `:146` → `authority-unavailable-503`" | both lines reproduce exactly | ✅ |
| "The table keeps its three-column shape because the C6 edge-count gate is pinned to that header" | `ConsumerDocsConformanceTests.ParseArchitectureC6Transitions` requires the literal `\| From → To \| Triggering Event \| Side Effect \|` header and terminates at `**Implementation enforcement:**` | ✅ |
| the four guard pairs appear as two rows each with the guard bolded inline | all four present and bolded in the matrix | ✅ |

---

# New findings raised by this run

### **RG-C1 (CRITICAL, NEW)** — S-7 claims the parity oracle has no rows for three categories; it has 121. *Detail in §1.4.*

### **RG-H1 (HIGH, NEW)** — "`parity-contract.schema.json` closes exactly two axes" is false; it closes five. *Detail in §1.3.*

### **RG-H2 (HIGH, NEW)** — "`code`, `clientAction`, `visibility` have no closed vocabulary anywhere" is false ×3 and self-contradicted. *Detail in §1.3.*

### **RG-H3 (HIGH, NEW)** — `:148`–`:152` attaches the closure to the DCP live-evidence limitation, contradicting `:184` and Story 11.15 (`backlog`). *Detail in §1.6.*

### **RG-H4 (HIGH)** — the CLI exit-code / MCP failure-kind tables are unpropagated and carry a false "asserted by C13 oracle" provenance. *Detail in §1.4.*

### **RG-H5 (HIGH)** — `FolderWorkspaceDirtyResolution` still presented as the four-guard discriminator (prior U-H4, unchanged)

`:446`: *"a switch expression over `(currentState, eventType, resolution)` … where `resolution` is the `FolderWorkspaceDirtyResolution` discriminator"* — then, in the same bullet, *"PD11 makes **four pairs** guard-discriminated."*

`FolderWorkspaceDirtyResolution` has exactly two members, `CommitConfirmed` and `CommitRejected`, both about commit outcome. It cannot discriminate staged-content presence (needed by `inaccessible` + `ProviderReadinessValidated` and `dirty` + `LockLeaseBecameStale`) or originating-task identity (needed by `dirty` + `WorkspaceLocked`). The very next bullet does the honest work of declaring the missing durable fields, so the fix stays small: say that `resolution` covers the commit-outcome guard only and that the other three need new discriminators, sized into the owning PD11 story.

### **RG-M1 (MEDIUM)** — the corrected concern-#1 row asserts a path that does not exist, inside the sentence that certifies which paths do not exist

`:1634`: *"`Authorization/`, `Server/Middleware/TenantContextProvenanceMiddleware.cs`; cache-key prefix enforced as-built by `GovernanceCompletenessGateTests` … (**not** by `Caching/TenantPrefixedCacheKey.cs` or a `ci.yml` lint job — **neither exists**)"*

`src/Hexalith.Folders.Server/Middleware/` does not exist, and `TenantContextProvenanceMiddleware.cs` has **zero occurrences anywhere in the repository**. The row was rewritten by the remediation to strip two non-existent paths and kept a third. Unlike the requirements-to-structure table, this row is **not** covered by a target-layout banner — it was corrected outright, which is what makes it read as verified. (Same path also at `:1614`, which *is* under the banner.) REAL-4's own substance (C10) stays CLOSED; this is collateral.

### **RG-M2 (MEDIUM)** — `:737` "the full `CanonicalErrorCategory` enum (43 post-SDK members)"

The generated `CanonicalErrorCategory` has **49** members; `$defs/canonical_error_category` in the schema has **50**. Neither is 43. The sentence's advice ("Assert against the oracle file, not this prose") is right; the number beside it is not, and it sits in the same paragraph RG-H4 covers.

### **RG-M3 (MEDIUM)** — the document's front matter now certifies its own gate outcome, using the pass-1 tally

`:41` `validationRemediation:` asserts *"Re-gated with 6 lenses: **28 of 49** prior criticals+highs closed, **0 criticals un-disposed**, **A6b blocker closed**, S-7 byte-exact to the approved matrix."*

Two problems. First, "28 of 49" is *my pass-1 number*, carried forward as if it were the post-pass-2 result (the correct figure is 31). Second and worse, a reviewed artifact declaring its own gate verdict — "A6b blocker closed" — is an authority inversion of exactly the kind S-7's own precedence clause forbids: the gate reports are the authority, and this key restates their conclusion independently. It is also unpinned by any test, so it will go stale silently (prior U-L2, now materialised). Fix: keep the key as a pointer to the reports; delete the verdict.

### **RG-M4 (MEDIUM)** — the document now says Story 10.8 is `done` and schedules it behind four `backlog` stories

`:151` (new this pass): "Stories 10.7 and 10.8 are `done`". `:223` (rank-30 row, unchanged): *"10.8 follows 12.1–12.3, 12.5, completed 10.7, the Story 11.15 DCP lane…"* — where `12-1`, `12-2`, `12-3`, `12-5` and `11-15` are all `backlog`. Prior U-M1 flagged the unsatisfiable edge; pass 2 made it an explicit **internal** contradiction by asserting the done-ness in the same document. Either apply the `:212` terminal-state escape hatch to this row or fold it into the `:215` routed rank-rule item.

### **RG-M5 (MEDIUM)** — `:1847` still says C6 is "~30 transitions" against the 41-edge pin (prior U-M3, unchanged)

`ConsumerDocsConformanceTests.cs:557` asserts exactly 41 and is green. This is the one keying/count site ADV-1's propagation missed.

### **RG-M6 (MEDIUM)** — `Octokit 14.0.0` restated at four prose sites against the delegation rule this same remediation introduced

`:605`, `:695`, `:1316`, `:1659` (`:1550` is a legitimate directory path). The value is **correct** (`Directory.Packages.props` = 14.0.0), so this is a consistency defect, not a currency one — but restating a version in prose is precisely the mechanism that produced TECH-1 through TECH-3.

### **RG-M7 (MEDIUM)** — two different C6 event pins live in two suites (prior U-M5, unchanged)

`ConsumerDocsConformanceTests` pins the architecture vocabulary at **24** events (including `LockLeaseBecameStale`, which the matrix does carry at `:414`); `ExitCriteriaDecisionArtifactTests.C6Events` carries **23** and omits it. The second is a `foreach … ShouldContain` subset assertion, so it passes and will keep passing — meaning the PD11 story can add the event to the architecture without either pin catching an omission on the mapping side. Worth naming in the `:437` lockstep list, which already warns correctly that *"the gate will not flag the gap for you."*

### **RG-L1 (LOW)** — `:684` hard-codes "The 49 protected operations" against the document's own thrice-stated rule

The value is right (`ConsumerDocsConformanceTests` pins the spine at 49 operations), but `:243`, `:737` and S-7 all now forbid transcribing the denominator, and this paragraph was authored by the remediation. Replace with "every protected operation in the generated inventory."

### **RG-L2 (LOW)** — the C9 measurement-method cell has nested `**…**` and will render broken

`:349`: `**event-write token-substitution tests asserting no durable cleartext in the **Dapr state store** (explicitly in scope …) or in any event, projection, …**`. The inner emphasis terminates the outer one mid-sentence.

### **RG-L3 (LOW)** — the `withheld` / `redacted` collision S-6 declares absent exists at the string layer

S-6 (`:656`): *"`withheld` … does not collide with the shipped meaning of `redacted` in `docs/.../safety-invariant-ci-gates.md` and `ConsoleStatusText.cs`."* But `ConsoleStatusText.cs:45` is literally `["redacted"] = "The requested evidence is **withheld** by tenant policy."` — the English word is already the user-visible gloss for `redacted`. Introducing a distinct `withheld` state gives one word two meanings on the same console. Relatedly, S-7's *"`withheld` has zero occurrences anywhere"* is true for tokens, enums and states (verified) but false for the literal string, which appears 8 times in prose and comments across `src/`, `tests/` and `docs/`. Tighten to "zero occurrences as a token, enum member, or state."

---

# Preservation audit — did pass 2 undo anything?

**No. Every "do not undo" item reproduces.**

| Item | Status | Evidence |
| --- | :---: | --- |
| OQ3 digest `5ffabd71faea234d884b3c45506fd7c19db562b3353072c396aca206378c52d7` | ✅ | `:233`, exactly one occurrence |
| C12 digest `5799e090a005addebb8361ba42f36a075ecafd60350837cdd5e29a1d6bad228a` | ✅ | `:337`, exactly one occurrence, with catalog version `1.0.0` intact |
| NFR **84 rows / 11 categories** | ✅ | `:59` ("Eleven NFR categories … NFR74–NFR84"), `:286` ("relocks at exactly 84 rows … the existing 73-row inventory is appended to, never renumbered") |
| C6 **41 edges / 24 events / 11 states** | ✅ **re-verified by execution** | `ConsumerDocsConformanceTests` green; matrix header and `**Implementation enforcement:**` terminator both intact so the parser still finds the table |
| `{C3, C4, C7, C12}` reference-pending hard-pin + the cascade rule | ✅ | `:339` byte-identical to `de281e7`; `nfr-traceability.md` and `c0-c13-governance-evidence.yaml` **not modified** by this change set (`git status` clean on both) |
| S-7 18/18 matrix cells | ✅ | §1.1 |
| Honesty convention + *Current reality* table | ✅ extended | `AS-BUILT` / `NOT BUILT` / `Target (not built)` / `Corrected anchor (2026-09-16)` markers all survive and multiplied |
| The nine+ routed `Open —` items | ✅ | all present, each naming a route target and (new this pass) a rank |
| A-9 / D-7 idempotency | ✅ | not in the diff |

## Gate health

| Suite | Result |
| --- | --- |
| `Hexalith.Folders.Contracts.Tests` | **314 total, 0 errors, 0 failed, 0 skipped** ✅ |
| `Hexalith.Folders.Testing.Tests` | 68 total, **3 failed** — `ScaffoldContractTests.{SolutionContainsOnlyCanonicalBuildableProjects, ProjectReferencesFollowAllowedDependencyDirection, RootBuildConfigurationOwnsTargetFrameworkAndPackageVersions}` |

**The three reds are pre-existing and are not caused by this change set.** They read `.slnx` / `.csproj` files, none of which this change set touched; the drift is `Hexalith.Folders.EventStore` appearing in the AppHost reference set. This is the known `.slnx`-inventory red carried on `main`, and the document itself now records it honestly at `:1841` ("`ScaffoldContractTests` is currently red on its two newest entries, a pre-existing drift unrelated to this document") — that statement is accurate about the *entries*, though three tests fail, not two.

**No doc gate reddened.** Pass 2 rewrote the C6 exit-criterion row, the C6 measurement method, the C10 and C12 artifact rows, the matrix intro and the transitions header — every one of which is parsed or substring-asserted by a green suite. That is a clean lockstep.

---

# Recommended disposition

**Before A6b — four edits, none longer than a sentence:**

1. **RG-C1** — delete "the oracle and" from S-7's no-row clause. (CRITICAL; one phrase.)
2. **RG-H1 / RG-H2** — restate vocabulary ownership as: the schema closes five vocabularies of which only `canonical_error_category` covers an S-7 token; `code`, `clientAction` and `visibility` are closed **per response schema** in the spine and generated client (`FileSafeResourceUnavailableProblemCode`, `ProblemDetailsClientAction`, `DetailsVisibility`) but have no single global closure, and creating one is part of this correction.
3. **RG-H3** — scope `:148`'s closure to the bridge registration and restate that the DCP live-evidence obligation stands (Story 11.15, `backlog`).
4. **RG-M3** — strip the gate verdict from `validationRemediation:`; keep the pointer.

With those applied the Tier-1 item is fully closed and this lens flips to **PASS-WITH-FINDINGS**.

**Before any PD10 regeneration work:** **RG-H4** — either reproduce the four S-7-relevant rows from `parity-contract.yaml` into the CLI/MCP tables, or delete both tables and point at the oracle (the `Directory.Packages.props` delegation pattern this same remediation adopted for versions). Fix **RG-M2** in the same edit.

**Before any Epic 4 / Epic 12 lifecycle story:** **RG-H5** (`resolution` vs the four guards), **RG-M5** (`~30` vs 41), **RG-M7** (the 23/24 event-pin split).

**Mechanical:** RG-M1, RG-M4, RG-M6, RG-L1, RG-L2, RG-L3.

**Not this run:** everything requiring `epics.md` (PD8/PD10/PD11 ownership; ADV-6's colliding header spelling) or production code.

---

## Run record

- Target read in full; full diff against `de281e7` (535 lines) read in full; pass-1 state reconstructed from the prior lens report's line-anchored quotations.
- Authority artifacts re-derived: `docs/contract/authorization-matrix.md` (§Canonical Outcomes `:143-147`, negative-access-state table `:70-80`, conjunct order), `tests/fixtures/parity-contract.schema.json` (all five `$defs` enumerated), `tests/fixtures/parity-contract.yaml` (610 outcome-mapping rows parsed programmatically; per-category exit-code and failure-kind distributions computed), `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`, `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, `docs/exit-criteria/nfr-traceability.md`, `docs/exit-criteria/c0-c13-governance-evidence.yaml`, `_bmad-output/implementation-artifacts/sprint-status.yaml`, `_bmad-output/planning-artifacts/epics.md`, `Hexalith.Folders.slnx`, `Directory.Packages.props`, `.github/workflows/policy-conformance.yml`.
- Code anchors re-derived: `FoldersServerServiceCollectionExtensions.cs` (`:67`, `:129-134`), `FolderStateTransitions.cs` (full switch + `:157`), `FolderStateTransitionsTests.cs:193`, `FolderWorkspaceDirtyResolution.cs`, `OctokitGitHubApiClient.cs:60`, `ConsoleStatusText.cs:45`, `TenantAccessAuthorizer.cs`, `ConsumerDocsConformanceTests.cs` (C6 parser, 41/24 pins, 49-operation spine pin), `ExitCriteriaDecisionArtifactTests.cs` (C6States/C6Events), `GovernanceCompletenessGateTests.cs`, `src/Hexalith.Folders.EventStore/`, and the verified **absence** of `src/Hexalith.Folders.Server/Middleware/`, `TenantContextProvenanceMiddleware.cs`, `RateLimiting/`, `Caching/TenantPrefixedCacheKey.cs`, `tests/tools/oasdiff/`, `stagedBy*`, `LockLeaseBecameStale` in `src/`, and `Testcontainers`.
- Tests executed: `Hexalith.Folders.Contracts.Tests` (**314/314 green**), `Hexalith.Folders.Testing.Tests` (68, 3 red — pre-existing `ScaffoldContractTests` `.slnx` drift).
- **No file under `src/`, `tests/`, `docs/`, or `_bmad-output/planning-artifacts/architecture.md` was modified by this run.**
