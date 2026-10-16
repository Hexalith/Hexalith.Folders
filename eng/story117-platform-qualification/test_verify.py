"""Mutation tests prove the packet stays inert when its JSON is edited."""

import contextlib
import importlib.util
import io
import json
from pathlib import Path
import shutil
import tempfile
import unittest
from unittest.mock import patch


DIRECTORY = Path(__file__).resolve().parent
MODULE_SPEC = importlib.util.spec_from_file_location("story117_packet_verify", DIRECTORY / "verify.py")
verify = importlib.util.module_from_spec(MODULE_SPEC)
MODULE_SPEC.loader.exec_module(verify)


class PacketMutationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.packet = Path(self.temporary.name)
        for name in ("configuration.proposed.json", "staging-checks.json"):
            shutil.copyfile(DIRECTORY / name, self.packet / name)

    def edit(self, name, change):
        path = self.packet / name
        data = json.loads(path.read_text(encoding="utf-8"))
        change(data)
        path.write_text(json.dumps(data), encoding="utf-8")

    def assert_rejected(self, pattern):
        with self.assertRaisesRegex(verify.PacketError, pattern):
            verify.validate_packet(self.packet, verify.WORKSPACE)

    def test_honest_proposal_and_diagnostic_are_explicitly_not_qualification(self):
        verify.validate_packet(self.packet, verify.WORKSPACE)
        output = io.StringIO()
        original_validate = verify.validate_packet
        with patch.object(verify, "validate_packet", lambda: original_validate(self.packet, verify.WORKSPACE)), \
                patch.object(verify.sys, "argv", ["verify.py"]), contextlib.redirect_stdout(output):
            self.assertEqual(0, verify.main())
        self.assertIn("packet consistency only", output.getvalue())
        self.assertIn("operational qualification remains HOLD", output.getvalue())
        self.assertIn("staging checks executed: 0", output.getvalue())

    def test_route_activation_is_rejected_in_either_document_or_module_key(self):
        mutations = (
            ("configuration.proposed.json", lambda d: d["productionControl"].update(routeMode="Coexistence")),
            ("staging-checks.json", lambda d: d["productionControl"].update(routeMode="V2Only")),
            ("configuration.proposed.json", lambda d: d["moduleConfiguration"]["folders-server"]["values"].update({"Folders:ApiRouting:Mode": "V2Only"})),
        )
        for name, change in mutations:
            with self.subTest(name=name):
                original = (self.packet / name).read_bytes()
                self.edit(name, change)
                self.assert_rejected("routeMode|Folders:ApiRouting:Mode")
                (self.packet / name).write_bytes(original)

    def test_claimed_production_authority_or_clock_is_rejected(self):
        for field, value in (("activationAuthority", "Administrator"), ("deploymentAuthorized", True),
                             ("publicationAuthorized", True), ("trackerTransitionAuthorized", True),
                             ("T0", "2026-10-16T00:00:00Z")):
            with self.subTest(field=field):
                path = self.packet / "configuration.proposed.json"
                original = path.read_bytes()
                self.edit(path.name, lambda d: d["productionControl"].update({field: value}))
                self.assert_rejected(field)
                path.write_bytes(original)

    def test_fabricated_passing_staging_evidence_is_rejected_even_if_marked_executed(self):
        for evidence in (
            dict(result="pass"),
            dict(status="passed", result="pass", observedEvidence=["invented-run"]),
            dict(status="executed", result="pass", startedAt="2026-10-16T00:00:00Z", completedAt="2026-10-16T00:01:00Z"),
            dict(observedEvidence=["invented-run"]),
        ):
            with self.subTest(evidence=evidence):
                path = self.packet / "staging-checks.json"
                original = path.read_bytes()
                self.edit(path.name, lambda d: d["checks"][0].update(evidence))
                self.assert_rejected("no executed evidence|status")
                path.write_bytes(original)

    def test_removing_any_required_check_is_rejected(self):
        path = self.packet / "staging-checks.json"
        original = path.read_bytes()
        count = len(json.loads(original)["checks"])
        for index in range(count):
            with self.subTest(index=index):
                self.edit(path.name, lambda d: d["checks"].pop(index))
                self.assert_rejected("required staging checks")
                path.write_bytes(original)

    def test_blank_owner_missing_evidence_or_unbounded_check_is_rejected(self):
        path = self.packet / "staging-checks.json"
        original = path.read_bytes()
        for mutation, message in ((dict(owner=" "), "owner"), (dict(requiredEvidence=[]), "requiredEvidence"),
                                  (dict(maxDurationSeconds=0), "finite duration"), (dict(maxDurationSeconds=True), "finite duration")):
            with self.subTest(mutation=mutation):
                self.edit(path.name, lambda d: d["checks"][0].update(mutation))
                self.assert_rejected(message)
                path.write_bytes(original)

    def test_unknown_authority_key_and_fixture_auth_are_rejected(self):
        path = self.packet / "configuration.proposed.json"
        original = path.read_bytes()
        self.edit(path.name, lambda d: d.update(approve=True))
        self.assert_rejected("required keys")
        path.write_bytes(original)
        self.edit(path.name, lambda d: d["moduleConfiguration"]["folders-ui"]["values"].update({"Folders:Authentication:Mode": "hermetic-test"}))
        self.assert_rejected("Folders:Authentication:Mode")

    def test_unresolved_hosting_and_release_cannot_be_claimed_qualified(self):
        path = self.packet / "configuration.proposed.json"
        original = path.read_bytes()
        for change, message in (
            (lambda d: d["platformBindings"]["release-and-hosting"].update(value="qualified"), "unresolved value"),
            (lambda d: d["releaseIdentity"].update(applicationPackageDigest="sha256:" + "0" * 64), "applicationPackageDigest"),
            (lambda d: d.update(qualification="PASS"), "qualification"),
        ):
            with self.subTest(message=message):
                self.edit(path.name, change)
                self.assert_rejected(message)
                path.write_bytes(original)

    def test_source_manifest_cannot_remove_a_source_or_read_arbitrary_paths(self):
        path = self.packet / "configuration.proposed.json"
        original = path.read_bytes()
        for change in (lambda d: d["sourceInputs"].pop(next(iter(d["sourceInputs"]))),
                       lambda d: d["sourceInputs"].update({"../../unrelated": "0" * 64})):
            self.edit(path.name, change)
            self.assert_rejected("sourceInputs")
            path.write_bytes(original)

    def test_changed_source_fingerprint_is_rejected(self):
        self.edit("configuration.proposed.json", lambda d: d["sourceInputs"].update({
            "src/Hexalith.Folders.Server/FoldersApiRouting.cs": "0" * 64,
        }))
        self.assert_rejected("source changed: src/Hexalith.Folders.Server/FoldersApiRouting.cs")

    def test_removed_module_key_fails_the_cli_and_preserves_hold_diagnostic(self):
        self.edit("configuration.proposed.json", lambda d: d["moduleConfiguration"]["folders-workers"]["values"].pop("APP_API_TOKEN"))
        output = io.StringIO()
        original_validate = verify.validate_packet
        with patch.object(verify, "validate_packet", lambda: original_validate(self.packet, verify.WORKSPACE)), \
                patch.object(verify.sys, "argv", ["verify.py"]), contextlib.redirect_stderr(output):
            self.assertEqual(1, verify.main())
        self.assertIn("required keys differ", output.getvalue())
        self.assertIn("operational qualification remains HOLD", output.getvalue())

    def test_duplicate_json_keys_are_rejected(self):
        path = self.packet / "configuration.proposed.json"
        data = path.read_text(encoding="utf-8")
        path.write_text(data.replace('"routeMode": "V1Only"', '"routeMode": "V1Only", "routeMode": "Coexistence"'), encoding="utf-8")
        self.assert_rejected("duplicate JSON key")

    def test_execution_or_approval_arguments_are_rejected(self):
        for flag in ("--execute", "--approve"):
            output = io.StringIO()
            with self.subTest(flag=flag), patch.object(verify.sys, "argv", ["verify.py", flag]), contextlib.redirect_stderr(output):
                self.assertEqual(2, verify.main())
                self.assertIn("no arguments", output.getvalue())


if __name__ == "__main__":
    unittest.main()
