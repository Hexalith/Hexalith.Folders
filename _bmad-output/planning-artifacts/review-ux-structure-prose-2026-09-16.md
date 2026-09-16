# UX Design Specification — Structure and Prose Review (2026-09-16)

**Target:** `_bmad-output/planning-artifacts/ux-design-specification.md` (1017 lines; +242 / -17 uncommitted)
**Lenses:** structure, prose. **Scope:** new and modified content, plus its integration with the pre-existing body.
**Verdict:** approve with changes. No critical findings. One high-severity vocabulary contradiction and one
high-severity stale-enumeration cluster should be fixed before this document is handed to an implementer as a
build spec. Markdown and YAML validity are clean.

## Structure

- **[high]** The five field-disclosure outcomes are named two different ways. UX-DR34 (line 197) and the
  consistency-pattern prose (line 864) call the fifth outcome **absent**; the component disclosure-contract
  table (line 669) calls it **Missing**, which is also what the shipped enum uses
  (`src/Hexalith.Folders.UI/Services/FieldDisclosure.cs:45`) and what UX-DR10/15/22 (lines 172, 178, 186) and
  line 859 use. "Absent" appears nowhere in code or in the pre-existing register. An implementer reading
  UX-DR34 as the normative row will introduce a sixth term. *Fix:* use `Missing` in UX-DR34 and at line 864,
  or rename everywhere including the enum — but pick one, in this commit.

