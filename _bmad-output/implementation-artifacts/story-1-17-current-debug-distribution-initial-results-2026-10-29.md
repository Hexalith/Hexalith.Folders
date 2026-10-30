# Current Debug distribution qualification — 2026-10-29

**Qualification remains incomplete.** Production source mapping resolves the
twelve root-declared source origins, but catalog materialization stops because
the Platform-selected Builds commit
`4ebd4d869fe1d3958d73e586bcaa74bd86e2ce42` is absent from the approved root
Builds object database. No substitute catalog, package or repository was used.

The decisive build-only attempt used the private SDK 10.0.402 layout, built the
actual source Tooling/helper and passed the SDK interception probe. Its exact
failed command is retained in the [materialization receipt](evidence/story-1-17-current-debug-distribution-2026-10-29-apphost-launch.json):

```text
/tmp/story-1-17-current-debug-distribution-2026-10-29-apphost-launch-0di82_1_/artifacts/bootstrap/bin/DistributionProbe/debug/DistributionProbe map /home/administrator/projects/hexalith/folders/references/Hexalith.Builds/test/fixtures/module/executable/hexalith.module-manifest.v1.json /tmp/story-1-17-current-debug-distribution-2026-10-29-apphost-launch-0di82_1_/workspace
```

It exited `-6` with `WorkspaceMappingException: Pinned Builds file
'Props/Directory.Packages.props' at 4ebd4d869fe1d3958d73e586bcaa74bd86e2ce42
cannot be read from '/home/administrator/projects/hexalith/folders/references/Hexalith.Builds'.`
The retained `mapping.json` records the resolved source roots and catalog pin;
the failed materialization grants no build or distribution acceptance.

Independent read-only inspection confirms the owner prerequisite:

```text
GIT_OPTIONAL_LOCKS=0 git -C references/Hexalith.Builds cat-file -t 4ebd4d869fe1d3958d73e586bcaa74bd86e2ce42
exit 128: fatal: git cat-file: could not get object info

GIT_OPTIONAL_LOCKS=0 git -C references/Hexalith.Builds show 4ebd4d869fe1d3958d73e586bcaa74bd86e2ce42:Props/Directory.Packages.props
exit 128: fatal: path 'Props/Directory.Packages.props' exists on disk, but not in '4ebd4d869fe1d3958d73e586bcaa74bd86e2ce42'
```

Builds is not shallow; `ls-tree` also exits `128` with `fatal: not a tree object`.
Platform/Builds owners must separately approve restoration of the verified
pinned object and catalog import closure in this same root repository, or a
distinct repair to invalid pin authority. This task performs no fetch, object
write, history repair or pin change.

Independent reversible verification completed:

| Evidence | Result | Qualification scope |
| --- | --- | --- |
| Factory selection tests linked into a private focused xUnit project | 8/8 passed; zero failures, errors, skips or unrun cases | Actual new test file, original helpers and actual Tooling source; supplementary only |
| Focused SDK 10.0.402 Debug build | Passed; zero warnings/errors | Separate focused graph using the live source catalog |
| Harness failure/corruption matrix | 16/16 passed under normal and optimized Python | Mandatory SDK controls, mapping omissions, private directories, drift, package fallback, incompatible identities, required assertions and inert replay |
| Five retained capsules | 636 members verified under normal and optimized Python | Byte integrity and honest incomplete outcomes; zero recorded commands executed |

The [focused final receipt](evidence/story-1-17-current-debug-distribution-2026-10-29-focused-final.json)
retains the exact private project, command vectors/environments, source compiler
inputs and bytes, assets, full logs/binlog and xUnit XML. The live catalog used
by this independent diagnostic project does not replace or qualify the missing
production-pinned catalog. The original Module.Tests distribution build and its
three required original-assembly classes did not run. Neither the actual model
API build, sealed CLI/three-host outputs, exact sealed-host factory selection,
nor model PE reference inspection ran. Their implemented success paths remain
unqualified; synthetic identity guard checks grant no runtime compatibility.

Earlier attempts remain separate: ambient `latestPatch` selected SDK 10.0.403
and stopped before a build; the first private-SDK attempt exposed a helper
launch/interception issue; an initial focused-project setup exposed late central
catalog import timing. Their immutable receipts preserve the first failure of
each attempt. The corrected private apphost launch and focused catalog import
have passing focused evidence. Historical source-host/loader results remain
dated evidence because current revisions advanced; none is reused as a current
distribution or runtime pass.

Folders HEAD remains `c3d295b15cd3e0c5f675d014033256458860603f`; Builds HEAD remains
`aa46f0c534206b4645b8d9eef08c9a90e1ab540e`. Each run binds and rechecks all thirteen
repository HEAD/index identities and all twelve committed/indexed root gitlinks.
The final focused run checked 80,090 tracked/pre-existing-untracked byte bindings,
retaining dirty bytes in the baseline, and verified certificate-store metadata
preservation. Parent remains **in-progress**, tracker **backlog**, routing
**V1Only**, **T0 unset**, activation authority **null**, staging **0/14**.

The [handoff](evidence/story-1-17-current-debug-distribution-2026-10-29-handoff.json)
binds the real direct Parties manifest at
`references/Hexalith.Parties/module/hexalith.module-manifest.v2.json` and the
supported `debug --mode source --output json` arguments. It records exact
planned private output paths as a blocked proposal. No qualified CLI/AppHost
output identities exist yet; the Builds qualification owner must produce and
seal them after catalog availability is resolved. The proposal was not executed.
Ordinary startup, accepted-tool replacement, signed distribution, tool/catalog/
composition/module intake and operational acceptance remain pending.

All five archives remain ignored local evidence under the
[existing archive policy](evidence/archives-out-of-git.md); receipts bind their
sizes/SHA-256 and member manifests. No external copy or upload was made. Replay
the focused evidence after restoring its recorded archive filename:

```bash
python3 -B eng/story117-source-distribution/verify.py --receipt _bmad-output/implementation-artifacts/evidence/story-1-17-current-debug-distribution-2026-10-29-focused-final.json
python3 -B -O eng/story117-source-distribution/verify.py --receipt _bmad-output/implementation-artifacts/evidence/story-1-17-current-debug-distribution-2026-10-29-focused-final.json
```

The final replay implementation additionally cross-checks the supplementary
8-test XML and retained compiler bytes. That replay-only hardening occurred
after focused execution and passed the same guard suite and all capsule replays.
The [verification record](evidence/story-1-17-current-debug-distribution-2026-10-29-verification.json)
also records an optimized replay rejection of a corrupted copy of the actual
focused capsule, unchanged original archive bytes, the final source/repository
audit, and passing `git diff --check` in both owning repositories. New owned
files have no whitespace diagnostics.
Production source semantics, dependency pins, tools, parent spec and tracker are
unchanged. No Git writes, reference initialization, Release packaging,
installation, publication, deployment, loader replay or application/topology
startup occurred. Changes remain uncommitted.
