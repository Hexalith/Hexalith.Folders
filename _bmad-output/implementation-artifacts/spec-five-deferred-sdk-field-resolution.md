---
title: 'Resolve SDK idempotency fields from the declared operation and schema'
type: 'bugfix'
created: '2026-11-23'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Generate SDK idempotency helpers that bind each field to the parameter, property or enum token declared for its operation.

- DW-98. Given a parameter containing an acronym, when the generator matches its equivalence field, then it treats the acronym as a word and reports any resulting ambiguous names.
- DW-109. Given the three file mutations, when their SDK helpers compute retry identity, then each uses the wire token declared by its request schema. A regression detects enum metadata drift.
- DW-212. Given a file-operation kind, when hashing the request, then one dispatch selects the operation and the SDK's enum encoding supplies its wire value.
- DW-214. Given a mutating operation with multiple equivalence fields, when generating its helper, then one parameter lookup serves every field and preserves the declared argument order.
- DW-223. Given a request schema inheriting properties through local references or allOf compositions, when resolving a helper field, then the generated helper can read the inherited property. Invalid or cyclic schema references produce a diagnostic before publication.

</frozen-after-approval>

## Implementation Notes

- The initial pull covered Hexalith.Folders and its 13 direct submodules. Eight submodule pointers advanced. SDK work is confined to the generator, generated helper, and client tests.
- Parameter lookup retains component aliases and accepts declared wire names. The generator walks composed schemas before choosing the base DTO that hosts a shared helper.
- Generation validates the contract before NSwag writes its client. Tests run the MSBuild target against existing outputs and compile an inherited helper with the pinned NSwag DTOs.
- File mutation helpers resolve the concrete request kind before hashing. Their serialization callbacks use the same kind, including when the caller constructs a change or removal request directly.

- The package-mode Release build of Hexalith.Folders.CI.slnx completed with zero warnings and zero errors. All 433 SDK tests passed in Debug and Release. All 753 contract tests and 74 CI tooling tests passed.
- The updated PD10 candidate inventory reproduced byte for byte. The SDK suite independently regenerated the client and helper from the canonical inputs.

## Review Triage Log

- Medium, fixed. Concrete change and removal requests inherited the base default of add before serialization. Each concrete DTO now supplies its validated kind. The three concrete DTOs are checked before and after serialization.
- Medium, fixed. A shared helper could hide a broken schema on its concrete request. A missing allOf target on AddFileRequest now fails validation.
- Medium, fixed. A property belonging to one alternative could be emitted on a shared request. The lookup accepts fields declared consistently across all alternatives and rejects the variant-only regression case.
- Medium, fixed. A referenced parameter with different component and wire names lost the wire name during lookup. The compiled probe includes a traceURL wire parameter under the TraceParameter component.
- Medium, fixed. A nested field could name an absent leaf. Resolution checks each property along the requested path before emitting an accessor.
- Medium, fixed. An MSBuild generation failure could overwrite the client before rejecting a parameter collision. The public target now checks helper metadata first. A regression starts with existing client and helper files and verifies their contents after the failure.
