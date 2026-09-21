# September A8 Corrective Relock

**Decision ID:** `RELOCK-A8-CORRECTIVE-2026-09-21`  
**Status:** Approved for corrective preparation  
**Approved on:** 2026-09-21  
**Approver:** Jerome  
**Approval source:** Explicit `I approve` response in the active September planning-authority relock exchange after presentation of the complete corrective package.

## Decision

Authorize the smallest governance-safe correction required to prepare new exact-bound A6b and A8 decisions after the failed post-A8 Section 9 result.

The authorization permits:

1. Replacing the pre-A8 global `approval_status: pending` text assertion in `GovernanceCompletenessGateTests` with exact YAML validation of the single A6b register record.
2. Accepting only a coherent A6b `pending` state with no current approvals or a coherent `approved` state with exactly Product, Architecture, and Security approvals bound to the current matrix and conformance artifacts.
3. Regenerating the PD10 v2 conformance set twice after the test correction and adopting it only when both outputs are byte-identical and contain exactly 149 artifacts.
4. Returning A6b to pending while preserving its prior 148-artifact approvals as invalidated history.
5. Regenerating only derived restore assets for explicit source and package dependency profiles, without changing accepted dependency versions or submodule pins.
6. Preparing new exact-bound A6b and corrective A8 approval records for later explicit human decisions.

## Authority boundaries

This decision is not an A6b approval and is not an A8 closure approval. It does not authorize inference or transfer of any prior approval to new bytes.

The following remain prohibited:

- removing the general execution hold or closing planning recovery;
- exposing v2 or starting ordinary implementation stories;
- regenerating `sprint-status.yaml`;
- changing either frozen Story 3.14 specification;
- accepting, updating, rolling back, or fetching external dependencies or submodules;
- changing PRD, epics, architecture, UX, C3 policy, the authorization matrix, or lifecycle intent.

## Required approval sequence

1. Produce the exact corrected test, conformance, candidate-set, and register digests and complete the authorized validations.
2. Obtain explicit Product, Architecture, and Security approval for the corrected A6b package.
3. Rerun the exact conformance, focused-test, and two-profile build validations after recording A6b approval.
4. Prepare and obtain explicit Product, Architecture, and Delivery approval for the corrective A8 package.
5. Run a new immutable Section 9 result. Hold removal remains conditional on every Section 9 check passing.

Until those steps complete, `general_execution_hold` remains `true`, `ordinary_story_execution_authorized` remains `false`, and `v2_exposure_authorized` remains `false`.
