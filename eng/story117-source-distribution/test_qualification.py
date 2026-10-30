"""Failure-matrix checks use disposable inputs; no production SDK is invoked."""
import copy
import json
import os
import struct
from pathlib import Path
import subprocess
import sys
import tarfile
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parent))
import qualify
import verify


class QualificationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.work = Path(self.temporary.name)
        environment = dict(qualify.CONTROLS, HOME=str(Path.home()))
        for key in qualify.PRIVATE:
            path = self.work / key.lower()
            path.mkdir()
            environment[key] = str(path)
        self.config = dict(work=str(self.work), preservedHome=str(Path.home()), environment=environment, stage="model", mappingEnvironment={})
        for key in qualify.IMPORTS:
            path = self.work / key
            path.write_text("<Project />")
            self.config["mappingEnvironment"][key] = str(path)
        self.config["mappingEnvironment"].update(Configuration="Debug", HEXALITH_SOURCE_MAPPING_HASH="selected")
        self.config.update(nugetConfig=str(self.work / "NuGet.Config"), captureTargets=str(self.work / "capture.targets"), versionSuffix="20261029000000")
        catalog = self.work / "pinned-catalog.props"
        catalog.write_text("<Project />")
        self.config["modelPinnedCatalog"] = qualify.binding(catalog)
        common_props = self.work / "model-common.props"
        common_props.write_text("<Project />")
        self.config["modelCommonProps"] = qualify.binding(common_props)
        self.model_arguments = ["build", "Model.csproj", "-p:CustomBeforeDirectoryBuildProps=" + str(catalog), "-p:DirectoryPackagesPropsPath=" + str(catalog)]

    def tearDown(self):
        self.temporary.cleanup()

    def test_model_retains_incoming_and_added_controls_separately(self):
        incoming = dict(PATH="incoming", DOTNET_GENERATE_ASPNET_CERTIFICATE="true")
        environment, added, arguments = qualify.controlled_child(self.config, "sdk", self.model_arguments, incoming)
        self.assertEqual(incoming["DOTNET_GENERATE_ASPNET_CERTIFICATE"], "true")
        self.assertEqual(added["DOTNET_GENERATE_ASPNET_CERTIFICATE"], "false")
        self.assertEqual(environment["HEXALITH_SOURCE_MAPPING_HASH"], "selected")
        self.assertTrue(all(value in arguments for value in qualify.SERIAL))

    def test_fixture_private_cache_selection_is_preserved(self):
        selected = self.work / "fixture/packages"
        selected.mkdir(parents=True)
        environment, _, _ = qualify.controlled_child(self.config, "sdk", self.model_arguments, {"NUGET_PACKAGES": str(selected)})
        self.assertEqual(environment["NUGET_PACKAGES"], str(selected))

    def test_shared_cache_selection_cannot_override_private_defaults(self):
        environment, _, _ = qualify.controlled_child(self.config, "sdk", self.model_arguments, {"NUGET_PACKAGES": "/shared/packages"})
        self.assertEqual(environment["NUGET_PACKAGES"], self.config["environment"]["NUGET_PACKAGES"])

    def test_model_preserves_pinned_catalog_and_genuine_mapping_hooks(self):
        environment, added, arguments = qualify.controlled_child(self.config, "sdk", self.model_arguments, {})
        self.assertEqual(arguments[:len(self.model_arguments)], self.model_arguments)
        properties = dict(value.removeprefix("-p:").split("=", 1) for value in arguments if value.startswith("-p:"))
        self.assertEqual(properties["CustomBeforeDirectoryBuildProps"], self.config["mappingEnvironment"]["CustomBeforeDirectoryBuildProps"])
        self.assertEqual(properties["AlternateCommonProps"], self.config["modelCommonProps"]["path"])
        self.assertEqual(environment["AlternateCommonProps"], self.config["modelCommonProps"]["path"])
        self.assertEqual(added["AlternateCommonProps"], self.config["modelCommonProps"]["path"])

    def test_model_refuses_missing_common_props_composition(self):
        del self.config["modelCommonProps"]
        with self.assertRaisesRegex(qualify.GateFailure, "Missing mandatory model common-props"):
            qualify.controlled_child(self.config, "sdk", self.model_arguments, {})

    def test_model_refuses_changed_common_props_composition(self):
        Path(self.config["modelCommonProps"]["path"]).write_text("changed")
        with self.assertRaisesRegex(qualify.GateFailure, "identity drift"):
            qualify.controlled_child(self.config, "sdk", self.model_arguments, {})

    def test_model_refuses_an_unbound_catalog_argument(self):
        arguments = [value.replace(self.config["modelPinnedCatalog"]["path"], "/foreign/catalog.props") for value in self.model_arguments]
        with self.assertRaisesRegex(qualify.GateFailure, "exact bound pinned catalog"):
            qualify.controlled_child(self.config, "sdk", arguments, {})

    def test_model_refuses_changed_catalog_bytes_before_execution(self):
        Path(self.config["modelPinnedCatalog"]["path"]).write_text("changed")
        with self.assertRaisesRegex(qualify.GateFailure, "identity drift"):
            qualify.controlled_child(self.config, "sdk", self.model_arguments, {})

    def test_approved_index_ahead_tuple_is_preserved(self):
        row = dict(path="references/Hexalith.Builds", revision="new", indexedGitlink="new", rootGitlink="old")
        approval = dict(repositories=[dict(path=row["path"], committedRootGitlink="old", indexedRootGitlink="new")])
        qualify.validate_gitlink(row, approval)

    def test_checkout_index_drift_refuses_even_with_sync_approval(self):
        row = dict(path="references/Hexalith.Builds", revision="foreign", indexedGitlink="new", rootGitlink="old")
        approval = dict(repositories=[dict(path=row["path"], committedRootGitlink="old", indexedRootGitlink="new")])
        with self.assertRaisesRegex(qualify.GateFailure, "misalignment"):
            qualify.validate_gitlink(row, approval)

    def test_unapproved_index_ahead_tuple_refuses(self):
        row = dict(path="references/Hexalith.Builds", revision="new", indexedGitlink="new", rootGitlink="old")
        with self.assertRaisesRegex(qualify.GateFailure, "without approved"):
            qualify.validate_gitlink(row)

    def test_reuse_refuses_changed_production_source(self):
        path = self.work / "Source.cs"
        path.write_text("original")
        row = qualify.binding(path, self.work)
        path.write_text("changed")
        with self.assertRaisesRegex(qualify.GateFailure, "outside the model wrapper"):
            qualify.reusable_source_changes([row], {"wrapper.py"}, self.work)

    def test_reuse_records_only_explicit_wrapper_changes(self):
        path = self.work / "wrapper.py"
        path.write_text("original")
        row = qualify.binding(path, self.work)
        path.write_text("changed")
        changes = qualify.reusable_source_changes([row], {"wrapper.py"}, self.work)
        self.assertEqual(changes[0]["before"], row)
        self.assertEqual(changes[0]["after"], qualify.binding(path, self.work))

    def test_reuse_accepts_unchanged_bound_source(self):
        path = self.work / "Source.cs"
        path.write_text("original")
        self.assertEqual(qualify.reusable_source_changes([qualify.binding(path, self.work)], set(), self.work), [])

    def test_reuse_compares_controls_outside_model_blocks(self):
        source = 'def controlled_child(config):\n    guard = "required"\n    if config.get("stage") == "model":\n        model = "old"\n    return guard\n'
        model_only = source.replace('model = "old"', 'model = "new"')
        changed_control = source.replace('guard = "required"', 'guard = "weakened"')
        self.assertEqual(qualify.shared_sdk_control_ast(source), qualify.shared_sdk_control_ast(model_only))
        self.assertNotEqual(qualify.shared_sdk_control_ast(source), qualify.shared_sdk_control_ast(changed_control))

    def test_reuse_binds_shared_constants_and_transitive_helpers(self):
        source = Path(qualify.__file__).read_text()
        baseline = qualify.shared_sdk_control_ast(source)
        for before, after in [('SDK_VERSION = "10.0.402"', 'SDK_VERSION = "10.0.403"'),
                              ('"-m:1", "-nr:false"', '"-m:2", "-nr:false"'),
                              ('"DOTNET_GENERATE_ASPNET_CERTIFICATE": "false"', '"DOTNET_GENERATE_ASPNET_CERTIFICATE": "true"'),
                              ('"DOTNET_CLI_HOME", "NUGET_PACKAGES"', '"DOTNET_CLI_HOME", "OTHER_PACKAGES"'),
                              ('"CustomBeforeDirectoryBuildProps", "CustomAfterMicrosoftCommonProps"', '"ChangedImport", "CustomAfterMicrosoftCommonProps"'),
                              ('if not condition:', 'if False:'), ('return digest.hexdigest()', 'return "unchecked"'),
                              ('binding(path, base) == item', 'True'), ('sha256=sha(path)', 'sha256="unchecked"')]:
            with self.subTest(before=before):
                self.assertIn(before, source)
                self.assertNotEqual(baseline, qualify.shared_sdk_control_ast(source.replace(before, after, 1)))
        model_only = source.replace('"Missing mandatory model common-props composition"', '"Changed model-only diagnostic"')
        self.assertEqual(baseline, qualify.shared_sdk_control_ast(model_only))

    def test_complete_input_gate_rejects_new_sources_and_preserves_partial_helpers(self):
        source = self.work / 'Original.cs'
        source.write_text('original')
        rows = [qualify.binding(source, self.work)]
        with patch.object(qualify, 'source_input_paths', return_value={'Original.cs', 'Injected.targets'}):
            with self.assertRaisesRegex(qualify.GateFailure, 'path set changed'):
                qualify.check_sources(rows, self.work, complete=True)
            qualify.check_sources(rows, self.work)

    def test_input_enumeration_excludes_only_explicit_owned_outputs(self):
        def observed(_, *args):
            if '--stage' in args:
                return b'100644 object 0\tOriginal.cs\0'
            return b'Injected.targets\0.artifacts/owned/Generated.cs\0'
        with patch.object(qualify, 'repositories', return_value=[self.work]), patch.object(qualify, 'git', side_effect=observed):
            self.assertEqual(qualify.source_input_paths(self.work, [self.work / '.artifacts/owned']), {'Original.cs', 'Injected.targets'})

    def test_source_reuse_rejects_additions_except_owned_json_recordings(self):
        previous = [dict(path='Original.cs')]
        qualify.check_reuse_additions(previous, previous + [dict(path='_bmad-output/implementation-artifacts/evidence/' + qualify.PREFIX + '-new.json')])
        for path in ('Added.cs', 'Injected.targets', '_bmad-output/implementation-artifacts/evidence/' + qualify.PREFIX + '-new.targets'):
            with self.subTest(path=path), self.assertRaisesRegex(qualify.GateFailure, 'New source/import inputs'):
                qualify.check_reuse_additions(previous, previous + [dict(path=path)])

    def test_every_missing_mandatory_control_refuses(self):
        for key in qualify.CONTROLS:
            with self.subTest(key=key):
                config = copy.deepcopy(self.config)
                del config["environment"][key]
                with self.assertRaises(qualify.GateFailure):
                    qualify.controlled_child(config, "sdk", ["build"], {})

    def test_missing_model_mapping_refuses(self):
        for key in qualify.IMPORTS:
            with self.subTest(key=key):
                config = copy.deepcopy(self.config)
                del config["mappingEnvironment"][key]
                with self.assertRaises(qualify.GateFailure):
                    qualify.controlled_child(config, "sdk", ["build"], {})

    def test_missing_mapping_file_refuses(self):
        Path(self.config["mappingEnvironment"][qualify.IMPORTS[0]]).unlink()
        with self.assertRaisesRegex(qualify.GateFailure, "import unavailable"):
            qualify.controlled_child(self.config, "sdk", ["build"], {})

    def test_escaped_private_cache_and_changed_home_refuse(self):
        for key, value in (("NUGET_PACKAGES", "/outside"), ("HOME", "/other-home")):
            with self.subTest(key=key):
                config = copy.deepcopy(self.config)
                config["environment"][key] = value
                with self.assertRaises(qualify.GateFailure):
                    qualify.controlled_child(config, "sdk", ["build"], {})

    def test_release_refuses(self):
        with self.assertRaisesRegex(qualify.GateFailure, "Release"):
            qualify.controlled_child(self.config, "sdk", ["build", "-c", "Release"], {})

    def test_debug_lane_normalizes_configuration_forms_and_conflicts(self):
        forms = [['--configuration=Release'], ['--configuration', 'release'], ['-c:Release'], ['-c=Release'],
                 ['/p:Configuration=Release'], ['-property:Other=x;configuration=Release'], ['--property', 'Configuration=Release'],
                 ['-c', 'Debug', '-p:Configuration=Release'], ['-p:Configuration=Other'], ['@configuration.rsp']]
        for form in forms:
            with self.subTest(form=form), self.assertRaisesRegex(qualify.GateFailure, 'Debug is required'):
                qualify.controlled_child(self.config, 'sdk', self.model_arguments + form, {})
        config = dict(self.config, stage='tests')
        _, _, args = qualify.controlled_child(config, 'sdk', ['build', '-c', 'Release'], {})
        self.assertIn('Release', args)  # Deliberate synthetic fixture policy is preserved.
        _, _, args = qualify.controlled_child(config, 'sdk', ['pack', 'Fixture.csproj'], {})
        self.assertIn('-p:Configuration=Debug', args)
        _, _, args = qualify.controlled_child(config, 'sdk', ['msbuild', 'Fixture.csproj', '/t:Pack'], {})
        self.assertIn('-p:Configuration=Debug', args)
        with self.assertRaisesRegex(qualify.GateFailure, 'Debug is required'):
            qualify.controlled_child(config, 'sdk', ['msbuild', '/target:Build;Pack', '/p:Configuration=Release'], {})

    def test_child_guard_prevents_actual_executable_on_missing_control(self):
        sentinel = self.work / "sdk-executed"
        executable = self.work / "dotnet"
        executable.write_text("#!/usr/bin/python3\nfrom pathlib import Path\nPath(" + repr(str(sentinel)) + ").write_text('unexpected')\n")
        executable.chmod(0o700)
        config = copy.deepcopy(self.config)
        config.update(sdk=str(executable), sdkSha256=qualify.sha(executable), childLog=str(self.work / "children.jsonl"))
        del config["environment"]["DOTNET_GENERATE_ASPNET_CERTIFICATE"]
        harness = self.work / "harness"
        harness.mkdir()
        (harness / "qualify.py").write_bytes(Path(qualify.__file__).read_bytes())
        path = self.work / "config.json"
        qualify.write(path, config)
        result = subprocess.run([sys.executable, "-B", str(Path(qualify.__file__).with_name("sdk_child.py")), str(path), "sdk", "build"], capture_output=True)
        self.assertEqual(result.returncode, 78, result.stderr.decode())
        self.assertFalse(sentinel.exists())
        self.assertFalse(json.loads(Path(config["childLog"]).read_text())["sdkExecuted"])

    def test_input_drift_refuses(self):
        path = self.work / "source.cs"
        path.write_text("original")
        item = qualify.binding(path)
        path.write_text("changed")
        with self.assertRaisesRegex(qualify.GateFailure, "identity drift"):
            qualify.same(item)

    def test_package_fallback_refuses(self):
        path = self.work / "project.assets.json"
        qualify.write(path, dict(libraries={"Hexalith.Example/1.0.0": dict(type="package")}))
        with self.assertRaisesRegex(qualify.GateFailure, "package fallback"):
            qualify.assets_audit([path])

    def test_incompatible_pe_identity_refuses(self):
        model = self.work / "Hexalith.Platform.Composition.dll"
        identity = dict(name="Hexalith.Example", version="1.0.0.0", culture="neutral", publicKeyToken="null")
        rows = [dict(path=str(model), references=[identity]), dict(path=str(self.work / "Hexalith.Example.dll"), identity=dict(identity, version="2.0.0.0"))]
        with self.assertRaisesRegex(qualify.GateFailure, "Incompatible"):
            qualify.verify_pe_references(rows, model)

    def test_unrun_required_assertions_refuse(self):
        path = self.work / "tests.xml"
        path.write_text('<assemblies><assembly total="1" passed="0" skipped="1"><collection><test type="Required" result="Skip" /></collection></assembly></assemblies>')
        with self.assertRaises(qualify.GateFailure):
            qualify.class_result(path, "Required", 1)

    def test_archive_excludes_private_runtime_fifos_before_inspecting_members(self):
        work = self.work / "run"
        cache = work / "tmpdir"
        cache.mkdir(parents=True)
        fifo = cache / "clr-debug-pipe"
        os.mkfifo(fifo)
        qualify.write(work / "result.json", dict(qualification="incomplete"))
        evidence = self.work / "retained"
        evidence.mkdir()
        with patch.object(qualify, "EVIDENCE", evidence):
            receipt = qualify.archive(work, "fifo-case")
        with tarfile.open(evidence / receipt["archive"]["path"]) as capsule:
            self.assertEqual([row.name for row in capsule], ["result.json"])
        self.assertTrue(fifo.exists())

    def test_partial_runner_capture_preserves_first_failure_and_seals_receipt(self):
        work = self.work / 'partial'
        artifacts = self.work / 'artifacts'
        runner = artifacts / 'original/bin/Hexalith.Builds.Module.Tests/debug'
        runner.mkdir(parents=True)
        (runner / 'Hexalith.Builds.Module.Tests.dll').write_bytes(b'partial assembly')
        first = dict(detail='original build failure')
        qualify.write(work / 'result.json', dict(privateArtifacts=str(artifacts), qualification='incomplete', firstFailure=first))
        retained = self.work / 'retained'
        retained.mkdir()
        with patch.object(qualify, 'EVIDENCE', retained):
            receipt = qualify.archive(work, 'partial')
        self.assertEqual(receipt['result']['firstFailure'], first)
        self.assertEqual(len(receipt['result']['originalTestRunnerBindings']), 1)
        self.assertEqual(len(receipt['result']['evidenceCaptureErrors']), 5)
        self.assertTrue((retained / receipt['archive']['path']).is_file())

    def test_focused_factory_success_cannot_mask_later_failures(self):
        result = dict(supplementaryFactoryTests=dict(passed=8), preservation=dict(status='verified'),
                      firstFailure=dict(type='UnavailablePinnedCatalogObject'), commands=[dict(exitCode=0, timedOut=False)])
        self.assertTrue(qualify.focused_success(result))
        for extra in (dict(supplementaryFailure=dict(detail='later guard failure')), dict(evidenceCaptureErrors=['missing compiler input']),
                      dict(compilerAuxiliaryCapture=dict(status='failed')), dict(commands=[dict(exitCode=1)])):
            with self.subTest(extra=extra):
                self.assertFalse(qualify.focused_success(dict(result, **extra)))

    def receipt(self):
        result = dict(qualification="incomplete", protectedAuthority=qualify.AUTHORITY, ordinaryStartup="pending", signedDistribution="pending", operationalAcceptance="pending",
                      firstFailure=dict(detail="owned gate"), preservation=dict(status="verified"), commands=[dict(argv=["this-command-must-never-execute"])])
        work = self.work / "evidence"
        work.mkdir()
        qualify.write(work / "result.json", result)
        archive = self.work / "artifacts.tar.gz"
        with tarfile.open(archive, "w:gz") as capsule:
            capsule.add(work / "result.json", arcname="result.json")
        manifest = self.work / "members.json"
        qualify.write(manifest, [qualify.binding(work / "result.json", work)])
        receipt = self.work / "receipt.json"
        qualify.write(receipt, dict(archive=qualify.binding(archive, self.work), memberManifest=qualify.binding(manifest, self.work), memberCount=1, result=result))
        return receipt, archive

    @staticmethod
    def managed_fixture():
        """Small PE/CLI metadata container; never executed or treated as signed."""
        payload = bytearray(1024)
        payload[:2] = b'MZ'
        struct.pack_into('<I', payload, 0x3c, 0x80)
        payload[0x80:0x84] = b'PE\0\0'
        struct.pack_into('<HH', payload, 0x84, 0x14c, 1)
        struct.pack_into('<H', payload, 0x94, 224)
        struct.pack_into('<H', payload, 0x98, 0x10b)
        struct.pack_into('<I', payload, 0x98 + 92, 16)
        struct.pack_into('<II', payload, 0x98 + 96 + 14 * 8, 0x2000, 72)
        struct.pack_into('<IIII', payload, 0x98 + 224 + 8, 512, 0x2000, 512, 512)
        struct.pack_into('<IHHII', payload, 512, 72, 2, 5, 0x2080, 128)
        payload[640:644] = b'BSJB'
        struct.pack_into('<I', payload, 652, 4)
        payload[656:660] = b'v4\0\0'
        struct.pack_into('<HHII', payload, 660, 0, 1, 48, 40)
        payload[672:675] = b'#~\0'
        struct.pack_into('<Q', payload, 640 + 48 + 8, 1 | (1 << 32))
        struct.pack_into('<II', payload, 640 + 48 + 24, 1, 1)
        return bytes(payload)

    def passed_fixture(self):
        work = self.work / 'passed-work'
        work.mkdir()
        def put(name, payload):
            path = work / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(payload if isinstance(payload, bytes) else json.dumps(payload).encode())
            return qualify.binding(path, work)
        result = dict(qualification='passed', protectedAuthority=qualify.AUTHORITY, ordinaryStartup='pending', signedDistribution='pending',
                      operationalAcceptance='pending', firstFailure=None, preservation=dict(status='verified'), commands=[], testClasses=[], classDiscoveries={})
        assembly = str(work / 'private-artifacts/original/bin/Hexalith.Builds.Module.Tests/debug/Hexalith.Builds.Module.Tests.dll')
        result['privateArtifacts'] = str(work / 'private-artifacts')
        result['originalTestRunnerBindings'] = []
        for name in verify.RUNNER_FILES:
            payload = self.managed_fixture() if name.endswith('.dll') else b'{}'
            retained = put('original-test-runner/' + name, payload)
            result['originalTestRunnerBindings'].append(dict(retained=retained, source=dict(retained, path=str(Path(assembly).parent / name))))
        command_labels = ['sdk-version', 'sdk-info', 'bootstrap-build', 'production-mapping', 'original-debug-build', 'current-api-helper-build',
                          'model-compilation-api', 'explicit-host-selection', 'metadata-only-inspection']
        import xml.etree.ElementTree as ET
        for short in qualify.CLASSES:
            full = 'Hexalith.Builds.ModuleTool.Tests.' + short
            method = 'QualificationCatalogProbeInterceptionIsEstablishedAsync' if short == 'CompositionSdkProcessTests' else 'Theory'
            values = ['A', 'B'] if short == 'CompositionCommandOptionsTests' else ['A']
            cases = [dict(Class=full, Method=method, DisplayName=full + '.' + method + '(value: "' + value + '\\path")', Assembly=assembly) for value in values]
            file = put('logs/discover-' + short + '.log', cases)
            result['classDiscoveries'][full] = dict(file=file, cases=cases)
            xml = ET.Element('assemblies')
            report = ET.SubElement(xml, 'assembly', total=str(len(cases)), passed=str(len(cases)), failed='0', errors='0', skipped='0', **{'not-run': '0'})
            collection = ET.SubElement(report, 'collection')
            for case in cases:
                ET.SubElement(collection, 'test', type=full, method=method, name=json.dumps(case['DisplayName'])[1:-1], result='Pass')
            binding = put(short + '.xml', ET.tostring(xml))
            result['testClasses'].append(dict(name=full, total=len(cases), passed=len(cases), failed=0, errors=0, skipped=0, notRun=0, discovered=len(cases), report=binding))
            command_labels += ['discover-' + short, 'class-' + short]
        for label in command_labels:
            path = 'logs/' + label + '.log'
            log = qualify.binding(work / path, work) if (work / path).exists() else put(path, b'recorded, never executed')
            result['commands'].append(dict(label=label, argv=['never-execute', label], launched=True, exitCode=0, timedOut=False, log=log))
        put('commands.json', result['commands'])
        sdk = put('sdk-layout/dotnet', b'private SDK host')
        compiler = put('sdk-layout/sdk/10.0.402/csc.dll', b'compiler bytes')
        sdk_source = dict(sdk, path=str(work / sdk['path']))
        compiler_source = dict(compiler, path=str(work / compiler['path']))
        result['sdkSelection'] = dict(sdkVersion='10.0.402', privateHost=sdk_source, installedHost=dict(sdk_source, path='/installed/dotnet'), links={'sdk/10.0.402': '/installed/sdk/10.0.402'})
        result['sdkBindings'] = put('sdk-bindings.json', [sdk_source, compiler_source])
        put('source-baseline.json', [dict(path='Source.cs', bytes=1, sha256='source')])
        repos = [dict(path='.', revision='a' * 40)] + [dict(path='references/R' + str(index), revision='a' * 40, rootGitlink='a' * 40, indexedGitlink='a' * 40) for index in range(12)]
        put('repository-baseline.json', repos)
        mapping = dict(Entries=[dict(Path='/source/' + row['path'], Origin='source') for row in repos[1:]])
        result['productionMapping'] = mapping
        put('workspace/source-mapping/mapping.json', mapping)
        result['outputs'] = {}
        for role, name in verify.OUTPUT_ROLES.items():
            result['outputs'][role] = put('distribution/' + role + '/' + name + '.dll', self.managed_fixture())
            for suffix in ['.deps.json', '.pdb'] + (['.runtimeconfig.json'] if role in {'CLI', 'AppHost', 'EventStoreHost', 'UiHost'} else []):
                put('distribution/' + role + '/' + name + suffix, b'{}')
        for role in ('CLI', 'AppHost', 'Model'):
            put('distribution/' + role + '/Hexalith.Builds.Tooling.dll', self.managed_fixture())
        put('distribution/Model/Hexalith.Example.dll', self.managed_fixture())
        selected = lambda role: str(work / result['outputs'][role]['path'])
        result['selection'] = dict(hostBinding=result['outputs']['AppHost'], AppHostAssemblyPath=selected('AppHost'), DescriptorChildEntryAssemblyPath=selected('CLI'))
        identity = lambda name: dict(name=name, version='1.0.0.0', culture='neutral', publicKeyToken='null')
        refs = [identity('Hexalith.Builds.Tooling'), identity('Hexalith.Example')]
        metadata = [dict(path=str(path), identity=identity(path.stem), references=refs if str(path) == selected('Model') else []) for path in sorted((work / 'distribution').rglob('*.dll'))]
        put('pe-metadata.json', metadata)
        result['modelReferenceIdentities'] = dict(model=selected('Model'), references=refs,
            hexalithMatches=[dict(reference=ref, output=str(work / 'distribution/Model' / (ref['name'] + '.dll'))) for ref in refs])
        put('output-seal.json', [qualify.binding(path, work) for path in sorted((work / 'distribution').rglob('*')) if path.is_file()])
        asset = put('assets/project.assets.json', dict(libraries={'Source/1.0.0': dict(type='project')}))
        result['originalAssets'] = result['modelAssets'] = [dict(retained=asset, hexalithPackages=[])]
        environment = dict(Configuration='Debug', DOTNET_GENERATE_ASPNET_CERTIFICATE='false', MSBUILDDISABLENODEREUSE='1')
        result['productionEnvironment'] = dict(Configuration='Debug')
        result['modelChildControls'] = dict(sdkExecuted=True, incomingArguments=['build'], effectiveEnvironment=environment, addedEnvironment=environment,
                                            effectiveArguments=['build', *qualify.SERIAL])
        compiler_inputs = []
        for kind, name in [('source', 'Source.cs'), ('reference', 'Reference.dll')]:
            payload = b'compiler ' + kind.encode()
            digest = __import__('hashlib').sha256(payload).hexdigest()
            put('compiler-bytes/' + digest + '/' + name, payload)
            compiler_inputs.append(dict(kind=kind, file=dict(path='/inputs/' + name, bytes=len(payload), sha256=digest)))
        vector = put('compiler/actual.txt', b'actual captured source/reference vector')
        result['compilerInputs'] = put('compiler-inputs.json', [dict(capture=vector, inputs=compiler_inputs)])
        retained_compiler = put('compiler-auxiliary-bytes/' + compiler['sha256'] + '/csc.dll', b'compiler bytes')
        row = dict(kind='compiler', file=compiler_source, retained=retained_compiler)
        snapshot = put('compiler-auxiliary-source-result.json', dict(scope='post-execution audit snapshot'))
        aux = dict(inputs=[row], commands=[dict(project='/source/Hexalith.Platform.Composition.csproj', arguments=['/noconfig', '/out:/model.dll'], inputs=[row], log=next(x['log'] for x in result['commands'] if x['label']=='model-compilation-api'))], sourceResult=snapshot)
        result['compilerAuxiliaryInputs'] = put('compiler-auxiliary-inputs.json', aux)
        result['compilerAuxiliaryCapture'] = dict(status='bound', commands=1, files=1)
        fixture = dict(work=work, result=result)
        return fixture

    def seal_passed_fixture(self, fixture):
        work, result = fixture['work'], fixture['result']
        qualify.write(work / 'result.json', result)
        archive, manifest, receipt = self.work / 'passed.tar.gz', self.work / 'passed.members.json', self.work / 'passed.receipt.json'
        rows = []
        with tarfile.open(archive, 'w:gz') as capsule:
            for path in sorted(work.rglob('*')):
                if path.is_file():
                    row = qualify.binding(path, work)
                    rows.append(row)
                    capsule.add(path, arcname=row['path'])
        qualify.write(manifest, rows)
        qualify.write(receipt, dict(archive=qualify.binding(archive, self.work), memberManifest=qualify.binding(manifest, self.work), memberCount=len(rows), result=result, privateWorkspace=str(work)))
        return receipt

    def test_valid_passed_capsule_and_rebound_wrong_host_in_both_python_modes(self):
        fixture = self.passed_fixture()
        receipt = self.seal_passed_fixture(fixture)
        self.assertEqual(verify.replay(receipt)['qualification'], 'passed')
        for wrong in (False, True):
            if wrong:
                fixture['result']['selection']['AppHostAssemblyPath'] += '.foreign'
                receipt = self.seal_passed_fixture(fixture)
            for optimized in (False, True):
                result = subprocess.run([sys.executable, '-B', *(['-O'] if optimized else []), str(Path(verify.__file__)), '--receipt', str(receipt)], capture_output=True)
                self.assertEqual(result.returncode, 2 if wrong else 0, result.stdout.decode())
                if wrong:
                    self.assertIn(b'Factory selected another host', result.stdout)

    def test_full_case_identity_rejects_duplicate_or_substituted_theory_arguments(self):
        fixture = self.passed_fixture()
        name = 'CompositionCommandOptionsTests'
        import xml.etree.ElementTree as ET
        path = fixture['work'] / (name + '.xml')
        original = path.read_bytes()
        for display in ('duplicate', 'substitution'):
            tree = ET.fromstring(original)
            tests = tree.findall('.//test')
            tests[1].set('name', tests[0].get('name') if display=='duplicate' else tests[1].get('name').replace(r'value: \"B', r'value: \"C'))
            path.write_bytes(ET.tostring(tree))
            discovery = fixture['result']['classDiscoveries']['Hexalith.Builds.ModuleTool.Tests.'+name]['cases']
            with self.assertRaisesRegex(qualify.GateFailure, 'case identit'):
                qualify.class_result(path, 'Hexalith.Builds.ModuleTool.Tests.'+name, 1, discovery)
            with self.assertRaisesRegex(ValueError, 'case identit'):
                verify.replay(self.seal_passed_fixture(fixture))

    def test_passed_capsule_requires_command_sdk_compiler_and_runner_provenance(self):
        fixture = self.passed_fixture()
        original = copy.deepcopy(fixture['result'])
        for field in ('commands', 'sdkSelection', 'sdkBindings', 'compilerInputs', 'compilerAuxiliaryInputs', 'originalTestRunnerBindings'):
            fixture['result'] = copy.deepcopy(original)
            fixture['result'].pop(field)
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, 'provenance absent'):
                verify.replay(self.seal_passed_fixture(fixture))
        fixture['result'] = copy.deepcopy(original)
        fixture['result']['commands'][0]['exitCode'] = 1
        qualify.write(fixture['work'] / 'commands.json', fixture['result']['commands'])
        with self.assertRaisesRegex(ValueError, 'command did not succeed'):
            verify.replay(self.seal_passed_fixture(fixture))

    def test_passed_reused_capsule_requires_exact_retained_input_proof(self):
        fixture = self.passed_fixture()
        work, result = fixture['work'], fixture['result']
        original_commands = copy.deepcopy(result['commands'])
        commands = [row for row in original_commands if row['label'] in {'model-compilation-api', 'explicit-host-selection', 'metadata-only-inspection'}]
        for label in ('resume-sdk-version', 'resume-production-mapping', 'original-debug-graph-refresh'):
            path = work / ('logs/' + label + '.log')
            path.write_bytes(b'fresh reuse control')
            commands.append(dict(label=label, launched=True, exitCode=0, timedOut=False, argv=['never-execute'], log=qualify.binding(path, work)))
        result['commands'] = commands
        qualify.write(work/'commands.json', commands)
        declared = dict(path='/prior/receipt', bytes=1, sha256='bound prior receipt')
        result['reusedSourceEvidence'] = dict(sharedSdkControlsUnchanged=True, originalCommands=original_commands, sourcePreservation='exact critical bindings', receipt=declared, archive=declared, memberManifest=declared)
        rows = [row['source'] for row in result['originalTestRunnerBindings']]
        inputs = json.loads((work/'compiler-inputs.json').read_text())
        rows += [row['file'] for record in inputs for row in record['inputs']]
        rows += [row['file'] for row in json.loads((work/'compiler-auxiliary-inputs.json').read_text())['inputs']]
        before, after = work/'refresh-before.json', work/'refresh-after.json'
        qualify.write(before, rows)
        qualify.write(after, rows)
        compiler = work/'refresh-commands.json'
        names = [*verify.OUTPUT_ROLES.values(), 'Hexalith.Builds.Module.Tests']
        qualify.write(compiler, dict(exactArgumentEquality=True, intentionalArgumentDifferences=[], commands={'/output/'+name+'.dll': ['/compiler', ['/noconfig']] for name in names}))
        changed = work/'refresh-changes.json'
        qualify.write(changed, [])
        result['originalGraphRefresh'] = dict(criticalInputsUnchanged=True, actualCompilations=len(names), boundFiles=len(rows),
            before=qualify.binding(before, work), after=qualify.binding(after, work), compilerCommands=qualify.binding(compiler, work), changes=qualify.binding(changed, work))
        self.assertEqual(verify.replay(self.seal_passed_fixture(fixture))['qualification'], 'passed')
        rows[0] = dict(rows[0], sha256='changed')
        qualify.write(after, rows)
        result['originalGraphRefresh']['after'] = qualify.binding(after, work)
        with self.assertRaisesRegex(ValueError, 'test/compiler inputs changed'):
            verify.replay(self.seal_passed_fixture(fixture))

    def test_passed_capsule_rejects_same_non_pe_output_for_all_roles(self):
        fixture = self.passed_fixture()
        fixture['result']['outputs'] = dict.fromkeys(verify.OUTPUT_ROLES, fixture['result']['outputs']['CLI'])
        with self.assertRaisesRegex(ValueError, 'distinct canonical paths'):
            verify.replay(self.seal_passed_fixture(fixture))

    def test_passed_capsule_rejects_non_pe_payload_at_canonical_role(self):
        fixture = self.passed_fixture()
        path = fixture['work'] / fixture['result']['outputs']['CLI']['path']
        path.write_bytes(b'not a PE')
        fixture['result']['outputs']['CLI'] = qualify.binding(path, fixture['work'])
        qualify.write(fixture['work'] / 'output-seal.json', [qualify.binding(p, fixture['work']) for p in sorted((fixture['work']/'distribution').rglob('*')) if p.is_file()])
        with self.assertRaisesRegex(ValueError, 'managed PE'):
            verify.replay(self.seal_passed_fixture(fixture))

    def test_passed_capsule_requires_companions_and_complete_seal_inventory(self):
        fixture = self.passed_fixture()
        work = fixture['work']
        companion = work/'distribution/CLI/Hexalith.Builds.Module.Cli.runtimeconfig.json'
        companion.unlink()
        seal = [qualify.binding(p, work) for p in sorted((work/'distribution').rglob('*')) if p.is_file()]
        qualify.write(work/'output-seal.json', seal)
        with self.assertRaisesRegex(ValueError, 'companion absent'):
            verify.replay(self.seal_passed_fixture(fixture))
        companion.write_bytes(b'{}')
        with self.assertRaisesRegex(ValueError, 'Seal/distribution inventory'):
            verify.replay(self.seal_passed_fixture(fixture))

    def test_passed_capsule_binds_model_and_entire_hexalith_reference_set(self):
        fixture = self.passed_fixture()
        reference = copy.deepcopy(fixture['result']['modelReferenceIdentities'])
        fixture['result']['modelReferenceIdentities']['model'] = str(fixture['work']/fixture['result']['outputs']['CLI']['path'])
        with self.assertRaisesRegex(ValueError, 'selected Model artifact'):
            verify.replay(self.seal_passed_fixture(fixture))
        fixture['result']['modelReferenceIdentities'] = reference
        fixture['result']['modelReferenceIdentities']['hexalithMatches'].pop()
        with self.assertRaisesRegex(ValueError, 'Entire Hexalith'):
            verify.replay(self.seal_passed_fixture(fixture))

    def test_replay_is_read_only_and_executes_no_recorded_command(self):
        receipt, _ = self.receipt()
        self.assertEqual(verify.replay(receipt)["commandExecutionCount"], 0)
        # The replay source has no process execution dependency or dynamic command evaluation.
        source = Path(verify.__file__).read_text()
        self.assertNotIn("import subprocess", source)
        self.assertNotIn("os.system", source)

    def test_corrupt_archive_rejected_by_normal_and_optimized_python(self):
        receipt, archive = self.receipt()
        archive.write_bytes(archive.read_bytes() + b"corrupt")
        for optimized in (False, True):
            with self.subTest(optimized=optimized):
                command = [sys.executable, "-B", *(["-O"] if optimized else []), str(Path(verify.__file__)), "--receipt", str(receipt)]
                result = subprocess.run(command, capture_output=True)
                self.assertEqual(result.returncode, 2)
                self.assertIn(b"Archive SHA-256 mismatch", result.stdout)

    def test_corrupt_manifest_refuses(self):
        receipt, _ = self.receipt()
        manifest = self.work / "members.json"
        manifest.write_text("[]")
        with self.assertRaisesRegex(ValueError, "Manifest SHA-256 mismatch"):
            verify.replay(receipt)

    def test_corrupt_member_refuses_even_when_outer_archive_hash_is_rebound(self):
        receipt, archive = self.receipt()
        retained = self.work / "evidence/result.json"
        retained.write_bytes(retained.read_bytes().replace(b"owned gate", b"wrong gate"))
        with tarfile.open(archive, "w:gz") as capsule:
            capsule.add(retained, arcname="result.json")
        document = json.loads(receipt.read_text())
        document["archive"] = qualify.binding(archive, self.work)
        qualify.write(receipt, document)
        for optimized in (False, True):
            with self.subTest(optimized=optimized):
                result = subprocess.run([sys.executable, "-B", *(["-O"] if optimized else []), str(Path(verify.__file__)), "--receipt", str(receipt)], capture_output=True)
                self.assertEqual(result.returncode, 2)
                self.assertIn(b"Member SHA-256 mismatch", result.stdout)

    def test_unsafe_member_names_refuse(self):
        for name in ("../outside", "/absolute", "bad\\name"):
            with self.subTest(name=name), self.assertRaises(ValueError):
                verify.safe_path(name)


if __name__ == "__main__":
    unittest.main()
