# SDK handoff: inherited original owner duplicate is rejected

Task 36 remains open. This continuation made no SDK production edits.

The current-source authenticated 10,001-position case passed staging, changed all
four managed host PIDs, resumed and verified the same session, then promoted an
independent owner generation. Its first original-owner replay returned Failed at
line 83. The live response ReasonCode was not retained, so its exact body is not
invented. Full live XML/logs remain alongside this handoff.

The retained [tiny reproduction](diagnostic/Program.cs),
[callback counter](diagnostic/CountingProxy.cs), [command](diagnostic/command.json)
and [raw output](diagnostic/qualified-repro.log) run the actual Folders handler,
bounded SDK rebuild dispatcher, ordinary named dispatcher and public coordinator
against the frozen DLLs. [Module and outcome binding](diagnostic/binding-summary.json)
matches six loaded PE hashes to the frozen Domain test group, whose DomainService
copy differs from live. The [second execution](diagnostic-live-modules/qualified-repro.log)
and its [binding summary](diagnostic-live-modules/binding-summary.json) explicitly
use live Folders Server DomainService SHA AB5C8B9F / MVID 64ccba5b and match all
five production modules to live runtime hashes; the sixth is the frozen test harness.
Both executions reproduce the same rejection. They run both without and
with the event-evolution registry and includes a Web JSON transport round trip.
Execution exits zero because it records the existing failure; it is a diagnostic,
not passing acceptance.

1. An ordinary primer occupies global position 1. Owner A captures sequence 1,
   global position 2; owner B captures sequence 1, position 3. Both owners retain
   the same lawful per-owner message name used by the consumer case.
2. Complete named rebuild of A and B succeeds. Original A replay returns
   AlreadyCompleted; direct SDK JournalAsync returns AlreadyCaptured.
3. A subsequent named rebuild containing only B succeeds and inherits A's view.
   A's captured checkpoint/reference and canonical delivery SHA-256 remain exactly
   equal to their pre-inheritance values.
4. The new epoch capture map contains B only. The same direct SDK JournalAsync
   returns IdentityConflict, and ordinary named dispatch returns
   Failed/delivery_identity_conflict. Its Folders fold callback count is zero.
   The primer's count of one proves the diagnostic counter is active.

The owning path is
`references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Projections/SharedProjectionEpochCoordinator.cs`:
JournalAsync at lines 738-796 validates captured identities only when the current
capture map contains the source. After inheritance the lookup is null, and lines
800-819 compare the historical sequence with acceptedSequence + 1, recording an
identity conflict. GetCapturedMarkerAsync at lines 3244-3261 reads only the current
CaptureHighWatermarks/CaptureChunks. The unchanged captured reference remains in
the owner's checkpoint. The ordinary dispatcher returns on IdentityConflict at
`DomainProjectionDispatcher.cs:926-928`, before any Folders FoldCatchUpAsync call.

The required SDK correction must preserve exact duplicate proof across inherited
generations using the existing retained owner identity evidence. Changed bytes,
positions, messages, missing/corrupt proof and CAS/epoch conflicts must still fail
closed. Keep the original owner, source continuity and 100-operation/one-MiB limits.
Do not work around this by rebuilding all owners on every consumer delivery,
discarding proof, acknowledging conflicts, or adding a Folders persistence protocol.

Qualify the correction in an EventStore-owned session, including this omitted-owner
inheritance case and true process recovery, then requalify Folders on that exact
source/binary graph. The strict provider failure at internal actor HTTP 429 and the
SDK broad regression, package/PD10, SM4 and whole-story review gates remain separate.
