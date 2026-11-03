# Story 12.2 source-head and mutation-budget qualification

**Status: in progress.** The approved independent source-head composition and two-second enabled
mutation-evidence budget are implemented. Current strict live acceptance remains failed at the
unchanged five-minute tenant-freshness policy. No fixture grant exception has been applied.

Verify this sealed packet without running services:

```text
python3 verify.py
```

Run from this directory. `seal.json` binds every other packet file. The verifier also checks exact
deduplicated source-tree identities, command drift/cleanup invariants, test counts and open gates.

| Current full consumer selection | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Domain | 2379 | 0 | 2 operator lanes |
| Server | 1354 | 0 | 0 |
| Workers | 162 | 0 | 0 |
| Integration | 854 | 0 | 0 |
| Gateway | 123 | 0 | 0 |
| Contracts | 636 | 0 | 0 |
| Client generation | 75 | 0 | 0 |

These phase-attributed full selections total 5,583 passes and two operator skips. Later fixture-only
lifecycle observation passes 16 focused cases, including nine new existing-singleton controls;
overlapping focused selections are not added to the full-suite count. The approved mutation-budget
selection passes 83 cases, including delayed valid authority and exhausted/noncooperative refusal.

All five frozen matrix rows have executed passing component coverage. Final Debug/source runtime
and Memories builds pass. Ninety-two selected modules plus the observer match their source/PDB
hashes, selected SDK copies have one hash/MVID variant per assembly, and selected Hexalith assets
are project references. Original strict/capacity bodies, frozen intent and historical PD10 bytes
remain unchanged. Source/head/runtime drift is zero in every retained parent command.

Eight strict attempts are retained as failures. The latest runs 442.310 seconds and passes bootstrap,
provider refusals and real preparation, clean task release, six-concern rebuild and TTL assertions,
interrupted-generation recovery, concurrent Workers and every main-restart process-ID assertion.
Both EventStore stops reach `ApplicationStopped`; an unresolved historical shutdown failure is not
reclassified as fixed. The earlier task corruption probe has fresh tenant evidence and reaches the
protected read model. The later post-restart read is denied before that read model: 17 actual 503s
show `TenantAccessFreshness/tenant_projection_stale`, beginning at age 309 seconds and continuing
through age 379. The original retry budget then cancels the test. All owned resources are removed.

[The fixture prerequisite proposal](../../proposal-12-2-live-tenant-freshness-prerequisite.md)
requests a fixture-only `tenants:update` grant and real authenticated `TenantUpdated` maintenance,
with unchanged authority semantics and normal source/projection verification. This would maintain
an active test tenant; it would not change idle-tenant policy. The current administrator lacks that
exact permission, and the approved scope excludes grant expansion. Approval is pending.

Separate gates remain open:

- SDK full selections: 12,599 passes, three retained-catalog parity failures and 30 skips. Current
  clean Tenants owners add a query and explicit admissions relative to the retained SDK catalog;
  no executed pristine-baseline result is claimed. Fixed catalog bytes and assertions are preserved.
  Skips comprise 25 existing DW1 red-phase server specs, two isolated-Dapr recovery cases, one
  content-live case and two release-package contract cases.
- Exact Release conformance command: NU1109, because Tenants.Client 7.0.0 requires EventStore
  Client/Contracts >= 4.0.4 while central pins remain 4.0.0. Later Debug source restore passes.
- Current-cohort live capacity, PD10/governance, SM4 and independent whole-story review.

`audit/qualification-summary.json` records phase-attributed outcomes; `audit/matrix-test-audit.json`
maps executed methods to the frozen rows. `commands/` retains sanitized receipts; `source/` binds
each phase to exact source maps; `runtime/` records actual loaded modules and owned resources.
`audit/` retains source/PDB/assets, lifecycle, cleanup and preservation records. `focused/` contains
implementation handoffs and closed-metadata diagnoses, including the exact eighth failure.
`patches/` retains reconstructible source snapshots for phases 2–8. `decisions/` archives the approved
and proposed scope documents and current spec; their original relative links refer to their original
repository locations.

Raw logs/XML and disposable credentials remain private. Test exports retain static type/method,
result/time, case ordinal and original display-name SHA-256, excluding parameter display payloads.
Repeated source maps are deduplicated by canonical JSON SHA-256. Privacy-pattern checks supplement
the closed-metadata exporter; no production confidentiality qualification is claimed.

The earlier sealed Nov2 packet remains unchanged; its historical pending-decision field predates
the later approvals. This packet does not authorize staging, commits, pushes, dependency/gitlink
updates, publication, deployment or public v2 exposure. Story/sprint remain in progress.
