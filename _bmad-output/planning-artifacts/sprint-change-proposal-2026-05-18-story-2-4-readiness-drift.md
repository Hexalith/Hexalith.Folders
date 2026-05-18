---
project_name: Hexalith.Folders
user_name: Jerome
date: 2026-05-18
workflow: bmad-correct-course
status: approved-direct-fix
change_scope: minor
---

# Sprint Change Proposal: Story 2.4 Readiness Drift

## 1. Issue Summary

The BMAD pre-development hardening job is blocked by status-artifact drift for `2-4-grant-and-revoke-folder-access`.

Evidence from `_bmad-output/process-notes/predev-preflight-latest.json` at `2026-05-18T07:07:28Z`:

- Failed check: `status-artifact consistency`
- Details: `1 status-artifact drift(s) found`
- Drift: `{"key":"2-4-grant-and-revoke-folder-access","status":"ready-for-dev","artifact":"absent","expected":"present"}`

Repository history shows the Story 2.4 artifact was intentionally removed by `a3cd62f` because it was created as Story 1.15 scope creep. The later sprint-status entry still marked Story 2.4 as `ready-for-dev`, leaving only half of the intended state.

## 2. Impact Analysis

Epic 2 remains valid and does not need product, PRD, architecture, or UX redesign. The blocker is operational planning metadata, not a change to folder-access requirements.

Affected artifact:

- `_bmad-output/implementation-artifacts/sprint-status.yaml`

No source code, tests, PRD, architecture, or UX specification changes are required.

## 3. Recommended Approach

Use a direct adjustment:

- Move `2-4-grant-and-revoke-folder-access` from `ready-for-dev` back to `backlog`.
- Let the normal `bmad-create-story` predev workflow create the canonical Story 2.4 artifact in a future run.

Rationale:

- A `ready-for-dev` story must have a corresponding artifact.
- The artifact that previously existed was explicitly removed as out-of-scope from Story 1.15.
- Reverting the status to `backlog` preserves the create-story workflow boundary and avoids resurrecting an artifact outside the normal story-creation operation.

## 4. Detailed Change Proposal

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

OLD:

```yaml
2-4-grant-and-revoke-folder-access: ready-for-dev
```

NEW:

```yaml
2-4-grant-and-revoke-folder-access: backlog
```

## 5. Implementation Handoff

Scope: Minor.

Route to: Developer agent for direct metadata correction.

Success criteria:

- Preflight status-artifact consistency passes.
- Story 2.4 is again eligible for the normal predev create-story operation.
- No generated preflight JSON files are committed.
