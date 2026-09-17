# A2b / PD6 Decision Payload

Version: `1.0.0-candidate.1`

Relock the PRD, epics, and NFR traceability inventory at exactly NFR1–NFR84 without renumbering NFR1–NFR73. Append these exact requirements:

- NFR74: bearer credentials are accepted only over HTTPS or an explicitly approved loopback development boundary.
- NFR75: provider endpoints deny private, loopback, link-local, metadata-service, and otherwise prohibited destinations unless an approved deployment policy explicitly allows them.
- NFR76: protected endpoints and internal service boundaries deny by default when authority is absent, stale, malformed, or unavailable.
- NFR77: local CLI and MCP credential material uses owner-only storage and is never emitted to logs, telemetry, diagnostics, or generated artifacts.
- NFR78: repository and workspace content is untrusted input and must not control commands, paths, templates, or rendered active content without validation or neutralization.
- NFR79: accepted mutations, their state transitions, and required evidence survive process restart.
- NFR80: supported multi-replica deployments converge on one authoritative state without seed-local or replica-local correctness assumptions.
- NFR81: readiness reports actual dependency health and cannot report ready from configuration or seed data alone.
- NFR82: every release-significant metric and alert has demonstrated emission, an owner, and fault-path evidence.
- NFR83: release evidence is classified as automated, operational, approval-bound, or reference-pending, with an owner for every non-automated item.
- NFR84: release verification covers edge-security behavior including safe denial, endpoint validation, credential handling, and untrusted-content boundaries.

Admission does not mark any appended requirement implemented. Approval of A2b must bind the exact post-`RELOCK-PLANNING` SHA-256 digests of `prd.md`, `epics.md`, and `docs/exit-criteria/nfr-traceability.md`; this candidate payload alone is not sufficient to accept A2b.

