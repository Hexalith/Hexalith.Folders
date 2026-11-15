---
title: 'Resolve Story 1.17 native scanner and Conversations release prerequisites'
type: 'chore'
created: '2026-11-15'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '336c3e82df1f66febf9cb30febcdb5b841d225e9'
context: []
---

<frozen-after-approval reason="user explicitly authorized autonomous reversible preparation; publication/deployment approval reserved">

## Intent

Folders Release compilation consumes native scanner APIs absent from EventStore 5.1.0. Projects previously could not restore Conversations 2.0.0 from an offline cache. Prepare compatible release artifacts in their owning repositories, resolve necessary package/catalog selections, and rerun Release/package and parity checks.

## Boundaries & Constraints

**Always:** Use package references for Release; work from owning repositories. Permit read-only remote discovery, restores, necessary package/catalog changes, and local packing. Preserve full source identities, exact commands, logs, package/DLL hashes, and honest gate outcomes. Use `run-with-temp` for disposable copies, caches, feeds, and builds; retain useful artifacts/logs outside TMPDIR. Wait for child jobs. Preserve existing historical evidence, V1Only, activation controls, migration T0, and tracker bytes. Follow repository guidance, reading the permitted Hexalith baseline first.

**Never:** Publish, deploy, push, commit, stage, trigger remote workflows, initialize nested submodules, change consumer behavior or weaken test expectations, disable audit/gates, or claim Story 1.17 closure. Before external publication/deployment, present exact artifacts and action for explicit approval. Preparation authorization includes implementation now; no additional spec approval is needed.

</frozen-after-approval>

## Code Map

- `references/Hexalith.EventStore/`: clean main `fdb59895975e86a1e234325a01237f259cdbd53b`; scanner introduced after published 5.1.0 (`49084f66f8d6545cae442875355a7066d5f5e73d`). Client native APIs and their existing tests own the capability.
- EventStore `tools/pack-release-packages.py`, `tools/release-packages.json`, `tools/validate-release-packages.py`, `tools/verify-release-api-compatibility.py`: existing all-fourteen-package lane; explicit version/output. Current semantic-release history selects patch **5.1.1**. Local unsigned candidate is unpublished. API checker accepts `--candidate-commit`, `--package-catalog`, `--work-directory` (initially empty).
- EventStore `.releaserc.json` and `.github/workflows/release.yml`: eventual dispatch publishes all fourteen packages, containers, GitHub release; no package-only input. Current source CI run 43701823933 failed; diagnose read-only and retain the actual blocker, repair only prerequisite/package scope. Failed Client/Server CI includes stale development catalog goldens and Server workload fixture missing GatewayNativeSourceScan, plus one native selector abandonment timeout; use authoritative regeneration and accurate fixture policies, preserving assertions.
- `references/Hexalith.Builds/Props/Directory.Packages.props`: owns default EventStore 5.1.0 and Conversations 2.0.0/Folders 1.4.0. Preserve special 4.2.0 views and file formatting. `Tools/test-authoritative-package-catalog.ps1` and `Tools/validate-platform-version-catalog.ps1` own checks; inspect assertions before changes.
- `references/Hexalith.Projects/Directory.Packages.props`: local EventStore 5.1.0 override must remain coherent with selected candidate if necessary. Projects CI solution, server Conversations/Folders tests own consumer evidence. Both NuGet Conversations 2.0.0 archives exist from `bd6265afe8aee2e555b637bd973653331c284c66`, release run 43642189667; no Conversations producer edits needed.
- Folders `Hexalith.Folders.CI.slnx`, Projects `Hexalith.Projects.CI.slnx`: complete Release package solutions. Folders `tests/tools/run-contract-parity-ci-gates.ps1` (`-SkipRestoreBuild -OutputReportPath`) covers twelve categories; governance counterpart writes its report in the disposable copy.
- `_bmad-output/implementation-artifacts/evidence/story-1-17-current-local-2026-11-15/release-commands.json`: exact earlier commands/selectors. Earlier package tests failed to build; do not reuse old binaries. Separate existing proposal/source-binding drift from these prerequisites.

## Tasks & Acceptance

**Execution:**
- [x] EventStore owner evidence directory -- prepare fresh all-fourteen 5.1.1 candidate using existing packer from isolated exact source; validate packages, existing all-family API compatibility, and focused native scanner tests. Investigate native endpoint-related source CI failures, correcting prerequisite fixtures/derived goldens through existing generation if justified; rerun affected tests without weakening assertions. Retain source inputs/patches, archives and symbols, manifest hashes, compiler outputs and test counts. Do not bypass a failure.
- [x] Builds/Projects catalogs -- adopt verified candidate in coherent selections if needed, retain proposed changes as reversible working files, clearly mark unpublished dependency status, run relevant existing catalog checks. Avoid pretending public-feed restore succeeds before publication.
- [x] Projects owner evidence -- download published Conversations 2.0.0 pair using authorized NuGet lane, retain provenance and hashes; use current Folders 1.4.0 pair unchanged. Run complete Release/package restore/build and relevant Conversations/Folders adapter tests; retain immutable runtime artifacts.
- [x] Folders additive evidence directory and summary -- run complete Release/package restore/build, native adapter/authority/routing checks, governance and all twelve parity categories against fresh binaries and selected candidates. Keep previous reports untouched. Record exact failures even if unrelated; narrow fallback checks must not imply broad success.
- [x] Add reviewable handoff -- exact candidate archive/version/source/hash list, commands/results and restore origins, preservation verification, outstanding blockers and precise proposed release action. Retain reproducible runner where useful; save durable logs/results outside scratch. No external action yet.

