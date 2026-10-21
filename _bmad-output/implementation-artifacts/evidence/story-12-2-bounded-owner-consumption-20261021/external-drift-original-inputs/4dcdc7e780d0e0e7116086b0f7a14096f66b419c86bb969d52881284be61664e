"""Offline selected-contract refusals; handwritten inputs are never live evidence."""
from contextlib import redirect_stdout
from copy import deepcopy
from datetime import datetime, timedelta, timezone
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as xml

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools"))
import access_telemetry_functional_retention as functional

CLI_SPEC = importlib.util.spec_from_file_location("selected_retention_cli", ROOT / "tools" / "verify-access-telemetry-lifecycle.py")
CLI = importlib.util.module_from_spec(CLI_SPEC)
CLI_SPEC.loader.exec_module(CLI)


class SelectedConsumerTests(unittest.TestCase):
    def fixture(self, directory):
        root = Path(directory)
        workspace = root / "run"
        candidate = root / "platform" / "references" / "Hexalith.Memories"
        for path in (workspace / "native-tests", candidate / "tools", candidate / "module"):
            path.mkdir(parents=True, mode=0o700)
        workspace.chmod(0o700)
        entry = candidate / "tools" / "verify-access-telemetry-lifecycle.py"
        entry.write_bytes(b"# inert selected entry fixture\n")
        manifest = candidate / "module" / "hexalith.module-manifest.v2.json"
        manifest.write_bytes(b"{}")
        selection = {"moduleId": "memories", "name": "functional-retention", "entry": "tools/verify-access-telemetry-lifecycle.py", "mode": "source", "daprRuntimeVersion": "1.18.1", "executionTimeoutSeconds": 14400, "stopTimeoutSeconds": 900, "manifestHash": hashlib.sha256(manifest.read_bytes()).hexdigest().upper(), "entryHash": hashlib.sha256(entry.read_bytes()).hexdigest().upper(), "executionDeadlineUtc": (datetime.now(timezone.utc) + timedelta(hours=4)).isoformat()}
        plan = {"schema": "hexalith.g4-run-plan.v1", "runId": "offline-run", "selectedTest": selection, "sourceMapping": {"root": str(root / "platform")}}
        readiness = {"schema": "hexalith.g4-run-readiness.v1", "runId": plan["runId"], "selectedTest": deepcopy(selection), "resources": [{"name": "offline-fixture", "state": "Healthy"}], "testObservation": None}
        environment = {"HEXALITH_G4_RUN_ID": plan["runId"], "HEXALITH_G4_TEST_NAME": "functional-retention", "HEXALITH_G4_TEST_OWNER": "memories", "HEXALITH_G4_TEST_MODE": "source", "HEXALITH_G4_PLAN_PATH": str(workspace / "plan.json"), "HEXALITH_G4_READINESS_PATH": str(workspace / "readiness.json"), "HEXALITH_G4_TEST_REPORT_PATH": str(workspace / "native-tests" / "native.trx"), "HEXALITH_G4_TEST_MANIFEST_HASH": selection["manifestHash"], "HEXALITH_G4_TEST_ENTRY_HASH": selection["entryHash"], "HEXALITH_G4_ACCESS_TOKEN": "fixture-secret-never-retained"}
        return workspace, candidate, plan, readiness, environment

    def invoke(self, candidate, environment):
        with patch.dict(os.environ, environment, clear=True), patch.object(functional, "__file__", str(candidate / "tools" / "access_telemetry_functional_retention.py")), patch("subprocess.Popen") as spawn, patch("subprocess.run") as run, patch("urllib.request.OpenerDirector.open") as contact, patch.object(functional, "_PlatformRetentionConsumer") as consumer, redirect_stdout(io.StringIO()) as output:
            result = CLI.main([])
        for operation in (spawn, run, contact, consumer):
            operation.assert_not_called()
        text = output.getvalue()
        self.assertNotIn("fixture-secret-never-retained", text)
        self.assertNotIn(str(candidate), text)
        self.assertEqual(1, result)
        self.assertFalse(json.loads(text)["qualificationCredit"])
        return json.loads(text)

    def test_missing_existing_observation_contract_produces_failed_native_trx(self):
        with tempfile.TemporaryDirectory() as directory:
            workspace, candidate, plan, readiness, environment = self.fixture(directory)
            for name, value in (("plan.json", plan), ("readiness.json", readiness)):
                path = workspace / name
                path.write_text(json.dumps(value))
                path.chmod(0o600)
            self.invoke(candidate, environment)
            report = xml.parse(environment["HEXALITH_G4_TEST_REPORT_PATH"])
            counters = report.find("{*}ResultSummary/{*}Counters")
            self.assertEqual("1", counters.attrib["total"])
            self.assertEqual("1", counters.attrib["failed"])
            self.assertEqual("0", counters.attrib["passed"])
            self.assertNotIn("fixture-secret-never-retained", Path(environment["HEXALITH_G4_TEST_REPORT_PATH"]).read_text())

    def test_foreign_partial_malformed_source_and_short_lifetime_refuse_before_access(self):
        for variant in ("foreign", "partial", "malformed", "entry-drift", "lifetime", "profile-owner"):
            with self.subTest(variant=variant), tempfile.TemporaryDirectory() as directory:
                workspace, candidate, plan, readiness, environment = self.fixture(directory)
                if variant == "foreign":
                    readiness["runId"] = "foreign"
                elif variant == "partial":
                    readiness["resources"][0]["state"] = "Starting"
                elif variant == "entry-drift":
                    (candidate / plan["selectedTest"]["entry"]).write_text("changed")
                elif variant == "lifetime":
                    plan["selectedTest"]["executionTimeoutSeconds"] = 900
                elif variant == "profile-owner":
                    plan["selectedTest"]["moduleId"] = "foreign"
                for name, value in (("plan.json", plan), ("readiness.json", readiness)):
                    path = workspace / name
                    path.write_text("{" if variant == "malformed" and name == "readiness.json" else json.dumps(value))
                    path.chmod(0o600)
                result = self.invoke(candidate, environment)
                expected = {"foreign": "functional-selected-readiness-identity", "partial": "functional-partial-startup", "malformed": "functional-selected-input-refused", "entry-drift": "functional-selected-source-drift", "lifetime": "functional-selected-lifetime", "profile-owner": "functional-selected-entry"}
                self.assertEqual(expected[variant], result["reason"])

    def test_foreign_postgresql_claim_refuses_even_with_claimed_complete_observations(self):
        with tempfile.TemporaryDirectory() as directory:
            workspace, candidate, plan, readiness, environment = self.fixture(directory)
            plan["sourceMappingHash"] = "a" * 64
            catalog = {image["id"]: image["reference"] for image in functional.artifact_catalog()["images"]}
            profile = {"purpose": "local-functional-retention", "runId": plan["runId"], "sourceMappingHash": plan["sourceMappingHash"], "retentionSeconds": 3600, "sslMode": "verify-full", "qualificationCredit": False, "runtimeImage": catalog["daprd"], "postgresqlImage": "foreign-postgresql"}
            raw_profile = json.dumps(profile, separators=(",", ":"))
            readiness["testObservation"] = {"isComplete": True, "runId": plan["runId"], "moduleId": "memories", "runtimeArtifactVerified": True, "profileJson": raw_profile, "profileHash": hashlib.sha256(raw_profile.encode()).hexdigest(), "observedBackendIdentity": "foreign-postgresql/180006|t|TLSv1.3"}
            for name, value in (("plan.json", plan), ("readiness.json", readiness)):
                path = workspace / name
                path.write_text(json.dumps(value))
                path.chmod(0o600)
            self.assertEqual("functional-selected-backend", self.invoke(candidate, environment)["reason"])

    def test_foreign_report_path_cannot_create_or_overwrite_an_output(self):
        with tempfile.TemporaryDirectory() as directory:
            workspace, candidate, plan, readiness, environment = self.fixture(directory)
            foreign = Path(directory) / "foreign.trx"
            environment["HEXALITH_G4_TEST_REPORT_PATH"] = str(foreign)
            self.invoke(candidate, environment)
            self.assertFalse(foreign.exists())

    def test_missing_selected_identity_refuses_without_any_file_io(self):
        with patch.dict(os.environ, {}, clear=True), patch("builtins.open") as standard, patch("os.open") as raw, patch("pathlib.Path.open") as path, redirect_stdout(io.StringIO()) as output:
            self.assertEqual(1, functional.run_selected_platform_retention())
        for operation in (standard, raw, path):
            operation.assert_not_called()
        self.assertEqual("functional-selected-test-required", json.loads(output.getvalue())["reason"])


if __name__ == "__main__":
    unittest.main()
