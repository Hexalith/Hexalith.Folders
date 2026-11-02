# Canonical Folders package cohort

This is local package qualification of EventStore 4.0.0, McpCli.Abstractions 2.0.0
and Tenants 7.0.0. It makes no published-package, release, deployment, activation,
or operational acceptance claim. The tracked NuGet configuration is unchanged.

## Sources and scope

The source revisions and working diffs are recorded in `qualification/sources.json`
and the adjacent source patches. A revision identifies the committed starting
point; the patch identifies additional working changes. EventStore uses exact
commit `a9317e4b228adfbcdd6686a2f86171b464f34c06` without producer changes.
McpCli reuses only the canonical scope implementation/tests from
`06cfdf25a352034b36041e9ed44da514b167ca5a`, plus its 4.0.0 SDK and 2.0.0 package
identity. Its enrolled Tenants.Contracts 5.10.0 and Parties pins, approvals,
vectors, and release automation remain intact. Tenants reuses only the three
bootstrap fixture corrections from `aea4b76a590be4b1122631d6e210332c24d3b8cb`.
Builds owns the coordinated family defaults and the family/override assertions.

The approved Platform mapping remains bound to baseline
`3d344e850471b6a21fc4649df7ede9a0390689b1` and mapping SHA-256
`46a4bef075af48791ec3f0bc4e4336de8597c36406ccad2f3ab5183e438b41cd`.
Current root siblings do not replace those approved revisions. Platform HEAD,
gitlinks, historical receipts, V1Only, unset T0, null activation authority, and the
tracker backlog are protected separately from this local package experiment.

## Ordered reproduction

Use a new scratch directory for each qualification so old same-version packages
cannot satisfy restore. The recorded run used
`/tmp/folders-canonical-cohort-20261102` with `feed/`, `cache/`, and `http-cache/`.
Create an equivalent temporary `nuget.config` using the source mapping retained in
`qualification/nuget.config`; adjust only the scratch path. Set both
`NUGET_PACKAGES` and `NUGET_HTTP_CACHE_PATH` to the new empty directories. Never
copy candidate packages from another run.

Before packing, run this executable identity preflight from the Folders root:

```bash
python3 eng/package-cohort/verify-cohort.py producers
```

It requires every full revision in `qualification/sources.json`, every recorded
working-file SHA-256, the recorded patch hashes, and exactly the recorded working
paths. Revision, content, or extra-path drift fails before packing. This manifest
now describes the reviewed source candidate. `sources.packaged.json` and its
preserved `.packaged.source.patch` files separately describe the earlier package
checkpoint, preserved as prior inputs.

The reviewed snapshot is now qualified by `qualification/final/result.json`.
`qualification/owner-commits.json` records the exact local commits capturing that
snapshot. The preflight accepts those commits only when their paths and worktree
bytes match the qualified base-plus-patch inputs; unrelated revisions still fail.

Run every command from the repository owning its project. For each producer,
use:

```bash
dotnet pack src/<project>/<project>.csproj -c Release -o "$cohort_scratch/feed" \
  -p:RestoreConfigFile="$cohort_scratch/nuget.config" \
  -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true \
  -m:1 -nr:false -warnaserror
```

Pack in this order, waiting for each command to finish:

1. McpCli: `Hexalith.McpCli.Abstractions` (including its analyzer project).
2. EventStore: Contracts, Client, ServiceDefaults, DomainService,
   Admin.Abstractions, Server, Gateway, Aspire, SignalR, RestApi.Generators,
   Testing, each with the `Hexalith.EventStore.` prefix.
3. Tenants: Contracts, Server, Client, Testing, Aspire, each with the
   `Hexalith.Tenants.` prefix. Keep the normal pack artifact verifier enabled.

