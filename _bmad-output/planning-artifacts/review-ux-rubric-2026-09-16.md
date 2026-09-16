# UX Specification Review — Rubric Walker — Folders

## Overall verdict

The 2026-09-16 update is well-sourced and unusually honest at the provenance layer: all three recorded
digests are byte-exact against the working tree, every NFR/OQ/PD/FR/C reference resolves, the UX-DR register
is clean from 1 to 39, and the two code claims in the lockstep banner (`FieldDisclosure` has four members,
`FolderStateTransitions.cs` maps `UnknownProviderOutcome` to `AwaitingHuman`) are both true. It fails as a
downstream contract in two places. First, the three admitted evidence surfaces dangle: they exist in the
register and in one prose block and nowhere else — no journey reaches them, no component defines them, no
roadmap phase builds them, no test lane exercises them. Second, UX-DR36 and its prose collapse the
authority-unavailable and resource-unavailable envelopes into one indistinguishable presentation, which
contradicts `architecture.md` S-7 and proposal §7.2, contradicts UX-DR20 and UX-DR38, and would hide a real
outage behind a denial.

## 1. Flow coverage — broken

Checked: the three User Journey Flows (lines 489–549) against the three surfaces admitted under
`## Added Evidence Surfaces` (705–795), plus the component catalogue (587–673), the Implementation Roadmap
(683–702), the filter list (849), and the Testing Strategy (995–1003). The three journeys are byte-identical
to the pre-update version — the diff touches nothing between lines 195 and 650.

### Findings

- **critical** All three new surfaces dangle. Durability Evidence, Indexing Status and Hardening Evidence
  appear only in the Update Record (75), the register (181, 200–202), their own section (717–795) and one
  Navigation Patterns sentence (839). Journey 1's drill-down fan-out (504–506) still offers exactly folder
  orientation, diagnosis and history; Journey 3's failure-type switch (538–542) still offers exactly provider,
  lock, commit, authorization and read-model; Journey 2 (516–527) ends at access evidence. No journey lands on
  any of the three. Because UX-DR32 (195) scopes keyboard-only and screen-reader validation to "the three
  critical journeys", surfaces outside them are also outside the accessibility gate. *Fix:* add a
  `H -->|Durability| ...` and `H -->|Search visibility| ...` branch to Journey 1, a
  `D -->|Durability| ...` arm to Journey 3's switch, and either a fourth journey or a Journey-3 provider-arm
  extension that reaches hardening evidence on the provider view.
- **high** The three sections have no component definition and no build phase. Every other evidence panel in
  the document is either a Custom Component with Purpose/Usage/Anatomy/States/Accessibility (589–673) or a
  named roadmap item — the roadmap even phases "Provider Readiness Evidence panel" and "Access Evidence panel"
  (695–696). UX-DR37/38/39 mandate three sections that appear in neither list (683–702). *Fix:* give each a
  States and Accessibility block in the same shape as the six existing components, and assign a roadmap phase.
- **high** Journey 1's no-match branch contradicts the new Indexing Status rule. Node D (499) reads
  "Show empty state with filter explanation and safe no-leak wording" — one branch for all zero-result cases.
  The new section (759–763) and UX-DR20 (183) both require an unavailable facade to be labelled unavailable
  rather than as no matches. The journey that owns the search entry point still collapses them. *Fix:* split
  node D into no-matches and read-model-unavailable outcomes.
- **medium** The three surfaces are absent from the validation and discovery surfaces that name every other
  panel: the screen-reader review list (1001) and the operational filter list (849). *Fix:* extend both lists.
- **medium** UX-DR39 (202) places hardening evidence on "the provider view", but no requirement defines the
  provider view's section structure — UX-DR18 (181) is scoped to workspace detail only. The section lands on a
  page with no predictable-section contract. *Fix:* add a provider-view section requirement, or move the
  hardening section under an extended UX-DR18-style rule.

## 2. UX-DR register integrity — adequate

