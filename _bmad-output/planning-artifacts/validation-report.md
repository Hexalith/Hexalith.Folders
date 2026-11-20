# Hexalith.Folders PRD validation

**Fair** · 0 critical · 5 high · 8 medium · 0 low

Working-tree PRD at Folders revision `4cc96fcf`. SHA-256 `dd6c722fdd5bcd36ce4e46e79c66d65dd8390878119e7e0fa8aecc6508639bb5`.

Validated 2026-11-20T15:57:47+01:00.

[PRD](prd.md) · [BMAD rubric](../../.agents/skills/bmad-prd/assets/prd-validation-checklist.md) · [Full rubric review](review-rubric.md)

## Overall verdict

The PRD gives the release owner a clear reason to hold the MVP and specifies what a successful provider round trip must prove. It earns a **Fair** grade because two teams could still implement different access rules, return different recovery instructions, or pass different authorization tests while following its requirements. The highest-priority corrections are the file-body permission, the public denial response, and the distinction between a failed local write and a provider outcome that needs reconciliation.

## Dimension verdicts

- **Decision-readiness** — adequate. The November 20 posture names the revision that failed, the observable release checks, and the owners of the first disposable-provider choice. OQ10 explicitly holds SM1 and SM6 scoring pending a decision on failure-injection populations. That is a usable decision boundary.
- **Substance over theater** — strong. The audit and acceptance table asks the reviewer to observe the remote ref, exact bytes, commit count and terminal task state after replay. The equivalent search test observes a change, removal, stale-hit suppression and a drained backlog. The original brief's local-storage scenario has an explicit scope disposition. Each example helps distinguish product behavior from a passing fixture.
- **Strategic coherence** — strong. The central promise is one tenant-scoped workspace lifecycle that leaves confirmed Git state and inspectable failures. SM1 tests the lifecycle across providers and surfaces. CM1 rejects apparent success with an unconfirmed commit. SM7 and CM5 test whether integrations actually give up direct filesystem and provider access. OQ11 permits a smaller foundation proof while keeping the full release bar explicit.
- **Done-ness clarity** — thin. Most limits are testable, including the inclusive lease boundary, maximum query size and reconciliation budget. The following conflicts change the expected result for the same caller or operation. Passing one interpretation can fail the other.
- **Scope honesty** — adequate. The PRD records signed package publication, the production `V1Only` control, incomplete provider qualification and deferred repair commands. The release remains blocked for documented reasons. Two descriptions of what already exists can misdirect the work of closing those gates.
- **Downstream usability** — thin. The stable IDs, glossary and named journeys make individual sections easy to cite. Several journeys still describe a broader permission or a different recovery decision than the table used by the contract and operator console.
- **Shape fit** — strong. The combination of role-based journeys, product outcome tables and stable FR/NFR inventories suits a brownfield platform with external providers and four public surfaces. The release owner can locate the current audit near the front. Engineering and operations can follow the linked decisions to their exact acceptance conditions.

## Findings by severity

### Critical (0)

None found.

### High (5)

**Clean-workspace reads specify two different grants** · Done-ness clarity · § Endpoint Specifications and Protected operation families; PRD lines 467–475, 586–592

The endpoint paragraph says a ready, locked or committed workspace is readable by “any actor holding the folder `read` grant” and includes body search and range reads. The permission table and A22 require `write` for the context-read family. An audit reviewer with only folder read could receive file contents under the first interpretation.

**Suggested edit.** Specify the grant per operation before applying workspace-state and task restrictions. Use a read-only principal and a write-authorized principal on the same clean workspace to distinguish allowed status inspection from protected content reads. [Authorization matrix, context-read family and operation inventory](../../docs/contract/authorization-matrix.md)

**A protected ACL denial has two caller-visible expectations** · Done-ness clarity · § Error Codes and FR44; PRD lines 630–636, 949–950

FR44 requires callers to distinguish “folder ACL denied,” while the v2 rule maps insufficient folder scope to the same 404 as a missing or hidden resource. A distinct response could disclose whether a protected folder exists.

**Suggested edit.** Map the ACL-specific reason to bounded audit evidence and specify a single public 404 for fresh negative facts. Preserve the separate 503 for unusable authority evidence. Reconcile any change to FR44 with the mirrored epic wording and C13 mapping. [Authorization matrix, canonical outcomes](../../docs/contract/authorization-matrix.md)

