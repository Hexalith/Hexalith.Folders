---
title: 'Complete the next ten actionable deferred items'
type: 'chore'
created: '2026-11-24'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete ten actionable open entries in ledger order, with a separate validated commit and ledger update for each item. Record why earlier entries were skipped. Run the full build and individual project test suites before pushing to main.

</frozen-after-approval>

## Implementation Notes

The ledger has no priority field. Existing batch records use ledger order.

| Item | Change and verification |
| --- | --- |
| DW-34 | Pass archive cancellation from the processor through the gate to repository load and append. The actor repository folds synchronously. The in-memory append checks cancellation before publishing state and its fingerprint atomically. Exercise pre-cancellation and same-key retry. |
| DW-42 | Resolve the lifecycle query handler per request. Verify a host can supply a scoped reader and retain the shared built-in read models. |
| DW-62 | Pin the access-operation field inventory and verify significant fields affect the replay fingerprint. A new field requires a deliberate compatibility decision. |
| DW-66 | Enforce the required create ACL action during construction and deserialization. Exercise missing, null, and round-tripped input. |
| DW-88 | Resolve request and response schema references before generating the parity oracle. Exercise broken nested references and the existing byte-for-byte oracle check. |
| DW-92 | Give extended scalar types explicit formatting and pin their existing canonical bytes. |
| DW-225 | Add a cancellation overload for SDK hashing. Exercise pre-cancellation, interrupted enumeration and disposal, and hash equivalence. |
| DW-260 | Preserve exact organization ACL duplicates by value and reject conflicting same-tuple records. Verify the first operation is retained and existing batch and replay outcomes hold. |
| DW-266 | Validate explicit enabled Dapr evidence allow-lists at host startup. Exercise missing and blank lists and confirm that explicit lists replace defaults. |
| DW-278 | Check for the accepted access fingerprint before ACL evaluation and repository access. Exercise accepted, malformed, and replay paths in the access-gate tests. |

### Entries skipped

- DW-1, DW-2. Production proofs still require Stories 6.12–6.13 and 4.18. Governed lifecycle Story 4.22 and durable projection Story 12.2 have yet to finish.
- DW-3, DW-4. The scope note was superseded and the cited NFR numbers now have different meanings.
- DW-5, DW-6, DW-7. Command names are canonical, the authoritative tenant is normalized by layered authorization, and the rejection event has no supported deserialization path through its private constructor.
- DW-20, DW-41. DW-9 already added cancellation to the processor evidence path, and Server already registers the EventStore lifecycle reader.
- DW-24, DW-27, DW-37. There is no second validator version, positional production construction of folder state, or unarchive operation, respectively.
- DW-38, DW-40, DW-56, DW-57, DW-117, DW-130, DW-131, DW-138, DW-139, DW-144, DW-147, DW-148, DW-150, DW-151, DW-152, DW-157, DW-158 and DW-179. Their changes require a governed Contract Spine or vocabulary revision. DW-39's older vocabulary decision is superseded by the current authorization matrix. DW-143 already declares HTTP 429 in v2.
- DW-58, DW-59. Production uses the EventStore permission reader, and no identity provider rule authorizes folding these principal IDs.
- DW-61. The production access-mutation service already observes cancellation. The legacy access gate uses a synchronous repository.
- DW-76, DW-77. Workflow ownership sits with CI/CD. The shallow checkout has no history-dependent consumer.
- DW-89, DW-91. The oracle uses the declared correlation header and no alternate path has been approved. The parser owns the selected raw control-character rejection policy.
- DW-103, DW-173. The scaffold tests intentionally retain an independently authored allowed graph.
- DW-166, DW-168, DW-171. Existing conventions cover async names and analyzer warnings. Local PDBs with machine paths remain appropriate for debugging.
- DW-184, DW-227, DW-233, DW-235, DW-236, DW-240. The generator uses local artifacts, the only approved explicit-null case has presence tracking, the scanner covers the required Unix roots, the corpus protocol pins its version, the active scanner supports multiple readers, and diagnostic equality already includes the output channel.
- DW-256, DW-259. Tenant event tombstones still need a retention decision. Organization ACL enrollment and its durable authorization proof belong to the production owner stories.
- DW-263, DW-275. The allow-list rejects the unsupported token, and the archive gate cannot enter its denial mapper with an allowed tenant outcome.
- DW-267. The current folder-scoped bounded-read policy intentionally obtains ACL evidence. The fixed-size tenant-only path and its permitted metadata need to be specified separately before changing that behavior.

## Review Triage Log

- Medium, fixed. The archive integration test makes cancellation occur inside the repository append reached by the production `/process` route. It verifies persisted state and the fingerprint ledger.
- Medium, fixed. The oracle checks request, response, parameter, and response-header schema references. Local pointers accept valid encodings and string-tagged keys. Invalid escapes, invalid indices, mistyped request-body references, and unsupported schema resource scopes fail before emission.
- Medium, fixed. Scalar schema targets require a valid YAML Boolean. Quoted strings and explicitly invalid Boolean values fail.
- Medium, fixed. Flag-only Dapr policy configuration keeps the built-in allow-lists. Explicit lists own both allow-lists and are checked at startup.
- Low, accepted. An in-memory repository needs its original writable singleton seed reader. The scoped query handler supports request-scoped readers in hosts that supply their own repository or use the query registration alone.
- Low, accepted. DW-225 applies to the public hasher. The generated convenience methods retain their existing signatures and invoke it synchronously.
- Verified. A compatibility probe matched the original canonical form for all 65,536 binary16 inputs and representative values for the other extended scalars.
- Fixed during project verification. The nested SDK probe locates the NSwag command amid MSBuild startup diagnostics. Its standalone program still exercises the original tokenless API.

## Verification

- `dotnet build Hexalith.Folders.slnx -c Debug --no-restore -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false` succeeded with zero warnings and errors.
- The rebuilt SDK test project passed all 446 tests. All 76 Python tooling tests and all 63 browser tests passed.
- The standard test runs for the 14 non-Server projects finished with zero failures after the SDK rerun. The two Forgejo live tests and ten live AppHost cases have their usual opt-in conditions.
- The full Server issuer fixture reached its default 600-second test-child deadline. An isolated retry with the same complete assembly and fixture and `--child-timeout 1500` passed all 1,477 tests.
- The AppHost workload issuer fixtures passed the five component tests and the run-owned enrollment test.
- `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true dotnet tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net11.0/Hexalith.Folders.AppHost.Tests.dll -noLogo -noColor -explicit off` exited 1. The opt-in suite reported six failures and two skips. FR58 hit its prerequisite gate. Durability fixtures encountered Dapr `ERR_DIRECT_INVOKE` during Tenants bootstrap, `CreateTenant` HTTP 500 responses, an initialization timeout, and an incomplete or inaccessible owner catalog. The two cross-process diagnostics skipped on sidecar discovery. Story 12.2 already documents the bootstrap and tenant-creation failures.
