---
title: 'Complete the next six eligible deferred Folders items'
type: 'bugfix'
created: '2026-11-25'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete six eligible deferred Folders items in the established priority order. Verify and close each item in a separate Conventional Commit. Record skipped items and their reasons. Run the full build and test suite, address failures, and push the verified commits directly to main.

</frozen-after-approval>

## Implementation Notes

Ledger line numbers refer to baseline `924e824edd77c167fbf907b4285ebaee61e70183`. Continue the untagged scan after the previous batch's L3079.

1. L3123 corrects the ProblemDetails titles and messages for Domain lock expiry, Domain provider rate limiting, and Provider safe absence.
2. L3127 validates correlation and task identifiers before echoing them on early read-key rejections.
3. L3131 recognizes fine-grained GitHub and GitLab PAT forms in HTTP diagnostics, provider readiness, and audit.
4. L3135 shares read-envelope checks and the Provider and OpsConsole correlation reader, preserving route-specific precedence and error shapes.
5. L3141 routes the archive callback through the existing authorization result mapper.
6. L3145 uses the canonical stream-segment grammar in both command validators.

### Skipped entries

| Entry | Reason |
| --- | --- |
| L3100 | The current contract requires matching managed-tenant and organization IDs. Positive distinct-ID fixtures require changing that invariant and the durable projection scope. |
| L3103 | The live AppHost returned 202 for a create submitted without a grant after real tenant bootstrap and ACL baseline setup. Story 12.1 requires a safe 404 at this boundary. Completing the split requires reconciling the production admission result. |
| L3106 | Commit `cc0b486d` already forwards the authorized folder and watermark to the durable task-status reader. |
| L3109 | Cleanup eligibility and provider-reference semantics require the lifecycle and provider projection work assigned by the Story 12.2 review. |
| L3113 | HTTP 400 query-result translation belongs to the EventStore SDK. The pinned implementation still omits InvalidPage. |
| L3117 | Commit `2df1253b` already handles both null wire shapes and checks bounded integration responses. |

## Review Triage Log

- Medium, fixed. Provider readiness and HTTP diagnostics now use the same credential pattern. The audit sanitizer also removes matching values before they reach telemetry. Tests cover both new token forms in responses and recorded metadata.
- Medium, fixed. The v2 candidate's early read-key response now omits a sensitive correlation identifier using its existing absent-value marker. The v1 gate still rejects the key before dispatch.
- Medium, fixed. Domain rate-limit and Provider safe-absence messages now agree with their titles. Factory and route cases assert both fields.
- Medium, fixed. Generated read short-circuiting uses the shared presence check. Domain and Audit share canonical header validation and retain each response's existing shape.
- Medium, fixed. A workspace status route test combines a blank key, sensitive correlation, and unsupported freshness. It verifies early rejection with no metadata echo or read-model access.
- Medium, addressed by deferral. The durability split needed stronger pre-replay persistence checks, phase-specific redaction controls, and narrower outage outcomes. L3103 remains open after its live admission prerequisite failed.
- Medium, fixed at commit time. Every item commit regenerates the pending PD10 source manifest from its exact staged paths. Historical approval artifacts retain their original digests.
