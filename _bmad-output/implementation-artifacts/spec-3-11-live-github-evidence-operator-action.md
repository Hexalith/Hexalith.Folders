---
title: 'Story 3.11: close remaining live GitHub evidence operator action'
type: 'feature'
created: '2026-09-05'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '25800374551dc4f330c27f954d53fde0dbcba403'
context:
  - '_bmad-output/project-context.md'
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/spec-3-11-github-file-mutation-commit-status-and-failure-behavior.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.11 remains `awaiting-operator` on one action: run a credential-gated live GitHub mutation/commit/status evidence suite and archive metadata-only results. Investigation shows that suite does not exist in this repository (Forgejo has the opt-in runner; GitHub does not), so the operator cannot execute the named action today.

**Approach:** Waive/close the live-archive operator action for Story 3.11. Treat hermetic adapter/transport proof plus the 2026-09-05 OQ4 GitHub profile approval as Story 3.11 completion; record live mutation archive as residual full provider-ready debt. Do not build a GitHub live runner and do not invent an “existing suite” pointer.

**Decision (2026-09-06):** CLOSURE PATH = **C** — waive/close without live archive.

## Boundaries & Constraints

**Always:** Keep live evidence opt-in, absent from PR and scheduled CI. Archive only metadata-only scenario/status/evidence-class rows under `_bmad-output/gates/github-provider-evidence/`. Credentials stay in process environment variables, never CLI args, history, reports, docs, or tests. Reuse the Forgejo report schema conventions (`diagnostic_policy: metadata-only`, scenario/status/evidence_class). Update the Story 3.11 operator-action record to match the chosen closure path. Leave `sprint-status.yaml` untouched (orchestrator-owned).

**Never:** Claim an existing GitHub live suite when none exists. Add live GitHub mutation to PR/nightly CI. Persist tokens, hosts, owner/repo/ref labels, URLs, provider bodies, file bytes, diffs, or stack traces. Expand durable workspace/orchestration ownership (Stories 12.3/12.4/4.20/4.21/3.14). Self-approve OQ4 or reinterpret the approved GitHub profile as full provider-ready release acceptance. Mutate a non-disposable GitHub organization or force-update refs.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Missing approval / env | Any required `HEXALITH_GITHUB_EVIDENCE_*` absent or approval ≠ `approved-isolated` | Gate fails closed before network; metadata-only failure report | Reason codes only; no secret echo |
| Hermetic prelude | Runner invoked with approval present | Offline GitHub hermetic classes pass first | Abort before live calls on hermetic failure |
| Live mutate/commit/status | Approved disposable org + least-privilege token | Ordered Git Data staging, one non-force commit/ref confirm, one read-only status observation | Fail closed on unexpected status; never blind-retry ambiguous mutation |
| Denial / isolation | Denied or cross-tenant credential | Known denial/concealment without mutating the primary target | Metadata-only failure/pass rows only |
| Waive path (if chosen) | Human waives live archive | Story 3.11 operator action completed/cleared; catalog/ops note records waiver authority and residual full-provider-ready debt | No live network; no fake green report |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/spec-3-11-github-file-mutation-commit-status-and-failure-behavior.md` -- parent story; under path C, clear live-evidence `operator_actions` and record waiver; do not rewrite adapter tasks.
- `docs/contract/provider-compatibility-catalog.md` -- record Story 3.11 live-archive waiver residual; keep OQ4 approval allowlist intact.
- `docs/operations/provider-integration-and-testing.md` -- record Story 3.11 live-evidence residual / path C waiver; point at Forgejo runner only as a future pattern.
- `_bmad-output/implementation-artifacts/sprint-status.yaml:103` -- may still read `awaiting-operator`; **never write/revert** from this work (orchestrator-owned).
- `tests/tools/run-forgejo-provider-evidence-gates.ps1` -- reference pattern only; path C does not create a GitHub twin.

**Do not change:** Octokit 14.0.0, REST `2022-11-28`, fail-closed operation sources, Forgejo runner, or Story 3.11 adapter code.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/spec-3-11-github-file-mutation-commit-status-and-failure-behavior.md` -- move the live-evidence operator action to completed with an explicit 2026-09-06 waiver; clear open `operator_actions`; update Spec Change Log, Auto Run Result, and Residual Risks; set status to reflect operator closure (do not invent a live archive).
- [x] `docs/contract/provider-compatibility-catalog.md` -- add a short residual note that Story 3.11 live mutation archive is waived; hermetic + approved OQ4 profile complete 3.11; live archive remains full provider-ready debt (do not claim a live run).
- [x] `docs/operations/provider-integration-and-testing.md` -- add a brief GitHub note that live opt-in evidence is not required for Story 3.11 closure and remains residual (point at Forgejo pattern if/when a runner is added later); do not add a fake runner command.
- [x] Always: leave `sprint-status.yaml` untouched; append Implementation Notes with exact files and verification.

**Acceptance Criteria:**
- Given path C, when waiver is recorded, then Story 3.11 has no remaining open `operator_actions` entry for live evidence and residual provider-ready debt is explicit in catalog or story residual risks.
- Given any path, when changes land, then PR/scheduled CI still does not invoke live GitHub mutation, and `sprint-status.yaml` is unmodified by this work.
- Given investigation found no GitHub live suite, when this story completes, then an explicit waiver is recorded — never a false pointer to a missing suite.

## Implementation Notes

