# Story 1.17 private release preparation

Use the repository runner through `run-with-temp`; choose new evidence names on every run:

```bash
run-with-temp python3 eng/story117-release-prerequisites/prepare.py --phase producer --evidence-name MY-PRODUCER-RUN
run-with-temp python3 eng/story117-release-prerequisites/prepare.py --phase consumers --evidence-name MY-CONSUMER-RUN --producer-evidence MY-PRODUCER-RUN
```

The runner snapshots tracked working-tree inputs into private Git clones. It never initializes nested submodules. Producer compilation selects the published EventStore 5.1.0 dependency cohort explicitly; the existing packer stamps all fourteen candidate packages at 5.1.1. Consumer compilation selects the exact retained 5.1.1 archives. Every job uses a fresh package cache and only NuGet.org plus explicitly selected private package feeds. Audits, warnings as errors, and all gate assertions remain enabled.

The producer retains packages, symbols, compiler/test logs, exact commands, source inventories and patches, the existing fourteen-family API comparison, authenticated public dependency archives, and runtime capsules in EventStore owner evidence. Conversations 2.0.0 and unchanged Folders 1.4.0 published pairs, provenance and consumer runtime capsules belong to Projects evidence. Builds catalog checks stay in Builds evidence; Folders solution, focused checks, governance and twelve-category parity results stay in Folders evidence.

Every invocation records the exact runner SHA-256. Individual command groups are terminated and waited on if interrupted or timed out; compiler and MSBuild server reuse is disabled for these private jobs. Existing evidence is never overwritten.

The retained final preparation uses producer evidence `story-1-17-release-prerequisites-2026-11-15-final-producer` and consumer evidence `story-1-17-release-prerequisites-2026-11-15-final`. Earlier attempts remain historical and are never selected implicitly.

The existing API gate compares the signed 4.2.0 baseline with the candidate across the intentional net10-to-net11 runtime change. `verify_patch_api.py` adds the immediate signed 5.1.0-to-5.1.1 comparison using freshly resolved net11 contexts:

```bash
run-with-temp python3 eng/story117-release-prerequisites/verify_patch_api.py --evidence-name MY-PATCH-API-RUN --producer-evidence story-1-17-release-prerequisites-2026-11-15-final-producer
```

The current catalog working files select an **unpublished** private candidate. They require its retained local feed until an approved producer publication. These results establish no Story 1.17 closure, production activation, migration T0, or publication approval. The release workflow rebuilds packages and publishes containers and a GitHub release as well as NuGet packages; there is no package-only dispatch input. Source review/landing, successful exact-source remote CI and commitlint, and explicit approval for a concrete release action remain separate requirements.