Checked: all 39 table rows (164–202). IDs run 1..39 with no duplicate, no gap, no out-of-order row and no ID
reused with a changed meaning. The five amendments (UX-DR10, 15, 18, 22, 27) are pure additions — `withheld`
into 10/15/22, `durability evidence` + `indexing status` into 18, `confidential-override correlation
reference` into 27 — and each is consistent with the prose that cites it (Navigation Patterns 839 mirrors
UX-DR18; the safe-identifier rule at 908–910 mirrors UX-DR27). UX-DR33, 35, 36, 37, 38 are testable as
written; 34 and 39 are not.

### Findings

- **high** UX-DR34 (197) is not implementable as written. It names the fifth disclosure outcome `absent`,
  but the component that "owns the rendering" names it `Missing` in its normative table (669), the shipped
  enum member is `FieldDisclosure.Missing`, and `OpsConsoleWireflowNotesTests.cs:115–122` pins the four member
  names as `Visible, Redacted, Unknown, Missing`. The document never states that `absent` and `missing` are
  the same outcome, and both words appear in it as normative terms — `missing` in UX-DR10/15/22 and in the
  component's States line (657), `absent` in UX-DR34 and at 864–869. A developer reading UX-DR34 literally
  adds a sixth member or renames a pinned one. *Fix:* pick one word; if `absent` is kept, state explicitly
  that it renames `FieldDisclosure.Missing` and add that rename to the lockstep banner.
- **medium** The canonical state vocabulary was not extended with the lifecycle states this same update
  now requires rendering. UX-DR13 (176) mandates consistent canonical state vocabulary and UX-DR15 (178)
  enumerates it, but neither carries `changes_staged`, `unknown_provider_outcome` or `reconciliation_required`
  — all three are canonical C6 states (`OpsConsoleWireflowNotesTests.cs:100–113`), and UX-DR35 (198) plus the
  Durability Evidence section's staged-versus-confirmed rule (735) both depend on them. UX-DR15 gained only
  `withheld`, a disclosure outcome. *Fix:* add the three lifecycle states to UX-DR15.
- **medium** UX-DR39 (202) has no testable content. It requires "the release-hardening signals owned by
  Epic 13" without naming one, and every signal in its section (775–782) is `[ASSUMPTION]`. A gate can assert
  only that a section exists. *Fix:* bind it to the named NFR rows (NFR74–NFR78, NFR82–NFR84) that the
  section's own note already cites as existing, or mark UX-DR39 provisional until OQ12 fixes the set.

## 3. State coverage — thin

Checked: every occurrence of a disclosure term across the document, against the five-way model
(visible/redacted/withheld/unknown/absent) plus the separate availability axis asserted at 867–869. The new
model reaches the register (173, 178, 185, 197), the Redaction component (649–673), the three new prose
sections (853–914), the Accessibility Strategy (980, 984) and the Testing Strategy (1001). Fourteen other
sites still carry the pre-update model.

### Findings

- **high** Two lists exist in duplicate and only one copy of each was updated. The semantic-color Neutral
  bucket at line 401 ("unknown, unavailable, not configured, redacted, archived, or inactive") was left alone
  while its twin in Feedback Patterns (822) gained `withheld`; the Info bucket at 400 was likewise left alone
  while 824 adds the new `unknown_provider_outcome` info-state rule. The same split hits accessibility:
  line 440 still reads "Redacted, inaccessible, unknown, unavailable, failed, delayed, dirty, locked, ready,
  and committed" while its twin at 984 gained `withheld`. An implementer who reads the Visual Design
  Foundation rather than the Consistency Patterns gets the old model. *Fix:* update 400, 401 and 440, or
  delete the duplicates and cross-reference the canonical copies.
- **high** Journey 2 node G (523) — the only in-journey rendering rule in the document — still reads
  "Mark inaccessible, redacted, unknown, and permitted entries distinctly". This is the tenant-isolation
  proof flow, the flow most likely to encounter a confidential override. *Fix:* add `withheld` to node G.
