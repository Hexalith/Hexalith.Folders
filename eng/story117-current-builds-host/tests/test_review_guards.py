"""Post-review regressions: only controlled Python children and synthetic evidence."""
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import signal
import sys
import tarfile
import tempfile
import unittest
from unittest.mock import patch

HERE = Path(__file__).resolve().parents[1]


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


q = module("review_qualification", "qualify.py")
v = module("review_verifier", "verify.py")


class ReviewGuards(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)

    def tearDown(self):
        self.temporary.cleanup()

    def runner(self):
        value = q.Qualification.__new__(q.Qualification)
        value.work = self.root
        value.snapshot = self.root
        value.logs = self.root
        value.env = {"PATH": "/usr/bin:/bin"}
        value.commands = []
        value.case_results = []
        value.fd = None
        value.stop = None
        value.plan = {"repositories": []}
        return value

    def testSdkClosureIncludesCoreBuildAndRestoreTaskLibraries(self):
        sdk = self.root / "sdk"
        sdk.mkdir()
        for name in ("Microsoft.Build.dll", "Microsoft.Build.Tasks.Core.dll", "NuGet.Build.Tasks.dll"):
            (sdk / name).write_bytes(name.encode())
        task = sdk / "Sdks/example/tools/Task.dll"
        task.parent.mkdir(parents=True)
        task.write_bytes(b"task")
        paths = q.sdk_build_dependency_paths(sdk)
        self.assertIn(task, paths)
        bindings = [q.binding(path) for path in paths]
        task.write_bytes(b"drift")
        with self.assertRaises(q.GateFailure):
            q.same(task, next(row for row in bindings if row["path"] == str(task)))

    def testExecutionPreflightFailureRecordsExactInvocationWithoutClaim(self):
        (self.root / "plan.json").write_text("{}")
        invocation = ["python3", "-I", "-B", "qualify.py", "--execute"]
        with patch.object(q, "HERE", self.root), patch.object(q, "EVIDENCE", self.root), \
             patch.object(q.sys, "argv", ["qualify.py", "--execute"]), patch.object(q.sys, "orig_argv", invocation), \
             patch.object(q, "verify_repository_predicate", side_effect=q.GateFailure("protected drift")), \
             patch.object(q.subprocess, "Popen") as launch:
            self.assertEqual(q.main(), 2)
            launch.assert_not_called()
        records = list(self.root.glob("*.failed-preflight-*.json"))
        self.assertEqual(len(records), 1)
        record = json.loads(records[0].read_text())
        self.assertEqual(record["invocation"], invocation)
        self.assertEqual(record["targetLaunchCount"], 0)
        self.assertFalse(record["claimConsumed"])
        self.assertFalse(list(self.root.glob("*.claim.json")))

    def testFailedPopenRetainsNotLaunchedCommand(self):
        runner = self.runner()
        with patch.object(runner, "inputs"), patch.object(runner, "sandbox", side_effect=lambda argv, readonly: argv), \
             patch.object(q.subprocess, "Popen", side_effect=OSError("controlled launch failure")):
            with self.assertRaises(OSError):
                runner.run("case-cold", ["unused"], 30)
        self.assertEqual(len(runner.commands), 1)
        self.assertFalse(runner.commands[0]["launched"])
        self.assertEqual(json.loads((self.root / "case-cold.command.json").read_text())["launchStatus"], "not-launched")

    def testPostCheckFailureStillRecordsConsumedLaunch(self):
        runner = self.runner()
        argv = [sys.executable, "-I", "-c", "print('Installed an ASP.NET Core HTTPS development certificate')"]
        with patch.object(runner, "inputs"), patch.object(runner, "sandbox", side_effect=lambda argv, readonly: argv):
            with self.assertRaisesRegex(q.GateFailure, "Certificate"):
                runner.run("case-cold", argv, 30)
        row = runner.commands[0]
        self.assertTrue(row["launched"])
        self.assertEqual(row["exitCode"], 0)
        self.assertEqual(row["status"], "failed")
        with patch.object(runner, "inputs"), patch.object(q, "state", return_value={}), patch.object(q, "store_metadata", return_value={}):
            runner.finish({}, {})
        self.assertEqual(json.loads((self.root / "outcome.json").read_text())["consumedLoaderAttempts"], {"cold": 1, "host-first": 0})

    def testPollingInterruptionCleansAndReapsControlledChild(self):
        runner = self.runner()
        with patch.object(runner, "inputs"), patch.object(runner, "sandbox", side_effect=lambda argv, readonly: argv), \
             patch.object(q.time, "sleep", side_effect=KeyboardInterrupt("controlled interruption")):
            with self.assertRaises(KeyboardInterrupt):
                runner.run("interrupted", [sys.executable, "-I", "-c", "import time; time.sleep(30)"], 30)
        record = runner.commands[0]
        self.assertTrue(record["launched"])
        self.assertEqual(record["exitCode"], -signal.SIGTERM)
        self.assertFalse(record["observedSurvivors"])
        self.assertNotIn(record["pid"], q.processes())
        self.assertTrue(record["termination"]["observations"])

    def testFirstObservationInterruptionStillTerminatesKnownPopenRoot(self):
        runner = self.runner()
        with patch.object(runner, "inputs"), patch.object(runner, "sandbox", side_effect=lambda argv, readonly: argv), \
             patch.object(q, "observe", side_effect=KeyboardInterrupt("first observation interruption")):
            with self.assertRaises(KeyboardInterrupt):
                runner.run("first-observation", [sys.executable, "-I", "-c", "import time; time.sleep(30)"], 30)
        record = runner.commands[0]
        self.assertEqual(record["rootIdentity"]["pid"], record["pid"])
        self.assertNotIn(record["pid"], q.processes())
        self.assertFalse(record["observedSurvivors"])
        self.assertTrue(record["termination"]["observations"])

    def testMissingInitialIdentityStillUsesOwnedPopenCleanupFallback(self):
        runner = self.runner()
        original = q.process_identity
        calls = []

        def identity(pid):
            calls.append(pid)
            if len(calls) == 1:
                raise OSError("controlled initial identity failure")
            return original(pid)

        with patch.object(runner, "inputs"), patch.object(runner, "sandbox", side_effect=lambda argv, readonly: argv), \
             patch.object(q, "process_identity", side_effect=identity), \
             patch.object(q, "observe", side_effect=KeyboardInterrupt("controlled first observation")):
            with self.assertRaises(KeyboardInterrupt):
                runner.run("missing-identity", [sys.executable, "-I", "-c", "import time; time.sleep(30)"], 30)
        record = runner.commands[0]
        self.assertIn("rootIdentityError", record)
        self.assertNotIn(record["pid"], q.processes())
        self.assertFalse(record["observedSurvivors"])

    def testKnownDescendantInDifferentSessionIsTerminatedAndReaped(self):
        pidfile = self.root / "controlled-child.pid"
        child = "import os,time; os.setsid(); time.sleep(30)"
        parent = "import subprocess,sys,time,pathlib; p=subprocess.Popen([sys.executable,'-I','-c'," + repr(child) + "]); pathlib.Path(" + repr(str(pidfile)) + ").write_text(str(p.pid)); time.sleep(0.6)"
        record = q.run_owned([sys.executable, "-I", "-c", parent], self.root, {"PATH": "/usr/bin:/bin"}, self.root, "escaped", 30)
        pid = int(pidfile.read_text())
        self.assertTrue(any(row.get("pid") == pid for row in record["termination"]["signals"]))
        self.assertTrue(any(row["pid"] == pid for row in record["termination"]["reaped"]))
        self.assertNotIn(pid, q.processes())
        self.assertFalse(record["observedSurvivors"])

    def testFailedPreservationObservationStillSealsUnknownEvidence(self):
        work = self.root / "work"
        work.mkdir()
        evidence = self.root / "evidence"
        evidence.mkdir()
        runner = self.runner()
        runner.work = work
        with patch.object(runner, "inputs", side_effect=RuntimeError("input observer failed")), \
             patch.object(q, "state", side_effect=RuntimeError("git observer failed")), \
             patch.object(q, "store_metadata", side_effect=OSError("store observer failed")):
            result = runner.finish({}, {})
        self.assertIsNone(result["revisionsIndicesFlagsAndStatusEqual"])
        self.assertIsNone(result["certificateStoreMetadataEqual"])
        self.assertEqual(result["status"], "failed-or-unknown")
        with patch.object(q, "EVIDENCE", evidence):
            receipt = q.archive(work, "controlled-unknown-preservation")
        self.assertEqual(receipt["outcome"]["stop"]["type"], "PreservationObservationFailure")

    def testPreparedFeedDriftIsRejectedAndSandboxMountIsReadOnly(self):
        runner = self.runner()
        runner.original_plan = None
        runner.snapshot_inputs = []
        runner.private_inputs = []
        runner.feed_ready = True
        runner.plan.update(sdkFiles=[], tools=[], frameworkDirectories={}, sources=[], harnessFiles=[])
        feed = self.root / "feed"
        feed.mkdir()
        package = feed / "example.1.0.0.nupkg"
        package.write_bytes(b"original")
        runner.plan["offlineFeedLayout"] = [dict(q.binding(package), feedPath=package.name, packageId="example", packageVersion="1.0.0")]
        with patch.object(q, "verify_repository_predicate"):
            runner.inputs()
            package.write_bytes(b"changed")
            with self.assertRaises(q.GateFailure):
                runner.inputs()
        (self.root / "deny-sockets.bpf").write_bytes(q.filter_bytes())
        argv = runner.sandbox(["unused"])
        try:
            index = argv.index(str(feed))
            self.assertEqual(argv[index - 1:index + 2], ["--ro-bind", str(feed), str(feed)])
        finally:
            os.close(runner.fd)

    def testCompilerReferencesRequireVerifiedPrivateSourceOutput(self):
        snapshot = self.root / "snapshot"
        project = snapshot / "references/Builds/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj"
        project.parent.mkdir(parents=True)
        project.write_text("<Project/>")
        plan = {"sources": [q.binding(project, snapshot)]}
        output = project.parent / "bin/Debug/net10.0/Hexalith.Builds.Tooling.dll"
        output.parent.mkdir(parents=True)
        output.write_bytes(b"verified source output fixture")
        row = dict(project=str(project), output=q.binding(output))
        line = "REF|" + str(output) + "|Hexalith.Builds.Tooling, Version=1.0.0.0|Hexalith.Builds.Tooling"
        self.assertEqual(len(q.validate_compiler_references([line], snapshot, plan, {str(output): row})), 1)
        with self.assertRaisesRegex(q.GateFailure, "lacks verified source"):
            q.validate_compiler_references([line], snapshot, plan, {})
        external = "REF|" + str(self.root / "renamed.dll") + "|Hexalith.Builds.Tooling, Version=1.0.0.0|Hexalith.Builds.Tooling"
        with self.assertRaisesRegex(q.GateFailure, "External/direct"):
            q.validate_compiler_references([external], snapshot, plan, {})
        output.write_bytes(b"injected bytes")
        with self.assertRaisesRegex(q.GateFailure, "drift"):
            q.validate_compiler_references([line], snapshot, plan, {str(output): row})

    def testSourceOutputPublicationRequiresMatchingPrecompileReferenceGate(self):
        snapshot = self.root / "snapshot"
        project = snapshot / "references/Builds/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj"
        project.parent.mkdir(parents=True)
        project.write_text("<Project/>")
        plan = self.root / "bound-plan.json"
        plan.write_text(json.dumps({"sources": [q.binding(project, snapshot)]}))
        output = project.parent / "bin/Debug/net10.0/Hexalith.Builds.Tooling.dll"
        output.parent.mkdir(parents=True)
        output.write_bytes(b"controlled source output fixture")
        proofs = self.root / "proofs"
        proofs.mkdir()
        capture = self.root / "references.txt"
        capture.write_text("")
        with self.assertRaises(FileNotFoundError):
            q.record_source_output(project, output, snapshot, plan, proofs)
        q.audit_reference_file(capture, project, snapshot, plan, proofs)
        q.record_source_output(project, output, snapshot, plan, proofs)
        self.assertEqual(json.loads(q.proof_path(proofs, output).read_text())["output"]["sha256"], q.sha(output))

    def testMsbuildReferenceGuardPrecedesCoreCompilation(self):
        import xml.etree.ElementTree as ET
        document = ET.parse(HERE / "source-references.targets").getroot()
        targets = {node.attrib["Name"]: node for node in document.findall("Target")}
        capture = targets["Story117CaptureCompiler"]
        self.assertEqual(capture.attrib["BeforeTargets"], "CoreCompile")
        self.assertIn("--audit-references", capture.find("Exec").attrib["Command"])
        publish = targets["Story117RecordSourceOutput"]
        self.assertEqual(publish.attrib["AfterTargets"], "CopyFilesToOutputDirectory")
        self.assertIn("--record-source-output", publish.find("Exec").attrib["Command"])

    def testProbeRejectsFatalRuntimeAndUnsealedResolution(self):
        root = self.root / "sealed"
        root.mkdir()
        target = root / "Hexalith.Example.dll"
        target.write_bytes(b"bound")
        seal = [q.binding(target, root)]
        context = {"kind": "context", "data": {"framework": ".NET 10.0.13"}}
        failure = {"kind": "failure", "data": {"phase": "preflight"}}
        with self.assertRaisesRegex(q.GateFailure, "Fatal probe preflight"):
            q.validate_probe_events([context, failure], 10, root, seal, "cold")
        bad_runtime = {"kind": "context", "data": {"framework": ".NET 10.0.12"}}
        with self.assertRaisesRegex(q.GateFailure, "Fatal runtime"):
            q.validate_probe_events([bad_runtime, failure], 10, root, seal, "cold")
        failure["data"]["phase"] = "model-EventStore-demand"
        assembly = dict(location=str(target), identity="Hexalith.Example, Version=1.0.0.0", context="Default",
                        sha256="0" * 64, mvid="11111111-1111-1111-1111-111111111111")
        resolved = {"kind": "resolved", "data": {"label": "example", "assembly": assembly}}
        with self.assertRaisesRegex(q.GateFailure, "Resolved hash"):
            q.validate_probe_events([context, resolved, failure], 10, root, seal, "cold")
        assembly["sha256"] = q.sha(target)
        assembly["location"] = str(self.root / target.name)
        with self.assertRaisesRegex(q.GateFailure, "not a sealed input"):
            q.validate_probe_events([context, resolved, failure], 10, root, seal, "cold")

    def capsule(self, manifest, members):
        archive = self.root / "capsule.tar.gz"
        with tarfile.open(archive, "w:gz") as tar:
            for name, data, kind in members:
                info = tarfile.TarInfo(name)
                info.type = kind
                info.size = len(data) if kind == tarfile.REGTYPE else 0
                tar.addfile(info, io.BytesIO(data) if kind == tarfile.REGTYPE else None)
        table = self.root / "members.json"
        table.write_text(json.dumps(manifest))
        receipt = dict(archive=q.binding(archive, self.root), memberManifest=q.binding(table, self.root),
                       memberCount=len(manifest), outcome={"controls": {"stagingChecksExecuted": 0}})
        path = self.root / "receipt.json"
        path.write_text(json.dumps(receipt))
        return q.sha(path)

    def member(self, name="safe.txt", data=b"bound"):
        return dict(path=name, bytes=len(data), sha256=hashlib.sha256(data).hexdigest())

    def testCorrectlyHashedSyntheticCapsuleIsValid(self):
        sha = self.capsule([self.member()], [("safe.txt", b"bound", tarfile.REGTYPE)])
        self.assertTrue(v.verify(self.root, "receipt.json", sha)["verified"])

    def testCorrectlyHashedCapsulesRejectDuplicateManifestAndTarMembers(self):
        scenarios = [([self.member(), self.member()], [("safe.txt", b"bound", tarfile.REGTYPE)]),
                     ([self.member()], [("safe.txt", b"bound", tarfile.REGTYPE)] * 2)]
        for manifest, members in scenarios:
            sha = self.capsule(manifest, members)
            with self.assertRaisesRegex(ValueError, "Duplicate|duplicate"):
                v.verify(self.root, "receipt.json", sha)

    def testCorrectlyHashedCapsulesRejectUnsafeSpecialMissingAndCorruptMembers(self):
        scenarios = [([self.member("../escape")], [("../escape", b"bound", tarfile.REGTYPE)]),
                     ([self.member()], [("safe.txt", b"", tarfile.SYMTYPE)]),
                     ([self.member()], []),
                     ([self.member()], [("unexpected.txt", b"bound", tarfile.REGTYPE)]),
                     ([self.member()], [("safe.txt", b"WRONG", tarfile.REGTYPE)])]
        for manifest, members in scenarios:
            sha = self.capsule(manifest, members)
            with self.assertRaises(ValueError):
                v.verify(self.root, "receipt.json", sha)


if __name__ == "__main__":
    unittest.main()
