---
title: 'Verify contract examples, references, fixtures, and acceptance coverage'
type: 'bugfix'
created: '2026-11-23'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Make contract verification catch drift in the examples, references, and fixtures used to demonstrate the Folders API contract.

- DW-149. Given a PD10 candidate file-context request or response with a declared JSON example, when the generator verifies it, then it validates the example against its bound Draft 2020-12 schema and identifies failures by operation and example. The historical spine keeps its existing targeted checks.
- DW-153. Given a local reference in an OpenAPI schema or parameter position, when the workspace-lock contract test checks it, then it requires an existing target of the declared kind and identifies an invalid location.
- DW-154. Given inline and component examples, when the workspace-lock field guard runs, then it examines nested payload map keys, including maps inside arrays. Descriptions and example labels can describe credentials without being treated as payload fields.
- DW-165. Given a previous-spine fixture, when the fixture test parses it, then it checks ownership and operations structurally and rejects OpenAPI version keys at any depth.
- DW-208. Given the idempotency and parity verification table, when the artifact suite runs, then each coverage row maps its declared acceptance criteria to the test methods that cover it.

</frozen-after-approval>

## Implementation Notes

- Hexalith.Folders started at `3a9f6817d9da6d2603110d8aa11d92049db6403c`. The workspace pull covered Folders and its 13 root-declared repositories.
- The generator validates media-level file-context JSON examples and retains problem-response validation across the candidate. The broader trial found existing non-file-context mismatches in workspace-transition evidence and commit acceptance. The generated candidate still reproduces byte for byte.
- `aspire run` timed out after 120 seconds during the AppHost build. `aspire describe` found no running AppHost.

- The package-mode Release solution built with zero warnings or errors. The Contracts suite passed all 761 tests and the Testing suite passed all 131. The Python tooling suite passed 60 tests with 14 environment-dependent skips. The pending conformance inventory reproduced byte for byte.

## Review Triage Log

- Medium, deferred. Schema Objects can also hold example annotations. The new gate follows examples bound to media types. The backlog records the separate schema annotation walk.
- Medium, fixed. JSON media selection recognizes case differences and strips media type parameters before checking the subtype.
- Medium, fixed. Schema validation resolves local references against the entire candidate, including inline schemas under `paths`.
- Medium, fixed. An unresolvable schema reference reports the operation, media type, and example without including the registry contents.
- Medium, fixed. Parameter references require Parameter Objects. Regression cases use a scalar and an unrelated mapping at an existing parameter pointer.
- Medium, fixed. Field enumeration checks both singular and plural schema example annotations when they coexist.
- Low, fixed. The fixture suite validates populated ownership text and nonempty lists in addition to the flags.
- Low, fixed. The previous-spine guard recognizes `openapiVersion` in addition to the underscore and hyphen spellings.
- Medium, fixed. The test cited for AC4 now checks adapter sourcing and error projection rows, including the documented missing-credentials outcome.
- Medium, fixed. Verification coverage binds the stated check as well as the artifact, acceptance criteria, and test name.