- **[high]** Three pre-existing enumerations of the workspace-detail sections were not updated alongside
  UX-DR18 (line 186) and Navigation Patterns (line 839), so the document now lists the page's sections five
  times with two different contents. Stale: line 381 ("Supporting tabs or sections should provide folder
  metadata list, diagnostic timeline, provider/readiness evidence, task and lock history, audit records, and
  access/authorization evidence"), line 461 ("supporting sections expose folder metadata, diagnosis, provider
  readiness, access evidence, and audit history"), and the Implementation Approach bullet list at lines
  479–485, which is the closest thing the document has to a page build spec. None of the three mentions
  durability evidence or indexing status. *Fix:* add both sections to lines 381, 461 and 479–485; or reduce
  those three to a cross-reference to UX-DR18 so there is one editable list.

- **[medium]** The Implementation Roadmap (lines 683–703) was extended for the withheld outcome (line 690) but
  not for the three new surfaces. Durability Evidence, Indexing Status and Hardening Evidence appear in no
  phase, immediately above the section that introduces them. Line 487 also still says custom components
  "should be limited to" the original five. *Fix:* assign the three surfaces to phases, and reword line 487 or
  extend its list.

- **[medium]** Update Record item 2 (line 68) says "Withheld, redacted, unavailable, and absent values stay
  visibly and semantically distinct", putting `unavailable` on the same axis as the disclosure outcomes. Lines
  867–869 and the closing sentence of UX-DR34 (line 197) say the opposite — availability is a separate axis
  that "must not be collapsed into any disclosure outcome". The banner falsifies the rule it is summarising.
  *Fix:* reword item 2 to "Withheld, redacted, unknown and missing values stay distinct, and read-model
  availability stays a separate axis."

- **[medium]** Lines 62–63 claim the revision "changes interaction contracts only", but lines 75–76 of the same
  banner add three page sections and seven register rows (UX-DR33–UX-DR39), and UX-DR37/38/39 mandate new
  surfaces. The scope disclaimer is narrower than the change it introduces. *Fix:* "It changes interaction
  contracts and admits three read-only evidence sections; it does not relax the MVP read-only boundary…".

- **[medium]** `unknown` is now overloaded across three vocabularies with conflicting visual treatment in the
  same document: the disclosure outcome (line 736, "the unknown disclosure outcome"), the data state in the
  neutral-colour list (line 821), and the C6 lifecycle state `unknown_provider_outcome`, which line 823
  assigns an **info** state. A reader applying line 821 to line 823's condition gets neutral, not info.
  *Fix:* at line 821 qualify the entry as "unknown (field disclosure)" and add an explicit pointer that
  `unknown_provider_outcome` is governed by line 823, not by this list.

- **[medium]** The disclosure vocabulary has two normative homes that must now be edited together: the contract
  table at lines 663–669 (component-level, with a "Value emitted" column) and the prose definitions at lines
  861–869 (pattern-level). The `absent`/`Missing` split above is this drift pair already firing on its first
  edit. *Fix:* make lines 861–869 a cross-reference to the table rather than a second definition.

- **[medium]** `## Added Evidence Surfaces` (line 705) is page-level information architecture placed between
  component-level content (`## Component Strategy`, ending in a phased build roadmap at line 703) and
  cross-cutting presentation rules (`## UX Consistency Patterns`, line 797). The document's own altitude order
  puts page IA much earlier, at lines 373–385 and 475–485. Landing on it straight after a Phase 1/2/3 roadmap
  reads as a jump backwards. The heading is also provenance-framed: "Added" dates the section rather than
  naming it, and will be meaningless once it is no longer new. *Fix:* rename to something like
  `## Workspace And Provider Evidence Sections` and move it directly after `### Implementation Approach`
  (line 486), where the workspace-detail page contents are already specified.

- **[medium]** The `unknown_provider_outcome` recovery rule is stated four times — lines 69–70 (banner),
  UX-DR35 (line 198), line 823 (state colour), lines 889–899 (own section) — and each copy carries a fact the
  others omit: only line 823 assigns the info/error colours, only lines 890–892 forbid the retry affordance,
  only UX-DR35 forbids rendering `auto-reconciling`. These will diverge when PD11 resolves, which the document
  itself anticipates at line 90. *Fix:* keep UX-DR35 and the `### Provider Outcome Recovery Presentation`
  section as the pair of record, and cut lines 69–70 and 823 down to pointers.

- **[low]** The three new `###` subsections are at the right altitude and the right parent, but their order
  splits a thematic pair: `Confidential Override And Withheld Values` (871) and `Non-Disclosing Unavailability
  Envelopes` (901) are both non-disclosure rules and both continue the disclosure discussion begun at line 861,
  while `Provider Outcome Recovery Presentation` (887) sits between them on an unrelated topic. *Fix:* order
  871 → 901 → 887.

- **[low]** Frontmatter still carries `status: "complete"` and `lastStep: 14` (lines 29–30) while the body
  declares unresolved assumptions (95–96), a pending PD11 decision (85–90), role sign-offs "pending"
  (line 42), and digests that must be recomputed (91–93). Any tooling that reads `status` will treat this as a
  settled artifact. *Fix:* either downgrade `status`, or add a sibling field such as
  `openDecisions: ["PD11"]` so the state is machine-visible.

- **[low]** Line 76 hand-maintains a count — "extending the UX-DR register to UX-DR39". It is correct today and
  will silently rot on the next row added. *Fix:* "adding UX-DR33 through UX-DR39", which stays true when
  UX-DR40 appears.

- **[low]** `[NOTE FOR UX]` and `[ASSUMPTION]` are new markers with no precedent in the pre-2026-09-16 document
  (verified against `HEAD`). `[ASSUMPTION]` gets a legend at lines 95–96 and a local restatement at 712–715;
  `[NOTE FOR UX]` (lines 673, 789) gets neither, so a reader has no statement of who is expected to action it
  or when it is removed. *Fix:* add one sentence to the legend at line 95 covering both markers.

**Clean:** Markdown validity is sound. No unbalanced code spans or emphasis anywhere in the file, no trailing
whitespace, the new disclosure table (663–669) and all seven new UX-DR rows (196–202) are well-formed
three-pipe rows matching the existing tables, and list nesting is consistent. The nested `updateProvenance:`
frontmatter block parses cleanly under `yaml.safe_load` — all eleven keys resolve, and the colons, backticks
and `sha256:` prefixes inside the quoted scalars are correctly quoted.

## Prose

- **[medium]** British spelling in the new text against an American-spelling document. New: `behaviour` (88,
  713), `authorised` (197, 666, 863), `authorisation` (199, 905), `unauthorised` (787, 914). Existing baseline
  is American throughout — `authorization` appears 32 times (e.g. 138, 166, 291, 839) and `behavior` at 982,
  994, 1007, 1013. The new content is also internally inconsistent: frontmatter line 40 says
  `behavior-visible corrections` while line 88 says `behaviour`. *Fix:* convert all nine to American to match
  the document; check nothing downstream greps for the British forms first.

- **[medium]** The new prose is hard-wrapped at 110 columns; the entire pre-existing body is unwrapped, with
  paragraphs up to 455 characters on one line. The mismatch is visible inside a single subsection — line 859
  is one long line and lines 861–869 immediately below it are wrapped. Beyond appearance, this repository's
  doc-cluster conformance tests assert raw unnormalized `ShouldContain` on document phrases, so any phrase
  that now spans a wrap point cannot be pinned by a gate without reflowing it first. *Fix:* unwrap the new
  paragraphs to match the document, or accept the mismatch deliberately and note that new phrases are not
  gate-assertable as written.

- **[medium]** Obligations are written as descriptions in a document that otherwise uses `must`. Line 890–892:
  "the console presents the workspace as automatically recovering: it names that confirmation is in progress,
  shows the bound…, and offers no retry, repair, or discard affordance" — the third clause is a prohibition
  dressed as an observation, and it is the one a reviewer would check. Same pattern at line 736 ("the section
  uses the non-disclosing unavailability envelope"), 759 ("This section fails safe and says so") and 784
  ("This section reports; it never acts"). Line 891's sibling rule at UX-DR35 is correctly imperative, so the
  two readings of the same requirement differ in force. *Fix:* "must present", "must not offer", "must use".

- **[medium]** Agentless passive in three places where the obligation owner is the point. Line 707: "These
  three surfaces were admitted on 2026-09-16" — by whom, under which decision? The next paragraph names A1/PD1
  and A2/PD3 for the epics but not for the surfaces. Line 765: "Search results are security-trimmed upstream
  and hydrated from authoritative reads" — "upstream" is not an owner, and this sentence is the security
  premise the whole section rests on. Line 873: "A tenant-confidential override is written as a correlation
  token at event-write time" — no writer named. *Fix:* name the deciding record at 707 and the owning
  component at 765 and 873.

- **[medium]** Essayistic padding, noticeably looser than the surrounding terse register. Lines 737–738,
  "Absence of evidence is not evidence of loss, and the section must not let an operator read it that way" —
  an aphorism carrying one rule already stated in the preceding sentence. Lines 912–914, "This presentation is
  deliberately less informative than an operator would prefer. That cost is accepted: a variation that helps a
  legitimate operator narrow down the cause is the same variation that lets an unauthorised caller probe…" —
  three clauses of rationale after the rule is already complete at line 906. Also lines 722–724 ("This section
  exists because…"), 748–750 ("Search results without a status panel are untrustworthy"), and 755 ("legible
  rather than mysterious"). *Fix:* cut 737–738 and 755; compress 912–914 to one sentence; keep the rationale
  at 722–724 and 748–750 but halve it.

- **[low]** Line 78 uses a bold pseudo-heading, "**Open lockstep deltas, deliberately not hidden by this
  document.**", for what is the most consequential block in the banner. It cannot be linked to, does not appear
  in a heading index, and "deliberately not hidden by this document" is the document praising itself. *Fix:*
  make it `### Open Lockstep Deltas`.

- **[low]** Run-in bold label punctuation diverges from the document's own convention. The pre-existing custom
  components use `**Purpose:**`, `**Usage:**`, `**Anatomy:**`, `**States:**`, `**Accessibility:**` — colon,
  one per line. The new text uses `**Disclosure contract.**` (661) alongside those five colonned siblings in
  the same component block, and `**Page.** … **Owner.** … **Question it answers.**` (719, 745, 772). *Fix:*
  use colons for consistency.

- **[low]** Lines 719–720 pack three labelled fields into one wrapped paragraph, so the third label's value
  ("Is this workspace's state actually persisted…") starts on the following line and the block cannot be
  scanned. The other two sections (745, 772) repeat it. *Fix:* one label per line, as the component blocks do.

- **[low]** Line 908, "What the console does surface is an operator-safe correlation reference…" — a cleft
  construction in a specification. *Fix:* "The console surfaces an operator-safe correlation reference, …".

- **[low]** Line 792 uses `should` for an editorial obligation on the next author — "This section's signals
  should now be reconciled against the real rows" — inside a `[NOTE FOR UX]` whose whole purpose is to assign
  that work. Same note ends with an untagged "Note that…" aside (793–795). *Fix:* "Reconcile this section's
  signals against NFR74–NFR84 and resolve or remove each `[ASSUMPTION]` above."

**Clean:** UX-DR33 through UX-DR39 (lines 196–202) are well-judged register prose — imperative, one obligation
per row, consistent with UX-DR1–UX-DR32 in voice and length, and none of them hedges. The `### Non-Disclosing
Unavailability Envelopes` rule itself (lines 903–906) is the sharpest new writing in the document.
