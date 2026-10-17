"""Offline safety mutations use disposable sources, independently of checkout drift."""

import ast
import contextlib
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


DIRECTORY = Path(__file__).resolve().parent
MODULE_SPEC = importlib.util.spec_from_file_location("story117_current_verify", DIRECTORY / "verify.py")
verify = importlib.util.module_from_spec(MODULE_SPEC)
MODULE_SPEC.loader.exec_module(verify)
VALIDATE_PACKET = verify.validate_packet
CONFIG = "configuration.proposed.json"
PLAN = "staging-checks.json"
CATALOG = "references/Hexalith.Builds/Props/Directory.Packages.props"


class DisposablePacket(unittest.TestCase):
    """Build controlled allowlisted sources; never modify real repository sources."""

    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.packet = Path(temporary.name) / "packet"
        self.sources = Path(temporary.name) / "sources"
        self.packet.mkdir()
        self.sources.mkdir()
        config = json.loads((DIRECTORY / CONFIG).read_bytes())
        self.source_bodies = {
            relative: f"Controlled disposable source: {relative}\n".encode()
            for relative in verify.SOURCES
        }
        self.source_bodies["deploy/dapr/production/sidecar-config-bindings.yaml"] = (
            "\n".join("name: " + row["sourceName"] for row in config["secretReferences"].values()) + "\n"
        ).encode()
        properties = {}
        packages = {}
        for name, (prop, package) in verify.CATALOG_BINDINGS.items():
            properties[prop] = config["dependencies"][name]["catalogInputPin"]
            packages[package] = prop
        self.source_bodies[CATALOG] = (
            "<Project><PropertyGroup>"
            + "".join(f"<{key}>{value}</{key}>" for key, value in properties.items())
            + "</PropertyGroup><ItemGroup>"
            + "".join(f'<PackageVersion Include="{name}" Version="$({prop})" />'
                      for name, prop in packages.items())
            + "</ItemGroup></Project>"
        ).encode()
        config["sourceInputs"] = {
            relative: hashlib.sha256(body).hexdigest()
            for relative, body in self.source_bodies.items()
        }
        self.packet_bodies = {
            CONFIG: (json.dumps(config, indent=4) + "\n").encode(),
            PLAN: (DIRECTORY / PLAN).read_bytes(),
        }
        self.reset()

    def reset(self):
        for name, body in self.packet_bodies.items():
            path = self.packet / name
            if path.is_symlink():
                path.unlink()
            elif path.is_dir():
                path.rmdir()
            elif path.exists() and not path.is_file():
                path.unlink()
            path.write_bytes(body)
        for relative, body in self.source_bodies.items():
            path = self.sources / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            if path.is_symlink():
                path.unlink()
            elif path.is_dir():
                path.rmdir()
            elif path.exists() and not path.is_file():
                path.unlink()
            path.write_bytes(body)

    @contextlib.contextmanager
    def case(self):
        """Reset before and in finally, including failed assertions in a subtest."""
        self.reset()
        try:
            yield
        finally:
            self.reset()

    def edit(self, name, change):
        path = self.packet / name
        data = json.loads(path.read_bytes())
        change(data)
        path.write_text(json.dumps(data), encoding="utf-8")

    def validate(self):
        VALIDATE_PACKET(self.packet, self.sources)

    def rejected(self, message):
        with self.assertRaisesRegex(verify.PacketError, message):
            self.validate()

    def cli(self, arguments=()):
        output, errors = io.StringIO(), io.StringIO()
        with patch.object(verify, "validate_packet", self.validate), \
                patch.object(verify.sys, "argv", ["verify.py", *arguments]), \
                contextlib.redirect_stdout(output), contextlib.redirect_stderr(errors):
            code = verify.main()
        return code, output.getvalue(), errors.getvalue()


