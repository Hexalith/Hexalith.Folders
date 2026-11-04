# Story 12.2 session handoff — 2026-11-04

**Paused at the user's explicit request.** The user then asked to save work for a new session.
All edits are saved on disk. Nothing was staged, committed, pushed, deployed or published.
No command or topology remains running. The implementation agent confirmed its last build
session45989 completed successfully and stopped all further work.

## Resume point

Continue the existing `$bmad-build12.2` workflow at **step03 implementation/verification**.
Do not restart or rerender the workflow. Existing rendered snapshot:
`_bmad/render/bmad-build/folders-02f7d5d6a8fa/d3ba1c521d4f7fe7c437/`.
Read AGENTS.md, the required Hexalith baseline, the current spec and its context before editing.

- Spec: [spec-12-2-durable-projections-and-task-completion-pipeline.md](spec-12-2-durable-projections-and-task-completion-pipeline.md).
- Spec/sprint status: `in-progress`; `review_loop_iteration:5`.
- Original baseline: `c22b13137202f7bf8c60b80c095a69748435bd6e` — preserve it.
- Current root HEAD: `9198d9213f6ea54c629148322f05e7ded994822d`.
- SDK HEAD: `aeec5012fa3e1e07426a8fdd145709bce93f619a`.
- Frozen-block SHA256: `497cba1a5e7c3670502f2c49e396ac77fbfa66a337ae199aafaeab6313226422`.
- Approved strict-test SHA256: `d29afa3ca0649744e84413c37bfbb41c9de0bd106160c900cb2a8c8e970c1b82`.

The explicit2-second mutation permission-evidence budget approval is already implemented.
There is no pending approval question. Preserve all current user/committed/uncommitted work.
No Git mutation, dependency-pin update, publication, deployment, force-stop or relaxed deadline/
assertion is authorized. Read the existing spec for the precise SDK prerequisite boundaries.

## Completed review-five implementation

All seven review-five correction tasks were implemented: bounded projection-only current state,
non-expiring bounded SDK task-closure evidence, native/source-local file validation, source-head
freshness precedence, cancellation and opaque observer drain/contention recovery. Schema3 live
writers require an existing protected complete-source named rebuild when upgrading schema2;
intact old rows remain readable. Primary command authority and SDK source were unchanged.
The source-specific review-five handoff and exact patch are preserved in the saved metadata.

Parent qualification passed full Debug/source runtime and Memories builds and98-module
source/PDB/assets audits. Current consumer selections passed **5681tests,2existing optional
skips,0failures/errors**: Domain2419, Server1412, Workers162, Integration854, Gateway123,
Contracts636 and ClientGeneration75. All five frozen matrix rows and actual10001/32769
production-handler capacity cases passed. Focused selections overlap those broad results.
SDK broad7684/25historical optional skips and3ABI passes are explicitly reused on identical SDK
binaries/source; the current source scanner passed274cases. Four earlier sealed packets still verify.

## Current unresolved live failure

Four current strict attempts failed the actual supported EventStore process-stop command:
315.760s,274.703s,274.922s (diagnostic stack attachment),270.423s. All retained exact
source/head/runtime/status drift and owned cleanup remainders are zero. They passed provider,
tenant-maintenance and six-concern integrity/retention/rebuild checkpoints before stopping.
**There is no full strict pass for the modified source.** The prior396.332-second pass remains
immutable evidence for the older source only.

Opaque trace/span joins show the native request completed and subsequent stopping snapshots had
zero in-flight requests and empty available inventories; ApplicationStopped never arrived before
DCP's deadline. A method-only stack capture showed idle workers and an exporter retry, but did
not identify the suspended async stop await or prove telemetry causal. Matching .NET10.0.14
source confirms HostStop is emitted after the web-server stop call. Normal SDK logging verbosity
also reproduced the failure. No generic SDK shutdown fix or deadline change was made.

