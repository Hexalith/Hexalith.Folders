---
title: 'Complete the next seven deferred work items in priority order (2026-11-25)'
type: 'chore'
created: '2026-11-25'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Implement the next seven items from deferred-work.md in priority order. Implement each fully with tests where applicable, mark it done in the ledger, and commit it separately with a message that references the item. Skip blocked or obsolete items with a recorded reason and take the next one until seven are complete. Then run the full build and test suite, fix any failures, push the commits directly to main, and report completed items, skipped items with reasons, and the number of deferred items still open.

</frozen-after-approval>

## Implementation Notes

### Priority rule

The rule from [the previous batch](spec-next-seven-deferred-work-items-2026-11-24.md#priority-rule) applies: open `high` entries, then `medium`, then untagged entries in ledger order (legacy bullets included), then `low`. Every open `high` and `medium` entry was skipped on 2026-11-24 for reasons that still hold, and that batch worked the untagged bullets up to the `ForDomain` bullet. This batch continues from the next bullet.

### Completed items

Line numbers are ledger lines at the start of this batch.

| Ledger line | Item | Change |
| --- | --- | --- |
| L2663 | `IsSafeDiagnosticId` and `SanitizeCorrelationId` have no direct tests | Direct unit tests cover both `FolderCanonicalPathIdentifier` helpers, including the 256-character boundary. |
| L2667 | Client identity-header maps copied across endpoint files | `FolderClientIdentityValues` builds the tenant and principal maps for the four endpoint groups. The all-values collectors in the PD10 v2 seam and the mutation paths stay separate. |
| L2682 | `project-context.md` still says to keep skipped E2E placeholders | The bullet describes the live, blocking E2E lane without test counts. Item L2828 later added the interactive-fixture rule. |
| L2742 | Does `CreateRepositoryBackedFolder` create a folder? | No. The spine, the creation service and the aggregate all treat the folder as existing, so the matrix row is correct. A new creation gate test pins the folder operation scope. |
| L2750 | No check ties gap G4 to `EffectivePermissionsActionCatalog` | `EffectivePermissionsActionCatalogMatrixGapTests` pins every G4 claim, the exact action set and every action's level. It found that G4 counts 22 actions while the catalog holds 24, which is a new ledger entry. |
| L2828 | No console test runs a server-handled interaction after the circuit connects | `FolderNavigationInteractionTests` opens a folder through a live circuit on `InteractiveConsoleHostFixture`, using `BlazorCircuit` to wait for the first render. See the finding below. |
| L2836 | Effective-permission evidence ignores the scope of the returned snapshot | The permission evidence provider returns `Malformed`, and the effective-permissions query handler returns `ReadModelUnavailable`, when the reader's snapshot names another tenant or folder. |

### Skipped entries

- L2674 and L2678: doc-literal pins and repository-litter guards. Story 11.16 (backlog) owns the redesign of package, E2E-count and generated-inventory gates, and its AC forbids hard-coded counts. A new literal pin would work against it.
- L2686, L2690 and L2694: complete OQ2, OQ3 and OQ4. All three are accepted. `planning-story-manifest.yaml` lists OQ1, OQ2 and OQ4 as accepted terminal references, and the OQ3 matrix 2.0.0 was re-approved on 2026-09-22.
- L2698 and L2702: C7 clock-skew policy and tenant timing overrides. No renewal, revocation or override runtime exists yet. Story 4.23 (backlog) is the first consumer.
- L2706: the EventStore gitlink move. It describes September working-tree state; the gitlink has moved many times since.
- L2716: typed Problem Details with a `success` category. Already fixed. The generic projection accepts only declared tuples, and none uses `success`. `GenericProblemProjectionValidatesDeclaredOptionalFieldsAndClosedScalars` rejects that body. CLI and MCP read only the projected problem.
- L2726, L2730, L2734 and L2738: `/pushall` defects. The canonical skill lives in Hexalith.AI.Tools, which is already ahead of the Folders mirror. Folders keeps three copies of it. A fix belongs upstream, then a re-mirror.
- L2746: OQ status vocabulary. The manifest no longer has `status: open` rows for OQ1 or OQ2.
- L2754, L2758 and L2766: NFR34, NFR49 and PD6 scope. They change hash-pinned NFR rows or a PD decision, which PD6 reserves for one lockstep relock.
- L2762: the GitHub `capability_profile_schema` row. It edits the approval-bound provider catalog.
- L2772 and L2784: C3, OQ3, OQ4 and scaffold reconciliation for CI alignment. Those approvals are complete, and the governance and scaffold tests passed in the 2026-11-24 full run.
- L2776 and L2780: parity 403s and UI E2E `h1` failures. The integration and UI E2E projects passed in full on 2026-11-24; this batch's full run rechecks them.
- L2788: self-invalidating baseline-CI gate reports. Checks on story evidence do not belong in module CI under the current Hexalith.CiCd policy.
- L2792: runtime Problem Details coverage of the PD10 vocabulary. The v2 seam now checks every outgoing problem against the generated operation-bound runtime catalog. Any undeclared tuple becomes the authority-unavailable problem, so no undeclared value reaches callers.
- L2796: the CommunityToolkit Dapr pin. Folders' `Directory.Packages.props` now resets that project-conditioned pin to the Aspire 17 preview.
- L2800 to L2824: Story 1.17 owner dispositions. Each needs an owner decision.
- L2832: the NuGet preflight entry records a fix that was already made.

### Findings beyond the selected items

- The E2E host fixtures boot in the `Test` environment and never load the static web assets manifest. Every stylesheet and `_framework/blazor.web.js` comes back as an empty 200, so no Blazor circuit starts. Until this batch, the axe, responsive, zoom and focus checks all measured unstyled prerendered markup. `InteractiveConsoleHostFixture` fixes this for the new test only. Fixing it for the whole lane changes what the other 63 tests measure, so it is a ledger entry, not a silent switch.
- In that host, Chromium refuses the loopback circuit WebSocket (`ERR_BLOCKED_BY_LOCAL_NETWORK_ACCESS_CHECKS`), and SignalR falls back to long polling. `BlazorCircuit` watches both transports.
- `App.razor` links `_content/Hexalith.FrontComposer.Shell/Hexalith.FrontComposer.Shell.styles.css`, which returns 404. The library ships `Hexalith.FrontComposer.Shell.bundle.scp.css`. This is ledgered as unverified in a deployed console.

Each code commit regenerates the pending PD10 v2 manifest. New source and test files under coverage roots are listed in `scripts/pd10-v2-story-owned-paths.txt`. Review fixes were folded into their item commits with an autosquash rebase that regenerated the manifest after every commit. A fresh generation matches the committed manifest at each of the seven commits.

## Review Triage Log

One Blind Hunter review covered the seven commits (76 kB, floor 9) and returned 13 findings.

- Low, fixed. The identity-map commit and ledger claimed one place for the source keys, but the PD10 v2 seam and the mutation paths keep all-values collectors. The message and resolution now say so.
- Maybe-false (medium if true), deferred. The first-safe-value maps ignore a second foreign tenant header, unsafe values and `?principalId=`, which the mutation collectors flag. This behavior predates the batch, and neither path takes authority from these values. A ledger entry asks for a detection-rule decision.
- Low, fixed. Both identifier helpers now have 256-character acceptance tests. Low, rejected: the doc's "trimmed" wording and the unreachable `Trim` and `IsSafeHeaderValue` branches. They are harmless, and the detector's markers are already pinned by its own tests.
- Medium, fixed. The repository-backed settlement relied on reading the code. `CreationShouldAuthorizeAgainstTheRequestedExistingFolder` now pins the folder operation scope.
- Medium, fixed. The G4 pin checked only the recorded drifts. It now pins every action's level as well.
- Low, fixed. The L2750 resolution notes that `manage_organization_acl` maps like the tenant-level `create_folder` and that `read_task_status` reads through `read_metadata`, so neither later addition is classified yet.
- Low, fixed. The E2E README layout and `tests/tools/run-e2e-ci-gates.ps1` still carried old counts. The script comment no longer states counts.
- Medium, fixed. The circuit wait was private to one test and contradicted the README wait rules. It now lives in `BlazorCircuit`, and the README explains its 30-second bound. False: the reviewer said `PopulatedConsoleHostFixture`'s "kept identical" note was stale, but that note lists only environment, scopes, port and probe, which still match.
- Low, fixed. The static-assets ledger entry now names the accessibility and E2E gate documents and the hash-pinned NFR65, NFR68 and NFR69 rows that present this coverage.
- Low, fixed. The project-context bullet now says the default fixtures are prerender-only and names the interactive fixture and circuit wait.
- Medium, fixed. `EffectivePermissionsQueryHandler` read the same reader without a scope check. It now returns `ReadModelUnavailable` without echoing the foreign snapshot. All three new handler cases fail without the check.
- Low, fixed: a foreign-tenant bounded-stale case was added. Low, rejected: logging a scope mismatch would add a logger dependency to the provider. The unchecked strict-stale path returns no grant, so its classification difference is cosmetic.
- False at push time. Three ledger entries cite this spec as their source, and this spec is committed with the batch record.
- Deferred idea. The seam's private `ClientControlledValues` appears to duplicate `HistoricalMutationAuthorization.ClientControlledValues`.

## Verification

- `dotnet build Hexalith.Folders.slnx -c Debug --no-restore -m:1 -nr:false -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false` succeeded with 0 warnings and 0 errors at the final code commit.
- All 15 test projects passed when their built assemblies were run directly. The Server project ran through the Keycloak component verifier with `--child-timeout 1500` and passed all 1,527 tests as a complete project. The core project passed 2,579 tests with 2 opt-in Forgejo skips, and AppHost passed with its 10 opt-in live skips. UI E2E passed 64 of 64. The new circuit test passed every run: six runs of its class, two of them after the `BlazorCircuit` refactor, plus both full-lane runs.
- `python3 -m pytest -q scripts/tests` passed 76 tests.
- `dotnet format whitespace Hexalith.Folders.CI.slnx --verify-no-changes --no-restore --include ./src ./tests ./samples` exits 0. Without `--include`, it fails only on line endings in `references/Hexalith.Tenants` sources, which Folders does not own.
- Every commit's pending PD10 manifest matches a fresh generation, checked in a detached worktree per commit.
- `python3 references/Hexalith.Builds/check.py` does not apply: Folders has not adopted Hexalith.CiCd, so `.github/hexalith-ci.json` is absent.
