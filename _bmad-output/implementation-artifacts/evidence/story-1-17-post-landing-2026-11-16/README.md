# Story 1.17 after the repairs were pushed

The Projects fixes are committed. **Story 1.17 remains open:** current product CI, immutable host artifacts, Platform enrollment and locked operational qualification still have pending gates. This [additive handoff](handoff.json) updates the [previous closure handoff](../../story-1-17-closure-prepared-handoff-2026-11-16.json); all historical approvals and results retain their original subjects.

Projects is clean at `981ceef13a1abc88219925e73c51a23eb9ac50c2`. Repair commit `b08b201b24ffe980daa2b785183c1deda8ec9f33` is its ancestor. The fixture and release manifest match their retained qualified bytes exactly. `Directory.Packages.props` contains the same repair plus one XML comment; removing that exact comment reproduces the historical hash. [Source bindings](source-bindings.json) record this distinction. Dependency gitlinks advanced, so the previous 2,837 passing local tests, one Dapr-lane skip and package-consumer checks remain historical results.

The observed [Projects CI run](https://github.com/Hexalith/Hexalith.Projects/actions/runs/43881420098) is pending. Separate dated reads returned zero jobs and artifacts. Its reusable Builds execution revision is `6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b`; Projects' declared Builds pin is `fdda17c0a4538b0f7c99f813c20ca24b49006530`. Neither commitlint success nor a pending run supplies product/runtime acceptance. The Folders push also has product and contract jobs in progress. [Queries](queries.json) and compressed original responses retain the exact subjects, times and bounded results; these observations are not continuous CI monitoring.

Platform at `829d68b4cb6bf4e27c9baba318555e2e7b3ebbaf` selects tool 6.0.5, range `[6.0.5, 7.0.0-0)`, producer `e2bbbd0893f586e4d447ef8358ff747ea3d34435` and catalog SHA-256 `d098dab50977041a536508f6df7e934f95678a4a0abedd80b28e9bae19ee0637`. The old 4.45.0 documentation mismatch is resolved. Story 2.13 retired companion replay obligations. [Hosted tuple run 43846045454](https://github.com/Hexalith/Hexalith.Platform/actions/runs/43846045454) passed source resolution, tuple execution and verdict on `215ae18c972e6ea8ccb27032dfc11a57b098f047`. The current source differs only in two tracking documents; that comparison grants no new runtime/image or module-registration acceptance. `candidateAcceptance` remains false.

## Next owner actions

1. **Projects maintainer:** obtain the existing exact-source CI result and qualify current G-6/runtime and scheduled managed-AppHost E2E. Do not dispatch a NuGet release to fill the host gap.
2. **Projects release/deployment maintainer:** supply the reviewed .NET SDK host-production lane, immutable retention destination, target platform/RID, base-image digest and exact candidate configuration/manifests. Produce the retained Server image/provenance bound to its committed source and the unchanged signed Folders Client/Contracts 2.0.0 archive and DLL identities in the previous handoff. Those owner selections are still missing.
3. **Folders release and Platform owners:** supply the retained Folders Server/Workers/UI and applicable composed EventStore artifacts and configuration identities. NuGet publication is complete and needs no repetition.
4. **Module owners with Platform `@jpiquot`:** complete the current executable mapping decision and enrollment under `docs/domain-module-enrollment.md` section 5. Actual `projects/domain` and Folders source/image bindings remain absent. Folders has no root declaration; Projects already has one, with empty smoke coverage and no `module/platform-handoff.md`. Supply scoped authentication, routing/Dapr policy, both fixed state stores (`statestore`, `projects-conversation-start-setup`), nonempty smoke and every required critical-flow E2E mapping. Do not infer enrollment from generic tuple CI.
5. **Deployment/data/identity owners, then Jerome:** follow all seven remaining gates and applicable-branch requirements in the handoff. Acquire each real environment lock/epoch before its mutation, refresh target/consumer/data disposition, and qualify the latest issued serialized full-pass staging attempt, applicable recovery, hosted callers/schedules and real rollout before closure. Production admission must precede the exclusive 24-hour deadline from staging start. Existing-v1 migration retains its baseline, 24-hour attributed observation and T0+168-hour retirement requirements. Mutating v2 callers remain blocked pending accountable completed-write reconciliation/disposition.

The historical Projects owner runbook is superseded only where it says the repairs are uncommitted or uses old source/tool inputs. Its artifact-receipt and enrollment requirements remain applicable. No host lane, retention coordinate, configuration or owner acceptance is invented here.

## Verification and preservation

Run from the Folders root:

```bash
run-with-temp python3 -B -O _bmad-output/implementation-artifacts/evidence/story-1-17-post-landing-2026-11-16/replay.py
```

Replay verifies packet hashes, retained response projections, committed repair bindings, selected source comparisons and unchanged protected files/revisions/indices. It makes no network request and grants no current product or hosted acceptance. After later legitimate source changes, the workspace portion must refuse; use `--packet-only` to verify the dated packet and historical controls without comparing the current workspace.

Independent review confirmed the original claims and identified eight missing consistency comparisons in replay. All eight were corrected. The [regression results](review-regressions.json) record sixteen altered-metadata probes rejected in disposable copies, covering normal and optimized Python, followed by successful unchanged-packet replays. No finding was deferred.

All three previous packet inventories were rehashed, and the previous package-alignment verifier passed in historical packet-only mode. No application build/test was repeated for this additive evidence change. Folders publication, audit, A8 and Story 11.6 work remain complete within their recorded scope. Recorded controls remain `V1Only`, null T0/activation authority, fourteen unexecuted staging checks, parent in-progress and tracker backlog. No Git mutation, dependency change, publication, deployment or activation was performed.
