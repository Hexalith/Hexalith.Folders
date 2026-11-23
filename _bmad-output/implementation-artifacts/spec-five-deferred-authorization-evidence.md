---
title: 'Resolve five deferred authorization evidence items'
type: 'bugfix'
created: '2026-11-23'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Ensure that folder authorization uses stable permission evidence and rejects invalid tenant evidence before accessing a protected resource. Resolve five open items from the Folders deferred ledger.

- DW-261. Given an active tenant projection observed one tick in the future, when the organization ACL command goes through the tenant authorizer and gate, then it returns malformed evidence and performs zero stream operations.
- DW-264. Given caller-owned permission tokens, when allowed evidence is built, then the normalized ordinal permission set is an immutable snapshot.
- DW-265. Given folder permission evidence observed before, at, or one tick after the clock, when timestamps use different UTC offsets, then the permission provider accepts the first two and rejects the future instant.
- DW-273. Given a blank client tenant-source key, when folder access is requested, then the gate returns tenant mismatch before it constructs a stream, even if the value is absent or agrees with the authoritative tenant.
- DW-340. Given missing tenant or principal authority in the four sibling lifecycle query fixtures, when a test requests default allowed claims, then fixture construction rejects the input. Explicit evidence remains available for denial scenarios and valid defaults use the operation's action.

</frozen-after-approval>

## Implementation Notes

- Starting Folders revision is `46944711ac1996df956830ed563b52b99e6a9465`. The workspace includes the latest fetched `main` from each direct reference.
- The tenant and folder permission tests use real authorization code with fixed clocks. The organization ACL test records idempotency lookups, stream construction, loads and append attempts. The four query fixtures use one authority validator and retain their individual permission tokens.
- The direct folder access gate returns its existing tenant-mismatch result. The layered mutation service uses its existing claim-denial result when a client source has no name. Both paths reject before reading protected folder data.
- Before implementation, eight of twelve new cases failed. After review, all seventeen focused cases passed. The unfiltered core assembly passed 2,504 tests with two opt-in Forgejo tests skipped. All 74 CI tooling tests passed with Linux PowerShell and a standalone temporary directory.
- All 753 contract tests and all 16 selected server authorization and command-processor tests passed. The contract run includes 21 PD10 conformance tests. A second generation of the pending source inventory matched byte for byte. All 14 changed C# files passed whitespace verification.
- The Release build of `Hexalith.Folders.CI.slnx` passed with zero warnings or errors in package mode. The separate live AppHost smoke check stopped while connecting because the runtime attempted to write to the read-only user Aspire directory. The authorization tests exercise the domain, server and mutation services directly.

## Review Triage Log

- Medium, fixed. The layered mutation service also accepts client-supplied tenant sources. Its claim check now rejects blank source names before it reads folder permissions or the folder stream.
- Medium, fixed. The public evidence constructor and record copy now snapshot assigned permission sets. Factory, constructor and copy regressions verify immutability.
- Medium, fixed. The accepted grant test and the mutation reauthorization test supply a named matching tenant and a named null value. Both still reach the expected authorized path.
- Low, fixed. The test uses UTC−05:00 for the instant of the US Eastern autumn clock change and UTC−04:00 for the preceding tick.
