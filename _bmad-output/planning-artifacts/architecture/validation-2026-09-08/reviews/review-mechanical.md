# Mechanical validation — 2026-09-08

Target: `../../architecture.md` relative to the validation folder (the canonical legacy architecture document). Existing run memory explicitly preserves the classic D-/A-/C-/F- decision identifiers and rejects an automatic migration to ARCHITECTURE-SPINE format.

## Verdict

No confirmed placeholder defect. Native spine-format validation is not applicable; semantic reviewers must assess the legacy decision tables. This is not a full linter pass.

## Exact native command and result

```sh
uv run .agents/skills/bmad-architecture/scripts/lint_spine.py --workspace _bmad-output/planning-artifacts/architecture/architecture-folders-2026-07-19
```

Exit 0, but the JSON reports `ok: false`, `total_findings: 0`, and error `_bmad-output/planning-artifacts/architecture/architecture-folders-2026-07-19/ARCHITECTURE-SPINE.md not found`. The tool always exits zero; exit status must not be interpreted as success.

## Read-only fallback

The existing linter's `lint()` function was applied directly to the actual document without copying, renaming, or changing it:

```sh
uv run python -c 'import importlib.util,json,pathlib; p=pathlib.Path(".agents/skills/bmad-architecture/scripts/lint_spine.py"); s=importlib.util.spec_from_file_location("spine_lint",p); m=importlib.util.module_from_spec(s); s.loader.exec_module(m); r=m.lint(pathlib.Path("_bmad-output/planning-artifacts/architecture.md").read_text()); r["spine"]="architecture.md"; r["scope_note"]="Legacy artifact: AD fields and Stack table checks do not cover D/A/C/F table format"; print(json.dumps(r,indent=2))'
```

Exit 0; JSON `ok: false`, 9 candidates: 1 high and 8 low. The function hardcodes `ARCHITECTURE-SPINE.md` in finding locations; all line numbers below actually refer to `architecture.md`.

| Line | Candidate | Disposition | Reason |
| --- | --- | --- | --- |
| 253 | `TBD` | Ignore | Historical quotation: promoted from “TBD” to blocking decision; not an unresolved placeholder. The C0 governance record is explicitly the live authority. |
| 78 | `{tenant}`, `{domain}` | Ignore | Literal components of the aggregate identity pattern. |
| 501 | `{domain}` | Ignore | Literal identity/key pattern. |
| 752 | `{domain}` | Ignore | Literal event naming convention. |
| 822 | `{domain}` | Ignore | Literal event naming convention. |
| 823 | `{domain}` | Ignore | Literal event naming convention. |
| 825 | `{domain}` | Ignore | Literal event naming convention. |
| 1558 | `{env}` | Ignore | Literal deployment configuration naming pattern. |

Duplicate/non-monotonic AD identifiers, AD Binds/Prevents/Rule fields, and a `## Stack` table are not exercised by the legacy format. Their lack of findings provides no coverage claim. Reviewers assess decision consistency and technology pins independently.
