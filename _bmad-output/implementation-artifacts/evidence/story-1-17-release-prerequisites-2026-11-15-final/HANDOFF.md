# Story 1.17 private release prerequisite handoff

Private release preparation is complete. **Story 1.17 remains open; publication and deployment are not approved.**

EventStore owns fourteen unsigned 5.1.1 candidate packages and fourteen symbol archives. Builds and Projects now coherently select the unpublished candidate, with explicit local-feed comments; the special 4.2.0 views remain intact. EventStore changes only the unactivated generated Development catalog, its exact expected hash/counts, the missing native scan test-host workload policy, and admission-test setup synchronization. Consumer behavior and assertions remain intact.

| Final check | Result |
| --- | --- |
| Existing all-fourteen package validation | Pass |
| Existing API gate, signed 4.2.0 → private 5.1.1 | 14/14; compatible/removed-member/renamed-parameter controls pass |
| Additional signed 5.1.0 → private 5.1.1 API comparison | 14/14 |
| Clean producer Client native/catalog subset | 441/441 |
| Clean producer Server native/catalog/HTTP/historical ABI subset | 400/400 |
| Native scanner / admission / actual DomainService DI composition | 17/17; 8/8; 5/5 |
| Repaired Server classes corresponding to original CI failures | 178/178 |
| Builds authoritative catalog / platform version catalog | Pass; 254 positive/negative view probes |
| Complete Projects and Folders CI solutions, Release/package restore and build | Both pass, warnings as errors, audits enabled |
| Projects Conversations/Folders adapters | 221/221 |
| Folders native adapter and authority / routing | 64/64; 20/20 |
| Folders governance / parity | Pass; all 12 categories pass |
| Exact restored candidate archives / runtime DLL package-member bindings | 138/138; 32/32 |

All counted focused tests passed without skips. Final runs use private disposable workspaces and caches, NuGet.org plus the exact retained candidates, and package references in Release. Conversations Client/Contracts 2.0.0 are verified against source `bd6265afe8aee2e555b637bd973653331c284c66`; unchanged Folders Client/Contracts 1.4.0 identify `f9808ddecaae791a9802561e7c513da21a61b473`. Official catalog hashes, signatures, archive/DLL hashes, exact commands, logs, source snapshots/patches and runtime capsules are retained in each owning repository.

The complete archive/version/source/SHA-256 list and per-gate command receipts are in [HANDOFF.json](HANDOFF.json). [artifact-manifest.json](artifact-manifest.json) indexes the final retained files, including runtime capsules, symbols, signatures, API reference contexts, runner bytes and source patches. [preservation.json](preservation.json) proves all 15,016 preexisting root tracked file bytes are unchanged, including the authorized workflow context-agent refresh. Owner changes are limited to the listed prerequisite files; all other checked owner bytes remain intact. No tracker, V1Only control, activation control or migration T0 was changed. No files were staged or committed; nothing was pushed, published or deployed, and no remote workflow or nested submodule initialization was performed.

The remaining release boundaries are explicit:

- The Development runtime completeness check still refuses `unrepresented-unbounded-query-fallback` with exit 2. Its generated declaration check passes; this is no runtime activation proof.
- Remote source CI run 43701823933 remains failed at the original base `fdb59895975e86a1e234325a01237f259cdbd53b`. Local corrected tests do not replace normal source review/landing followed by successful exact-source push CI and commitlint.
- The catalog edits depend on an unpublished private 5.1.1 feed. EventStore packages embed the base commit and are bound to their retained source patch; the later published rebuild must have its own newly qualified source and signed identities.
- Earlier cache-fed/incomplete-layout attempts and the zero-test incorrect DI selector are historical, nonqualifying evidence. The clean runs resolve the earlier historical Tenants ABI failures; the actual DI class is `Hexalith.EventStore.DomainService.Tests.NativeSourceScannerCompositionTests`.

The proposed future action, after a newly identified source SHA qualifies and the user explicitly approves its artifacts and effects, is `gh workflow run release.yml --repo Hexalith/Hexalith.EventStore --ref main`. It has no package-only input: it rebuilds/publishes all fourteen NuGet families, container images and a GitHub release. **Do not dispatch it from this preparation.** The precise prerequisites and pending source SHA are recorded in the JSON handoff.

The supported Python preparation entry points were retired after review. Their exact latest bytes and old-path/hash mapping, plus the original artifact manifest/handoff/runtime receipt, are preserved under `pre-review/`. Historical executing snapshots were not retroactively repaired and must not be reused. The manual guide in `eng/story117-release-prerequisites/README.md` names existing owner tools, repaired selectors, explicit audit pins and source/archive checks without depending on ignored triage JSON.

The two added bindings are the actual Folders 1.4.0 `lib/net10.0` Client/Contracts DLLs in the already retained Projects runtime capsule. Both match the already verified published archives; total runtime bindings are now **32**. Package bytes and existing passing .NET results remain unchanged. Nine focused Python review controls passed; see `review-corrections/result.json`. These controls validate artifact corrections and manual guard primitives, not historical runner repairs. The old unmatched Client DI selector remains uncredited; scanner **17** and actual DomainService composition **5** are separate proofs.