The exact Release contract-spine gate separately failsCS1061 at Server67/Workers87 because the
published SDK lacks AddEventStoreGatewayNativeSourceMetadataReader. Debug source assets were
restored and audited afterward. Five AppHost/tooling outputs changed across that rebuild;
93matched, including the tested consumer assemblies and SDK copies. Treat producer cohorts
separately. Package/Release, PD10/governance, SM4 and independent review remain open.

## In-progress fixture control — finish this first

The final spec section, **Fixture-only lifecycle observer control**, has three pending tasks.
Clearing the existing shutdown path is invalid: the hook then selects the folder fold observer.
An explicit diagnostic opt-out has now been implemented, preserving the nonempty EventStore path
and mandatory Folders fold evidence:
`FOLDERS_TASK36_LIFECYCLE_DIAGNOSTICS_DISABLED=true`.
Only the exact value `true` disables lifecycle-observer registration; default/invalid values
preserve existing behavior. Fixture forwarding targets EventStore only.

Saved modified/new files for this phase:

- `tests/tools/Task36FoldObserver/Task36HostingStartup.cs`
- `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs`
- `tests/Hexalith.Folders.Server.Tests/Task36HostingStartupControlTests.cs` (new)
- `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityObserverConfigurationTests.cs` (new)
- `scripts/pd10-v2-story-owned-paths.txt` and the pending source-requalification inventory

Completed before stopping: Server Debug/source build10.540s, AppHost Debug/source build59.105s,
43observer/startup checks passed, and pending inventory generation passed.
**Unfinished:** four AppHost model cases have not run; pending-inventory controls, current privacy
scanner, final guard audit and the phase-only patch/released handoff are not finished. Do not
mistake generated inventory for passing inventory verification. No topology was launched in this phase.

Then inspect the entire phase diff and guard/result/output identities; rebuild coherent runtime+
Memories with both source flags; run the source/PDB/assets audit; and execute the unchanged d29
strict class with the supported explicit opt-out. Proposed fresh label `review5-strict-5`, port36043,
audit prefix `observer-control-runtime`. Compare with normal-verbosity strict4; keep outcomes
separate and make no causal claim from one run. Finish required ACs before step04. Read step04
fully when reached; its review-loop cap applies if another bad_spec/intent_gap would increment5to6.

All development restore/build commands require BOTH flags, Debug assets and serialized execution:
`-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1 -nr:false`.
Run individual built xUnit assemblies with `-class` and `-result-xml`, not solution-level tests.
Do not build while an owned topology holds selected outputs. Do not relax strict/capacity assertions,
proofs, permissions, lifecycle results or deadlines. Keep all existing sealed packets immutable.

## Saved evidence and tooling

A durable metadata/source backup is in the ignored directory
`.artifacts/story12-2-session-save-20261104/` with a SHA256 file manifest. It excludes raw private
logs/XML, stack text, binaries and huge full-baseline diffs. Source patches include untracked code;
the Git index was not touched. The working tree itself contains the authoritative latest edits.

Original detailed workspaces (retain; do not overwrite receipts):

- `/tmp/story12-2-review5-qualification-20261104-xnn_g7da` — parent receipts, audits, four failed
  strict runs and prepared tools; `CONTINUATION.md` contains the detailed chronology.
- `/tmp/story12-2-round5-implementation-20261104` — completed seven-task implementation handoff.
- `/tmp/story12-2-observer-control-20261104` — unfinished latest control phase and completed receipts.
- `/tmp/story12-2-foreign-scope-20261104-s76ae741` — earlier qualified scope/observer work.

Parent helper scripts exist but are not a replacement for inspecting the new handoff.
`observer_control_qualification.py` requires a new release marker before builds. The prepared
pass-packet verifier requires a real current strict pass; never weaken it to make failed runs green.
If the live gate remains blocked, produce a distinct truthful blocked-result packet instead.
No new review-five parent packet has been sealed yet. Do not publish raw logs, XML parameter
payloads, stack text, credentials or protected bodies.
