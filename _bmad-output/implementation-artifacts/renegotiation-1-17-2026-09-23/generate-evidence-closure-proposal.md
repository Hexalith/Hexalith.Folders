# 1.17-GENERATE delivery evidence — closure proposal (R2)

Status: proposal only. Nothing is applied. `spec-1-17-generate-pd10-v2-relock-milestone.md` (`5472e48e…`) and the manifest (`927641dd…`) stay untouched.

## Facts at `19c29e0`

- The manifest node `1.17-GENERATE` is `relock-only`, rank 4, `delivery_evidence_status: open`. Story `1.17` lists it as a prerequisite, so closing 1.17 requires this evidence to be accepted.
- The GENERATE spec is `in-progress` at `review_loop_iteration: 13`. The loop ceiling was exceeded under repeated human exceptions. Its last recorded seal is the 196-artifact candidate `76e36d37…` (conformance `24957048…`).
- After that seal, the scoped A6b review (`c1346b76…`) found one blocking defect: the missing `ValidateProviderReadiness` 429 runtime tuple. It was fixed with a regression test (`GoldenLifecycleParityTests.CandidateProviderReadinessRateLimitKeepsItsDeclaredTuple`) and resealed as candidate `c5bea429…`, conformance `4c8f33f0…`. The GENERATE spec does not record that final reseal.
- A6b was re-approved on exactly `4c8f33f0…` (record `4f2429e4…`). The corrective Section 9 passed (`fdb62216…`) and the corrective A8 was approved (`57bf08ed…`). The 17 remaining findings were triaged and the triage accepted under DEC-EXEC-1.17.
- The GENERATE spec is outside the 196-artifact set, so editing it changes no conformance digest.

## Options

**A. Accept as-is with evidence (recommended).** The GENERATE milestone produced exactly the candidate that A6b, Section 9 and A8 accepted. Its open work is bookkeeping, not delivery. Within the 1.17-G change:
1. Add one final Spec Change Log and Verification entry to the GENERATE spec. It records the post-review 429 fix, the final digests `c5bea429…`/`4c8f33f0…`, the review `c1346b76…` and the A6b re-approval `4f2429e4…`. It sets `status: done` and leaves `review_loop_iteration` at 13.
2. Change manifest `1.17-GENERATE.delivery_evidence_status` from `open` to `accepted`, with `delivery_evidence_record` pointing at those records and a register entry.
3. No new review loop, and no code change attributed to GENERATE. The must-fix work belongs to Story 1.17 (DEC-EXEC-1.17 triage), not to GENERATE.

**B. Fold GENERATE into 1.17.** Treat the GENERATE spec as superseded and let the 1.17 closure check stand as its evidence. This is not recommended:
- It collapses a rank-4 relock milestone into a rank-10 ordinary story that lists it as a prerequisite, which makes the graph self-referential.
- V2-014 limits relock-only authorization to `1.17-GENERATE`, so folding moves relock work under ordinary execution authority.
- It rewrites the history that A6b, Section 9 and A8 were bound to.

## Timing choice (open decision D-2b)

- **In the 1.17-G change (recommended).** One atomic governance change, one re-validation, and no extra manifest churn on main.
- **Standalone and earlier.** This is a smaller governance change on main that needs its own approval and a manifest/register rebind. It touches no conformance digest, but it does move the manifest that the 1.17-G preparation will bind. The only benefit is that the prerequisite shows `accepted` sooner.

## Approval wording, if Jerome chooses A in the bundle

> "R2 (2026-09-__): I, Jerome, for Delivery, accept 1.17-GENERATE delivery evidence as-is, bound to candidate set c5bea4292e918605ba9a4331012806fc0e3e3da818dea66ae68c080929bf2c7f / conformance 4c8f33f0cb00afac3a5541eab7b9d30ec15b832d6114316ff672338cc02f5be7, review c1346b76…, A6b re-approval 4f2429e4…. The GENERATE spec is closed with one final change-log entry and manifest 1.17-GENERATE moves open→accepted, both applied only inside the 1.17-G closure change. This does not approve Story 1.17 closure, v2 routing, or any tracker edit."