**An interrupted local file mutation has two recovery paths** · Done-ness clarity · § Command and Query Contract, Error Codes and Glossary; PRD lines 511, 536–551, 640, 874

The command contract assigns an interrupted local file batch to `dirty` with per-operation evidence because it has no provider side effect. The error section says an unconfirmed “file mutation” enters `unknown_provider_outcome`, and the glossary includes it as an external effect. The two paths entail different locks, evidence and retry instructions.

**Suggested edit.** Classify by the side effect that may have occurred. Specify local operation recovery for staged file bytes and reserve bounded provider reconciliation for uncertain external writes. Check both cases before allowing commit. [OQ8 admission state machine](../../docs/exit-criteria/oq8-idempotency-design.md)

**Confirmed commit refusal lacks its terminal branch** · Done-ness clarity · § Task and Lock Completion Model; PRD lines 529–540, 750

The reconciliation instruction says “confirmation of no remote update transitions to dirty” and permits another lock and retry. A confirmed upstream refusal also leaves the ref unchanged. C6 sends that refusal from an unknown outcome to `failed`, while a known retryable failure with no remote effect preserves staged changes in `dirty`.

**Suggested edit.** Distinguish affirmative refusal, positively verified retryable non-application, confirmed success and unresolved evidence. Name the allowed state and next action for each. [C6 guard discriminators](../../docs/exit-criteria/c6-transition-matrix-mapping.md)

**Restarting the caller can imply a second valid lock** · Downstream usability · § UJ3, Task identity and Workspace State and Concurrency; PRD lines 289–293, 515, 523–525, 658

Asha's process stops with staged work and a still-valid lock. The journey immediately calls the workspace `dirty` and offers a “new lock instance.” The lock rule returns the existing instance and ownership proof while the lease is valid. Lease expiry is the documented transition from staged to dirty in this case.

**Suggested edit.** Walk the same task through restart before expiry and after expiry. Give each case its observed workspace state, fence and legal acquisition behavior. [Architecture, C6 transition matrix](architecture.md)

### Medium (8)

**Say what deferred core operations mean for MVP sign-off** · Decision-readiness · § Current Delivery Posture and Public Surfaces; PRD lines 181–186, 442–448, 957–958

“Confidential operations that remain deferred” includes the shared add/change/remove command and folder archive. The PRD requires the core lifecycle and tenant administration on all four surfaces, then permits a generic inventory deferral plus a C13 declaration. A reviewer needs to know whether a deferred core cell can count as a pass.

**Suggested edit.** Name the affected core operations and carry forward the November 10 decision that a deferred core operation blocks MVP. Reserve diagnostic exceptions for approved cells, and route any proposed reduction in core coverage to the product owner. [November 10 decision, §4.1 E3](sprint-change-proposal-2026-11-10.md) · [Migration inventory, Reading the rows](../../references/Hexalith.McpCli/_bmad-output/implementation-artifacts/folders-migration-inventory-v1.1.0.md)

**The release test lists only half the negative authorization states** · Done-ness clarity · § Actors and Contract and Quality Gates; PRD lines 570, 706, 1118

The quality gate explicitly lists four negative cases. The September matrix contains eight, adding disabled, unknown, absent-resource and insufficient-scope to that list. A team could satisfy the checklist while missing these four independent inputs.

**Suggested edit.** Derive the test population from all eight matrix rows and check the expected 404 or 503 and zero protected observations for each. [Authorization matrix, Canonical Negative Access States](../../docs/contract/authorization-matrix.md)

**The equivalence rule omits behavior-affecting credential scope** · Done-ness clarity · § Command and Query Contract; PRD lines 544–550, 944–945

The list introduced by “equivalent intent means” includes delegated task scope and excludes authentication tokens. The governing idempotency rules also compare server-derived credential scope when it changes behavior. Without that distinction the acceptance text can classify a different intent as an equivalent replay.

**Suggested edit.** Include the behavior-affecting scope in the comparison and keep raw token bytes excluded. Cover unchanged scope with a refreshed token and changed scope with the same live key. [Idempotency and adapter parity rules, admission identity](../../docs/contract/idempotency-and-parity-rules.md)

