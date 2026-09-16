# UX Validation Report — Folders

- **Specification:** `_bmad-output/planning-artifacts/ux-design-specification.md`
- **Format:** legacy single-document (no `DESIGN.md` / `EXPERIENCE.md` spine pair)
- **Run at:** 2026-09-16
- **Trigger:** bmad-ux Update mode, Reviewer Gate at Finalize
- **Lenses run:** rubric walker; structure + prose
- **Baseline reviewed:** the +242/−17 revision applying section 5.3 of `sprint-change-proposal-2026-09-15.md`

## Overall verdict

The provenance layer came through clean: all three digests byte-exact, every cross-reference resolving, the
UX-DR register intact from 1 to 39, and both code claims in the lockstep banner verified true against the
shipped source. The revision failed as a downstream contract in two places, both introduced by this update and
both now fixed — the three newly added evidence sections dangled with no journey, component, roadmap or test
coverage, and UX-DR36 collapsed two denial envelopes that `architecture.md` S-7 deliberately separates.

The structure and prose pass found no critical defects and confirmed markdown and YAML validity, but surfaced
a vocabulary collision in a normative register row and a stale-enumeration cluster where the same list of
workspace-detail sections appeared five times with two different contents.

All 2 critical and all 5 high findings are resolved. The pinned `OpsConsoleWireflowNotesTests` gate is green
at 17/17 after the fixes.

## Category verdicts (as found, before fixes)

| Category | Verdict |
| --- | --- |
| Flow coverage | **broken** → fixed |
| UX-DR register integrity | adequate |
| State coverage | **thin** → fixed |
| Internal cross-reference resolution | **strong** |
| Bloat & overspecification | adequate |
| Inheritance discipline | adequate |
| Honesty | adequate, with one critical error → fixed |
| Structure | approve with changes |
| Prose | approve with changes |

## Findings by severity

### Critical (2) — both fixed

**[Rubric] Three new evidence sections had zero journey coverage.**
The three Mermaid journeys were byte-identical to the pre-update document, so no flow landed on Durability
Evidence, Indexing Status or Hardening Evidence — and UX-DR32 scopes accessibility validation to exactly those
three journeys, so the new surfaces would never be keyboard- or screen-reader-validated.
*Fixed:* Journeys 1 and 3 extended with branches reaching all three sections. A fourth journey was rejected
because `OpsConsoleWireflowNotesTests.cs:430-433` pins the wireflows doc to three Mermaid flowcharts named as
UX-DR32's "three critical journeys"; adding one would have broken that lockstep.

**[Rubric] UX-DR36 collapsed two envelopes that architecture S-7 separates.**
The revision required authority-unavailable and resource-unavailable to share one presentation with
non-varying status, category and code. S-7 mandates two: HTTP 404 `authorization` / `tenant_access_denied`
(`retryable: false`) for a post-authorization denial, and HTTP 503 `availability` / `authority_unavailable`
(`retryable: true`, `details.visibility: redacted`) for authority that is absent, stale, malformed or
unavailable. S-7's stated rationale is removing the existence oracle "without hiding a real outage behind it";
the collapsed form would have rendered a genuine outage as a denial and suppressed the retry the operator
needs. Also contradicted UX-DR20, UX-DR38 and the document's own separate-axis rule.
*Fixed:* UX-DR36 rewritten and the section replaced with a two-envelope table carrying each envelope's status,
category, code, retryability and the operator's next move.

### High (5) — all fixed

**[Rubric] PD11 misstated as unresolved.** The banner claimed the `unknown_provider_outcome` disposition was
"not yet settled", citing a three-way split. That split is the *superseded question retained as history*
column of a **PM decisions resolved** table; PD11 is sponsor-approved option (a), selecting auto-recovering.
*Fixed:* restated as decided, with role attestation and the `FolderStateTransitions.cs` correction named as
what actually remains open.

**[Rubric + Structure] `absent` vs `Missing` vocabulary collision.** UX-DR34 — a normative register row —
named the fifth disclosure outcome `absent`, while the document's own contract table, UX-DR10/15/22, the
shipped `FieldDisclosure` enum and the pinned test constant all say `Missing`. Architecture S-6 does use
`absent`, so this is an upstream split, not a pure invention.
*Fixed:* `Missing` made the normative render token; `absent` recorded explicitly as the S-6 synonym so no
surface invents a sixth term.

**[Structure] Three stale section enumerations.** UX-DR18 and Navigation Patterns were updated; three other
enumerations of the workspace-detail sections were not, including the build-spec bullet list closest to
implementation. The page's sections appeared five times with two different contents.
*Fixed:* all three reconciled, with UX-DR18 named in-document as the single authoritative list.

**[Rubric] State coverage thin on the new sections.** Disclosure and availability handling was specified for
the console generally but not walked per new surface.
*Fixed:* each new section now states its unavailable-path behaviour explicitly.

**[Rubric] Inheritance discipline on disposition vocabulary.** Resolved together with PD11 above; UX-DR35 now
names `auto-recovering` as canonical and explicitly forbids rendering `auto-reconciling`.

### Medium (21) — addressed

Axis error in Update Record item 2 (put `unavailable` on the disclosure axis, contradicting the two-axis rule
three paragraphs later); understated scope disclaimer; three new sections absent from the Implementation
Roadmap and from the custom-component list; `unknown` overloaded across three vocabularies with conflicting
colour treatment; duplicate normative homes for the disclosure vocabulary; provenance-framed heading renamed
to `## Workspace And Provider Evidence Sections`; obligations written as description where the document
otherwise uses `must`; agentless passive hiding the owner of the tokenizer and the security-trimming step;
essayistic padding trimmed; British spellings converted to the document's American baseline.

### Low (9) — addressed or accepted

Hand-maintained register count replaced with a range; `[NOTE FOR UX]` given a legend entry alongside
`[ASSUMPTION]`; machine-visible `openDecisions` added to frontmatter beside the legacy `status: complete`.
**Accepted, not fixed:** the three new `###` subsections remain in their current order, and the stale
`L758-764` line citation in `ResponsiveViewportSmokeTests.cs:17` was left alone — it is an XML doc comment,
never an assertion, and the user scoped this run to leave tests untouched.

## Prose and structure notes

Markdown validity confirmed sound: no unbalanced code spans or emphasis, no trailing whitespace, well-formed
tables, consistent list nesting. The nested `updateProvenance` frontmatter block parses cleanly under
`yaml.safe_load`, now with fifteen top-level keys.

The new prose was hard-wrapped at 110 columns against a wholly unwrapped body. Beyond appearance this matters
because the repository's doc-cluster conformance gates assert raw unnormalized `ShouldContain`, so a phrase
spanning a wrap point cannot be pinned by a gate. All added prose was unwrapped to match the document; the
transformation was verified content-preserving by word-stream comparison.

## Open items carried forward

- 12 `[ASSUMPTION]` tags on Epic 12 and Epic 13 signal lists, awaiting confirmation.
- 2 `[NOTE FOR UX]` reconciliations owed by upstream artifacts.
- PD11 role attestation and the `FolderStateTransitions.cs` disposition correction.
- PD8 `FieldDisclosure.Withheld` member, specified here, not implemented.
- Upstream digests to be recomputed at the commit that relocks prd/epics/architecture.

## Reviewer files

- `review-ux-rubric-2026-09-16.md`
- `review-ux-structure-prose-2026-09-16.md`
