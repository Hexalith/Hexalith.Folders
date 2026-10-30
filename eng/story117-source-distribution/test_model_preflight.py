"""Exercise refusal, native Git authority and portable no-execution replay."""
import copy
import hashlib
import io
import json
from pathlib import Path
import subprocess
import tarfile
import tempfile
import unittest
from unittest.mock import patch

import qualify_model as model
import verify_model as replay


class ModelPreflightTests(unittest.TestCase):
    def test_ambient_import_controls_refuse_before_any_execution(self):
        for name in (*model.q.IMPORTS, "AlternateCommonProps", "MSBuildSDKsPath", "HEXALITH_TEST_DOTNET_EXECUTABLE"):
            with self.subTest(name=name), patch("subprocess.run", side_effect=AssertionError("must not execute")):
                with self.assertRaises(model.q.GateFailure):
                    model.preflight({name: "/foreign", "PATH": "/unused"})

    def test_recorded_cache_escape_refuses_without_filesystem_resolution(self):
        self.assertTrue(replay.private_path("/private/cache", "/private"))
        for value in ("/private/../outside", "/private/cache/../../outside", "/private/./cache", "/outside", "relative/cache"):
            with self.subTest(value=value):
                self.assertFalse(replay.private_path(value, "/private"))

    def test_missing_installed_sdk_refuses_without_installing(self):
        with patch.object(model.shutil, "which", return_value=None), self.assertRaises(model.q.GateFailure):
            model.preflight({"PATH": "/unavailable"})

    def test_unsafe_control_or_shared_cache_refuses_without_execution(self):
        with tempfile.TemporaryDirectory() as temporary:
            work = Path(temporary)
            environment = dict(model.q.CONTROLS, HOME="/preserved")
            for key in model.q.PRIVATE:
                environment[key] = str(work / key.lower())
            model.validate_environment(environment, work, "/preserved")
            mutations = [(key, "unsafe") for key in model.q.CONTROLS]
            mutations += [(key, "/outside") for key in model.q.PRIVATE]
            mutations += [("HOME", "/changed"), ("AlternateCommonProps", "/injected"), ("DOTNET_STARTUP_HOOKS", "/injected")]
            for key, value in mutations:
                selected = dict(environment, **{key: value})
                with self.subTest(key=key), patch.object(subprocess, "Popen", side_effect=AssertionError("must not execute")):
                    with self.assertRaises(model.q.GateFailure):
                        model.validate_environment(selected, work, "/preserved")
                self.assertEqual(selected[key], value, "preflight must refuse without repairing inputs")

    def test_revision_or_index_drift_refuses_before_dependent_commands(self):
        qualification = object.__new__(model.ModelQualification)
        qualification.baseline_repos = [{"revision": "approved", "index": "approved"}]
        with patch.object(model.q, "repository_state", return_value=[{"revision": "drifted", "index": "approved"}]), patch.object(subprocess, "Popen", side_effect=AssertionError("must not execute")):
            with self.assertRaises(model.q.GateFailure):
                qualification.inputs()

    def test_native_git_object_proves_gitlink_and_refuses_false_authority(self):
        documents, inventory, objects = {}, {}, []
        def add(kind, raw):
            oid = hashlib.sha1(kind.encode() + b" " + str(len(raw)).encode() + b"\0" + raw).hexdigest()
            path = "git-objects/" + oid
            item = dict(path=path, bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest())
            documents[path], inventory[path] = raw, item
            objects.append(dict(kind=kind, oid=oid, file=item))
            return oid
        selected = "a" * 40
        refs = add("tree", b"160000 Hexalith.Builds\0" + bytes.fromhex(selected))
        tree = add("tree", b"40000 references\0" + bytes.fromhex(refs))
        commit = add("commit", ("tree " + tree + "\n\nfixture\n").encode())
        authority = dict(revision=commit, objects=objects, paths={"references/Hexalith.Builds": dict(kind="commit", oid=selected)})
        self.assertEqual(replay.verify_git_path(authority, "references/Hexalith.Builds", documents, inventory)[:2], (selected, "commit"))
        wrong = copy.deepcopy(authority)
        wrong["paths"]["references/Hexalith.Builds"]["oid"] = "b" * 40
        with self.assertRaises(ValueError):
            replay.verify_git_path(wrong, "references/Hexalith.Builds", documents, inventory)
        damaged = dict(documents)
        damaged[objects[0]["file"]["path"]] += b"tampered"
        with self.assertRaises(ValueError):
            replay.verify_git_path(authority, "references/Hexalith.Builds", damaged, inventory)


class ModelReplayTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.base = Path(self.directory.name)
        self.result = dict(qualification="incomplete", protectedAuthority=replay.AUTHORITY,
                           ordinaryStartup="pending", signedDistribution="pending", operationalAcceptance="pending",
                           historicalEvidence=dict(status="not reused"), preservation=dict(status="verified"),
                           firstFailure=dict(owner="fixture", detail="refused before compilation"), commands=[])

    def capsule(self, result=None, extra=None):
        result = copy.deepcopy(self.result if result is None else result)
        payloads = {"result.json": (json.dumps(result) + "\n").encode()}
        payloads.update(extra or {})
        members = []
        archive = self.base / "artifacts.tar.gz"
        with tarfile.open(archive, "w:gz") as capsule:
            for name, raw in payloads.items():
                item = tarfile.TarInfo(name)
                item.size = len(raw)
                capsule.addfile(item, io.BytesIO(raw))
                members.append(dict(path=name, bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest()))
        manifest = self.base / "members.json"
        manifest.write_text(json.dumps(members))
        def bind(path):
            return dict(path=path.name, bytes=path.stat().st_size, sha256=hashlib.sha256(path.read_bytes()).hexdigest())
        receipt = self.base / "receipt.json"
        receipt.write_text(json.dumps(dict(schemaVersion=1, privateWorkspace="/private", archive=bind(archive), memberManifest=bind(manifest), memberCount=len(members), result=result)))
        return receipt

    def test_intact_incomplete_capsule_is_data_only(self):
        path = self.capsule()
        with patch.object(subprocess, "Popen", side_effect=AssertionError("must not execute")), patch.object(subprocess, "run", side_effect=AssertionError("must not execute")):
            result = replay.replay(path)
        self.assertEqual(result["qualification"], "incomplete")
        self.assertEqual(result["commandExecutionCount"], 0)
        self.assertFalse(any(result[key] for key in ("startupCredit", "runtimeCredit", "acceptedToolCredit", "signedReleaseCredit", "operationalCredit")))

    def test_corruption_fails_with_original_receipt(self):
        path = self.capsule()
        with (self.base / "artifacts.tar.gz").open("ab") as stream:
            stream.write(b"corruption")
        with self.assertRaises(ValueError):
            replay.replay(path)

    def test_rebound_false_authority_fails(self):
        for key, value in (("routingPolicy", "V2"), ("T0", "now"), ("activationAuthority", "invented"), ("stagingExecuted", 14)):
            with self.subTest(key=key):
                result = copy.deepcopy(self.result)
                result["protectedAuthority"][key] = value
                with self.assertRaises(ValueError):
                    replay.replay(self.capsule(result))

    def test_rebound_unearned_credits_fail(self):
        for key in ("ordinaryStartup", "signedDistribution", "operationalAcceptance"):
            with self.subTest(key=key):
                result = copy.deepcopy(self.result)
                result[key] = "passed"
                with self.assertRaises(ValueError):
                    replay.replay(self.capsule(result))

    def test_invented_success_cannot_bypass_missing_provenance(self):
        result = copy.deepcopy(self.result)
        result.update(qualification="passed", firstFailure=None, historicalPreservation=dict(status="verified"))
        with self.assertRaises((ValueError, KeyError)):
            replay.replay(self.capsule(result))

    def test_incomplete_without_failure_refuses(self):
        result = copy.deepcopy(self.result)
        result["firstFailure"] = None
        with self.assertRaises(ValueError):
            replay.replay(self.capsule(result))

    def test_historical_reuse_cannot_be_relabelled_current(self):
        result = copy.deepcopy(self.result)
        result["historicalEvidence"]["status"] = "reused"
        with self.assertRaises(ValueError):
            replay.replay(self.capsule(result))

    def test_rebound_path_traversal_refuses(self):
        with self.assertRaises(ValueError):
            replay.replay(self.capsule(extra={"../outside": b"forbidden"}))

    def test_receipt_result_disagreement_refuses(self):
        path = self.capsule()
        receipt = json.loads(path.read_text())
        receipt["result"]["firstFailure"]["detail"] = "different"
        path.write_text(json.dumps(receipt))
        with self.assertRaises(ValueError):
            replay.replay(path)


if __name__ == "__main__":
    unittest.main()
