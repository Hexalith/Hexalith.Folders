# Adversarial Review — Hardened PRD Snapshot

Verdict: **Ready for downstream architecture/story work with explicit OQ dependencies; not ready for release acceptance.**

Search-scope shorthand: **Resolved.** Cross-workspace/indexed body snippets and recall are deferred; bounded direct live-workspace search and snippets remain required by FR34–FR35.

- `reconciliation_required` still has no maximum residence time, accountable human responder, response SLO, or terminal escalation outcome after the bounded automatic phase. Because these workspaces are excluded from cleanup, sensitive temporary content can persist indefinitely.

- Effective-permission inspection omits grant provenance, expanded group/role/delegation sources, evaluation timestamp, authority checkpoint, and explicit stale/incomplete result behavior. UJ9’s “who can write here today?” answer remains unverifiable during membership churn or partial identity-service failure.

- The stable tenant-scoped C9 correlation token preserves confidential incident linkage, but token construction, collision bounds, tenant-domain separation, key rotation, deletion behavior, and stability across the seven-year audit window remain unspecified. A reversible or globally correlatable implementation can still satisfy the wording.

- C9 has no canonical fixture path or exhaustive permitted-field inventory, while FR58 permits “other approved C9 metadata” through an unnamed approval channel. Indexed or disclosed metadata can expand without an explicit PRD or Contract Spine change.

- FR58 still omits query grammar, token normalization, match semantics, ordering, pagination, result/byte/time bounds, and opaque-identity stability. OQ5 requires non-empty behavior and security evidence but does not define these user-visible search semantics.

- FR58 requires egress trimming and current-state hydration but not tenant-partitioned index/cache storage, bounded archive/revocation tombstone propagation, or count/timing side-channel tests. Post-filtering alone does not prove that another tenant’s indexed objects cannot be inferred.

- The Folders lock does not serialize humans, provider automation, or other integrations writing directly to the remote ref. Commit has no expected-head compare-and-swap rule, force-push prohibition, or external-write conflict outcome, leaving no-lost-update behavior undefined outside Folders-controlled writers.

- Seven-year audit reconstruction lacks time-window limits, pagination bounds, stable ordering, snapshot/cursor consistency, duplicate suppression, and completeness markers. An audit reviewer cannot prove that a reconstructed incident is complete rather than truncated or racing new events.

- The one-second command acknowledgement does not require durable recording of authorization evidence, idempotency reservation, operation identity, or accepted state before response. An acknowledged operation can be lost before the promised inspectable state exists.

- Cleanup is expected at the C3 seven-day boundary but may remain failed and merely expose status/escalation. No retention-breach state, emergency action, or operational consequence defines what happens when retries cannot satisfy the boundary.

- C3 invokes legal hold and an approved deletion workflow while hard deletion is outside MVP, but the owning external workflow, legal-hold actors, hold/release audit, and handoff contract are not named or tracked in the Open Release Items.

- Provider calls require explicit timeout budgets, retry limits, and backoff caps, but those numbers are neither specified nor assigned to a canonical Open Release Item. Provider failure behavior remains quantitatively undefined for architecture acceptance.

- The frozen Contract Spine/C13 snapshot has version and digest requirements but no canonical storage path, required signers, retention period, or direct linkage from a release identifier. Its content is immutable, but its custody trail remains underspecified.

- OQ4 does not state the minimum GitHub products, Forgejo versions, API profiles, or credential types that MVP must support. The compatibility catalog can close against a conveniently narrow provider envelope while the PRD continues to claim support for both providers.

- Large change-set projections must preserve counts, operation types, failure attribution, and “explicit limits,” but the PRD supplies neither the detail limits nor a dedicated Open Release Item. Architecture and stories cannot derive the accepted large-batch summary shape.

- The p95 latency and capacity metrics defer their population, workload mix, environment, and measurement rules to OQ10. That is honest release governance, but performance and scaling stories cannot receive reproducible acceptance criteria until the calibration plan exists.

- OQ1–OQ4 are correctly described as bounded parameter and inventory closures, yet they still contain product-significant choices that can materially reshape architecture and story estimates. Downstream work can proceed only if those stories carry explicit dependency links rather than treating the open decisions as release-testing details.

Total findings: 17