- **medium** Five of the six custom components were not updated. Workspace Trust Summary States (597),
  Tenant Scope Banner States (609), Metadata-Only Folder Tree Anatomy/States (619, 621) and Trust Matrix
  States (645) carry no `withheld`, although the Redaction component's own Usage line (653) names folder
  metadata, audit evidence, provider diagnostics and access checks as exactly the places withheld values
  appear. *Fix:* add `withheld` to the four States lists and the folder-tree Anatomy marker.
- **medium** The Redaction component now carries two overlapping enumerations with no stated mapping:
  nine "States" (657: redacted, withheld, inaccessible, denied, unknown, missing, unavailable, stale, failed)
  and five disclosure outcomes (663–669). `inaccessible`, `denied`, `unavailable` and `stale` appear only in
  the first, and the document asserts at 867–869 that availability is a separate axis — but does not say which
  of the nine belong to which axis. *Fix:* label each of the nine States with its axis, or split the component
  contract into a disclosure table and an availability table.
- **medium** Framing and principle prose still enumerates the four-way model at 114, 156, 259, 293, 337,
  371 and 557. Line 337 is the most consequential: "Color must never be the only signal for readiness,
  failure, lock, dirty, inaccessible, redacted, unknown, or delayed states" omits `withheld`, which is
  precisely the outcome UX-DR34 requires a non-color cue for. *Fix:* extend these seven lines, or reduce them
  to a pointer at the canonical list so they cannot drift again.

## 4. Internal cross-reference resolution — strong

Checked: every `UX-DR*`, `NFR*`, `FR*`, `OQ*`, `PD*`, `C*` token and every file path in the document.
All 39 UX-DR self-references resolve to register rows. `FR58`, `OQ11`, `OQ12`, `PD1`, `PD3`, `PD8`, `PD11`,
`NFR74`–`NFR84`, `C6` and `C9` all resolve in `prd.md` (OQ11/OQ12 at 1103–1104; PD1/PD3/PD8/PD11 at
1151/1152/1155/1157; NFR74–NFR84 at 1068 and 1078). The A-to-PD mappings are correct: A1→PD1, A2→PD3,
A5→PD8, A7→PD11. The OQ gating split at 794 matches `prd.md:163` exactly. All four cited file paths exist.
All three frontmatter digests were recomputed and match byte-for-byte:
`5d12ae4d…` for the proposal, `0d6f1ab8…` for `prd.md`, `f7b5a8d2…` for `architecture.md`.

### Findings

- **medium** The register's own lockstep clause at line 160 makes `docs/ux/ops-console-wireflows.md` a
  downstream consumer that "must preserve these IDs", and that document plus its conformance gate hard-pin
  the old bounds: `OpsConsoleWireflowNotesTests.cs:380` asserts `ids.Length.ShouldBe(32)` for the §5
  traceability table and `:357` asserts 30 rows for §4. Extending the register to 39 creates an unnamed
  lockstep debt on a blocking gate. *Fix:* name the wireflow document and its test constants in the Open
  lockstep deltas banner.
- **low** `updateProvenance` pins `prdDigest` and `architectureDigest` but no `epicsDigest`, although the
  banner at 92–94 names `epics.md` as one of the three uncommitted upstream artifacts and the hardening note
  at 790 relies on `epics.md` carrying the mirrored NFR rows. *Fix:* add `epicsDigest`.

## 5. Bloat & overspecification — adequate

Checked: the 242 added lines for prose that restates rather than decides. Most of the new prose earns its
place — the Confidential Override section (871–885) decides that withheld must not reuse redaction copy, the
Provider Outcome section (887–899) decides where the escalation boundary is, and the Disclosure contract
table (661–671) decides that the value is emitted only in the visible branch, which is a real implementation
constraint rather than a restatement.

### Findings

- **medium** The Hardening Evidence section (770–795) defers the decision it was added to make. Four of its
  five bullets are `[ASSUMPTION]`, and its closing note (789–795) states that the NFR74–NFR84 rows now exist
  in `prd.md`, `epics.md` and the traceability document, then instructs a future reader to reconcile the
  assumptions against them. The rows the section needs are already published and already cited by row number
  in the same paragraph. *Fix:* replace the four assumptions with signals derived from the named NFR rows, or
  drop the signal list and keep only the framing question until OQ12 fixes the evidence set.
