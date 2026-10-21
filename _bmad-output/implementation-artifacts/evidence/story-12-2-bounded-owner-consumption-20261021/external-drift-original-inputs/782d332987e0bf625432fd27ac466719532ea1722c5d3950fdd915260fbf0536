"""Focused tests for the production-authority and minimal P1R gate."""

from __future__ import annotations

import importlib.util
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


PROJECT_ROOT = Path(__file__).resolve().parents[2]
GUARD_PATH = PROJECT_ROOT / "tools" / "planning" / "validate_production_authority.py"
SPEC = importlib.util.spec_from_file_location("validate_production_authority", GUARD_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Cannot load scheduling guard from {GUARD_PATH}")
GUARD = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GUARD)


class ProductionAuthorityGuardTests(unittest.TestCase):
    """Cover the open gate, acceptance record, and scheduling authority."""

    @classmethod
    def setUpClass(cls) -> None:
        def seed_open_field(
            text: str,
            section_start: str,
            section_end_prefix: str,
            field: str,
        ) -> str:
            lines = text.splitlines(keepends=True)
            section_starts = [
                index
                for index, line in enumerate(lines)
                if line.rstrip("\r\n") == section_start
            ]
            if len(section_starts) != 1:
                raise AssertionError(
                    f"expected one fixture section marker: {section_start}"
                )
            start = section_starts[0] + 1
            section_ends = [
                index
                for index in range(start, len(lines))
                if lines[index].rstrip("\r\n").startswith(section_end_prefix)
            ]
            if not section_ends:
                raise AssertionError(
                    f"missing fixture section boundary: {section_end_prefix}"
                )
            end = section_ends[0]
            open_value = f"{field}: open"
            accepted_value = f"{field}: done"
            matches = [
                index
                for index in range(start, end)
                if lines[index].rstrip("\r\n") in (open_value, accepted_value)
            ]
            if len(matches) != 1:
                raise AssertionError(
                    f"expected one complete fixture field line: {field}"
                )
            field_index = matches[0]
            current_value = lines[field_index].rstrip("\r\n")
            if current_value == open_value:
                return text
            line_ending = lines[field_index][len(current_value) :]
            lines[field_index] = open_value + line_ending
            return "".join(lines)

        sprint = GUARD.DEFAULT_SPRINT_STATUS.read_text(encoding="utf-8")
        sprint = seed_open_field(
            sprint,
            '    id: "6.1-P1R"',
            "  - epic:",
            "    status",
        )
        cls.sprint = seed_open_field(
            sprint,
            '    id: "6.1-P0"',
            "  - epic:",
            "      stage_1_p1r_baseline",
        )

        deferred = GUARD.DEFAULT_DEFERRED_WORK.read_text(encoding="utf-8")
        deferred_sections = (
            "### DW-35: Accept the selected P1R baseline through the minimal gate.",
            "### DW-68: Keep the P1R coordinate qualification open until "
            "the minimal record is accepted.",
        )
        for section_start in deferred_sections:
            deferred = seed_open_field(
                deferred,
                section_start,
                "### DW-",
                "status",
            )
        cls.deferred = deferred

        p0 = GUARD.DEFAULT_P0_ARTIFACT.read_text(encoding="utf-8")
        cls.p0 = seed_open_field(
            p0,
            "p1r_stage_1:",
            "capability_status:",
            "  status",
        )

    def replace_once(self, text: str, old: str, new: str) -> str:
        self.assertEqual(1, text.count(old), old)
        return text.replace(old, new, 1)

    def replace_after(self, text: str, marker: str, old: str, new: str) -> str:
        self.assertEqual(1, text.count(marker), marker)
        before, separator, after = text.partition(marker)
        self.assertIn(old, after)
        return before + separator + after.replace(old, new, 1)

    def accepted_sprint(self) -> str:
        result = self.replace_after(
            self.sprint,
            '    id: "6.1-P1R"',
            "    status: open",
            "    status: done",
        )
        return self.replace_once(
            result,
            "      stage_1_p1r_baseline: open",
            "      stage_1_p1r_baseline: done",
        )

    def accepted_deferred(self) -> str:
        result = self.deferred
        for entry_id in (35, 68):
            result = self.replace_after(
                result,
                f"### DW-{entry_id}:",
                "status: open",
                "status: done",
            )
        return result

    def accepted_p0(self) -> str:
        return self.replace_once(
            self.p0,
            "p1r_stage_1:\n  status: open",
            "p1r_stage_1:\n  status: done",
        )

    def acceptance_record(self) -> dict[str, object]:
        return {
            "schema": GUARD.ACCEPTANCE_SCHEMA,
            "selected": dict(GUARD.SELECTED_TUPLE),
            "rollback": dict(GUARD.ROLLBACK_TUPLE),
            "timestamp_utc": "2026-09-22T12:00:00Z",
            "decisions": {
                role: {
                    "decision": "accept",
                    "approver": "Jérôme Piquot",
                    "record": GUARD.ACCEPTANCE_RECORD_RELATIVE_PATH.as_posix(),
                    "selected": GUARD.SELECTED_REFERENCE,
                }
                for role in GUARD.ACCEPTED_ROLES
            },
        }

    def validate_boundary(
        self,
        *,
        sprint: str | bytes | None = None,
        deferred: str | bytes | None = None,
        p0: str | bytes | None = None,
        record: dict[str, object] | bytes | None = None,
        symlink_record: bool = False,
    ) -> tuple[int, ...]:
        with tempfile.TemporaryDirectory() as directory:
            workspace = Path(directory)
            sprint_path = workspace / "sprint-status.yaml"
            deferred_path = workspace / "deferred-work.md"
            p0_path = workspace / "p0.md"

            for path, value in (
                (sprint_path, self.sprint if sprint is None else sprint),
                (deferred_path, self.deferred if deferred is None else deferred),
                (p0_path, self.p0 if p0 is None else p0),
            ):
                if isinstance(value, bytes):
                    path.write_bytes(value)
                else:
                    path.write_text(value, encoding="utf-8")
            if record is not None:
                record_path = workspace / GUARD.ACCEPTANCE_RECORD_RELATIVE_PATH
                record_path.parent.mkdir(parents=True)
                payload = record if isinstance(record, bytes) else json.dumps(record).encode()
                if symlink_record:
                    target_path = record_path.with_name("acceptance-target.json")
                    target_path.write_bytes(payload)
                    record_path.symlink_to(target_path.name)
                else:
                    record_path.write_bytes(payload)
            return GUARD.validate_index(
                sprint_path,
                deferred_path,
                p0_path,
                workspace_root=workspace,
            )

    def test_selected_baseline_matches_the_explicit_current_owner_decision(self) -> None:
        self.assertEqual(
            {
                "eventstore_version": "3.110.0",
                "eventstore_tag": "v3.110.0",
                "eventstore_revision": "27279fe6431925a6ea046c3f89af61487185c7de",
                "builds_revision": "21ce044ab465ccb2adab58b3d66e394ffbecf3c2",
            },
            GUARD.SELECTED_TUPLE,
        )

    def test_historical_or_mixed_selected_tuple_cannot_accept_the_current_baseline(self) -> None:
        historical = {
            "eventstore_version": "3.106.0",
            "eventstore_tag": "v3.106.0",
            "eventstore_revision": "76051c70cbf868c40edc00ca0344fa5bd8879b69",
            "builds_revision": "ad52f350a2f0bc47849179ae17b4594dafff5363",
        }
        candidates = [("historical tuple", historical)]
        candidates.extend(
            (coordinate, {**GUARD.SELECTED_TUPLE, coordinate: value})
            for coordinate, value in historical.items()
        )
        candidates.append(
            (
                "later source checkout",
                {
                    **GUARD.SELECTED_TUPLE,
                    "eventstore_revision": "6dededdecd62dd6dc6d1f15810108d860ec70c8f",
                },
            )
        )
        for name, selected in candidates:
            with self.subTest(name=name):
                record = self.acceptance_record()
                record["selected"] = selected
                with self.assertRaisesRegex(GUARD.GuardViolation, "selected coordinate mismatch"):
                    self.validate_boundary(
                        sprint=self.accepted_sprint(),
                        deferred=self.accepted_deferred(),
                        p0=self.accepted_p0(),
                        record=record,
                    )

    def test_current_p1r_acceptance_keeps_stale_g6_prerequisites_unusable(self) -> None:
        sprint = self.accepted_sprint()
        self.assertEqual(
            GUARD.EXPECTED_PRODUCTION_EPICS,
            self.validate_boundary(
                sprint=sprint,
                deferred=self.accepted_deferred(),
                p0=self.accepted_p0(),
                record=self.acceptance_record(),
            ),
        )
        index = GUARD._load_yaml_text(sprint, "accepted current P1R fixture")
        self.assertIs(index["p1r_current_revalidation"]["usable_as_prerequisite"], False)
        self.assertIs(index["qualification_gates"]["G-6"]["current_candidate_usable"], False)
        action = next(item for item in index["action_items"] if item.get("id") == "6.1-P1R")
        self.assertIs(action["current_candidate_usable"], False)
        self.assertEqual("NOT_READY", index["readiness_provenance"]["current_result"])
        self.assertEqual("blocked", index["development_status"][GUARD.STORY_6_1_KEY])

    def test_remediation_cannot_advance_or_replace_accepted_p1r(self) -> None:
        sprint = self.accepted_sprint()
        self.assertEqual(
            GUARD.EXPECTED_PRODUCTION_EPICS,
            self.validate_boundary(
                sprint=sprint,
                deferred=self.accepted_deferred(),
                p0=self.accepted_p0(),
                record=self.acceptance_record(),
            ),
        )
        for status in ("done", "in-progress", "blocked-external"):
            candidate = self.replace_after(
                sprint,
                '    id: "6.1-P1R-remediation"',
                "    status: open",
                "    status: " + status,
            )
            with self.subTest(status=status):
                with self.assertRaisesRegex(GUARD.GuardViolation, "6.1-P1R-remediation"):
                    self.validate_boundary(
                        sprint=candidate,
                        deferred=self.accepted_deferred(),
                        p0=self.accepted_p0(),
                        record=self.acceptance_record(),
                    )
        renamed = self.replace_once(
            sprint,
            '    id: "6.1-P1R-remediation"',
            '    id: "6.1-P1R-replacement"',
        )
        with self.assertRaisesRegex(GUARD.GuardViolation, "inventory must remain exact"):
            self.validate_boundary(sprint=renamed)

    def test_open_gate_passes_without_acceptance_record(self) -> None:
        self.assertEqual(GUARD.EXPECTED_PRODUCTION_EPICS, self.validate_boundary())
        with self.assertRaisesRegex(GUARD.GuardViolation, "6.1-P1R"):
            self.validate_boundary(sprint=self.accepted_sprint())

    def test_current_p0_handoff_preserves_implemented_but_unaccepted_boundary(self) -> None:
        fields = GUARD._p0_fields(self.p0, "P0 fixture")
        self.assertEqual(
            {
                "manifest_contract": "implemented-unaccepted",
                "evidence_validator": "implemented-unaccepted",
                "persisted_runner": "source-implemented-historically-qualified-unaccepted",
                "published_tools": "historical-4.29.1-v1-no-current-v2-adoption",
                "supported_composition": "source-implemented-historically-qualified-unaccepted",
                "published_consumer_pin": "absent",
                "persisted_qualification": "historical-local-only-current-approved-tuple-not-run",
                "acceptance_record": "absent",
                "owner_acceptance": "absent",
            },
            fields["capability_status"],
        )
        self.assertEqual(
            (
                "usable-p1r",
                "approved-g6-tuple",
                "selected-p0-tool-runtime-tuple",
                "consumer-adoption",
                "current-persisted-qualification",
                "acceptance-record",
                "owner-acceptance",
            ),
            fields["blocked_by"],
        )
        self.assertEqual(GUARD.EXPECTED_PRODUCTION_EPICS, self.validate_boundary())

    def test_valid_acceptance_closes_only_the_four_authorized_items(self) -> None:
        self.assertEqual(
            GUARD.EXPECTED_PRODUCTION_EPICS,
            self.validate_boundary(
                sprint=self.accepted_sprint(),
                deferred=self.accepted_deferred(),
                p0=self.accepted_p0(),
                record=self.acceptance_record(),
            ),
        )

    def test_missing_or_rejected_role_fails_closed_and_names_role(self) -> None:
        for role, mutation in (
            (role, mutation)
            for role in GUARD.ACCEPTED_ROLES
            for mutation in ("missing", "reject")
        ):
            with self.subTest(role=role, mutation=mutation):
                record = self.acceptance_record()
                decisions = record["decisions"]
                assert isinstance(decisions, dict)
                if mutation == "missing":
                    del decisions[role]
                else:
                    decisions[role]["decision"] = "reject"
                with self.assertRaisesRegex(GUARD.GuardViolation, role):
                    self.validate_boundary(
                        sprint=self.accepted_sprint(),
                        deferred=self.accepted_deferred(),
                        p0=self.accepted_p0(),
                        record=record,
                    )

    def test_selected_and_rollback_coordinate_mismatches_fail_closed(self) -> None:
        for tuple_name, coordinate in (
            ("selected", "builds_revision"),
            ("rollback", "eventstore_revision"),
        ):
            with self.subTest(tuple_name=tuple_name, coordinate=coordinate):
                record = self.acceptance_record()
                coordinates = record[tuple_name]
                assert isinstance(coordinates, dict)
                coordinates[coordinate] = "0" * 40
                with self.assertRaisesRegex(
                    GUARD.GuardViolation, f"{tuple_name} coordinate mismatch"
                ):
                    self.validate_boundary(
                        sprint=self.accepted_sprint(),
                        deferred=self.accepted_deferred(),
                        p0=self.accepted_p0(),
                        record=record,
                    )

    def test_decision_metadata_and_timestamp_fail_closed(self) -> None:
        def mutate_decision(field: str, value: str) -> dict[str, object]:
            record = self.acceptance_record()
            decisions = record["decisions"]
            assert isinstance(decisions, dict)
            decisions["EventStore Owner"][field] = value
            return record

        cases = (
            ("rejected", mutate_decision("decision", "reject"), "EventStore Owner"),
            ("blank approver", mutate_decision("approver", " "), "EventStore Owner"),
            ("wrong record", mutate_decision("record", "wrong.json"), "EventStore Owner"),
            ("wrong selected", mutate_decision("selected", "#/rollback"), "EventStore Owner"),
            (
                "noncanonical timestamp",
                {**self.acceptance_record(), "timestamp_utc": "2026-09-22T12:00:00+00:00"},
                "canonical UTC",
            ),
        )
        for name, record, error in cases:
            with self.subTest(name=name):
                with self.assertRaisesRegex(GUARD.GuardViolation, error):
                    self.validate_boundary(
                        sprint=self.accepted_sprint(),
                        deferred=self.accepted_deferred(),
                        p0=self.accepted_p0(),
                        record=record,
                    )

    def test_each_required_closure_site_transitions_atomically(self) -> None:
        accepted_sprint = self.accepted_sprint()
        accepted_deferred = self.accepted_deferred()
        accepted_p0 = self.accepted_p0()
        cases = (
            (
                "P1R action",
                self.replace_after(
                    accepted_sprint,
                    '    id: "6.1-P1R"',
                    "    status: done",
                    "    status: open",
                ),
                accepted_deferred,
                accepted_p0,
                "6.1-P1R",
            ),
            (
                "sprint P0 Stage 1",
                self.replace_once(
                    accepted_sprint,
                    "      stage_1_p1r_baseline: done",
                    "      stage_1_p1r_baseline: open",
                ),
                accepted_deferred,
                accepted_p0,
                "Stage 1",
            ),
            (
                "P0 artifact Stage 1",
                accepted_sprint,
                accepted_deferred,
                self.replace_once(
                    accepted_p0,
                    "p1r_stage_1:\n  status: done",
                    "p1r_stage_1:\n  status: open",
                ),
                "Stage 1",
            ),
            (
                "DW-35",
                accepted_sprint,
                self.replace_after(
                    accepted_deferred, "### DW-35:", "status: done", "status: open"
                ),
                accepted_p0,
                "DW-35",
            ),
            (
                "DW-68",
                accepted_sprint,
                self.replace_after(
                    accepted_deferred, "### DW-68:", "status: done", "status: open"
                ),
                accepted_p0,
                "DW-68",
            ),
        )
        for name, sprint, deferred, p0, error in cases:
            with self.subTest(name=name):
                with self.assertRaisesRegex(GUARD.GuardViolation, error):
                    self.validate_boundary(
                        sprint=sprint,
                        deferred=deferred,
                        p0=p0,
                        record=self.acceptance_record(),
                    )

    def test_unauthorized_closure_fails_closed(self) -> None:
        sprint = self.replace_once(
            self.accepted_sprint(),
            f"  {GUARD.STORY_6_1_KEY}: blocked",
            f"  {GUARD.STORY_6_1_KEY}: ready-for-dev",
        )
        with self.assertRaisesRegex(GUARD.GuardViolation, "open boundary"):
            self.validate_boundary(
                sprint=sprint,
                deferred=self.accepted_deferred(),
                p0=self.accepted_p0(),
                record=self.acceptance_record(),
            )

    def test_each_downstream_category_remains_closed(self) -> None:
        accepted = self.accepted_sprint()
        story_6_5 = "  6-5-inspect-projects-through-an-authenticated-frontcomposer-read-surface"
        cases = (
            (
                "P0 action",
                self.replace_after(
                    accepted, '    id: "6.1-P0"', "    status: open", "    status: done"
                ),
                "6.1-P0",
            ),
            (
                "P0 remaining stages",
                self.replace_once(
                    accepted,
                    "      stages_2_through_7: open",
                    "      stages_2_through_7: done",
                ),
                "stages 2 through 7",
            ),
            (
                "P2",
                self.replace_after(
                    accepted, '    id: "6.1-P2"', "    status: open", "    status: done"
                ),
                "6.1-P2",
            ),
            (
                "P3",
                self.replace_after(
                    accepted, '    id: "6.1-P3"', "    status: open", "    status: done"
                ),
                "6.1-P3",
            ),
            (
                "P4",
                self.replace_after(
                    accepted, '    id: "6.1-P4"', "    status: open", "    status: done"
                ),
                "6.1-P4",
            ),
            (
                "readiness",
                self.replace_once(
                    accepted, "  current_result: NOT_READY", "  current_result: READY"
                ),
                "NOT_READY",
            ),
            (
                "Epic 6 completion",
                self.replace_once(accepted, "  epic-6: in-progress", "  epic-6: done"),
                "open boundary",
            ),
            (
                "Epic 6 regression",
                self.replace_once(accepted, "  epic-6: in-progress", "  epic-6: backlog"),
                "open boundary",
            ),
            (
                "Story 6.4 dispatch",
                self.replace_once(
                    accepted,
                    "  6-4-resolve-projects-with-transient-current-explanations: blocked",
                    "  6-4-resolve-projects-with-transient-current-explanations: ready-for-dev",
                ),
                "open boundary",
            ),
            (
                "Story 6.5 dispatch",
                self.replace_once(
                    accepted, f"{story_6_5}: blocked", f"{story_6_5}: ready-for-dev"
                ),
                "open boundary",
            ),
            (
                "Story 6.5 regression",
                self.replace_once(accepted, f"{story_6_5}: blocked", f"{story_6_5}: backlog"),
                "open boundary",
            ),
            (
                "Epic 7",
                self.replace_once(accepted, "  epic-7: backlog", "  epic-7: in-progress"),
                "open boundary",
            ),
            (
                "Epic 8",
                self.replace_once(accepted, "  epic-8: backlog", "  epic-8: in-progress"),
                "open boundary",
            ),
        )
        for name, sprint, error in cases:
            with self.subTest(name=name):
                with self.assertRaisesRegex(GUARD.GuardViolation, error):
                    self.validate_boundary(
                        sprint=sprint,
                        deferred=self.accepted_deferred(),
                        p0=self.accepted_p0(),
                        record=self.acceptance_record(),
                    )

    def test_p0_capability_fields_cannot_advance_or_regress(self) -> None:
        capabilities = GUARD._p0_fields(self.p0, "P0 fixture")["capability_status"]
        cases = [(field, "accepted") for field in capabilities]
        cases.extend(
            (
                ("persisted_runner", "not-available"),
                ("published_tools", "published-stale-pin-not-supported"),
                ("published_tools", "historical-4.29.2-v1-no-current-v2-adoption"),
                ("supported_composition", "not-implemented"),
                ("persisted_qualification", "not-run"),
            )
        )
        for field, value in cases:
            for accepted in (False, True):
                with self.subTest(field=field, value=value, p1r_accepted=accepted):
                    candidate = self.replace_once(
                        self.accepted_p0() if accepted else self.p0,
                        f"  {field}: {capabilities[field]}\n",
                        f"  {field}: {value}\n",
                    )
                    with self.assertRaisesRegex(GUARD.GuardViolation, "P0 capability_status"):
                        self.validate_boundary(
                            sprint=self.accepted_sprint() if accepted else None,
                            deferred=self.accepted_deferred() if accepted else None,
                            p0=candidate,
                            record=self.acceptance_record() if accepted else None,
                        )

    def test_each_p0_blocker_must_remain_in_the_recorded_order(self) -> None:
        blockers = GUARD._p0_fields(self.p0, "P0 fixture")["blocked_by"]
        candidates = [blockers[:index] + blockers[index + 1 :] for index in range(len(blockers))]
        candidates.append(tuple(reversed(blockers)))
        for candidate_blockers in candidates:
            for accepted in (False, True):
                with self.subTest(blockers=candidate_blockers, p1r_accepted=accepted):
                    candidate = self.replace_once(
                        self.accepted_p0() if accepted else self.p0,
                        f"blocked_by: [{', '.join(blockers)}]",
                        f"blocked_by: [{', '.join(candidate_blockers)}]",
                    )
                    with self.assertRaisesRegex(GUARD.GuardViolation, "P0 blocked_by"):
                        self.validate_boundary(
                            sprint=self.accepted_sprint() if accepted else None,
                            deferred=self.accepted_deferred() if accepted else None,
                            p0=candidate,
                            record=self.acceptance_record() if accepted else None,
                        )

    def test_p0_capability_inventory_must_remain_exact(self) -> None:
        line = "  published_tools: historical-4.29.1-v1-no-current-v2-adoption\n"
        for replacement in ("", line + "  unknown_capability: accepted\n"):
            with self.subTest(replacement=replacement):
                candidate = self.replace_once(self.p0, line, replacement)
                with self.assertRaisesRegex(GUARD.GuardViolation, "P0 capability_status"):
                    self.validate_boundary(p0=candidate)

    def test_duplicate_p0_capability_fields_fail_closed(self) -> None:
        for field, value in (
            ("manifest_contract", "implemented-unaccepted"),
            ("published_tools", "historical-4.29.1-v1-no-current-v2-adoption"),
        ):
            for duplicate_value in (value, "accepted"):
                with self.subTest(field=field, duplicate_value=duplicate_value):
                    line = f"  {field}: {value}\n"
                    candidate = self.replace_once(
                        self.p0, line, line + f"  {field}: {duplicate_value}\n"
                    )
                    with self.assertRaisesRegex(GUARD.GuardViolation, "unique named fields"):
                        self.validate_boundary(p0=candidate)

    def test_versioned_p0_capability_values_reject_trailing_content(self) -> None:
        for suffix in (" accepted", ": accepted", " # accepted", "\taccepted"):
            with self.subTest(suffix=suffix):
                line = "  published_tools: historical-4.29.1-v1-no-current-v2-adoption"
                candidate = self.replace_once(self.p0, line + "\n", line + suffix + "\n")
                with self.assertRaisesRegex(GUARD.GuardViolation, "unique named fields"):
                    self.validate_boundary(p0=candidate)

    def test_acceptance_record_symlink_fails_closed(self) -> None:
        with self.assertRaisesRegex(GUARD.GuardViolation, "symlink"):
            self.validate_boundary(
                sprint=self.accepted_sprint(),
                deferred=self.accepted_deferred(),
                p0=self.accepted_p0(),
                record=self.acceptance_record(),
                symlink_record=True,
            )

    def test_malformed_json_fails_closed(self) -> None:
        with self.assertRaisesRegex(GUARD.GuardViolation, "malformed acceptance record"):
            self.validate_boundary(
                sprint=self.accepted_sprint(),
                deferred=self.accepted_deferred(),
                p0=self.accepted_p0(),
                record=b'{"schema":',
            )

    def test_invalid_utf8_fails_closed(self) -> None:
        with self.assertRaisesRegex(GUARD.GuardViolation, "cannot read sprint status"):
            self.validate_boundary(sprint=b"\xff")

    def test_current_production_epics_are_allowed(self) -> None:
        for epic, story in ((6, 1), (6, 4), (7, 15), (8, 11)):
            with self.subTest(epic=epic):
                self.assertEqual((epic, story), GUARD.validate_story_request(f"{epic}.{story}"))

    def test_story_mode_uses_all_supplied_paths_and_reads_sprint_once(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            workspace = Path(directory)
            sprint_path = workspace / "candidate-sprint.yaml"
            deferred_path = workspace / "candidate-deferred.md"
            p0_path = workspace / "candidate-p0.md"
            sprint_path.write_text(self.sprint, encoding="utf-8")
            deferred_path.write_text(self.deferred, encoding="utf-8")
            p0_path.write_text(self.p0, encoding="utf-8")

            with mock.patch.object(
                GUARD, "_read_text", wraps=GUARD._read_text
            ) as read_text:
                self.assertEqual(
                    (6, 1),
                    GUARD.validate_story_request(
                        "6.1",
                        sprint_path,
                        deferred_path,
                        p0_path,
                        workspace_root=workspace,
                    ),
                )

            read_paths = [call.args[0] for call in read_text.call_args_list]
            self.assertEqual(1, read_paths.count(sprint_path))
            self.assertEqual({sprint_path, deferred_path, p0_path}, set(read_paths))

    def test_historical_and_unknown_stories_are_rejected(self) -> None:
        for story_id in ("1.1", "5.12", "9.1", "6.1.2", "epic-6"):
            with self.subTest(story_id=story_id):
                with self.assertRaises(GUARD.GuardViolation):
                    GUARD.assert_story_allowed(story_id)
        with self.assertRaisesRegex(GUARD.GuardViolation, "exactly one"):
            GUARD.validate_story_request("6.999")

    def test_scheduling_authority_and_history_fail_closed(self) -> None:
        mutations = (
            self.replace_once(
                self.sprint,
                "production_authority_epics: [6, 7, 8]",
                "production_authority_epics: [5, 6, 7, 8]",
            ),
            self.replace_once(
                self.sprint,
                "  1-1-module-scaffold-build-ci-wiring: done",
                "  1-1-module-scaffold-build-ci-wiring: backlog",
            ),
            self.sprint + "\nproduction_authority_epics: [6, 7, 8]\n",
        )
        for candidate in mutations:
            with self.subTest(candidate=candidate[-80:]):
                with self.assertRaises(GUARD.GuardViolation):
                    self.validate_boundary(sprint=candidate)

    def test_command_line_exit_codes_enforce_the_boundary(self) -> None:
        environment = {**os.environ, "PYTHONDONTWRITEBYTECODE": "1"}
        for arguments, expected in (
            (("--validate-index",), 0),
            (("--story-id", "6.1"), 0),
            (("--story-id", "6.4"), 0),
            (("--story-id", "1.1"), 1),
        ):
            with self.subTest(arguments=arguments):
                completed = subprocess.run(
                    [sys.executable, str(GUARD_PATH), *arguments],
                    cwd=PROJECT_ROOT,
                    env=environment,
                    capture_output=True,
                    text=True,
                    check=False,
                )
                self.assertEqual(expected, completed.returncode, completed.stderr)

    def test_workflows_and_ci_consume_only_the_minimal_guard(self) -> None:
        project_context = (
            PROJECT_ROOT / "_bmad-output" / "project-context.md"
        ).read_text(encoding="utf-8")
        self.assertIn("validate_production_authority.py --story-id <epic.story>", project_context)
        self.assertIn("--validate-index --sprint-status <candidate>", project_context)

        ci = (PROJECT_ROOT / ".github" / "workflows" / "ci.yml").read_text(
            encoding="utf-8"
        )
        self.assertIn("pyyaml==6.0.3", ci)
        self.assertIn("tests/tools/test_production_authority_guard.py", ci)
        self.assertNotIn("test_p1r_" + "postacceptance_guard.py", ci)


if __name__ == "__main__":
    unittest.main()
