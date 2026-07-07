# Sprint Change Proposal — 2026-07-07

- **Workflow:** BMAD Correct Course (Sprint Change Management)
- **Author:** Jerome (Developer)
- **Triggering issue (as stated):** *Reconcile `.slnx` canonical inventory drift in
  `ScaffoldContractTests.SolutionContainsOnlyCanonicalBuildableProjects` while preserving
  intentional submodule project entries.*
- **HEAD at analysis:** `40cc5e1` (Epic 11 baseline lineage; `.slnx` + test byte-identical to `5ce25e4`)
- **Disposition:** **NO ACTION — already reconciled.** False premise; no code change.
- **Change scope classification:** **N/A (no code).** Documentation-only follow-up: stale memory phrasing corrected (see §2 / close-out).
- **Approval:** **APPROVED — Jerome, 2026-07-07.** Finalized; no code/pin-file changes. Memory reconciliation (item 1) applied; Epic 11 lockstep guardrail (item 2) acknowledged.

---

## Section 1 — Issue Summary

The change request asked to reconcile a canonical `.slnx` inventory *drift* in the scaffold
conformance test `SolutionContainsOnlyCanonicalBuildableProjects`, keeping the deliberately
curated submodule project subset intact.

**Investigation outcome: the drift does not exist.** The premise traces to a *stale project-memory
note* that claimed a "pre-existing `.slnx`-inventory red." That note was **already corrected on
2026-07-07** and the Epic 11 Story 11.1 baseline artifact independently records the test as GREEN.
Acting on the request as written would mean editing a 10/10-green test and a hand-curated `.slnx` to
"fix" a defect that is not present — which would risk *introducing* the very drift it claims to remove.

**How discovered:** direct file-level reconciliation of the test's two assertions against the tree,
plus a live test run, plus cross-check against the Story 11.1 baseline evidence.

---

## Section 2 — Impact Analysis

### The test's two assertions vs. the actual tree (HEAD `40cc5e1`)

| Assertion | Left (source of truth) | Right (test-pinned array) | Result |
|---|---|---|---|
| 1 | `Hexalith.Folders.slnx` `<Project>` entries — **50** | `ExpectedSolutionProjects` — **50** | **identical** (`diff` empty) |
| 2 | disk `Hexalith.Folders*.csproj` in `samples`/`src`/`tests` (excl. `Generation/`, `tests/load/…`) — **29** | `ExpectedRootPolicyProjects` — **29** | **identical** (`diff` empty) |

- `.slnx` breakdown: **50 = 31 Folders-owned + 19 `references/` submodule** projects.
- `.slnx` and `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` are **byte-identical**
  to their state at the Story 11.1 baseline commit `5ce25e4` (`git diff 5ce25e4 HEAD -- <both>` = empty).

### Live confirmation (this session, current working tree)

```
dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj \
  --filter "FullyQualifiedName~ScaffoldContractTests"
=> Passed!  - Failed: 0, Passed: 10, Skipped: 0, Total: 10
```

`SolutionContainsOnlyCanonicalBuildableProjects` is one of those 10.

### "Preserving intentional submodule entries" — structurally guaranteed, no action needed

The Epic 11 EventStore submodule bump (`5ce25e4`) grew `references/Hexalith.EventStore/src` from
8 → **19** projects on disk (new `Admin.*`, `Gateway`, `SignalR`, `Testing*`, `AppHost`,
`RestApi.Generators`). The `.slnx` intentionally pins only the **8** EventStore projects Folders
consumes. Because **assertion 1 reads only the curated `.slnx` manifest — it never enumerates the
submodule disk** — upstream project additions *cannot* redden this test. The curation is preserved
by construction; there is no reconciliation step that would "protect" it, because nothing threatens it.

### Corroborating governance evidence

- **Story 11.1 baseline** (`_bmad-output/implementation-artifacts/11-1-establish-refactor-baseline-and-governance-pin-map.md`):
  - §2: *"`ScaffoldContractTests` `.slnx`-inventory: **GREEN** at HEAD … reconciling the older
    project-memory note of a pre-existing red."*
  - §7 gate #5: `dotnet test … ~ScaffoldContractTests` → **10 passed / 0 failed / 0 skipped**.
  - §9 pin map: *"29 root-policy + 50 solution projects; ScaffoldContractTests 10/10 green."*
- **Auto-memory (2026-07-07 correction):** the former `.slnx`-inventory red is GREEN at `533806b`
  (an ancestor of HEAD); drift reconciled.

### Artifact conflicts

- **PRD / Epics / Architecture / UX:** none. No requirement or design element is affected.
- **Code (`.slnx`, `ScaffoldContractTests.cs`):** none — must remain unchanged (they are the pin).
- **Docs/memory:** one residual stale phrasing exists (memory `story-10-5-design-and-pause.md` still
  says *"ONE pre-existing `.slnx`-inventory red"*). This is already superseded by the 11.1 baseline
  and the 2026-07-07 memory correction. Rewording it was offered and **declined** for this pass
  (disposition = no-op close). Recorded here so it is not lost.

---

## Section 3 — Recommended Approach

**Path chosen: No change (neither Direct Adjustment, Rollback, nor MVP Review applies).**

Rationale: the triggering condition is not reproducible — the artifact it targets is green and the
invariant it worries about (curated submodule subset) is enforced by the manifest's read-only,
hand-curated nature. The correct and safe action is to **close the change request as already
reconciled** and leave the pin files untouched.

- **Effort:** none (verification-only; completed in this session).
- **Risk of acting anyway:** *elevated* — any edit to `.slnx` or the pinned arrays to "reconcile"
  a non-existent drift would break the 10/10 lockstep pin and redden the master inventory gate.
- **Timeline impact:** none. Does **not** change sprint status; Epic 11 sequencing unaffected.

### Forward-looking note for Epic 11 (informational, not a change)

Per the 11.1 governance pin map (§9/§10), the `.slnx` + `ScaffoldContractTests` inventories are the
**master lock**. Stories that add/rename/re-reference projects (e.g. 11.5/11.8 domain refs to
`Hexalith.Commons.*`/`EventStore.Client`, 11.9 `ServiceDefaults` removal) **must** update the pinned
arrays and `.slnx` **in the same commit**, and never rename `Hexalith.Folders.slnx` (repo-root
sentinel for ~40 `RepositoryRoot()` walkers). That lockstep is the real, live guardrail — this
proposal changes none of it.

---

## Section 4 — Detailed Change Proposals

**None.** No Story, PRD, Architecture, or UI/UX edits are proposed. `Hexalith.Folders.slnx` and
`tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` are to remain **exactly as-is**.

(Optional, not applied this pass: reword the single residual stale memory phrase noted in §2 to
"GREEN — reconciled." Deferred by user choice.)

---

## Section 5 — Implementation Handoff

- **Scope:** N/A (no code). Verification-only correct-course.
- **Recipient:** none required. Developer (Jerome) — informational close-out.
- **Success criteria (all met at close):**
  1. `SolutionContainsOnlyCanonicalBuildableProjects` green — ✅ (10/10, live run).
  2. `.slnx` (50) ≡ `ExpectedSolutionProjects` (50) — ✅ (`diff` empty).
  3. disk buildable (29) ≡ `ExpectedRootPolicyProjects` (29) — ✅ (`diff` empty).
  4. Curated 8-of-19 EventStore submodule subset intact and structurally protected — ✅.
  5. No pin files modified — ✅.

**Close-out:** change request resolved as **NO ACTION — already reconciled at `5ce25e4`**; premise
was a stale, already-corrected memory note. Sprint status unchanged.
