# Story 1.17 private release preparation

The supported `prepare.py` and `verify_patch_api.py` entry points were **retired after review**. Their control flow, evidence reservation, environment handling and capture guarantees were insufficient. This is deletion of supported entry points, not a retroactive repair of historical scripts. Do not execute or reuse the retained runner snapshots. Exact latest entry-point bytes, old-path-to-retained-path hashes and the pre-review artifact manifest remain under the final handoff's `pre-review/` directory.

The existing passing .NET/package results, package bytes and raw command receipts remain unchanged. The final handoff identifies the qualified producer (`story-1-17-release-prerequisites-2026-11-15-final-producer`), consumer (`...-final`), DomainService composition (`...-final-composition`) and supplemental API (`...-final-patch-api`) evidence. Earlier attempts are historical and nonqualifying.

## Manual reproduction boundaries

Use `run-with-temp` for each disposable job. Reconstruct the exact root-declared repository snapshots using the final source identities, working patches and producer source-input archive; never initialize nested submodules. Reserve **all** new owning evidence directories with plain `mkdir` before writing any metadata. Use simple names containing letters, digits, `.`, `_` and `-`; reject empty names, `.`/`..`, path separators and existing paths. Do not use `mkdir -p` to reuse an evidence directory.

Capture live source hashes/HEAD before copying and compare them again after copying; stop on source drift. Derive the compilation inventory and working patch **from the private snapshot**, not from the later live owner. For an owner without initialized nested references, the relevant commands are:

```bash
# Run from that owner's private snapshot; retained paths are outside TMPDIR.
git rev-parse HEAD
git diff HEAD --binary > "$evidence/source.patch"
git ls-files -z -- ':!:references' | xargs -0 sha256sum > "$evidence/snapshot-inputs.sha256"
sha256sum --check "$evidence/snapshot-inputs.sha256"
```

Run required commands under `set -euo pipefail`, retain their exact argv/output/exit status, and stop after any failed restore, build, test, packing, validation or API gate. Require passing, unskipped test cases for **each** requested selector; aggregate totals cannot prove that a missing selector ran. Do not substitute old binaries. The final scanner proof remains **17 cases**; the separate, correctly selected DomainService composition proof is **5 cases**. The old unmatched Client DI selector executed zero cases and proves nothing.

Use a fresh package cache and only NuGet.org plus the specifically verified private feed. Before copying a candidate feed, require successful producer pack/validation/API/test receipts, verify their retained log hashes, require exactly the fourteen manifest package families and their symbols, and compare every archive's size/SHA-256 with `candidate-inventory.json` and the final artifact manifest. Reject unsuccessful producer evidence, missing/extra files and changed archives. Recheck the copied feed before restore; a directory named `5.1.1` is not qualification.

Clear inherited environment inputs for owner tools that spawn their own subprocesses. This allowlist example keeps audits enabled for the existing packer and its children; add only explicitly reviewed connectivity variables if the environment requires them:

```bash
# Inside run-with-temp, from the exact private EventStore snapshot.
env -i PATH="$PATH" HOME="$HOME" TMPDIR="$TMPDIR" TMP="$TMPDIR" TEMP="$TMPDIR" \
  NUGET_PACKAGES="$TMPDIR/packages" NUGET_HTTP_CACHE_PATH="$TMPDIR/http-cache" \
  DOTNET_CLI_HOME="$TMPDIR/dotnet-home" DOTNET_CLI_USE_MSBUILD_SERVER=0 \
  MSBUILDDISABLENODEREUSE=1 UseSharedCompilation=false \
  NuGetAudit=true NuGetAuditMode=all NuGetAuditLevel=low \
  RestoreConfigFile="$TMPDIR/NuGet.Config" HexalithEventStoreVersion=5.1.0 \
  python3 tools/pack-release-packages.py "$TMPDIR/candidate-packages" 5.1.1
```

Use the same private environment and normal owner tools below. Explicit audit properties on direct .NET commands override hostile inherited values. Wait for every owned command/child before workspace cleanup; on interruption terminate only this job's owned process group. Historical imported helper calls do not establish a managed-child guarantee.

## Existing owner commands

EventStore, from its private snapshot:

```bash
python3 tools/validate-release-packages.py "$TMPDIR/candidate-packages" 5.1.1
python3 tools/verify-release-api-compatibility.py "$TMPDIR/candidate-packages" 5.1.1 \
  --candidate-commit fdb59895975e86a1e234325a01237f259cdbd53b \
  --package-catalog "$snapshot/references/Hexalith.Builds/Props/Directory.Packages.props" \
  --work-directory "$TMPDIR/api-compatibility"
```

The existing API tool uses the signed **4.2.0** baseline across net10-to-net11. The additional signed **5.1.0-to-5.1.1** comparison is retained as frozen supplemental evidence, including its exact per-family APICompat command arrays and resolved reference inventories; the retired supplemental runner is not a supported rerun mechanism.

Build each test project individually in Release/package mode before running its assembly:

```bash
dotnet restore "$project" -p:Configuration=Release -p:UseHexalithProjectReferences=false \
  -p:NuGetAudit=true -p:NuGetAuditMode=all -p:NuGetAuditLevel=low \
  --configfile "$TMPDIR/NuGet.Config" -m:1 --disable-build-servers
dotnet build "$project" --configuration Release -p:UseHexalithProjectReferences=false \
  -p:NuGetAudit=true -p:NuGetAuditMode=all -p:NuGetAuditLevel=low \
  -m:1 -warnaserror --disable-build-servers --no-restore
dotnet "$assembly" -noLogo -noColor -class "$selector" -result-xml "$evidence/result.xml"
```

Known repaired Server selectors are explicit; no ignored historical triage JSON is required:

- `Hexalith.EventStore.Server.Tests.Controllers.NativeSourceScannerAuthorizationTests`
- `Hexalith.EventStore.Server.Tests.Controllers.SyntheticOriginalSourceReceiverTests`
- `Hexalith.EventStore.Server.Tests.DomainServices.DevelopmentRouteCatalogParityTests`
- `Hexalith.EventStore.Server.Tests.Routing.DevelopmentGatewayIdentityTests`
- `Hexalith.EventStore.Server.Tests.Routing.HeldMetadataDispatchTests`
- `Hexalith.EventStore.Server.Tests.Streams.*`

The Client scanner selector is `Hexalith.EventStore.Client.Tests.Streams.NativeSourceScannerTests`. Admission uses `Hexalith.EventStore.Client.Tests.Streams.GatewayNativeSourceMetadataAdmissionTests`. DI composition belongs to the **DomainService test project**: `Hexalith.EventStore.DomainService.Tests.NativeSourceScannerCompositionTests`. Original source-CI triage files remain optional diagnostics, not executable inputs.

The declaration generator/check commands are `dotnet run tools/routing-catalog.cs -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -- development . deploy/dapr/eventstore-routing-catalog.json` and the corresponding `check-development` command. Required generation/check failures stop preparation. Run `check-development-runtime .` separately, retain its exit/output and label the existing `unrepresented-unbounded-query-fallback` refusal as a remaining boundary; it is never promoted into runtime success.

Builds uses `pwsh -NoLogo -NoProfile -File Tools/test-authoritative-package-catalog.ps1` and `Tools/validate-platform-version-catalog.ps1`.

Restore/build both complete `Hexalith.Projects.CI.slnx` and `Hexalith.Folders.CI.slnx` with the same explicit Release/package/audit/warnings-as-errors properties above, retaining their full root-declared AppHost inputs. Exact focused consumer test selectors remain in the final `consumers-commands.json` receipts. Folders gates are:

```bash
pwsh -NoLogo -NoProfile -File tests/tools/run-contract-parity-ci-gates.ps1 \
  -SkipRestoreBuild -OutputReportPath "$evidence/parity-report.json"
pwsh -NoLogo -NoProfile -File tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild
```

Require all twelve parity categories and governance to pass. Verify restored candidate archive bytes and every required package DLL against its actual selected package member, including **Folders 1.4.0 `lib/net10.0`** members loaded by Projects' net11 runtime. The corrected final receipt has **32** runtime bindings. Package identity/existence alone is insufficient.

The catalog working files select an **unpublished** private candidate. These results establish no Story 1.17 closure, activation, migration T0 or publication approval. The release workflow rebuilds/publishes all fourteen NuGet families, container images and a GitHub release; it has no package-only dispatch. Normal source review/landing, successful exact-source remote CI and commitlint, and explicit approval for the exact future action remain required.