class PacketMutationTests(DisposablePacket):
    def test_controlled_proposal_passes_and_cli_reports_only_consistency(self):
        self.validate()
        code, output, errors = self.cli()
        self.assertEqual(0, code)
        self.assertEqual("", errors)
        for text in ("packet consistency only", "operational qualification remains HOLD",
                     "staging checks executed: 0", "V1Only", "T0 unset", "no activation authority"):
            self.assertIn(text, output)

    def test_route_activation_rejected_in_both_documents_and_module(self):
        mutations = (
            (CONFIG, lambda d: d["productionControl"].update(routeMode="Coexistence")),
            (PLAN, lambda d: d["productionControl"].update(routeMode="V2Only")),
            (CONFIG, lambda d: d["moduleConfiguration"]["folders-server"]["values"].update(
                {"Folders:ApiRouting:Mode": "V2Only"})),
        )
        for name, change in mutations:
            with self.subTest(name=name), self.case():
                self.edit(name, change)
                self.rejected("routeMode|Folders:ApiRouting:Mode")

    def test_authority_and_clock_claims_rejected(self):
        for name, fields in ((CONFIG, ("activationAuthority", "T0", "deploymentAuthorized",
                                      "publicationAuthorized", "trackerTransitionAuthorized")),
                             (PLAN, ("activationAuthority", "T0"))):
            for field in fields:
                with self.subTest(name=name, field=field), self.case():
                    self.edit(name, lambda d: d["productionControl"].update({field: True}))
                    self.rejected(field)

    def test_status_and_qualification_cannot_change_in_either_document(self):
        for name in (CONFIG, PLAN):
            for field, value in (("qualification", "PASS"), ("status", "approved")):
                with self.subTest(name=name, field=field), self.case():
                    self.edit(name, lambda d: d.update({field: value}))
                    self.rejected(field)

    def test_every_check_refuses_results_or_execution_claims(self):
        for index in range(14):
            for field, value in (("status", "passed"), ("result", "pass"),
                                 ("observedEvidence", ["invented-run"]),
                                 ("startedAt", "2026-10-17T00:00:00Z"),
                                 ("completedAt", "2026-10-17T00:01:00Z")):
                with self.subTest(index=index, field=field), self.case():
                    self.edit(PLAN, lambda d: d["checks"][index].update({field: value}))
                    self.rejected(field)

    def test_removing_any_required_check_is_rejected(self):
        for index in range(14):
            with self.subTest(index=index), self.case():
                self.edit(PLAN, lambda d: d["checks"].pop(index))
                self.rejected("required staging checks")

    def test_reordered_or_duplicated_checks_are_rejected(self):
        for change in (lambda d: d["checks"].reverse(),
                       lambda d: d["checks"].__setitem__(1, d["checks"][0])):
            with self.subTest(change=change), self.case():
                self.edit(PLAN, change)
                self.rejected("required staging check order")

    def test_all_checks_require_owners_procedures_evidence_and_finite_duration(self):
        for index in range(14):
            for field, value in (("owner", " "), ("preconditions", []), ("procedure", []),
                                 ("requiredEvidence", []), ("maxDurationSeconds", 0),
                                 ("maxDurationSeconds", -1), ("maxDurationSeconds", 14401),
                                 ("maxDurationSeconds", True), ("maxDurationSeconds", 1.5)):
                with self.subTest(index=index, field=field, value=value), self.case():
                    self.edit(PLAN, lambda d: d["checks"][index].update({field: value}))
                    self.rejected(field if field != "maxDurationSeconds" else "finite duration")

    def test_unknown_authority_keys_and_fixture_auth_are_rejected(self):
        for name, change, message in (
            (CONFIG, lambda d: d.update(approve=True), "required keys"),
            (PLAN, lambda d: d.update(execute=True), "required keys"),
            (CONFIG, lambda d: d["moduleConfiguration"]["folders-ui"]["values"].update(
                {"Folders:Authentication:Mode": "hermetic-test"}), "Folders:Authentication:Mode"),
        ):
            with self.subTest(name=name, message=message), self.case():
                self.edit(name, change)
                self.rejected(message)

    def test_first_install_scope_and_baseline_stay_unconfirmed(self):
        for field, value in (("confirmed", True), ("productionWorkingBaseline", "invented-v1"),
                             ("retainedApplicationData", False), ("branch", "approved-first-install")):
            with self.subTest(field=field), self.case():
                self.edit(CONFIG, lambda d: d["installationProposal"].update({field: value}))
                self.rejected("installation")

    def test_release_platform_and_source_observations_cannot_gain_acceptance(self):
        config = json.loads(self.packet_bodies[CONFIG])
        for field in config["releaseIdentity"]:
            if field in ("owner", "boundary"):
                continue
            with self.subTest(release=field), self.case():
                self.edit(CONFIG, lambda d: d["releaseIdentity"].update({field: "qualified"}))
                self.rejected(field)
        for group in ("platformBindings", "admissionPrerequisites"):
            for name in config[group]:
                with self.subTest(group=group, name=name), self.case():
                    self.edit(CONFIG, lambda d: d[group][name].update(value="accepted"))
                    self.rejected("unresolved value")
        for name in config["sourceSnapshots"]:
            with self.subTest(snapshot=name), self.case():
                self.edit(CONFIG, lambda d: d["sourceSnapshots"][name].update(artifactProvenance="attested"))
                self.rejected("artifactProvenance")

    def test_secret_or_environment_values_are_refused_without_disclosure(self):
        config = json.loads(self.packet_bodies[CONFIG])
        sentinel = "PRIVATE-CREDENTIAL-MUST-NOT-BE-DISCLOSED"
        for name in config["secretReferences"]:
            for field in ("value", "environmentSource"):
                with self.subTest(secret=name, field=field), self.case():
                    self.edit(CONFIG, lambda d: d["secretReferences"][name].update({field: sentinel}))
                    code, output, errors = self.cli()
                    self.assertEqual(1, code)
                    self.assertIn(f"secret {name}.{field}", errors)
                    self.assertNotIn(sentinel, output + errors)
        for name, row in config["moduleConfiguration"].items():
            for key, value in row["values"].items():
                if value is not None:
                    continue
                with self.subTest(module=name, key=key), self.case():
                    self.edit(CONFIG, lambda d: d["moduleConfiguration"][name]["values"].update({key: sentinel}))
                    code, output, errors = self.cli()
                    self.assertEqual(1, code)
                    self.assertNotIn(sentinel, output + errors)

    def test_secret_mappings_cannot_change_names_keys_consumers_or_targets(self):
        config = json.loads(self.packet_bodies[CONFIG])
        for name in config["secretReferences"]:
            for field, value in (("sourceName", "hexalith"), ("sourceKey", "wrong-key"),
                                 ("consumers", ["other-app"]), ("targetKeys", ["Other:Secret"])):
                with self.subTest(name=name, field=field), self.case():
                    self.edit(CONFIG, lambda d: d["secretReferences"][name].update({field: value}))
                    self.rejected(f"secret {name}.{field}")

    def test_secret_source_presence_requires_an_exact_name_value(self):
        relative = "deploy/dapr/production/sidecar-config-bindings.yaml"
        name = "hexalith-folders-server-app-token"
        body = self.source_bodies[relative].replace(name.encode(), (name + "-unrelated").encode())
        (self.sources / relative).write_bytes(body)
        self.edit(CONFIG, lambda d: d["sourceInputs"].update({relative: hashlib.sha256(body).hexdigest()}))
        self.rejected("secret folders-app-channel: exact source reference missing")

    def test_modules_refuse_unrelated_allowlisted_source_substitution(self):
        config = json.loads(self.packet_bodies[CONFIG])
        for name in config["moduleConfiguration"]:
            with self.subTest(name=name), self.case():
                self.edit(CONFIG, lambda d: d["moduleConfiguration"][name].update(source=verify.SOURCES[0]))
                self.rejected(f"module {name} reviewed source")

    def test_stale_full_snapshot_revisions_are_refused_for_every_repository(self):
        for name in verify.SNAPSHOTS:
            revisions = ["0" * 40]
            if name == "Builds":
                revisions.append(verify.PUBLISHED_BUILDS["sourceRevision"])
            for revision in revisions:
                with self.subTest(name=name, revision=revision), self.case():
                    self.edit(CONFIG, lambda d: d["sourceSnapshots"][name].update(revision=revision))
                    self.rejected(f"snapshot {name} canonical revision")

    def test_reviewed_requirement_text_refuses_contradictions_and_removals(self):
        config = json.loads(self.packet_bodies[CONFIG])
        mutations = [
            lambda d: d["installationProposal"].update(branchRule="Always first-install; ignore retained data."),
            lambda d: d["acceptedEnvelope"].update(rtoScope="Every outage, without response coverage limits."),
        ]
        for field in verify.RUNTIME_TEXT:
            mutations.append(lambda d, field=field: d["runtimeConstraints"].update(
                {field: "Disable mTLS and allow every caller; treat fixture success as live qualification."}))
        for name in config["admissionPrerequisites"]:
            mutations.append(lambda d, name=name: d["admissionPrerequisites"][name].update(
                requirement="No tool pin, accepted range, intake or other owner evidence required."))
        for name in config["platformBindings"]:
            mutations.append(lambda d, name=name: d["platformBindings"][name].update(
                contract="No security or operational safeguards required."))
        for index, change in enumerate(mutations):
            with self.subTest(index=index), self.case():
                self.edit(CONFIG, change)
                self.rejected("configuration requirements: reviewed content changed")

    def test_staging_safeguards_cannot_be_reduced_to_nonempty_placeholders(self):
        for index in range(14):
            for field, value in (("owner", "anyone"), ("preconditions", ["none"]),
                                 ("procedure", ["declare success"]), ("requiredEvidence", ["none"]),
                                 ("maxDurationSeconds", 1)):
                with self.subTest(index=index, field=field), self.case():
                    self.edit(PLAN, lambda d: d["checks"][index].update({field: value}))
                    self.rejected("staging safeguards: reviewed content changed")
        with self.case():
            self.edit(PLAN, lambda d: d.update(evidencePolicy="Offline success grants execution authority."))
            self.rejected("staging safeguards: reviewed content changed")

    def test_source_allowlist_cannot_be_removed_extended_or_replaced(self):
        for change in (lambda d: d["sourceInputs"].pop(verify.SOURCES[0]),
                       lambda d: d["sourceInputs"].update({"../../PRIVATE-PATH": "0" * 64}),
                       lambda d: d.update(sourceInputs=[])):
            with self.subTest(change=change), self.case():
                self.edit(CONFIG, change)
                code, output, errors = self.cli()
                self.assertEqual(1, code)
                self.assertIn("sourceInputs", errors)
                self.assertNotIn("PRIVATE-PATH", output + errors)

    def test_each_allowlisted_source_change_or_absence_is_refused(self):
        for relative in verify.SOURCES:
            for missing in (False, True):
                with self.subTest(relative=relative, missing=missing), self.case():
                    path = self.sources / relative
                    if missing:
                        path.unlink()
                    else:
                        path.write_bytes(b"changed disposable source\n")
                    self.rejected(("source unavailable: " if missing else "source changed: ")
                                  + relative.replace(".", r"\."))

    def test_source_symlink_escape_is_refused_without_target_disclosure(self):
        relative = verify.SOURCES[0]
        outside = self.sources.parent / "PRIVATE-TARGET"
        outside.write_text("PRIVATE-CONTENT", encoding="utf-8")
        path = self.sources / relative
        path.unlink()
        path.symlink_to(outside)
        code, output, errors = self.cli()
        self.assertEqual(1, code)
        self.assertIn("source escapes workspace: " + relative, errors)
        for private in (str(self.sources.parent), "PRIVATE-TARGET", "PRIVATE-CONTENT"):
            self.assertNotIn(private, output + errors)

    def test_internal_source_symlinks_refuse_unallowlisted_target_reads(self):
        relative = verify.SOURCES[0]
        target = self.sources / "PRIVATE-UNALLOWLISTED-SOURCE"
        target.write_bytes(self.source_bodies[relative])
        path = self.sources / relative
        path.unlink()
        path.symlink_to(target)
        original_read = Path.read_bytes

        def bounded_read(candidate):
            self.assertNotEqual(target, candidate, "redirected target must never be read")
            return original_read(candidate)

        with patch.object(Path, "read_bytes", bounded_read):
            code, output, errors = self.cli()
        self.assertEqual(1, code)
        self.assertIn("source redirected: " + relative, errors)
        self.assertNotIn("PRIVATE-UNALLOWLISTED-SOURCE", output + errors)
        self.assertNotIn(str(self.sources), output + errors)

    def test_source_parent_directory_symlink_is_refused(self):
        relative = verify.SOURCES[0]
        parent = (self.sources / relative).parent
        target = self.sources / "PRIVATE-REDIRECTED-DIRECTORY"
        parent.rename(target)
        parent.symlink_to(target, target_is_directory=True)
        try:
            self.rejected("source redirected: " + relative.replace(".", r"\."))
        finally:
            parent.unlink()
            target.rename(parent)

    def test_special_packet_and_source_inputs_are_refused_before_reads(self):
        targets = [(self.packet / name, name) for name in (CONFIG, PLAN)]
        targets.append((self.sources / verify.SOURCES[0], verify.SOURCES[0]))
        original_read = Path.read_bytes
        for path, relative in targets:
            for fifo in ((True, False) if hasattr(os, "mkfifo") else (False,)):
                with self.subTest(relative=relative, fifo=fifo), self.case():
                    path.unlink()
                    if fifo:
                        os.mkfifo(path)
                    else:
                        path.mkdir()

                    def bounded_read(candidate):
                        self.assertNotEqual(path, candidate, "special file read refused before it can block")
                        return original_read(candidate)

                    with patch.object(Path, "read_bytes", bounded_read):
                        code, output, errors = self.cli()
                    self.assertEqual(1, code)
                    self.assertIn("regular", errors)
                    self.assertIn(relative, errors)
                    self.assertNotIn(str(self.sources.parent), output + errors)

    def test_source_digests_have_strict_types_and_are_checked(self):
        for digest in (True, 1, None, [], "", "0" * 63, "A" * 64, "0" * 64):
            with self.subTest(digest=digest), self.case():
                self.edit(CONFIG, lambda d: d["sourceInputs"].update({verify.SOURCES[0]: digest}))
                self.rejected("source digest invalid|source changed")

    def test_dependency_pins_and_package_bindings_match_observed_catalog(self):
        for name in verify.DEPENDENCIES:
            for field, value in (("catalogInputPin", "99.0.0"), ("catalogProperty", "Other"),
                                 ("catalogPackage", "Other"), ("qualifiedArtifact", "qualified")):
                with self.subTest(name=name, field=field), self.case():
                    self.edit(CONFIG, lambda d: d["dependencies"][name].update({field: value}))
                    self.rejected(field)

    def test_catalog_inconsistency_fails_after_matching_fingerprint(self):
        original = self.source_bodies[CATALOG]
        for body, message in (
            (original.replace(b"3.124.0", b"3.123.0"), "observed catalog selection"),
            (original.replace(b'Version="$(HexalithEventStoreVersion)"', b'Version="3.124.0"'),
             "catalog package binding"),
            (b"<invalid", "invalid XML"),
        ):
            with self.subTest(message=message), self.case():
                (self.sources / CATALOG).write_bytes(body)
                self.edit(CONFIG, lambda d: d["sourceInputs"].update({CATALOG: hashlib.sha256(body).hexdigest()}))
                self.rejected(message)

    def test_published_release_cannot_be_substituted_with_local_source(self):
        for field, value in (("sourceRevision", verify.SNAPSHOTS["Builds"]), ("version", "4.99.0"),
                             ("receiptSha256", "0" * 64)):
            with self.subTest(field=field), self.case():
                self.edit(CONFIG, lambda d: d["publishedBuildsObservation"].update({field: value}))
                self.rejected("publishedBuildsObservation." + field)
        with self.case():
            self.edit(CONFIG, lambda d: d["publishedBuildsObservation"]["sourceCatalogObservation"].update(
                eventStorePin="3.124.0"))
            self.rejected("sourceCatalogObservation.eventStorePin")

    def test_ad9_adoption_evidence_or_alternate_hosting_is_refused(self):
        for field, value in (("adoptionEvidence", "live-enforcement-proven"),
                             ("canonicalHosting", ["custom-host"]),
                             ("supportingPrimitives", [])):
            with self.subTest(field=field), self.case():
                self.edit(CONFIG, lambda d: d["runtimeConstraints"].update({field: value}))
                self.rejected("runtimeConstraints." + field)

    def test_projects_operations_remain_read_only(self):
        for index in range(3):
            for field, value in (("mutates", True), ("method", "DELETE"), ("path", "/admin")):
                with self.subTest(index=index, field=field), self.case():
                    self.edit(CONFIG, lambda d: d["projectsReadOnlyOperations"][index].update({field: value}))
                    self.rejected("Projects operation")

    def test_missing_module_key_preserves_hold_in_cli_failure(self):
        self.edit(CONFIG, lambda d: d["moduleConfiguration"]["folders-workers"]["values"].pop("APP_API_TOKEN"))
        code, output, errors = self.cli()
        self.assertEqual(1, code)
        self.assertEqual("", output)
        self.assertIn("required keys differ", errors)
        self.assertIn("operational qualification remains HOLD", errors)

    def test_json_rejects_duplicate_keys_nonstandard_numbers_and_bad_encoding(self):
        for name in (CONFIG, PLAN):
            for body, message in (
                (b'{"PRIVATE-KEY": 1, "PRIVATE-KEY": 2}', "duplicate JSON key"),
                (b'{"value": NaN}', "non-standard JSON constant"),
                (b'{"value": Infinity}', "non-standard JSON constant"),
                (b'{"value": -Infinity}', "non-standard JSON constant"),
                (b'{"PRIVATE-KEY":', "invalid UTF-8 JSON"),
                (b"\xffPRIVATE-CONTENT", "invalid UTF-8 JSON"),
            ):
                with self.subTest(name=name, message=message), self.case():
                    (self.packet / name).write_bytes(body)
                    code, output, errors = self.cli()
                    self.assertEqual(1, code)
                    self.assertIn(message, errors)
                    self.assertNotIn("PRIVATE-KEY", output + errors)
                    self.assertNotIn("PRIVATE-CONTENT", output + errors)

    def test_json_documents_and_nested_fields_have_strict_types(self):
        for name in (CONFIG, PLAN):
            for body in (b"[]", b"null", b"1", b'"text"'):
                with self.subTest(name=name, body=body), self.case():
                    (self.packet / name).write_bytes(body)
                    self.rejected("required keys")
        for change, message in (
            (lambda d: d["productionControl"].update(deploymentAuthorized=0), "incorrect type"),
            (lambda d: d["installationProposal"].update(confirmed=0), "incorrect type"),
            (lambda d: d["acceptedEnvelope"].update(rpoSeconds=True), "incorrect type"),
            (lambda d: d["sourceSnapshots"].update(Folders=[]), "required keys"),
            (lambda d: d.update(moduleConfiguration=[]), "required keys"),
            (lambda d: d.update(dependencies=[]), "required keys"),
            (lambda d: d.update(admissionPrerequisites=[]), "required keys"),
        ):
            with self.subTest(message=message), self.case():
                self.edit(CONFIG, change)
                self.rejected(message)

    def test_missing_packet_and_packet_symlinks_are_bounded(self):
        for name in (CONFIG, PLAN):
            for symlink in (False, True):
                with self.subTest(name=name, symlink=symlink), self.case():
                    path = self.packet / name
                    path.unlink()
                    if symlink:
                        path.symlink_to(self.sources / verify.SOURCES[0])
                    code, output, errors = self.cli()
                    self.assertEqual(1, code)
                    self.assertIn(name, errors)
                    self.assertNotIn(str(self.sources.parent), output + errors)

    def test_execution_approval_or_any_other_argument_is_refused_before_reads(self):
        for flag in ("--execute", "--approve", "--accept-tool", "--source-root", "--help", "PRIVATE-ARG"):
            with self.subTest(flag=flag), patch.object(verify, "validate_packet") as validate, \
                    patch.object(verify.sys, "argv", ["verify.py", flag]), \
                    contextlib.redirect_stderr(io.StringIO()) as errors:
                self.assertEqual(2, verify.main())
                validate.assert_not_called()
                self.assertIn("no arguments", errors.getvalue())
                self.assertNotIn(flag, errors.getvalue())

    def test_verifier_has_no_subprocess_or_network_imports(self):
        tree = ast.parse((DIRECTORY / "verify.py").read_text(encoding="utf-8"))
        imports = set()
        for node in ast.walk(tree):
            if isinstance(node, ast.Import):
                imports.update(alias.name.split(".")[0] for alias in node.names)
            elif isinstance(node, ast.ImportFrom):
                imports.add(node.module.split(".")[0])
        self.assertLessEqual(imports, {"hashlib", "json", "pathlib", "re", "sys", "xml"})

    def test_case_restores_packet_and_sources_even_after_failure(self):
        with self.assertRaisesRegex(AssertionError, "intentional isolation probe"):
            with self.case():
                self.edit(CONFIG, lambda d: d.update(qualification="PASS"))
                (self.sources / verify.SOURCES[0]).unlink()
                raise AssertionError("intentional isolation probe")
        self.assertEqual(self.packet_bodies[CONFIG], (self.packet / CONFIG).read_bytes())
        for relative, body in self.source_bodies.items():
            self.assertEqual(body, (self.sources / relative).read_bytes())
        self.validate()