**Acceptance Criteria:**
- Given missing APIs in published 5.1.0, when the candidate is restored in package mode, then native scanner consumer compilation and relevant behavior checks execute from fresh artifacts, or exact prerequisite failure is retained without substituting source.
- Given published Conversations 2.0.0, when Projects restores through authorized sources, then Client/Contracts resolve at 2.0.0 with Folders 1.4.0 and their actual runtime identities are recorded.
- Given fresh consumer artifacts, when Release/package and parity checks run, then each gate has exact result, command, log/hash and current source provenance; only passing gates are claimed.
- Given the protected baseline, when preparation ends, then tracker and operational control bytes are identical and publication/deployment remain pending explicit approval.

## Implementation Notes

- Completed bounded reversible preparation and independent review. User constraints override automatic commit and canonical tracker synchronization; nothing was staged, committed, pushed, published or deployed.

- No unresolved intent gaps: reversible package qualification and necessary fixture/catalog preparation are authorized. Publishing the release remains irreversible and requires a later approval. User authorization overrides workflow spec checkpoint, remote-operation prohibition for read-only discovery/restores, tracker synchronization and staging instructions. Changes remain unstaged. Work sequentially during implementation; use isolated snapshots to avoid user output interference.

## Spec Change Log

## Review Triage Log

- BH1: **medium**, patch — Required command/test statuses are discarded in producer/consumer callers and main returns normally; current recorded final gates are independently all successful. Patch fail-closed phase status and stop build after restore failure.
- BH2: **medium**, patch — Job construction rewrites runner receipts before checking collisions; all four evidence targets must be reserved fresh before writes. Patch immutable evidence reservation.
- BH3: **medium**, patch — Package-member enumeration only accepts net11 while Folders1.4.0 pair targets net10. Parent directly verified both archived runtime DLLs equal published net10 package payloads; add those required bindings and update total from30 to32.
- BH4: **medium**, patch — Consumer seed trusts current candidate directory without checking successful producer receipts and recorded inventory hashes; changed bytes can become its own authority. Patch producer input preflight.
- BH5: **medium**, patch — Producer unconditionally hashes an ignored hardcoded historical triage file; consumer also parses it. Remove the mandatory history dependency using explicit existing repaired class selectors and optional diagnostic copy.
- BH6: **medium**, patch — Test success validates only positive total; skipped-only results are accepted. Require actual passed unskipped cases.
- BH7: **medium**, patch — Aggregate counts hide a missing class selector; retained native-scanner command actually includes an unmatched old Client DI selector. Its17 scanner cases pass and the separately built real DomainService class passes5/5. Add per-selector matching guards and preserve correction evidence.
- BH8: **medium**, patch — Job inherits NuGetAudit=false and does not enforce its audit guarantee. Explicitly enforce and retain required audit settings for new invocations.
- BH9: **medium**, patch — Imported API helpers use their own subprocess.run; producer/consumer calls do not supply job.env or managed process cleanup. Route helper execution through private managed command handling while preserving expected return contract.
- BH10: **medium**, patch — Source patch and hashes are collected from live owner after snapshot copies, so concurrent edits can misdescribe compiled inputs. Record actual snapshot inventories/patches and reject source drift during capture.
- EC1: **medium**, patch — Same verified ignored required-result defect as BH1; failed restore can also be followed by successful build status. Patch groupG1.
- EC2: **medium**, patch — Same verified evidence overwrite before collision detection as BH2. Patch groupG2.
- EC3: **medium**, patch — Same verified skipped-only success as BH6. Patch groupG6.
- VG1: **medium**, patch — Verified caller status loss permits successful phase after focused tests, package/API, parity or governance failures. Same demonstrated root cause as BH1; patch groupG1.
- VG2: **medium**, patch — Verified repeated evidence names replace runner/source metadata before existing log guard. Same demonstrated root cause as BH2; patch groupG2.


### Review resolution

- All15 findings were resolved without changing product code or package payloads. The supported preparation/supplemental Python entry points were deleted; their exact bytes and pre-review records remain historical evidence. Existing owner tools and explicit manual checks replace the unsupported automation, removing the demonstrated future execution paths for gate-result loss, evidence overwrite, unqualified producer reuse, hidden triage, skipped/missing selectors, inherited audit settings, unmanaged helpers and live-source capture races.
- BH3 received an actual artifact correction: both published Folders1.4.0 net10 Client/Contracts DLLs match the retained Projects runtime. The corrected binding receipt has32 entries; parent independently read every package member and capsule payload.
- Nine focused review controls pass. Parent independently rehashed all593 final retained files and verified15,016 protected root files remain unchanged. Original passing .NET tests/builds/API checks and138 restored archive comparisons were preserved.
- No findings are deferred. Only reversible release preparation is complete; remote exact-source CI, a newly qualified release source, explicit publication approval and wider Story1.17 prerequisites remain separate. V1Only and the canonical backlog tracker remain unchanged.

## Verification

Run existing manifest pack/validation/API checks; individual native scanner tests; both complete CI solution Release restores/builds with `UseHexalithProjectReferences=false`, `-m:1`, warnings as errors; focused Projects and Folders tests; governance and twelve-category parity. No audit-disable properties. Record any existing warning/error honestly. Verify catalogs using their own tooling, protected-file hashes, and per-owner `git diff --check`.