Only these current-source packages enter the local feed. Other families use
NuGet.org. The exact published legacy `Hexalith.EventStore.Testing.Integration`
3.128.4 fixture remains a separate compatibility dependency in Tenants tests.
McpCli tests use `qualification/mcpcli-tests.nuget.config` to restore their
unchanged approved Tenants.Contracts 5.10.0 from NuGet.org; this exception is not
used for Folders or the Tenants producer builds.

From the Folders repository, build AppHost, then AppHost.Tests, with:

```bash
dotnet build <project> -c Release \
  -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true \
  -p:RestoreConfigFile="$cohort_scratch/nuget.config" \
  -m:1 -nr:false -warnaserror
```

Use `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj`, followed by
`tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj`.
The latter retains both selected Tenants owner build paths: its project reference
and `BuildSelectedTenantsCatalogOwner` target. Both select the same current
`references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj` that
runtime repository metadata resolves, with Release and the coherent catalog.
Run the built xUnit assembly with
`-class Hexalith.Folders.AppHost.Tests.FoldersDevelopmentCatalogCompositionTests`.
This constructs the model without starting resources.

Then rebuild Tenants Server.Tests in Debug with
`UseHexalithProjectReferences=true`, `UseNuGetDeps=false`, and the same temporary
restore configuration. Invoke that built xUnit assembly directly for the two
named prior failures and the hosted-service, authority, transport, readiness,
bound-command, receipt, redirect, and bootstrap-configuration classes.
Run McpCli Core and CLI tests after their builds finish. Exact commands,
exit codes, durations, counts, and logs are retained in `qualification/`.

Run Builds `Tools/test-authoritative-package-catalog.ps1` and
`Tools/validate-platform-version-catalog.ps1` after the feed has been restored.
The former checks all family bindings and exercises NU1013 refusal of package
version overrides for all three families. Compare package DLL SHA-256 values
with their producer outputs and verify the selected runtime SDK dependencies.
Finish with the protected-byte comparison and `git diff --check` in every changed
owner. Keep all earlier receipts unchanged.

## Initial full-checkpoint results and limits

Strict Release AppHost and AppHost.Tests builds passed with **0 warnings and
0 errors**. The unchanged catalog model class passed **1/1**. All 17 newly packed
DLLs match their producer outputs; `qualification/packages.json` records the
package and assembly SHA-256 values. All 12 cohort archives consumed from the
isolated cache match the feed SHA-256 values and identify that feed as their
source (`qualification/cache-provenance.json`). `runtime-identities.json` records the coherent
Release consumer/selected-owner package identities and output hashes.

Tenants Debug/source tests passed both named failures separately and all eight
adjacent classes: hosted service 205, authority 25, transport 32, readiness 8,
bound command 29, receipt 6, redirect 21, configuration 2. That is **328 distinct
cases**, plus two separately repeated cases, with no failures or skips.
At the initial full checkpoint, McpCli Core passed **731 total / 727 passed / 4 skipped**; CLI passed
**487 total / 485 passed / 2 skipped**. Skips cover Windows-only filesystem
fixtures and the dedicated benchmark child-process entry point. Core was repeated
sequentially after the earlier diagnostic run overlapped its adjacent CLI build;
the repeated result is the final Core evidence. CLI was also confirmed in a
sequential repeat (487 total, two Windows-only skips).

Both Builds catalog checks passed, including three NU1013 negative probes.
The first standalone platform catalog check ran before the freshly packed Aspire
package had been restored into the isolated cache and returned NU1102; its later
qualified run and the normal Tooling builds passed. The earlier diagnostic log is
retained without being promoted to acceptance evidence.

The initial protected comparison passed for **64,374 tracked paths** across Folders,
Builds, EventStore, McpCli, Tenants, and Platform. All repository HEADs and gitlinks
remain unchanged; the previous approvals, mapping, controls, historical receipts,
and tracker bytes are identical. Current Platform HEAD remains
`759bb89753de7e70cb0b0db61c7142e96ec5bac0`; it does not replace the approved
baseline above. Per-owner `git diff --check` results are retained alongside the
other commands.

