"""Focused fail-closed regression tests. They never invoke the SDK or target libraries."""
import importlib.util
import json
import os
from pathlib import Path
import signal
import tempfile
import unittest
import zipfile
from unittest.mock import patch

HERE = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


q = module("qualification", HERE / "qualify.py")
v = module("verifier", HERE / "verify.py")


class QualificationGuards(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def testProtectedByteDriftFailsBeforeAnySdkUse(self):
        path = self.root / "source.cs"
        path.write_text("original")
        expected = q.binding(path)
        path.write_text("changed")
        with patch.object(q.subprocess, "Popen") as launch:
            with self.assertRaises(q.GateFailure):
                q.same(path, expected)
            launch.assert_not_called()

    def testHexalithPackageFallbackIsRejected(self):
        with self.assertRaisesRegex(q.GateFailure, "package fallback"):
            q.validate_assets({"libraries": {"Hexalith.EventStore.Aspire/3.128.0": {"type": "package"}}}, self.root)

    def testThirdPartyPackagesRemainPermitted(self):
        q.validate_assets({"libraries": {"Aspire.Hosting/13.6.3": {"type": "package"}}}, self.root)

    def testProjectReferenceCannotEscapePrivateSnapshot(self):
        data = {"project": {"restore": {"frameworks": {"net10.0": {"projectReferences": {"/outside/x.csproj": {}}}}}}}
        with self.assertRaisesRegex(q.GateFailure, "External project"):
            q.validate_assets(data, self.root)

    def testStaleExtraOutputFailsSealMembership(self):
        path = self.root / "host.dll"
        path.write_bytes(b"new")
        seal = [q.binding(path, self.root)]
        (self.root / "stale.dll").write_bytes(b"old")
        with self.assertRaisesRegex(q.GateFailure, "membership"):
            q.validate_seal(self.root, seal)

    def testCorruptOutputFailsSealHash(self):
        path = self.root / "host.dll"
        path.write_bytes(b"new")
        seal = [q.binding(path, self.root)]
        path.write_bytes(b"bad")
        with self.assertRaisesRegex(q.GateFailure, "drift"):
            q.validate_seal(self.root, seal)

    def testOneShotDescriptorCannotBeReused(self):
        claim = self.root / "claim.json"
        q.claim_once(claim, {"authorized": "this spec only"})
        with self.assertRaises(FileExistsError):
            q.claim_once(claim, {"retry": True})

    def testEverySandboxGetsFreshZeroOffsetFilter(self):
        qualification = q.Qualification.__new__(q.Qualification)
        qualification.work = self.root
        qualification.snapshot = self.root
        (self.root / "deny-sockets.bpf").write_bytes(q.filter_bytes())
        descriptors = []
        try:
            first = qualification.sandbox(["unused"])
            descriptors.append(qualification.fd)
            os.read(qualification.fd, 9999)
            second = qualification.sandbox(["unused"])
            descriptors.append(qualification.fd)
            self.assertEqual(os.lseek(qualification.fd, 0, os.SEEK_CUR), 0)
            self.assertIn("--unshare-all", first)
            self.assertIn("--ro-bind", second)
            self.assertNotEqual(descriptors[0], descriptors[1])
        finally:
            for fd in descriptors:
                os.close(fd)

    def testFailedInputGateCannotLaunchSdk(self):
        qualification = q.Qualification.__new__(q.Qualification)
        qualification.snapshot = self.root
        qualification.env = {}
        qualification.logs = self.root
        qualification.commands = []
        qualification.fd = None
        with patch.object(qualification, "inputs", side_effect=q.GateFailure("SDK drift")), patch.object(q.subprocess, "Popen") as launch:
            with self.assertRaisesRegex(q.GateFailure, "SDK drift"):
                qualification.run("unused", ["dotnet", "--version"], 30)
            launch.assert_not_called()

    def testUnbuiltTupleCannotLaunchAnyProbe(self):
        qualification = q.Qualification.__new__(q.Qualification)
        qualification.builds_verified = False
        qualification.case_seal = None
        with patch.object(qualification, "checked") as launch:
            with self.assertRaisesRegex(q.GateFailure, "verified builds"):
                qualification.probe()
            launch.assert_not_called()

    def probe_fixture(self, outcome):
        qualification = q.Qualification.__new__(q.Qualification)
        qualification.work = self.root
        qualification.logs = self.root / "logs"
        qualification.logs.mkdir()
        qualification.dotnet = Path("/bound/dotnet")
        qualification.plan = {"sdkFiles": []}
        qualification.env = {}
        qualification.builds_verified = True
        qualification.case_results = []
        for path in ("host/Hexalith.Builds.Module.AppHost.dll", "model/Hexalith.Platform.Composition.dll",
                     "host/Aspire.Hosting.dll", "host/Hexalith.Builds.Tooling.dll", "host/StackExchange.Redis.dll",
                     "host/Hexalith.EventStore.Aspire.dll", "model/Hexalith.EventStore.ServiceDefaults.dll"):
            target = self.root / "sealed" / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(path.encode())
        qualification.case_seal = [q.binding(p, self.root / "sealed") for p in (self.root / "sealed").rglob("*") if p.is_file()]
        launched = []

        def compile_probe(*args, **kwargs):
            (self.root / "sealed/host/LoaderProbe.dll").write_bytes(b"private probe")

        def run(label, *args, **kwargs):
            launched.append(label)
            if outcome == "timeout":
                raise q.GateFailure("Timeout: " + label)
            def assembly(path):
                file = self.root / "sealed" / path
                return dict(identity=file.stem + ", Version=1.0.0.0", location=str(file), context="Default",
                            mvid="11111111-1111-1111-1111-111111111111", sha256=q.sha(file))
            events = [{"kind": "context", "data": {"framework": ".NET 10.0.13"}}]
            if outcome == 0:
                demands = [("host Aspire", "host/Aspire.Hosting.dll"), ("matching source Tooling", "host/Hexalith.Builds.Tooling.dll"),
                           ("host Tooling Redis", "host/StackExchange.Redis.dll"), ("model transitive Redis", "host/StackExchange.Redis.dll"),
                           ("optional ServiceDefaults", "model/Hexalith.EventStore.ServiceDefaults.dll"),
                           ("model EventStore Aspire", "host/Hexalith.EventStore.Aspire.dll")]
                if label == "case-host-first":
                    demands.append(("deliberate host-first EventStore", "host/Hexalith.EventStore.Aspire.dll"))
                events += [{"kind": "resolved", "data": dict(label=name, assembly=assembly(path))} for name, path in demands]
                events += [{"kind": "model", "data": assembly("model/Hexalith.Platform.Composition.dll")},
                           {"kind": "method-metadata-only", "data": dict(Name="ComposeRun", returnType="System.Void",
                            signature=["Aspire.Hosting.IDistributedApplicationBuilder", "Hexalith.Builds.Tooling.Runtime.CompositionRunPlan", "System.String", "System.String"])}]
            events.append({"kind": "complete" if outcome == 0 else "failure", "data": {"phase": "preflight" if outcome == "preflight" else "model-EventStore-demand"}})
            (qualification.logs / (label + ".stdout")).write_text("\n".join(json.dumps(e) for e in events) + "\n")
            return {"exitCode": 10 if outcome == "preflight" else outcome}

        return qualification, launched, compile_probe, run

    def testValidSealedTupleRunsOnlyTheTwoFixedCases(self):
        qualification, launched, compile_probe, run = self.probe_fixture(0)
        with patch.object(qualification, "checked", side_effect=compile_probe), patch.object(qualification, "sdk_gate"), patch.object(qualification, "run", side_effect=run):
            qualification.probe()
        self.assertEqual(launched, ["case-cold", "case-host-first"])
        self.assertTrue(all(row["bindingSucceeded"] for row in qualification.case_results))
        self.assertFalse(q.CONTROLS["currentTupleAccepted"])

    def testBindingFailureRemainsFailureAndPermitsOnlyTheSecondFixedCase(self):
        qualification, launched, compile_probe, run = self.probe_fixture(10)
        with patch.object(qualification, "checked", side_effect=compile_probe), patch.object(qualification, "sdk_gate"), patch.object(qualification, "run", side_effect=run):
            qualification.probe()
        self.assertEqual(launched, ["case-cold", "case-host-first"])
        self.assertFalse(any(row["bindingSucceeded"] for row in qualification.case_results))

    def testCaseTimeoutPreventsTheSecondCase(self):
        qualification, launched, compile_probe, run = self.probe_fixture("timeout")
        with patch.object(qualification, "checked", side_effect=compile_probe), patch.object(qualification, "sdk_gate"), patch.object(qualification, "run", side_effect=run):
            with self.assertRaisesRegex(q.GateFailure, "Timeout"):
                qualification.probe()
        self.assertEqual(launched, ["case-cold"])

    def testFatalPreflightDoesNotRecordBindingSuccessOrLaunchSecondCase(self):
        qualification, launched, compile_probe, run = self.probe_fixture("preflight")
        with patch.object(qualification, "checked", side_effect=compile_probe), patch.object(qualification, "sdk_gate"), patch.object(qualification, "run", side_effect=run):
            with self.assertRaisesRegex(q.GateFailure, "Fatal probe preflight"):
                qualification.probe()
        self.assertEqual(launched, ["case-cold"])
        self.assertEqual(qualification.case_results, [])

    def testInstructionLinkIsOmittedButBuildInputSymlinkIsRejected(self):
        repo = self.root / "references/Hexalith.Commons"
        repo.mkdir(parents=True)
        (self.root / "global.json").write_text("{}")
        rows = [{"path": "references/Hexalith.Commons"}]
        instruction = b"120000 deadbeef 0\t.clinerules\0"
        with patch.object(q, "git", return_value=instruction):
            result = q.source_files(self.root, rows)
        self.assertEqual([row["path"] for row in result], ["global.json"])
        with patch.object(q, "git", return_value=b"120000 deadbeef 0\tsource.cs\0"):
            with self.assertRaisesRegex(q.GateFailure, "Special tracked input"):
                q.source_files(self.root, rows)

    def testOnlyTheTwentyIdentifiedGeneratedInputsAreOmitted(self):
        self.assertEqual(len(q.STALE_INPUTS), 20)
        self.assertEqual(sum(path.endswith("project.assets.json") for path in q.STALE_INPUTS), 5)
        rel = "references/Hexalith.EventStore"
        repo = self.root / rel
        genuine = repo / "src/Hexalith.EventStore.Contracts/.artifacts/meaningful-source.cs"
        genuine.parent.mkdir(parents=True)
        genuine.write_text("source")
        (self.root / "global.json").write_text("{}")
        stale = next(path.removeprefix(rel + "/") for path in q.STALE_INPUTS if path.endswith("project.assets.json"))
        entries = ("100644 deadbeef 0\t" + stale + "\0" + "100644 deadbeef 0\t" + str(genuine.relative_to(repo)) + "\0").encode()
        with patch.object(q, "git", return_value=entries):
            rows = q.source_files(self.root, [{"path": rel}])
        self.assertEqual({row["path"] for row in rows}, {"global.json", str(genuine.relative_to(self.root))})

    def testRestoreArgvEnforcesOneNodeAndDisablesParallelBuilds(self):
        qualification = q.Qualification.__new__(q.Qualification)
        qualification.work = self.root
        qualification.snapshot = self.root / "snapshot"
        qualification.dotnet = Path("/bound/dotnet")
        argv = list(map(str, qualification.restore_argv(Path("/private/AppHost.csproj"), [])))
        q.validate_serial_restore(argv)
        self.assertIn("-m:1", argv)
        self.assertIn("-nr:false", argv)
        self.assertIn("-p:RestoreBuildInParallel=false", argv)
        self.assertIn("-p:BuildInParallel=false", argv)
        for required in ("-m:1", "-nr:false", "-p:RestoreBuildInParallel=false", "-p:BuildInParallel=false"):
            with self.assertRaises(q.GateFailure):
                q.validate_serial_restore([arg for arg in argv if arg != required])
        with self.assertRaises(q.GateFailure):
            q.validate_serial_restore(argv + ["-m:2"])

    def cached_package(self, directory, identity, version, payload=b"original"):
        target = self.root / directory / "unversioned.nupkg"
        target.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(target, "w") as archive:
            archive.writestr(identity + ".nuspec", '<package><metadata><id>' + identity + '</id><version>' + version + '</version></metadata></package>')
            archive.writestr("lib/content", payload)
        return q.binding(target)

    def testOfflineFeedPreservesVersionsDespiteIdenticalBasenames(self):
        first = self.cached_package("one", "dotnet-inspect", "0.16.0")
        second = self.cached_package("two", "dotnet-inspect", "0.17.0")
        layout = q.package_feed_layout([first, second])
        self.assertEqual([row["feedPath"] for row in layout], [
            "dotnet-inspect.0.16.0.nupkg", "dotnet-inspect.0.17.0.nupkg"])

    def testFeedDiscoveryRequiresCanonicalArchivesAtSourceRoot(self):
        package = self.cached_package("cache", "Aspire.AppHost.Sdk", "13.6.3")
        layout = q.package_feed_layout([package])
        feed = self.root / "feed"
        feed.mkdir()
        (feed / layout[0]["feedPath"]).write_bytes(Path(package["path"]).read_bytes())
        q.validate_feed_directory(feed, layout)
        (feed / layout[0]["feedPath"]).unlink()
        nested = feed / "aspire.apphost.sdk/13.6.3"
        nested.mkdir(parents=True)
        (nested / layout[0]["feedPath"]).write_bytes(Path(package["path"]).read_bytes())
        with self.assertRaisesRegex(q.GateFailure, "discovery/membership"):
            q.validate_feed_directory(feed, layout)

    def testCompletePrivatePreparationSucceedsBeforeAnySdkUse(self):
        import hashlib
        packages = [self.cached_package("cache/one", "Aspire.AppHost.Sdk", "13.6.3"),
                    self.cached_package("cache/two", "dotnet-inspect", "0.16.0")]
        project = self.root / "references/Builds/host/AppHost.csproj"
        project.parent.mkdir(parents=True)
        project.write_text("<Project/>")
        harness = self.root / "original-harness"
        harness.mkdir()
        (harness / "source-references.targets").write_text("<Project/>")
        work = self.root / "private"
        for name in ("snapshot", "feed"):
            (work / name).mkdir(parents=True)
        qualification = q.Qualification.__new__(q.Qualification)
        qualification.work = work
        qualification.snapshot = work / "snapshot"
        qualification.env = {}
        qualification.plan = {"harnessFiles": [q.binding(harness / "source-references.targets", harness)],
                              "sources": [q.binding(project, self.root)], "projects": [str(project.relative_to(self.root))],
                              "thirdPartyPackages": packages, "offlineFeedLayout": q.package_feed_layout(packages),
                              "seccompSha256": hashlib.sha256(q.filter_bytes()).hexdigest()}
        with patch.object(q, "ROOT", self.root), patch.object(q, "HERE", harness), patch.object(q.subprocess, "Popen") as launch:
            qualification.prepare()
            launch.assert_not_called()
        self.assertTrue((work / "plan.json").is_file())
        self.assertTrue((work / "harness/source-references.targets").is_file())
        self.assertTrue((work / "offline-feed-discovery.json").is_file())
        q.validate_feed_directory(work / "feed", qualification.plan["offlineFeedLayout"])

    def testFlatCanonicalFilenameAmbiguityFailsBeforeCopy(self):
        first = self.cached_package("one", "Example", "1.2.0")
        second = self.cached_package("two", "Example.1", "2.0")
        with self.assertRaisesRegex(q.GateFailure, "Conflicting canonical"):
            q.package_feed_layout([first, second])

    def testOfflineFeedRejectsConflictingBytesForSameIdentityVersion(self):
        first = self.cached_package("one", "Example", "1.0.0")
        second = self.cached_package("two", "Example", "1.0.0", payload=b"different")
        with self.assertRaisesRegex(q.GateFailure, "Conflicting bytes"):
            q.package_feed_layout([first, second])

    def testOfflineFeedDeduplicatesIdenticalIdentityVersionBytes(self):
        first = self.cached_package("one", "Example", "1.0.0")
        copy = self.root / "copy.nupkg"
        copy.write_bytes(Path(first["path"]).read_bytes())
        self.assertEqual(len(q.package_feed_layout([first, q.binding(copy)])), 1)

    def metadata_fixture(self, identity="Hexalith.EventStore.Aspire", version="3.128.0"):
        nuspec = self.root / "selected.nuspec"
        raw = ('<package><metadata><id>' + identity + '</id><version>' + version
               + '</version><repository commit="' + '1' * 40 + '"/></metadata></package>').encode()
        nuspec.write_bytes(raw)
        package = self.root / "selected.nupkg"
        with zipfile.ZipFile(package, "w") as archive:
            archive.writestr(identity + ".nuspec", raw)
            archive.writestr("lib/net10.0/Hexalith.EventStore.Aspire.dll", b"package payload must never be seeded")
        return package, nuspec

    def testOnlySelectedCatalogNuspecIsSeededWithoutPackagePayload(self):
        package, nuspec = self.metadata_fixture()
        record = q.catalog_metadata_binding(package, nuspec, "1" * 40)
        cache = self.root / "private-cache"
        destination = q.seed_catalog_metadata(record, cache)
        self.assertEqual(destination.read_bytes(), nuspec.read_bytes())
        self.assertEqual([str(p.relative_to(cache)) for p in cache.rglob("*") if p.is_file()],
                         ["hexalith.eventstore.aspire/3.128.0/hexalith.eventstore.aspire.nuspec"])
        with self.assertRaisesRegex(q.GateFailure, "package fallback"):
            q.validate_assets({"libraries": {"Hexalith.EventStore.Aspire/3.128.0": {"type": "package", "metadataOnly": True}}}, self.root)

    def testCatalogMetadataRejectsOtherPackageOrVersion(self):
        for identity, version in (("Hexalith.EventStore.Gateway", "3.128.0"), ("Hexalith.EventStore.Aspire", "3.127.0")):
            package, nuspec = self.metadata_fixture(identity, version)
            with self.assertRaisesRegex(q.GateFailure, "Only the exact selected"):
                q.catalog_metadata_binding(package, nuspec, "1" * 40)

    def testCatalogMetadataRejectsDifferentMemberBytesOrSourceRevision(self):
        package, nuspec = self.metadata_fixture()
        with self.assertRaisesRegex(q.GateFailure, "repository revision"):
            q.catalog_metadata_binding(package, nuspec, "2" * 40)
        nuspec.write_bytes(nuspec.read_bytes() + b"\n")
        with self.assertRaisesRegex(q.GateFailure, "differs from nupkg"):
            q.catalog_metadata_binding(package, nuspec, "1" * 40)

    def testSdkResolverSourcesArePrivateBeforeEvaluation(self):
        project = self.root / "snapshot/references/Builds/host/AppHost.csproj"
        project.parent.mkdir(parents=True)
        snapshot = self.root / "snapshot"
        feed = self.root / "feed"
        (snapshot / "NuGet.Config").write_text('<configuration><packageSources><clear/><add key="private-offline" value="' + str(feed) + '"/></packageSources></configuration>')
        self.assertEqual(q.sdk_feed_configuration(project, snapshot, feed)["effectivePackageSources"], {"private-offline": str(feed)})
        (project.parent / "nuget.config").write_text('<configuration><packageSources><add key="external" value="https://example.invalid"/></packageSources></configuration>')
        with self.assertRaisesRegex(q.GateFailure, "External SDK resolver"):
            q.sdk_feed_configuration(project, snapshot, feed)

    def testFailedSdkSelectionCannotLaunchDependentCommand(self):
        qualification = q.Qualification.__new__(q.Qualification)
        with patch.object(qualification, "sdk_gate", side_effect=q.GateFailure("wrong SDK")), patch.object(qualification, "run") as launch:
            with self.assertRaises(q.GateFailure):
                qualification.checked("build", ["dotnet", "build"])
            launch.assert_not_called()

    def testTimeoutTerminatesOwnedGroupAndRetainsReceipt(self):
        import sys
        with patch.object(q.time, "monotonic", side_effect=[0, 31]):
            with self.assertRaisesRegex(q.GateFailure, "Timeout"):
                q.run_owned([sys.executable, "-I", "-c", "import time; time.sleep(30)"], self.root,
                            {"PATH": "/usr/bin:/bin"}, self.root, "timeout", 30)
        record = json.loads((self.root / "timeout.command.json").read_text())
        self.assertTrue(record["timedOut"])
        self.assertEqual(record["exitCode"], -signal.SIGTERM)
        self.assertFalse(record["observedSurvivors"])

    def testRecordingDescendantsCannotIncludeOperationalChanges(self):
        self.assertTrue(q.recording_path("eng/story117-current-builds-host/plan.json"))
        self.assertFalse(q.recording_path("src/Hexalith.Folders/Program.cs"))
        self.assertFalse(q.recording_path("_bmad-output/implementation-artifacts/sprint-status.yaml"))

    def testVerifierRejectsCorruptionBeforeOpeningArchive(self):
        (self.root / "receipt.json").write_text("{}")
        with patch.object(v.tarfile, "open") as archive:
            with self.assertRaisesRegex(ValueError, "Receipt hash"):
                v.verify(self.root, "receipt.json", "0" * 64)
            archive.assert_not_called()

    def testVerifierRejectsUnsafeAndDuplicatePaths(self):
        for path in ("../escape", "/absolute", "a/../b", "a\\b", "./a"):
            self.assertFalse(v.safe_name(path))
        self.assertTrue(v.safe_name("logs/cold.stdout"))


if __name__ == "__main__":
    unittest.main()
