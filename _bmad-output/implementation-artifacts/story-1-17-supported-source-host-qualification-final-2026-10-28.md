# Reviewed source-host qualification — 2026-10-28

The supported Debug source-host prerequisite is complete. The original Builds Module.Tests graph builds, and all four required original-assembly classes pass **202/202**, with zero failures, errors, skips or unrun cases. Both the source-host qualification spec and its preceding mapping spec are done. Parent Story 1.17 remains **in-progress**, with its tracker **backlog**.

The fixes preserve literal percent escapes in forwarded roots and catalog-matching version-only metadata. The SDK test launcher now owns its default caches, cleans them at process exit, resolves symlinked SDK installations and isolates environment-mutating tests. Process-bound tests observe a real descendant PID and its termination. Unsafe-reference tests demonstrate their child-evaluation probe before checking refusal. The required Platform assertion uses the verified root-declared mapping and the model copied by the original test project. Ordinary package-mode and absent-harness invocations explicitly skip only the inapplicable qualification assertion; those diagnostic skips grant no qualification credit.

| Final original-assembly class | Passed | Failed / errors | Skipped / unrun |
| --- | ---: | ---: | ---: |
| SDK launcher | 17 | 0 / 0 | 0 / 0 |
| Workspace MSBuild | 139 | 0 / 0 | 0 / 0 |
| Catalog version | 34 | 0 / 0 | 0 / 0 |
| Workspace AppHost/model | 12 | 0 / 0 | 0 / 0 |

The required `PlatformPlanDelegatesToThePinnedModelAsync` ran and passed. All **29** original/fixture restored asset files contain zero Hexalith package libraries. The final run independently repeated production resolution/materialization, the original Debug build, and all four classes under absolute SDK **10.0.402**, preserved HOME, private outputs/caches, certificate suppression and serial controls. The six tested source files stayed byte-identical through final execution. Exact command vectors, explicit environments, XML, imports, source patches and selected output bindings are in the [final receipt](evidence/story-1-17-supported-source-host-qualification-resume-2026-10-28.review-final.json).

Folders revision is `5fb5cfcfcda10044faccc71b48f8a3d6c08c5b87`; Builds revision is `e4e4568aa59050b5890c60ff4a1f39bcc83399b4`, plus the six retained uncommitted source/test changes. Final production mapping is `83e97d9576a14774675d2431a52df23ed1204b0cc57fa309ee5f291852ae00b6`; all twelve root-declared dependencies have source origin. All thirteen repository HEAD/index identities and all twelve committed/indexed gitlinks remained stable and aligned.

Three independent review layers completed. The triage log retains fifteen findings: fourteen medium findings were corrected in ten patch groups; one finding was refuted against the approved build-only intent. Nothing was deferred. The 45 focused affected cases and two separate expected-skip diagnostics remain supplementary evidence; the complete final 202-test run is the acceptance evidence. The earlier 192-test result and failed setup/compile/escaped-project-layout attempts remain dated evidence rather than final-byte passes.

Both owning repositories pass `git diff --check`; the root emits its existing spec CRLF-normalization warning. The tracked-file audit checked **76,742** before-images and found only the six owned source/test files and qualification tracking notes changed, with no missing or unexpected files. The subsequent preceding-slice completion edits and narrowly scoped attributes for these new evidence files are explicit metadata changes. The new capsule/manifest attributes preserve their exact bytes across checkouts; the replay script uses LF. The parent closure spec, tracker, candidate inventory, authority policy and dependency pins remain unchanged. Frozen intent blocks and original baseline identifiers are preserved. The [independent parent verification](evidence/story-1-17-supported-source-host-qualification-resume-2026-10-28.parent-verification.json) confirms final source, repository, authority and archive-policy checks.

Four pre-existing **MSB3277** host assembly-conflict warnings remain (Redis/IdentityModel). The separate historical baseline audit command was:

```text
git -c core.quotePath=false diff --no-ext-diff --no-color f48886763a1b57b30f7767deb9909f741dc812f3 --
```

It exited `0` but emitted `error: hash mismatch 6ba02654ca390b3333183962355bd5c5857b8224`. That old commit object belongs to the Parties repository. The broad historical diff is therefore incomplete; the current owned diffs, exact source bindings and selected gates are independently verified. No Git object was repaired.

The [final capsule](evidence/story-1-17-supported-source-host-qualification-resume-2026-10-28.review-final.artifacts.tar.gz) retains 236 selected members; its [manifest](evidence/story-1-17-supported-source-host-qualification-resume-2026-10-28.review-final.members.json) and the immutable 154-member pre-review capsule are anchored by SHA-256 in the receipt. They retain selected commands, logs, imports, source before-images, assets and outputs, rather than every cache/output byte or a full compiler/PDB capture. Per the repository [archive policy](evidence/archives-out-of-git.md), both archives remain in the ignored local evidence directory; the text receipt records their byte counts and SHA-256. No external archive copy or upload was made. Another checkout must receive the archives at their recorded filenames before replay. Replay reads the retained files without executing any recorded command:

```bash
python3 _bmad-output/implementation-artifacts/evidence/verify-story-1-17-source-host-qualification-2026-10-28.py
```

Normal and optimized-Python replay both pass, checking all 390 retained members, exact class totals, no Hexalith package fallback and inert parent authority. An optimized replay of a disposable copy with a modified archive fails with `Archive SHA-256 mismatch`; the original archive is unchanged.

The explicit no-Git-writes and no-automatic-tracker-transition constraints take precedence over generic workflow commit/tracker instructions. Changes are uncommitted. No publication, deployment, installation, dependency/reference update, consumed-loader replay or application/topology startup occurred; tests own their isolated SDK/process/model fixtures. Routing policy remains **V1Only**, **T0 unset**, activation authority **null**, staging **0/14**.

The next parent gates remain distribution/ordinary-execution acceptance, actual tool/catalog/composition and module intake acceptance, an exact signed current-candidate release with immutable hosted artifacts, and target/data, hosted identity, staging, observation/recovery and authorized closure evidence. This local source Debug result grants none of those acceptances.
