# A8 / General Execution Hold Approval Target

Version: `1.0.0-candidate.1`

A8 authorizes removal of the general execution hold only after `RELOCK-SECTION9` produces a complete, passing, exact-digest result for every Section 9 check in `sprint-change-proposal-2026-09-15.md`. The accepted A8 record must bind the regenerated PRD, architecture, UX, epics, version-2 manifest, sprint tracker, approval register, C3/C6/C9 governance, authorization matrix 2.0.0, generated v2 conformance set, NFR traceability evidence, lifecycle reconciliation journal, and Section 9 result digests.

The same final change that sets `general_execution_hold: false` must close the planning-recovery action and record the release timestamp and validation evidence. If any Section 9 check or required role approval is absent, stale, mismatched, or failing, A8 remains pending, the hold remains true, the recovery action remains open, and every ordinary nonterminal node remains held.

This target is deliberately not approvable yet because `RELOCK-SECTION9` has not run and `sprint-status.yaml` has not undergone the separately authorized deterministic lifecycle/provenance reconciliation. The current `bmad-correct-course` and next `bmad-create-epics-and-stories` runs do not perform that tracker change.
