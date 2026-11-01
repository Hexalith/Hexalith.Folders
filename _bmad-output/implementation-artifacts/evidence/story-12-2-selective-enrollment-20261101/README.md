# Story 12.2 selective enrollment investigation

Current-source integrated acceptance is blocked by a conflict between Folders' preserved bespoke
endpoint behavior and the newer SDK's mandatory HTTP endpoint classification. The
[concrete migration proposal](../../proposal-12-2-current-sdk-security-enrollment.md) identifies the
owner decision, affected route groups/files, observable behavior changes and verification gates.
No product or SDK code is changed by this packet.

`reproduce.py` checks the prior packet's 17,022 source/config inputs, 406 original runtime/config
artifacts and 403 corrected artifacts before and after execution. Both audits have zero differences.
It also binds all 138 files in the actual probe output directory before execution and checks them
again afterward; the exact count is authoritative in `probe-runtime-after.json`. No build occurs.
`integration-inputs.json` freezes the exact owning implementation and approved security specs.
`heads.json` records full current Folders/EventStore revisions directly from Git. The prior selected
candidate and all previous results remain historical evidence; no prior passing count is newly executed.

| Command receipt | Fresh result |
| --- | --- |
| `production-discovery.json` | Actual production-composition test: 0 passed, 1 failed, 0 skipped, 0.334 xUnit seconds. Missing `EventStoreHttpEndpointSource` during pipeline construction. |
| `registration-original.json` | Existing diagnostic harness, unchanged composition: exit 1, same missing-service exception with source stack and loaded Server/DomainService/ServiceDefaults identities. |
| `registration-public-seam.json` | Same harness plus public `AddEventStoreHttpHostSecurity`: exit 2; registration removes the exception and the SDK inventory reports 58 classification failures across 55 paths. |

The exact argv, working directories, start/finish UTC times and exit codes are retained in those JSON
receipts; logs and raw xUnit XML remain alongside them. `summary.json` explicitly records
`qualified:false` and zero newly passing consumer cases. Successful reproduction of a blocker is
diagnostic evidence, not a passing acceptance test. The probe has the same source/PE identities as the
prior consumer packet and uses the real public SDK registration and inventory, without internal DI or
policy modifications.

All three synchronous child commands exited. No Aspire/Dapr/provider/container topology was started
and no unrelated processes, quotas or stores were touched. Product/SDK source, prior evidence,
dependency pins, Git index, frozen intent, sprint status and public v2 are preserved. This packet's
files and the new proposal/non-frozen checkpoint are the only intended repository changes.

`final-audit.json` independently rechecks those source/runtime inventories, all 174 entries in the
historical selected-candidate seal, frozen-intent equality against HEAD, prior-packet tracked bytes,
Python/JSON/XML parsing, whitespace and exact diagnostic-process cleanup. All pass. Packet-local
ignore exceptions retain raw logs; attributes preserve packet bytes. `SHA256SUMS` seals every other
packet file. The proposal/spec hashes in the audit bind this checkpoint, not a later approval/edit.
