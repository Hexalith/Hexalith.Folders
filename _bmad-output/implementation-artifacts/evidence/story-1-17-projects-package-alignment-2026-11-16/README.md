# Projects package alignment — 2026-11-16

The user's follow-up fix authorizes the two previously identified package repairs. Projects now selects McpCli.Abstractions 3.0.0 and its four applicable release manifest closures expect that version plus the existing EventStore.Contracts 5.1.1 selection. Production C# contracts, wire shapes, validation code, gate policies and workflows are unchanged. The earlier Conversations fixture repair remains intact.

Actual results: 20/20 Python fixtures; eight individual product projects report 2,837 passed, zero failed and one existing isolated-Dapr environment skip. Strict validation accepts all five real diagnostic packages; five isolated API consumers restore from their private feed/cache and compile. The full CI solution build succeeds with zero warnings/errors. OpenAPI fingerprinting passes 101 focused cases; FrontComposer inspect passes without warnings; both exact McpCli decoration/serialization cases pass. Focused cases are subsets of the full project totals. The actual Server output's Folders Client/Contracts DLLs match the protected signed 2.0.0 payload hashes. No new immutable image, live served identity or distributed authority qualification is claimed.

Source is base Projects bf9f98969efa036fd60700c627e712b4375e654a plus the retained uncommitted source patch and hashed copied inputs. Diagnostic nuspec RepositoryCommit records that base. This does not represent a new committed candidate; exact-source CI remains for separately authorized landing. Historical Story 6.6 catalog/enrollment and source-only gateway matrices retain their prior scope.

## Commands and evidence

All disposable jobs use run-with-temp and fresh output directories; outputs/logs are retained here. Each runner refuses an existing output directory. To reproduce, use a NEW absolute evidence destination:

```bash
run-with-temp python3 _bmad-output/implementation-artifacts/evidence/story-1-17-projects-package-alignment-2026-11-16/verify-product.py --output <NEW-absolute-directory>
run-with-temp python3 _bmad-output/implementation-artifacts/evidence/story-1-17-projects-package-alignment-2026-11-16/verify-packages.py --output <NEW-absolute-directory>
run-with-temp python3 _bmad-output/implementation-artifacts/evidence/story-1-17-projects-package-alignment-2026-11-16/verify-current-product.py --output <NEW-absolute-directory>
run-with-temp python3 _bmad-output/implementation-artifacts/evidence/story-1-17-projects-package-alignment-2026-11-16/verify-integration.py --output <NEW-absolute-directory>
```

`product/` records seven unit assemblies and an initial incomplete snapshot's solution/Integration failures. `product-qualified/` materializes ordinary source/build inputs from the existing permitted root owner object databases at Projects' exact declared revisions, then records the passing full solution/generated/decoration checks. Its initial Integration model run failed six cases because it still omitted Folders' source project metadata. `integration-qualified/` adds that exact declared Folders source from the owning Folders object database and passes all 125 cases; these are structural/model tests, not the distributed runtime lane. No nested Git submodule is initialized. `dependency-inputs.json` records every selected dependency file hash and archive identity.

`packages/` records the first strict validation pass followed by an isolated consumer provenance failure because the disposable copy had no Git metadata. The retained corrected runner's `packages-qualified/` repeats the same source bytes and passes every stage. Consumer provenance lookup uses the existing Projects Git metadata read-only, with GIT_OPTIONAL_LOCKS=0; source/patch hashes remain explicit, validators are unmodified and no Git candidate is fabricated. Actual normalized nuspecs and the dependency report are retained before scratch cleanup. Original diagnostic failures remain visible and are not called product regressions or passed gates.

`test-summary.json` records exact per-project outcomes and the unchanged skip reason/environment keys. `mcpcli-api-diff.json` is dotnet-inspect's complete public API shape comparison (zero changes); it does not prove arbitrary runtime/enrollment compatibility. Every command, environment SDK observation and log hash is retained in the corresponding ledger. `receipt.json` binds the updated closure handoff, owning Projects next action and source patch. Their prior bytes are retained. Previous packets and all approval/activation/tracking evidence remain unchanged; no audit refresh or repeat publication occurred.

After sealing, verify the exact current tuple:

```bash
run-with-temp python3 -O _bmad-output/implementation-artifacts/evidence/story-1-17-projects-package-alignment-2026-11-16/verify.py
```

For historical sealed-byte integrity after later authorized source/handoff changes, add `--packet-only`. SHA256SUMS must exist and covers every packet member except itself and the verifier's `verification.log`. No commit, push, publish, deploy, activate, parent tracker change or new approval is performed. V1Only, unset T0, null activation authority and fourteen unexecuted staging checks remain recorded controls.

## Review corrections and replay limits

The retained `.executed.py` runner versions document the original orchestration behavior (wrapper success did not imply every stage passed). Future runners now propagate required command failures. Future `verify-product.py` runs all seven unit assemblies; `verify-current-product.py` includes the exact declared Folders project metadata and can reproduce the complete solution/Integration/generated checks without the earlier six missing-input failures. The separate Integration runner remains available. Focused tests are not counted twice.

The verifier recomputes ordinary dependency archives/file hashes directly from the permitted owners' declared Git revisions. `builds-input-reconstruction.json` is an explicitly post-run reconstruction of the deterministic Builds archive command used in those runs; future runners capture archive/extracted file hashes in-run. `postcheck-build-environment.json` records an empty relevant inherited-property allowlist after the checks, not during them. Original ledgers' empty suppression/override lists describe explicit command flags, not a complete effective environment guarantee. Future runners retain the allowlist and reject conflicting audit/diagnostic/version settings. No arbitrary environment or secret values are captured.

`api-inspection-command.json` records the fresh input-bound API comparison's invocation, tool version, DLL/package hashes and result. McpCli 2.0.1 supplies net10 assets; 3.0.0 supplies net11 assets, consistent with Projects' unchanged net11 target. `fixture-command.json` records a fresh 20-test fixture execution with timestamps, environment/cwd, exit status and source hashes; the initial fixture log remains historical.

Diagnostic `.nupkg` archives were deliberately removed with scratch cleanup. Their archive hashes, strict archive/compile-asset validation and isolated consumer compilations are **historical execution evidence**; the verifier checks their retained logs and actual normalized nuspecs, and does not rehash unavailable archives or replay compilation against them. Reproduction creates a fresh diagnostic cohort. No disposable package or archive digest is promoted to an immutable release/host identity.
