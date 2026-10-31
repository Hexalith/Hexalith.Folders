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



class CompletedDiagnosticReplayTests(unittest.TestCase):
    """A portable valid capsule anchors semantic tampering tests to passing evidence."""
    def fixture(self, incomplete=False):
        from model_replay_fixture import CompletedFixture
        value=CompletedFixture()
        return value.incomplete() if incomplete else value

    def run_fixture(self, fixture):
        with tempfile.TemporaryDirectory() as directory:
            path=fixture.save(directory)
            with patch.object(subprocess,'Popen',side_effect=AssertionError('no execution')),patch.object(subprocess,'run',side_effect=AssertionError('no execution')):
                return replay.replay(path)

    def rejects(self, fixture):
        with self.assertRaises((ValueError,KeyError)):
            self.run_fixture(fixture)

    def command(self,fixture,label):return next(row for row in fixture.result['commands'] if row['label']==label)

    def replace_json(self,fixture,name,value):return fixture.put(name,value)

    def test_valid_complete_and_incomplete_diagnostics_pass_without_execution(self):
        for incomplete in (False,True):
            with self.subTest(incomplete=incomplete):
                result=self.run_fixture(self.fixture(incomplete))
                self.assertEqual(result['qualification'],'incomplete' if incomplete else 'passed')
                self.assertEqual(result['originalAssemblyTests'],11)
                self.assertEqual(result['commandExecutionCount'],0)

    def test_prior_nss_failure_and_actual_observations_cannot_be_relabelled(self):
        fixture=self.fixture(True);fixture.result.update(qualification='passed',firstFailure=None)
        self.rejects(fixture)
        fixture=self.fixture(True);fixture.result.pop('currentCorrection');fixture.result.pop('priorGlobalStoreObservation');fixture.result.update(qualification='passed',firstFailure=None);fixture.put('certificate-stores-after.json',fixture.documents['certificate-stores-before.json']);self.rejects(fixture)
        for mutation in ('observations','errors'):
            fixture=self.fixture()
            if mutation=='observations':fixture.put('certificate-stores-after.json',[dict(path='/new/NSS',mtimeNs=4)])
            else:fixture.result['preservation']['errors']=['retained failure']
            self.rejects(fixture)

    def test_incomplete_credit_requires_every_successful_class_and_discovery_command(self):
        for label in ('class-CompositionProcessTests','discover-CompositionProcessTests','mcpcli-guards','discover-mcpcli-guards'):
            for mutation in ('missing','failed','timed-out'):
                with self.subTest(label=label,mutation=mutation):
                    fixture=self.fixture(True);row=self.command(fixture,label)
                    if mutation=='missing':fixture.result['commands'].remove(row)
                    elif mutation=='failed':row['exitCode']=1
                    else:row['timedOut']=True
                    self.rejects(fixture)

    def test_rebound_actual_child_executable_is_refused(self):
        fixture=self.fixture();fixture.result['modelChild']['executable']='/bin/true'
        name='processes/production-model.json';observations=json.loads(fixture.documents[name]);observations[1]=copy.deepcopy(fixture.result['modelChild'])
        self.command(fixture,'production-model')['processes']=fixture.put(name,observations)
        self.rejects(fixture)

    def test_closed_command_contract_rejects_rebound_executables_projects_filters_and_targets(self):
        for label,mutation in (
            ('helper-build',lambda row:row['argv'].append('-t:Publish')),
            ('original-test-build',lambda row:row['argv'].append('-p:Configuration=Release')),
            ('cli-build',lambda row:row['argv'].__setitem__(2,'/other/project.csproj')),
            ('production-model',lambda row:row['argv'].__setitem__(1,'/other/DistributionProbe.dll')),
            ('prepare-build',lambda row:row['argv'].__setitem__(1,'/other/Hexalith.Builds.Module.Cli.dll')),
            ('class-CompositionProcessTests',lambda row:row['argv'].__setitem__(3,replay.FULL+'OtherTests')),
            ('sdk-version',lambda row:row['argv'].__setitem__(0,'/bin/true'))):
            with self.subTest(label=label):
                fixture=self.fixture();row=self.command(fixture,label);mutation(row)
                observations=json.loads(fixture.documents[row['processes']['path']]);observations[0].update(argv=row['argv'],executable=row['argv'][0])
                row['processes']=fixture.put(row['processes']['path'],observations)
                self.rejects(fixture)

    def test_all_prevention_and_injection_controls_are_enforced(self):
        controls=list(model.q.CONTROLS)+['DOTNET_STARTUP_HOOKS','DOTNET_ADDITIONAL_DEPS','DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR','MSBUILD_EXE_PATH','AlternateCommonProps']
        for key in controls:
            with self.subTest(control=key):
                fixture=self.fixture();fixture.result['modelChild']['environment'][key]='unsafe'
                name='processes/production-model.json';observations=json.loads(fixture.documents[name]);observations[1]=copy.deepcopy(fixture.result['modelChild'])
                self.command(fixture,'production-model')['processes']=fixture.put(name,observations)
                self.rejects(fixture)
        for key in replay.PRIVATE:
            fixture=self.fixture();fixture.result['modelChild']['environment'][key]=fixture.workspace+'/../shared'
            name='processes/production-model.json';observations=json.loads(fixture.documents[name]);observations[1]=copy.deepcopy(fixture.result['modelChild'])
            self.command(fixture,'production-model')['processes']=fixture.put(name,observations);self.rejects(fixture)

    def test_discovery_and_xml_cannot_shrink_together(self):
        fixture=self.fixture();full=replay.FULL+'Workspace.PlatformModelCatalogTests';discovery=fixture.result['classDiscoveries'][full];removed=discovery['cases'].pop()
        row=self.command(fixture,'discover-Workspace.PlatformModelCatalogTests');row['log']=fixture.put(discovery['file']['path'],discovery['cases']);discovery['file']=row['log']
        report=next(item for item in fixture.result['testClasses'] if item['name']==full)
        for key in ('total','passed','discovered'):report[key]-=1
        xml=__import__('xml.etree.ElementTree',fromlist=['ElementTree']);root=xml.fromstring(fixture.documents[report['report']['path']]);assembly=root.find('assembly');collection=assembly.find('collection')
        for parent in (assembly,collection):parent.set('total',str(report['total']));parent.set('passed',str(report['total']))
        for test in list(collection):
            if json.loads('"'+test.get('name')+'"')==removed['DisplayName']:collection.remove(test)
        report['report']=fixture.put(report['report']['path'],xml.tostring(root));self.rejects(fixture)

    def test_every_xml_counter_is_validated(self):
        import xml.etree.ElementTree as xml
        for target in ('assembly','collection'):
            for counter in ('errors','failed','skipped','not-run'):
                with self.subTest(target=target,counter=counter):
                    fixture=self.fixture();report=fixture.result['testClasses'][0];root=xml.fromstring(fixture.documents[report['report']['path']]);node=root.find('assembly');node=node if target=='assembly' else node.find('collection');node.set(counter,'1');report['report']=fixture.put(report['report']['path'],xml.tostring(root));self.rejects(fixture)

    def test_compiler_inventory_arguments_and_response_bytes_are_bound(self):
        for mutation in ('shrink','arguments','response','output'):
            with self.subTest(mutation=mutation):
                fixture=self.fixture();name=fixture.result['modelCompiler']['path'];report=json.loads(fixture.documents[name])
                if mutation=='shrink':report[0]['inputs'].pop(1)
                elif mutation=='arguments':report[0]['arguments'].pop()
                elif mutation=='response':report[0]['process']['responses'][0]['text']+=' changed'
                else:report[0]['output']='/other/Model.dll'
                fixture.result['modelCompiler']=fixture.put(name,report);self.rejects(fixture)

    def test_mapping_guard_tokens_in_comments_do_not_supply_executable_guards(self):
        fixture=self.fixture();name='workspace/source-mapping/SourceMapping.values.targets';fixture.put(name,b'<Project><!-- HexalithVerifySourceMappingSelection HexalithValidateSourceMapping HexalithValidateSourceProjectsBeforeRestore HXW006 --></Project>');self.rejects(fixture)
        fixture=self.fixture();name='workspace/source-mapping/SourceMapping.targets';fixture.put(name,b'<Project><!-- UsingTask SourceMappingValidationTask SourceMapping.values.targets --></Project>');self.rejects(fixture)

    def test_canonical_output_roles_and_actual_pe_identity_are_required(self):
        fixture=self.fixture();fixture.result['outputs']['AppHost']=copy.deepcopy(fixture.result['outputs']['CLI']);self.rejects(fixture)
        fixture=self.fixture();model=fixture.result['outputs']['Model']['path'];metadata=json.loads(fixture.documents['pe-metadata.json']);next(row for row in metadata if row['path']==fixture.workspace+'/'+model)['identity']['name']='Invented';fixture.put('pe-metadata.json',metadata);self.rejects(fixture)

    def test_actual_root_observation_is_required_for_execution_credit(self):
        fixture=self.fixture();row=self.command(fixture,'class-CompositionProcessTests');row['processes']=fixture.put(row['processes']['path'],[]);self.rejects(fixture)

    def test_intact_failed_popen_is_incomplete_and_uncredited(self):
        fixture=self.fixture();row=copy.deepcopy(self.command(fixture,'helper-build'));row.update(launched=False,exitCode=None,timedOut=False);row.pop('pid');row['processes']=fixture.put(row['processes']['path'],[])
        fixture.result['commands']=[row];fixture.result.update(qualification='incomplete',firstFailure=dict(owner='Popen',detail='executable unavailable'));fixture.result.pop('independentDiagnosticGates',None)
        result=self.run_fixture(fixture);self.assertEqual(result['qualification'],'incomplete');self.assertEqual(result['originalAssemblyTests'],0)

    def test_linked_build_inputs_are_refused_before_file_filtering(self):
        for dangling in (False,True):
            with self.subTest(dangling=dangling),tempfile.TemporaryDirectory() as directory:
                root=Path(directory);target=root/'target.cs'
                if not dangling:target.write_text('source')
                (root/'linked.cs').symlink_to(target)
                with patch.object(model.q,'repositories',return_value=[root]),patch.object(model.q,'git',return_value=b'linked.cs\0'):
                    with self.assertRaises(model.q.GateFailure):model.source_inputs()
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);target=root/'real';target.mkdir();(target/'Code.cs').write_text('source');(root/'linked-dir').symlink_to(target,target_is_directory=True)
            with patch.object(model.q,'repositories',return_value=[root]),patch.object(model.q,'git',return_value=b'linked-dir/Code.cs\0'):
                with self.assertRaises(model.q.GateFailure):model.source_inputs()


if __name__ == "__main__":
    unittest.main()
