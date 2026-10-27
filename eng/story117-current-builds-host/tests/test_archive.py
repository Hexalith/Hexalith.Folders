"""Archive regressions using synthetic files only; no qualification or SDK execution."""
import hashlib
import importlib.util
import json
from pathlib import Path
import tarfile
import tempfile
import unittest
from unittest.mock import patch

HERE = Path(__file__).resolve().parents[1]
MIB = 1024 * 1024


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


q = module("archive_qualification", "qualify.py")
v = module("archive_verifier", "verify.py")


def record(name, data):
    return dict(path=name, bytes=len(data), sha256=hashlib.sha256(data).hexdigest())


class ArchiveTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.work = self.root / "work"
        self.evidence = self.root / "evidence"
        self.work.mkdir()
        self.evidence.mkdir()
        self.put("outcome.json", json.dumps({"controls": {"stagingChecksExecuted": 0}}).encode())
        self.put("preservation.json", b'{"status": "verified"}')

    def put(self, name, data):
        path = self.work / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        return path

    def plan(self, sources):
        self.put("plan.json", json.dumps({"sources": sources}).encode())

    def archive(self, record_id="synthetic"):
        with patch.object(q, "EVIDENCE", self.evidence):
            return q.archive(self.work, record_id)

    def archived(self, receipt):
        with tarfile.open(self.evidence / receipt["archive"]["path"], "r:gz") as capsule:
            return {item.name: capsule.extractfile(item).read() for item in capsule}

    def omitted(self, receipt):
        return json.loads((self.evidence / receipt["omittedManifest"]["path"]).read_text())

    def assertVerified(self, receipt, record_id="synthetic"):
        result = v.verify(self.evidence, record_id + ".json", q.sha(self.evidence / (record_id + ".json")))
        self.assertTrue(result["verified"])
        self.assertEqual(result["members"], receipt["memberCount"])
        archived = self.archived(receipt)
        manifest = json.loads((self.evidence / receipt["memberManifest"]["path"]).read_text())
        self.assertEqual(manifest, [record(name, data) for name, data in sorted(archived.items())])
        omitted_path = self.evidence / receipt["omittedManifest"]["path"]
        self.assertEqual(receipt["omittedManifest"], record(omitted_path.name, omitted_path.read_bytes()))
        omissions = self.omitted(receipt)
        for category, count in receipt["omissionCounts"].items():
            if category != "sourceCopies":
                self.assertEqual(count, sum(item["category"] == category for item in omissions))

    def testSyntheticCapsuleOmitsSnapshotBulkAndArchivesExactLogTail(self):
        source = b"class Example {}"
        build_output = b"intermediate assembly"
        restore_output = b"restored assets"
        log = b"discarded prefix\n" + bytes(range(256)) * (MIB // 256)
        sealed = b"the binaries actually loaded"
        self.put("snapshot/references/Example/Example.cs", source)
        self.put("snapshot/references/Example/bin/Debug/Example.dll", build_output)
        self.put("snapshot/references/Example/obj/project.assets.json", restore_output)
        self.put("logs/build-AppHost.stdout", log)
        self.put("sealed/host/Example.dll", sealed)
        self.plan([record("references/Example/Example.cs", source)])
        before = {str(p.relative_to(self.work)): (p.read_bytes(), p.stat().st_mtime_ns)
                  for p in self.work.rglob("*") if p.is_file()}

        receipt = self.archive()

        archived = self.archived(receipt)
        self.assertFalse(any(name.startswith("snapshot/") for name in archived))
        self.assertEqual(archived["logs/build-AppHost.stdout"], log[-MIB:])
        self.assertEqual(archived["sealed/host/Example.dll"], sealed)
        self.assertEqual(self.omitted(receipt), [
            dict(record("logs/build-AppHost.stdout", log), category="logTails"),
            dict(record("snapshot/references/Example/bin/Debug/Example.dll", build_output), category="snapshotBuildOutput"),
            dict(record("snapshot/references/Example/obj/project.assets.json", restore_output), category="snapshotBuildOutput")])
        self.assertEqual(receipt["omissionCounts"], dict(sourceCopies=1, snapshotBuildOutput=2,
                                                       privateRuntimeFiles=0, logTails=1))
        self.assertIn("plan.json sources", receipt["omissions"])
        after = {str(p.relative_to(self.work)): (p.read_bytes(), p.stat().st_mtime_ns)
                 for p in self.work.rglob("*") if p.is_file()}
        self.assertEqual(after, before)
        self.assertVerified(receipt)

    def testSourceCopyWithMatchingHashButDifferentRecordedSizeIsRetained(self):
        source = b"class Example {}"
        self.put("snapshot/Example.cs", source)
        self.plan([dict(record("Example.cs", source), bytes=len(source) + 1)])

        receipt = self.archive()

        self.assertEqual(self.archived(receipt)["snapshot/Example.cs"], source)
        self.assertEqual(receipt["omissionCounts"]["sourceCopies"], 0)
        self.assertVerified(receipt)

    def testSnapshotExtrasAndDriftedSourcesAreRetainedWhole(self):
        self.plan([record("Example.cs", b"original")])
        extras = {"snapshot/NuGet.Config": b"<configuration/>",
                  "snapshot/compiler-inputs/references.txt": b"compiler references",
                  "snapshot/Example.cs": b"modified",
                  "snapshot/unrecorded.cs": b"unrecorded source",
                  "snapshot/obj": b"a filename, not an output directory",
                  "snapshot/binary/inputs.txt": b"not bin/"}
        for name, data in extras.items():
            self.put(name, data)

        receipt = self.archive()

        for name, data in extras.items():
            self.assertEqual(self.archived(receipt)[name], data)
        self.assertEqual(self.omitted(receipt), [])
        self.assertTrue(all(count == 0 for count in receipt["omissionCounts"].values()))
        self.assertVerified(receipt)

    def testPreparationFailureWithoutPlanStillRetainsUnrecordedSnapshotFiles(self):
        self.put("snapshot/Example.cs", b"copied before plan was written")
        self.put("snapshot/obj/partial", b"partial output")

        receipt = self.archive()

        self.assertIn("snapshot/Example.cs", self.archived(receipt))
        self.assertNotIn("snapshot/obj/partial", self.archived(receipt))
        self.assertEqual(self.omitted(receipt), [dict(record("snapshot/obj/partial", b"partial output"),
                                                     category="snapshotBuildOutput")])
        self.assertVerified(receipt)

    def testPreparationFailureWithPartialPlanStillRetainsSourceCopies(self):
        partial_plan = b'{"sources": ['
        self.put("plan.json", partial_plan)
        self.put("snapshot/Example.cs", b"copied before interrupted plan write")

        receipt = self.archive()

        archived = self.archived(receipt)
        self.assertEqual(archived["plan.json"], partial_plan)
        self.assertIn("snapshot/Example.cs", archived)
        self.assertEqual(receipt["omissionCounts"]["sourceCopies"], 0)
        self.assertVerified(receipt)

    def testEveryPrivateRuntimeFileIsRecordedAndMsbuildCrashReportsAreRetained(self):
        excluded = {name + "/private-file": name.encode() for name in
                    ("feed", "nuget", "cache", "http-cache", "plugin-cache", "dotnet-home", "bundle", "tmp")}
        excluded["tmp/MSBuild_pid-123.other.txt"] = b"not a crash report"
        for name, data in excluded.items():
            self.put(name, data)
        self.put("tmp/reports/MSBuild_pid-123.failure.txt", b"crash diagnostic")

        receipt = self.archive()

        archived = self.archived(receipt)
        self.assertTrue(set(excluded).isdisjoint(archived))
        self.assertEqual(archived["tmp/reports/MSBuild_pid-123.failure.txt"], b"crash diagnostic")
        self.assertEqual(self.omitted(receipt), [dict(record(name, data), category="privateRuntimeFiles")
                                                for name, data in sorted(excluded.items())])
        self.assertEqual(receipt["omissionCounts"]["privateRuntimeFiles"], len(excluded))
        self.assertVerified(receipt)

    def testLargeBinlogsAndFilesOutsideLogsRemainWhole(self):
        data = b"binary build record" * (MIB // 10)
        names = ("logs/AppHost.binlog", "logs/nested/Build.BINLOG", "sealed/host/Example.dll",
                 "graph/compiler-input.txt", "snapshot/NuGet.Config")
        for name in names:
            self.put(name, data)

        receipt = self.archive()

        archived = self.archived(receipt)
        for name in names:
            self.assertEqual(archived[name], data)
        self.assertEqual(self.omitted(receipt), [])
        self.assertVerified(receipt)

    def testLogLimitIsStrictAndAppliesToNestedLogsAndAllNonBinlogSuffixes(self):
        logs = {"logs/empty.stderr": b"", "logs/exact.stdout": b"x" * MIB,
                "logs/nested/trace.log": b"a" + b"b" * MIB,
                "logs/build.stdout": b"prefix" + bytes(range(256)) * (MIB // 256)}
        for name, data in logs.items():
            self.put(name, data)

        receipt = self.archive()

        archived = self.archived(receipt)
        for name, data in logs.items():
            self.assertEqual(archived[name], data[-MIB:])
            self.assertEqual((self.work / name).read_bytes(), data)
        self.assertEqual(self.omitted(receipt), [dict(record(name, data), category="logTails")
                                                for name, data in sorted(logs.items()) if len(data) > MIB])
        self.assertEqual(receipt["omissionCounts"]["logTails"], 2)
        self.assertVerified(receipt)

    def testHardlinkedTailAndBinlogAreIndependentRegularArchiveMembers(self):
        data = b"a" + b"b" * MIB
        source = self.put("logs/a.stdout", data)
        (self.work / "logs/z.binlog").hardlink_to(source)

        receipt = self.archive()

        archived = self.archived(receipt)
        self.assertEqual(archived["logs/a.stdout"], data[-MIB:])
        self.assertEqual(archived["logs/z.binlog"], data)
        self.assertVerified(receipt)

    def testWholeFileDriftDuringArchivingPreventsReceipt(self):
        target = self.put("sealed/host/Example.dll", b"original")
        original_add = tarfile.TarFile.add

        def add(capsule, name, *args, **kwargs):
            original_add(capsule, name, *args, **kwargs)
            if Path(name) == target:
                target.write_bytes(b"modified")

        with patch.object(q.tarfile.TarFile, "add", add):
            with self.assertRaisesRegex(q.GateFailure, "Input drift"):
                self.archive()
        self.assertFalse((self.evidence / "synthetic.json").exists())

    def testLogDriftDuringTailArchivingPreventsReceipt(self):
        target = self.put("logs/build.stdout", b"a" * (MIB + 1))
        original_addfile = tarfile.TarFile.addfile

        def addfile(capsule, info, *args, **kwargs):
            original_addfile(capsule, info, *args, **kwargs)
            if info.name == "logs/build.stdout":
                with target.open("ab") as stream:
                    stream.write(b"changed")

        with patch.object(q.tarfile.TarFile, "addfile", addfile):
            with self.assertRaisesRegex(q.GateFailure, "Input drift"):
                self.archive()
        self.assertFalse((self.evidence / "synthetic.json").exists())

    def testOmittedFileDriftDuringArchivingPreventsReceipt(self):
        original_add = tarfile.TarFile.add
        for index, name in enumerate(("snapshot/Example.cs", "snapshot/obj/output", "nuget/package")):
            with self.subTest(name=name):
                target = self.put(name, b"original")
                self.plan([record("Example.cs", b"original")])
                record_id = "omitted-drift-" + str(index)

                def add(capsule, path, *args, **kwargs):
                    original_add(capsule, path, *args, **kwargs)
                    if Path(path) == self.work / "outcome.json":
                        target.write_bytes(b"modified")

                with patch.object(q.tarfile.TarFile, "add", add):
                    with self.assertRaisesRegex(q.GateFailure, "Input drift"):
                        self.archive(record_id)
                self.assertFalse((self.evidence / (record_id + ".json")).exists())
                target.unlink()

    def testSourceOmissionsCannotUseADifferentPlanThanTheArchivedPlan(self):
        source = b"class Example {}"
        self.put("snapshot/Example.cs", source)
        self.plan([record("Example.cs", source)])
        original_binding = q.binding

        def binding(path, relative_to=None):
            if Path(path) == self.work / "outcome.json" and relative_to == self.work:
                self.plan([])  # Drift after plan parsing, before its archive binding.
            return original_binding(path, relative_to)

        with patch.object(q, "binding", binding):
            with self.assertRaisesRegex(q.GateFailure, "Input drift"):
                self.archive()
        self.assertFalse((self.evidence / "synthetic.json").exists())

    def testArchivedAndOmittedSymlinksAreRejectedWithoutFollowingThem(self):
        target = self.root / "external"
        target.write_bytes(b"outside work")
        for name in ("plan.json", "snapshot/Example.cs", "snapshot/bin/Example.dll", "nuget/package"):
            with self.subTest(name=name):
                path = self.work / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.symlink_to(target)
                with self.assertRaisesRegex(q.GateFailure, "Archive symlink"):
                    self.archive()
                path.unlink()


if __name__ == "__main__":
    unittest.main()