**The supported deployment target already exists** · Scope honesty · § Glossary and Open Release Items; PRD lines 184, 866, 1127–1128

“No profile is enumerated yet” conflicts with the deployment document that names single-region Kubernetes, the preproduction evidence environment, the state store, broker and replica floors. Qualification is still pending.

**Suggested edit.** Reference the named profile as the target for OQ12/OQ13 and identify the remaining proofs as rendered deployment, security, capacity and recovery evidence. [Supported MVP deployment profile](../../docs/deployment/supported-mvp-profile.md)

**OQ5 overstates the missing search work** · Scope honesty · § Open Release Items; PRD lines 182, 977, 1120

“Functionally empty” obscures the indexed-only filtering, post-authorization availability check and indexing-status work recorded by Story 10.8. The outstanding proof runs from an authenticated public mutation to an authorized hit and removal through shipped components.

**Suggested edit.** Credit the existing component evidence and describe the missing unseeded, restart-safe round trip, including refusal and removal. [Story 10.8, Completion Notes and Task 0 recheck](../implementation-artifacts/10-8-real-produce-index-authorize-hydrate-redact-search-round-trip.md)

**Dirty workspaces do not all require a person** · Downstream usability · § Workspace State and Concurrency; PRD lines 652–658, 924

The state summary labels all dirty workspaces “awaiting-human” and says no automatic transition will occur. The adjacent clean/stale rule returns a workspace to ready automatically. C6 also distinguishes work the originating task can resume from orphaned staging.

**Suggested edit.** Have status and console acceptance use the C6 disposition for clean, resumable and orphaned cases. [C6 state catalog](../../docs/exit-criteria/c6-transition-matrix-mapping.md)

**Operator diagnostics and readiness validation require different permissions** · Downstream usability · § UJ2, Protected operation families and FR15; PRD lines 279–285, 581, 594, 905

“A platform engineer acting as a tenant-scoped operator may validate” can be read as permission to invoke the readiness validation operation. That operation requires tenant folder-create authority. An operator can inspect provider support evidence under a different permission.

**Suggested edit.** Give the journey two named actions and explicitly identify the additional grant when the operator performs validation. [Authorization matrix, PD10 amendment and readiness family](../../docs/contract/authorization-matrix.md)

**The administrator journey implies effective-permission lookup for another actor** · Downstream usability · § UJ9 and FR5–FR6; PRD lines 351–353, 587, 889–890, 1179

Elise asks “Who can write here today?” and checks the automation team's grant in the effective-permissions view. PD10 restricts that operation to self-inspection. An administrator can list ACL entries, but those entries alone do not establish another principal's effective access after membership and delegation checks.

**Suggested edit.** Use the administrator's ACL-list operation to confirm the granted entry, followed by the intended principal's self-inspection to confirm effective access. [Authorization matrix, PD10 amendment](../../docs/contract/authorization-matrix.md)

### Low (0)

None found.

## Open release decisions

Jerome and the provider maintainer still need to choose the first disposable provider. Jerome and the Test owner must settle failure-injection scoring under OQ10. Production routing remains `V1Only`; provider qualification and public v2 activation retain their own gates.

## Mechanical checks

- Reviewed working-tree PRD SHA-256 `dd6c722fdd5bcd36ce4e46e79c66d65dd8390878119e7e0fa8aecc6508639bb5`. The decision log and original product brief were available. Every declared input document and local Markdown link in the PRD resolved.
- All 58 FRs matched their epic inventory text. All 84 NFRs matched the epic inventory and the corresponding 12-character SHA-256 values in the traceability bridge. UJ1–UJ9, SM1–SM8, CM1–CM5 and OQ1–OQ13 are contiguous.
- Every active assumption ID has an inline reference and an index row. A3 and A4 are explicitly withdrawn. Several rows still give September 30 as the revisit date, including A22; refresh their disposition when the associated evidence or decision is reviewed.
- Release evidence remains open for both providers, cross-surface parity, recovery and other named gates. This validation examined the product contract and repository evidence.
- All 51 documents named in PRD frontmatter and all 13 distinct relative Markdown link targets exist.
- The focused console wireflow documentation suite passed all 17 tests. These checks exercise documentation consistency. Production deployment and provider qualification have separate evidence requirements.

## Reviewer file

[Seven-dimension rubric review](review-rubric.md)