class CurrentSourceBindingTests(unittest.TestCase):
    """Real checkout binding is a separate gate from synthetic safety mutations."""

    def test_current_proposal_binds_all_25_real_sources(self):
        config = json.loads((DIRECTORY / CONFIG).read_bytes())
        self.assertEqual(25, len(verify.SOURCES))
        self.assertEqual(set(verify.SOURCES), set(config["sourceInputs"]))
        for relative in verify.SOURCES:
            with self.subTest(relative=relative):
                self.assertEqual(config["sourceInputs"][relative],
                                 hashlib.sha256((verify.WORKSPACE / relative).read_bytes()).hexdigest())
        verify.validate_packet()

    def test_current_binding_refuses_missing_or_changed_sources_in_disposable_copy(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            config = json.loads((DIRECTORY / CONFIG).read_bytes())
            for relative in verify.SOURCES:
                path = root / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes((verify.WORKSPACE / relative).read_bytes())
            verify.validate_packet(DIRECTORY, root)
            for relative in verify.SOURCES:
                path = root / relative
                original = path.read_bytes()
                for missing in (False, True):
                    with self.subTest(relative=relative, missing=missing):
                        try:
                            if missing:
                                path.unlink()
                            else:
                                path.write_bytes(original + b"\nchanged\n")
                            with self.assertRaises(verify.PacketError) as raised:
                                verify.validate_packet(DIRECTORY, root)
                            self.assertEqual(("source unavailable: " if missing else "source changed: ")
                                             + relative, str(raised.exception))
                            self.assertNotIn(str(root), str(raised.exception))
                        finally:
                            path.write_bytes(original)
            self.assertEqual(25, len(config["sourceInputs"]))


if __name__ == "__main__":
    unittest.main()
