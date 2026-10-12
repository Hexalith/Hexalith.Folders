# Decision note: adopt EventStore 6.6 and execute Story 12.1 (2026-10-11)

- **Decision maker:** Jerome, under the [project decision policy](../../docs/governance/approval-policy.md).
- **Proposal:** The 2026-10-11 comparison of adapting Folders to the completed EventStore 6.6 contract or adding a new platform URI-identity release. The recommendation was to adapt Folders.
- **Response:** "apply recommended", given 2026-10-11 to that recommendation after the earlier `$bmad-build 12.1` implementation request.
- **Execution decision:** `DEC-EXEC-12.1`, authorized for Story 12.1 only after the compatibility checks below pass.

## Accepted design scope

- Use EventStore 6.6 metadata V1: full CLR `EventTypeName`, optional `PayloadVersion` (absent or 1 means version 1; versions 2–1024 are stamped), separate `MetadataVersion`, and unused `EventContractType`.
- Freeze published Folders event full names as durable storage identifiers. Use EventStore's registered upcasters for supported schema or name changes. Retain historical bytes and exact fixtures for any alias actually found in persisted history.
- Revise D-12, the canonical Story 12.1 acceptance criteria, and its frozen spec to state that contract. Replace the unissued URI-based external acceptance path with a Folders consumer compatibility record bound to the observed EventStore revision and passing focused checks.
- Preserve authorization, transactional append, replay, conflict, idempotency, and deployed-state evidence requirements. This decision changes the event-evolution contract only.

## Execution boundary and checks

- The response accepts the design change and conditionally authorizes Story 12.1 implementation. The earlier Story 1.17 authorization excluded other ordinary stories at that time; this later decision supersedes that exclusion for Story 12.1 only. It does not authorize another story, v2 exposure, deployment, or completion claims.
- The Folders consumer record verifies the exact EventStore source revision and focused payload-evolution, projection, and wire-storage tests. Story 12.1 must additionally test unknown Folders events and unsupported versions on its own replay paths before claiming the revised D-12 behavior.
- Historical A6b/A8 approval records and their bound digests remain untouched. The current user-owned `sprint-status.yaml` comment and submodule pointers are preserved when the Story 12.1 workflow updates its lifecycle key. No deployment or completion is asserted by this decision.

## Material tradeoff

The persisted full CLR name now needs the same change control that a logical URI would have received. EventStore 6.6's unstamped version-1 rule is adopted explicitly. A future URI identity contract would be separate platform work if an interoperability need arises.