The historical inventory command still fails separately:

```bash
python3 _bmad-output/implementation-artifacts/evidence/verify-story-1-17-current-inventory-2026-11-02.py
# ValueError: Pending manifest bytes changed
```

That blocker predates this work and was not repinned. Published 4/2/7 availability,
release acceptance, and operational acceptance remain unqualified. The retained
logs and hashes describe this local run; package archives remain only in its
private temporary feed.


## Focused review corrections

The current reviewed candidate passes the focused Core platform class (19), CLI
platform class (3), and MCP platform protocol/schema class (14), with no failures
or skips. These checks cover mapped tenant payloads, legacy fixed-system Core
routing, both protocol versions, describe/send/query scope precedence and
conflicts, closed output schemas, and mixed-case per-call tenants with a matching
lowercase session or no session. The catalog test also rejects removal of the
required Tenants.Server identity. The final broad qualification below covers
the reviewed changes; the earlier complete run remains a distinct checkpoint.

The legacy approved Tenants.Contracts **5.10.0** fixed-system operations remain
unqualified against the EventStore **4.0.0** public gateway, which reserves
`system`. Retaining fixed-system behavior in Core is a compatibility behavior,
not gateway acceptance. The new protocol cases use synthetic canonical-scope
fixtures. Enrollment approvals and vectors remain unchanged.

`qualification/packages.json` now also records the exact embedded analyzer path,
its archive SHA-256, its producer path and producer SHA-256. Each package points
to the preserved producer-source checkpoint. `runtime-identities.json` binds the
actual tested AppHost, AppHost.Tests, and selected Tenants executable assemblies
by path and SHA-256; these are explicitly the initial tested artifacts.

Reproduce protection with:

```bash
python3 eng/package-cohort/verify-cohort.py protected
```

`qualification/protection-policy.json` defines baseline Git-tree path selection,
explicit exclusions, and the complete hash-input/serialization algorithm; the
verifier's docstring states the same definition. The initial receipt is retained
byte-for-byte as `protected-comparison.initial.json`. The current comparison
covers **64,373 tracked paths**, explicitly excluding the newly reviewed
`QueryCliHarness.cs` change. The workflow's authorized deferred-work entry is
verified with the retained `deferred-work-review-append.json`: both the exact
336,202-byte historical prefix and the complete appended file must match their
recorded SHA-256 values. Only that verified original prefix enters its protected
aggregate. Historical bytes and all other controls remain protected.

## Final reviewed qualification

The final sequential run used a new empty feed/cache at
`/tmp/folders-cohort-final-20261102-ytqrwm20`. Its exact commands, logs, package
and consumer hashes, and results are retained under `qualification/final/`.
All 45 recorded commands passed. Both strict Release builds passed with zero
warnings/errors, and the unchanged selected-owner catalog model passed 1/1.
Both named Tenants failures passed separately; all 328 distinct selected Tenants
cases passed without skips. McpCli Core passed 735 total (731 passed, four
existing skips), CLI passed 487 (485 passed, two existing skips), and the complete
MCP protocol suite passed 72/72 without skips.

All 17 fresh package DLLs and the embedded analyzer match their producer outputs.
The 12 consumed cache archives match the fresh feed. Final receipts bind actual
AppHost, test, and selected-owner assemblies to the source snapshot. The local
owner commits capture identical source bytes; package provenance remains explicit
about the qualified base revision and source patch. Root commit capture changes
only the three cohort pointers and the listed qualification/tracking artifacts.
Platform revision, direct gitlinks, approved mapping, historical approval bytes,
V1Only, unset T0, null activation authority, and backlog status remain protected.

The two unresolved limits above remain: the older inventory verifier fails on
its pre-existing manifest bytes, and the preserved legacy McpCli enrollment is
not qualified against the new gateway. No publication, deployment, activation,
or operational acceptance is claimed.