- Closure path **C** applied 2026-09-06: waive/close without building a GitHub evidence runner and without inventing a live archive.
- `_bmad-output/implementation-artifacts/spec-3-11-github-file-mutation-commit-status-and-failure-behavior.md`: `status: done`; `operator_actions: []`; live-evidence action moved to `operator_actions_completed` with explicit 2026-09-06 waiver; Spec Change Log, Auto Run Result, and Residual Risks updated.
- `docs/contract/provider-compatibility-catalog.md`: ownership note records Story 3.11 live mutation archive waiver and residual full provider-ready debt (no live-run claim; wording avoids extra OQ4 approval-claim lines outside the guard allowlist).
- `docs/operations/provider-integration-and-testing.md`: added “Story 3.11 live evidence residual” under GitHub integration; points at Forgejo runner pattern for a future opt-in lane; no fake GitHub runner command.
- `_bmad-output/implementation-artifacts/sprint-status.yaml`: not written or reverted.
- No `tests/tools/run-github-provider-evidence-gates.ps1` and no `_bmad-output/gates/github-provider-evidence/` archive created.
- Verification (path C): parent frontmatter `status: done` and `operator_actions: []`; catalog/ops residual notes present; `git diff -- _bmad-output/implementation-artifacts/sprint-status.yaml` empty; catalog OQ4 approval-claim allowlist still has exactly the three permitted lines and no unexpected `OQ4`+`approved`/`accepted` lines.
- Matrix audit (path C): only the “Waive path” row applies; covered by parent/catalog/ops inspection above. Runner-oriented matrix rows are unused under decision C and were not implemented.
- `_bmad-output/implementation-artifacts/epic-3-context.md`: regenerated during build step-01 because planning artifacts were newer than the prior epic context cache; not part of the path C waiver task list.
- Review patches (2026-09-06): removed leftover A/B runner Verification commands and stale Code Map pointers; ops residual now names path C / 2026-09-06 operator waiver and separates full provider-ready debt from Story 3.11 `done`.

## Spec Change Log

- 2026-09-06: Path C waiver recorded; Story 3.11 parent operator action cleared; catalog and ops residual notes added; closure status `done`.
- 2026-09-06: Review patches — Verification and Code Map no longer point at a non-existent GitHub evidence runner; ops residual names path C / 2026-09-06 operator waiver and separates full provider-ready debt from Story 3.11 `done`. KEEP: path C decision, empty parent `operator_actions`, catalog waiver residual, untouched `sprint-status.yaml`.

## Review Triage Log

- `false` — Closure frontmatter `in-review` while notes say done: expected mid-workflow; step-05 closes status. carried: process timing, not a product defect.
- `false` — Parent `done` vs `sprint-status.yaml` still `awaiting-operator`: frozen Always forbids writing sprint-status; orchestrator-owned handoff is intentional, not divergence to patch.
- `false` — Parent `deferred: []` with residual debt only in Residual Risks/catalog/ops: path C required residual documentation there; empty `deferred` is not a false claim of zero debt.
- `false` — Parent Spec Change Log 2026-09-05 still says awaiting-operator: append-only historical entry; 2026-09-06 entry supersedes.
- `false` — Frozen parent Block If still mentions live evidence as operator follow-up: frozen is read-only; waiver is recorded in non-frozen frontmatter/Auto Run/Residual Risks and the closure spec Decision.
- `false` — Blind hunter “fix epic Technical Decisions / UX / Cross-Story” as part of this waiver: those lines come from step-01 `compile-epic-context` regen, not path C tasks; not caused by the waiver docs.
- `medium` / **patch** — Waiver Verification still cited `run-github-provider-evidence-gates.ps1` (false suite pointer under path C). Fixed: path-C-only Verification commands.
- `medium` / **patch** — Waiver Code Map still described open operator_actions and A/B runner adaptation. Fixed: path-C Code Map.
- `medium` / **patch** — Ops residual omitted path C / date / authority. Fixed: ops section now names 2026-09-06 path C operator waiver.
- `low` / **patch** — Implementation Notes omitted epic-3-context regen. Fixed: noted as step-01 side effect.
- `defer` — `epic-3-context.md` regen drops prior rate-limit/idempotency-tier/Contract-Spine/parity detail relative to the previous cache (edge-case + blind hunter). Pre-existing planning-compile quality issue; not path C.
- `defer` — HEAD `ForgejoProvider.cs` has an extra `}` after `HasNoPriorOutcomeFields`, so Release rebuild of Folders tests fails independently of this waiver.

## Design Notes

“Point at an existing suite” is closed: **none exists**. Closest reuse is the Forgejo opt-in runner. Story 3.10 also shipped without a GitHub live suite (hermetic + catalog → `done`). If building, use controlled GitHub REST/Git Data like Forgejo — not the fail-closed production `IGitProvider` operation sources (later stories).

## Verification

**Commands:**
- Path C: inspect Story 3.11 frontmatter and catalog/ops waiver notes -- expected: `operator_actions: []`; completed waiver entry dated 2026-09-06; residual full provider-ready debt stated; no claim that a live run occurred
- `git diff -- _bmad-output/implementation-artifacts/sprint-status.yaml` -- expected: empty
- Confirm no new `tests/tools/run-github-provider-evidence-gates.ps1` and no `_bmad-output/gates/github-provider-evidence/` archive were invented as a false suite pointer