- **low** The document has grown a justificatory register — 911–914 ("This presentation is deliberately less
  informative than an operator would prefer…"), 735–737, 749–751 — that argues for decisions already stated
  as requirements. Defensible as recorded rationale, but it thickens the read for a story-dev consumer.

## 6. Inheritance discipline — adequate

Checked: every state, disposition and disclosure term in the new content against `prd.md`, `epics.md`,
`architecture.md` and `OpsConsoleWireflowNotesTests.cs`. No invented vocabulary. `withheld` and `absent` are
both inherited from `architecture.md:639` ("kept visibly distinct from redacted, unavailable, and absent") and
`prd.md:1155` (PD8). `auto-recovering`, `awaiting-human` and `degraded-but-serving` are three of the pinned
five dispositions (`OpsConsoleWireflowNotesTests.cs:92–96`). `unknown_provider_outcome` and
`reconciliation_required` are two of the pinned eleven C6 states (`:100–113`). UX-DR35's explicit ban on
rendering `auto-reconciling` is correct and matches the pinned set.

### Findings

- **medium** `architecture.md:640` (S-7) defines `visibility` as an enumerated wire field with values
  `metadata_only`, `redacted`, `withheld`, and says it is enumerated precisely "so two surfaces cannot invent
  different vocabularies". The document's five-way disclosure model never mentions that field, so the console
  now has two overlapping vocabularies for the same concept with no stated mapping — in particular, nothing
  says how the three-valued wire field projects onto the five-valued render model. *Fix:* add the mapping to
  the Disclosure contract table.
- **medium** The document says `correlation reference` (UX-DR27, UX-DR33, 655, 873–879) where
  `architecture.md:639` says `correlation token` and defines it as a keyed truncated HMAC. Same object, two
  names, and the architecture text is the one that pins the derivation. *Fix:* adopt `correlation token`, or
  state the synonym once.

## 7. Honesty — adequate, with one critical error

Checked every factual claim in the Update Record, the Open lockstep deltas banner and the two
`[NOTE FOR UX]` blocks against the repository. Verified **true**: `FieldDisclosure` has exactly four members
(`src/Hexalith.Folders.UI/Services/FieldDisclosure.cs` — Visible, Redacted, Unknown, Missing);
`FolderStateTransitions.cs:161` maps `UnknownProviderOutcome => FolderOperatorDisposition.AwaitingHuman`;
`auto-reconciling` is genuinely absent from the canonical five; all three digests match byte-for-byte;
`prd.md`, `epics.md` and `architecture.md` all did carry uncommitted changes; the NFR74–NFR84 inventory claim
is accurate in all three places (two new PRD categories at 1068/1078, eleven mirrored rows in `epics.md`,
traceability relocked at 84); and the OQ11/OQ12 gating split matches `prd.md:163`.

### Findings

- **critical** UX-DR36 (199) and the Non-Disclosing Unavailability Envelopes section (901–914) are wrong
  against the architecture they claim to apply. The document requires authority-unavailable and
  resource-unavailable to "share one non-disclosing presentation" and states that "Status, category, code,
  message, and detail keys must not vary" in a way that reveals "whether the authority service could not be
  reached". `architecture.md:640` (S-7) and proposal §7.2 items 1–2 mandate **two** envelopes: a 404 with
  category `authorization`, code `tenant_access_denied`/`resource_unavailable`, `retryable: false`, client
  action `verify_tenant_context_and_authorization`; and a 503 with category `availability`, code
  `authority_unavailable`, `retryable: true`, client action `retry_after_backoff`,
  `details.visibility: redacted`. S-7's stated rationale is that one denial envelope removes the existence
  oracle "**without hiding a real outage behind it**", and it explicitly rejects the collapse the UX document
  now requires. The requirement is also internally contradictory: UX-DR20 (183) requires empty states to
  distinguish an unavailable read model from denied access, UX-DR38 (201) forbids rendering unavailable as
  absent, and 867–869 asserts availability is an independent axis. As written it would give an operator the
  wrong next action on every authority outage and would strand the Hardening Evidence section's own
  authority-reachability signal (775–776). *Fix:* rewrite UX-DR36 as two non-disclosing envelopes that are
  distinguishable from each other but not from one another's protected-resource detail, and drop "status,
  category, code" from the must-not-vary list.
- **high** "The operator disposition for `unknown_provider_outcome` is not yet settled" (87) misstates the
  PRD. `prd.md:1157` carries PD11 in the **resolved** PM decisions table, sponsor-approved as A7/A7b on
  2026-09-15, selecting Option (a) — which states verbatim that "`unknown_provider_outcome` is automatically
  recovering during bounded provider checks and escalates to `reconciliation_required` only when those checks
  cannot establish the result". The three-way disagreement the document describes lives in PD11's
  *superseded question* column, not in its decision. What is pending is the Product/Architecture/Security
  attestation and the code lockstep, not the direction. Presenting UX-DR35 as a UX-side tiebreak that "changes
  with" a future PD11 resolution invites a story-dev to defer it. *Fix:* restate as "PD11 is sponsor-approved
  and selects auto-recovering; role attestation and the C6/code lockstep remain pending".
- **high** The Open lockstep deltas banner (79–94) claims to be the complete set — "deliberately not hidden
  by this document" — but lists only two code deltas and the digest recomputation. It omits that the register
  now runs to UX-DR39 while `docs/ux/ops-console-wireflows.md` and `OpsConsoleWireflowNotesTests.cs` pin 32
  traceability rows, 30 implementation rows and four `FieldDisclosure` member names including `Missing`; and
  it omits the `absent`-versus-`Missing` rename implied by UX-DR34. The `[NOTE FOR UX]` at 673 names "a
  lockstep code, rendering, and test change" but not the wireflow document. *Fix:* add both to the banner.
- **low** The document carries 12 `[ASSUMPTION]` occurrences but only 9 tagged signals; line 95 is the tag's
  definition and 714 and 793 are meta-references to it. Harmless, but a counting gate would disagree with a
  reader. *Fix:* none required.

## Mechanical notes

- **Register:** UX-DR1..UX-DR39, 39 rows, no duplicate, no gap, no renumbering. Amendments are additive only.
- **Name inconsistencies:** `absent` (UX-DR34, 864, 869) versus `missing` (UX-DR10/15/22, 657, 669, shipped
  enum, pinned test constant) — same outcome, two normative words, no stated equivalence.
  `correlation reference` (UX doc) versus `correlation token` (`architecture.md:639`).
- **Duplicate lists updated asymmetrically:** semantic colors 400/401 versus 822/824; accessibility
  distinctness 440 versus 984.
- **Broken cross-references:** none. Every UX-DR, NFR, FR, OQ, PD, C token and file path resolves.
- **Digests:** `sha256:5d12ae4dd4e8f306b8dde5caf8f187d13e40c1fabc6d8e56c0757e24f2004104` (proposal),
  `sha256:0d6f1ab858e5774a87e0a639ceb521b0fc7e57a93f48297bf69ec53fd5788158` (`prd.md`),
  `sha256:f7b5a8d2a503bdee37f9833724a8896289930f1d8ecfc16453efa60b139900b1` (`architecture.md`) — all three
  recomputed and matching.
- **Frontmatter completeness:** `lastUpdatedAt` and the full `updateProvenance` block are present and
  well-formed; `roleSignoffs: pending` and `upstreamLockstepNote` are accurate. Missing `epicsDigest`.
  `status: "complete"`, `lastStep: 14` and `completedAt` were correctly left at their original values.
- **Untouched by this update, as expected:** the three journey mermaid blocks (495–549) and the
  Implementation Roadmap (683–702) — which is the mechanism behind finding 1.
