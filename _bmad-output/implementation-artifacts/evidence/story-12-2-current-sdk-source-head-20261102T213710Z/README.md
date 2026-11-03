# Story 12.2 current SDK qualification — 2026-11-02

Story/sprint remain **in-progress**. Strict real-provider acceptance, SM4 and whole-story review remain open. The [source-head prerequisite](../../proposal-12-2-current-sdk-source-head-transport.md) is proposed and unapproved.

This packet binds Folders 313f0f32bd31622fdd4f2f0bcb716e496ec301d0 and EventStore a9317e4b228adfbcdd6686a2f86171b464f34c06 plus the recorded working-tree changes. No branch, commit, publication, deployment, dependency pin or gitlink update was performed.

## Implemented and checked

- EventStore catalog decoding reuses deterministic validation after exact private byte equality, while reparsing independent models and checking supplied roots. On the tracked 44,075-byte catalog, 500 warm decodes took 381.122 ms versus 2,184.545 ms, with 88.7% fewer allocated bytes. This local probe does not establish SM4.
- The disposable real-issuer fixture supplies its designated administrator's current permissions and canonical scopes. Nonadministrators retain their original grants. Retained administrator source uses the existing signed owner/native route.
- The public command validator accepts canonical platform namespace syntax. Production MVC/JWT/catalog controls preserve human-bootstrap 403, exact permission/ingress checks and zero denied effects.
- Seven diagnostic controls prove the source-head raw-actor refusal and preserve independently supplied zero separately from unavailable evidence. They do not qualify live absence or new enrollment.
- The pending PD10 inventory includes the three diagnostic helpers. Historical conformance SHA-256 remains 6289c143af409f72e53aff555310d37faa25da0bb852488fe9a2f5ab7196a286; v2 remains disabled and pending A6b.

Focused stages passed: catalog 202/202; native/catalog 151/151; fixture 94/94; Tenants owner 69/69; platform validator 51/51; source-head controls 7/7. Each result binds its respective source/output stage; no broad SDK acceptance is claimed.

## Final consumer checks

| Check | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Domain | 2364 | 0 | 2 existing operator lanes |
| Server | 1331 | 0 | 0 |
| Workers | 162 | 0 | 0 |
| Integration | 854 | 0 | 0 |
| Gateway | 123 | 0 | 0 |
| Contracts after pending-inventory correction | 636 | 0 | 0 |
| Client generation fallback | 75 | 0 | 0 |
| **Total** | **5545** | **0** | **2** |

The first Contracts run had three missing-inventory failures introduced by the diagnostic helpers. Updating the allowlist and pending manifest restored 636/636. Earlier failures remain recorded.

The exact command pwsh -NoLogo -NoProfile -File tests/tools/run-contract-spine-gates.ps1 failed at package restore with NU1102: Hexalith.Tenants.Client >= 7.0.0 is required, while nuget.org reports nearest version 6.0.0. No Release build/test ran. The focused Debug Contracts/client-generation fallback passed, and the Debug source graph was restored afterward. No package gate was overridden.

## Strict live attempts

All attempts ran the unchanged OperationalStatusDurabilityAcceptanceTests class with explicit opt-in. Exact argv, UTC times, exit status and private output hashes are under commands/.

| Attempt / isolated port | Result | Established stopping point |
| --- | --- | --- |
| 1 / 36011 | SIGKILL, exit -9, no xUnit result | Fixture initialization terminated; cause unknown, no OOM record. Exact leftovers were subsequently removed. |
| 2 / 36012 | 1 failed, 0 errors/skips, 132.397 s | Owner receipt wait: imported administrator lacked required bootstrap permission. |
| 3 / 36013 | 1 failed, 0 errors/skips, 42.929 s | Genuine delivery/terminal status reached; human-bootstrap probe received model-validation 400 instead of required 403. |
| 4 / 36014 | 1 failed, 0 errors/skips, 233.011 s | Verified bootstrap, tenant creation/enrollment and organization grants passed. First unknown-profile provider-binding PUT ended prematurely. |

Attempt 4's BOOTSTRAP_VERIFIED observation proves genuine 202/Completed, all four credential/proof refusals, original retained administrator source and projected authority. It does not prove durability acceptance.

The independent organization-head invocation was refused by the mandatory raw-actor guard before transport; /process returned 503 and the actor rejected the command. The phrase repeats in three fields of one structured event, not three established requests. This proves source-head incompatibility, **not** the cause of the outer ResponseEnded error. That cause remains separately unresolved. The failed request was provider configuration, not folder creation.

The current raw SDK metadata adapter is also refused. The gateway alternative requires separate source-only identity/key/grant enrollment and cannot convert null/unsupported proof to authoritative zero. This distinct capability exceeds the approved host-enrollment exception. No new production reader, enrollment or guard exception was implemented.

## Audit and reuse limits

- Pre-live audit: 92 selected Hexalith modules, matching PE/PDB identities, no ordinary-source checksum mismatch/missing file, consistent SDK copies and project-only Hexalith asset edges.
- Actual managed/native mappings, executable/container identities and live source/head snapshots are under runtime/ and source/. No live source/head/runtime file drift was observed.
- Original strict/capacity bodies and frozen intent are unchanged; change-audit.json binds their hashes. The two changes.patch files record this run's changes.
- matrix-audit.json binds executed component coverage for all five frozen rows. Strict live qualification remains false.
- All observed owned processes, containers, fixture and mTLS directories are gone. Attempt 1's follow-up cleanup is separate from its interruption receipt. Unrelated containers were untouched.
- Raw logs/XML remain private in /tmp/story12-2-resume-20261102-1EZDgA; only safe metadata, summaries and hashes are copied. Runtime credentials/protected bodies are excluded.

Run python3 verify.py here to check the artifact seal. Broad SDK, Release/package, current live capacity identity, SM4 and independent whole-story review retain separate open dispositions. Resume from the linked owner proposal without weakening source/proof/final observations, denials, assertions or deadlines.

