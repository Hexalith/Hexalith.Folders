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
        packages["Hexalith.Folders.Client"] = "HexalithFoldersVersion"
        self.source_bodies[CATALOG] = (
            "<Project><PropertyGroup><HexalithVersionsLoaded>true</HexalithVersionsLoaded>"
            + "".join(f'<{key} Condition="\'$(' + key + f')\' == \'\'">{value}</{key}>' for key, value in properties.items())
            + "</PropertyGroup><ItemGroup>"
            + "".join(f'<PackageVersion Include="{name}" Version="$({prop})" />'
                      for name, prop in packages.items())
            + "</ItemGroup></Project>"
        ).encode()
        for owner, (relative, overrides) in verify.OWNER_CATALOGS.items():
            self.source_bodies[relative] = (
                "<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally><CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>"
                + "".join(f"<{key}>{value}</{key}>" for key, value in verify.IMPORT_PATHS.items())
                + "".join(f"<{key}>{value}</{key}>" for key, value in overrides.items())
                + "</PropertyGroup>"
                + "".join('<Import Project="' + row["Project"] + '" Condition="' + row["Condition"] + '" />'
                          for row in verify.IMPORTS)
                + "</Project>"
            ).encode()
        for relative in verify.BUILD_PROPS:
            self.source_bodies[relative] = b"<Project />"
        self.source_bodies[verify.TOOL_MANIFEST] = json.dumps({"version": 1, "isRoot": True, "tools": {
            "hexalith.builds.module.cli": {"version": "4.35.2", "commands": ["hexalith-module"], "rollForward": False}}}).encode()
        self.source_bodies[verify.TOOL_RANGE] = json.dumps({"toolVersionRange": "[4.35.2, 5.0.0-0)"}).encode()
        self.source_bodies[verify.TOOL_RECEIPT] = json.dumps({"version": "4.35.2", "rollForward": False,
            "supportedToolRange": "[4.35.2, 5.0.0-0)",
            "buildsSourceCommit": "4248b8c70cb35ba79fdd7e4dd63049d4c73211a0",
            "buildsReleaseTag": "v4.35.2",
            "platformRecordedBuildsGitlink": "4248b8c70cb35ba79fdd7e4dd63049d4c73211a0",
            "platformToolManifestSha256": hashlib.sha256(self.source_bodies[verify.TOOL_MANIFEST]).hexdigest(),
            "restoredVersion": "4.35.2+4248b8c70cb35ba79fdd7e4dd63049d4c73211a0",
            "ciRunId": 39736790198, "ciConclusion": "success",
            "releaseRunId": 39741376261, "releaseConclusion": "success",
            "packageHashes": verify.TOOL_PACKAGE_HASHES,
            "freshNuGetOnlyAcceptance": {"emptyCliHomeAndPackageCache": True, "commands": 4,
                "unchanged": True, "restoredPackageSha256": verify.TOOL_PACKAGE_HASHES["Hexalith.Builds.Module.Cli"]["publishedSignedArchiveSha256"]},
            "platformKitTests": {"class": "Hexalith.Platform.Composition.Tests.DomainEnrollmentKitTests", "total": 5, "failed": 0},
            "publishedBinaryRealModuleAcceptance": [{"exitCode": 1, "unchanged": True}],
            "runtimeInputsUnchanged": False,
            "humanSm2": "Not performed; owner-led demonstration remains unverified."}).encode()
        self.source_bodies[verify.PLATFORM_TRACKER] = (
            "  1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagno: done\n"
            "  1-9-ratify-and-pin-the-first-platform-accepted-tool-version: done\n").encode()
        delegated = verify.DELEGATED_OPERATIONS[0]
        for field, body in {
            "source": "VerifiedQueryCaller.Get FolderOwnerReadQuery.QueryType client().InvokeAsync",
            "transportSource": delegated["gatewayPath"] + " BoundOwnerQueryProof.Issue OriginatingAssertion",
            "queryContractSource": '"' + delegated["queryType"] + '"',
            "ownerHandlerSource": 'AuthorizeCurrentMetadataReadAsync "delegated-owner-query"',
        }.items():
            self.source_bodies[delegated[field]] = body.encode()
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

    def test_post_12_1_settings_require_exact_defaults_and_unresolved_credentials(self):
        cases = (
            ("folders-server", "EventStore:Gateway:BaseAddress", "http://unreviewed-gateway"),
            ("folders-server", "Authentication:HumanAuthorityReceipt:ExpectedWorkload", "projects"),
            ("folders-server", "Authentication:HumanAuthorityReceipt:ExpectedWorkload", ""),
            ("folders-server", "Authentication:WorkloadJwtBearer:Authority", "unaccepted-authority"),
            ("folders-server", "Authentication:WorkloadJwtBearer:Issuer", "unaccepted-issuer"),
            ("eventstore-inputs", "Authentication:WorkloadIssuer:EnableResourceBindingRequests", True),
            ("eventstore-inputs", "Authentication:WorkloadIssuer:EnableResourceBindingRequests", 0),
        )
        for module, key, value in cases:
            with self.subTest(module=module, key=key, value=value), self.case():
                self.edit(CONFIG, lambda d: d["moduleConfiguration"][module]["values"].update({key: value}))
                self.rejected(key)
        for module, key, _ in cases:
            with self.subTest(missing=key), self.case():
                self.edit(CONFIG, lambda d: d["moduleConfiguration"][module]["values"].pop(key))
                self.rejected("required keys differ")

    def test_authentication_semantics_cannot_claim_fallback_delegation_or_enrollment(self):
        mutations = (
            lambda a: a["gatewaySubmission"].update(requiresAuthenticatedPrincipal=False),
            lambda a: a["gatewaySubmission"].update(allowedSourceContexts=["any-public-read"]),
            lambda a: a["gatewaySubmission"].update(requiresNonemptyBearer=False),
            lambda a: a["gatewaySubmission"].update(target="All forwarding issues a command receipt"),
            lambda a: a["gatewaySubmission"].update(source=verify.SOURCES[0]),
            lambda a: a["gatewaySubmission"].update(generalDelegationEstablished=True),
            lambda a: a["workloadPolicies"].remove("DomainServiceQuery"),
            lambda a: a["workloadPolicies"].remove("DomainServiceMetadata"),
            lambda a: a["workloadJwtContract"].update(fallbackOnlyWhenDedicatedSectionAbsent=False),
            lambda a: a["workloadJwtContract"].update(partialDedicatedContractFailsClosed=False),
            lambda a: a["workloadJwtContract"].update(section="Authentication:JwtBearer"),
            lambda a: a["humanReceipt"].update(defaultExpectedWorkload="projects"),
            lambda a: a["humanReceipt"].update(requiresAuthenticatedMatchingWorkload=False),
            lambda a: a["humanReceipt"].update(requiresCommandBoundSignedReceipt=False),
            lambda a: a["resourceBindingRequests"].update(default=True),
            lambda a: a["resourceBindingRequests"].update(enablingCreatesGrant=True),
            lambda a: a["resourceBindingRequests"].update(authorityEnrollmentEvidence="invented-enrollment"),
        )
        for index, change in enumerate(mutations):
            with self.subTest(index=index), self.case():
                self.edit(CONFIG, lambda d: change(d["authenticationRequirements"]))
                self.rejected("configuration requirements: reviewed content changed")

    def test_complete_dedicated_jwt_inventory_refuses_unreviewed_or_weakened_values(self):
        for suffix, value in (
            ("Audience", "folders"), ("ValidAudiences", ["folders"]),
            ("ValidAudiences", []), ("AllowedAlgorithms", ["RS256"]),
            ("AllowedAlgorithms", ["HS256"]), ("AllowedAlgorithms", []),
            ("SigningKey", "PRIVATE-UNREVIEWED-KEY"),
            ("RequireHttpsMetadata", False), ("RequireHttpsMetadata", 1),
            ("AllowInsecureSymmetricKey", True), ("AllowInsecureSymmetricKey", 0),
        ):
            key = "Authentication:WorkloadJwtBearer:" + suffix
            with self.subTest(key=key, value=value), self.case():
                self.edit(CONFIG, lambda d: d["moduleConfiguration"]["folders-server"]["values"].update({key: value}))
                code, output, errors = self.cli()
                self.assertEqual(1, code)
                self.assertIn(key, errors)
                self.assertNotIn("PRIVATE-UNREVIEWED-KEY", output + errors)
        for suffix in ("Audience", "ValidAudiences", "AllowedAlgorithms", "SigningKey",
                       "RequireHttpsMetadata", "AllowInsecureSymmetricKey"):
            with self.subTest(missing=suffix), self.case():
                self.edit(CONFIG, lambda d: d["moduleConfiguration"]["folders-server"]["values"].pop(
                    "Authentication:WorkloadJwtBearer:" + suffix))
                self.rejected("required keys differ")

    def test_cross_operation_and_resource_binding_negatives_cannot_be_removed(self):
        plan = json.loads(self.packet_bodies[PLAN])
        for index in (-2, -1):
            self.assertIn("1200-second" if index == -2 else "resource-binding", plan["checks"][5]["procedure"][index])
            with self.subTest(index=index), self.case():
                self.edit(PLAN, lambda d: d["checks"][5]["procedure"].pop(index))
                self.rejected("staging safeguards: reviewed content changed")

    def test_catalog_mismatch_stays_an_observation_without_acceptance(self):
        for field, value in (
            ("directBuildsRevision", "0" * 40),
            ("platformBuildsGitlinkRevision", verify.SNAPSHOTS["Builds"]),
            ("currentCatalogContentMatches", False),
            ("acceptedToolCatalogContentMatchesCurrent", True),
            ("recordedToolPin", "4.32.0"),
            ("matches", True), ("acceptanceEvidence", "invented-acceptance"),
            ("platformStartupStoryStatus", "backlog"), ("platformToolStoryStatus", "backlog"),
        ):
            with self.subTest(field=field), self.case():
                self.edit(CONFIG, lambda d: d["catalogCoherenceObservation"].update({field: value}))
                self.rejected("configuration requirements: reviewed content changed")

    def test_module_boundaries_cannot_weaken_authentication_requirements(self):
        for module in ("folders-server", "eventstore-inputs"):
            with self.subTest(module=module), self.case():
                self.edit(CONFIG, lambda d: d["moduleConfiguration"][module].update(
                    boundary="Any bearer or resource-binding flag grants full downstream authority."))
                self.rejected("configuration requirements: reviewed content changed")

    def test_new_bearer_source_binding_cannot_be_omitted_or_substituted(self):
        relative = "src/Hexalith.Folders.Server/Authentication/FoldersGatewayHumanBearerHandler.cs"
        self.assertEqual(relative, verify.SOURCES[25])
        for missing in (False, True):
            with self.subTest(missing=missing), self.case():
                if missing:
                    self.edit(CONFIG, lambda d: d["sourceInputs"].pop(relative))
                    self.rejected("sourceInputs: required keys differ")
                else:
                    sentinel = b"PRIVATE-SOURCE-CONTENT-MUST-NOT-BE-DISCLOSED"
                    (self.sources / relative).write_bytes(sentinel)
                    code, output, errors = self.cli()
                    self.assertEqual(1, code)
                    self.assertIn("source changed: " + relative, errors)
                    self.assertNotIn(sentinel.decode(), output + errors)
                    self.assertNotIn(str(self.sources), output + errors)

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
            (original.replace(b"3.127.0", b"3.126.1"), "observed catalog selection"),
            (original.replace(b'Version="$(HexalithEventStoreVersion)"', b'Version="3.127.0"'),
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

    def replace_source(self, relative, body):
        """Rebind fixtures to reach structural checks and lexical anchors independently."""
        (self.sources / relative).write_bytes(body)
        self.edit(CONFIG, lambda d: d["sourceInputs"].update({relative: hashlib.sha256(body).hexdigest()}))

    def test_nearer_consumer_catalog_appearance_is_refused_without_reading_or_hash_changes(self):
        for relative in verify.NEARER_CATALOGS:
            with self.subTest(relative=relative), self.case():
                original_config = (self.packet / CONFIG).read_bytes()
                alternate = self.sources / relative
                alternate.parent.mkdir(parents=True, exist_ok=True)
                alternate.write_bytes(b"PRIVATE-NEARER-CATALOG")
                original_read = Path.read_bytes
                def bounded_read(candidate):
                    self.assertNotEqual(alternate, candidate, "nearer unreviewed catalog must never be read")
                    return original_read(candidate)
                try:
                    with patch.object(Path, "read_bytes", bounded_read):
                        code, output, errors = self.cli()
                    self.assertEqual(1, code)
                    self.assertIn("nearer consumer catalog substitution refused: " + relative, errors)
                    self.assertNotIn("PRIVATE-NEARER-CATALOG", output + errors)
                    self.assertEqual(original_config, (self.packet / CONFIG).read_bytes())
                finally:
                    alternate.unlink()

    def test_build_props_version_and_package_overrides_are_refused_after_rebinding(self):
        for relative in verify.BUILD_PROPS:
            for body in (
                b"<Project><PropertyGroup><HexalithFoldersVersion>99.0.0</HexalithFoldersVersion></PropertyGroup></Project>",
                b"<Project><PropertyGroup><HexalithEventStoreVersion>99.0.0</HexalithEventStoreVersion></PropertyGroup></Project>",
                b'<Project><ItemGroup><PackageVersion Update="Hexalith.Folders.Client" Version="99.0.0" /></ItemGroup></Project>',
                b'<Project><ItemGroup><PackageReference Update="Hexalith.Folders.Client"><VersionOverride>99.0.0</VersionOverride></PackageReference></ItemGroup></Project>',
                b'<Project><Import Project="unreviewed.props" /></Project>',
                b'<Project><Choose><When Condition="true" /></Choose></Project>',
            ):
                with self.subTest(relative=relative, body=body), self.case():
                    self.replace_source(relative, body)
                    self.rejected("consumer build props: .*refused")

    def test_catalog_rejects_updates_removals_indirection_excludes_and_child_metadata(self):
        original = self.source_bodies[CATALOG]
        for fragment in (
            b'<ItemGroup><PackageVersion Update="Hexalith.Folders.Client" Version="99.0.0" /></ItemGroup>',
            b'<ItemGroup><PackageVersion Remove="Hexalith.Folders.Client" /></ItemGroup>',
            b'<Import Project="unreviewed.props" />',
            b'<Choose><When Condition="true"><PropertyGroup><HexalithFoldersVersion>99.0.0</HexalithFoldersVersion></PropertyGroup></When></Choose>',
        ):
            with self.subTest(fragment=fragment), self.case():
                self.replace_source(CATALOG, original.replace(b"</Project>", fragment + b"</Project>"))
                self.rejected("Builds catalog: unsupported")
        for package in ("Hexalith.Folders.Client", "Hexalith.Folders.Contracts"):
            declaration = ('<PackageVersion Include="' + package + '" Version="$(HexalithFoldersVersion)" />').encode()
            for replacement in (declaration.replace(b" />", b' Exclude="' + package.encode() + b'" />'),
                                declaration.replace(b" />", b"><Version>99.0.0</Version></PackageVersion>")):
                with self.subTest(package=package, replacement=replacement), self.case():
                    self.replace_source(CATALOG, original.replace(declaration, replacement))
                    self.rejected("Builds catalog: unsupported package construct")

    def test_catalog_requires_true_unconditional_unique_import_sentinel(self):
        original = self.source_bodies[CATALOG]
        sentinel = b"<HexalithVersionsLoaded>true</HexalithVersionsLoaded>"
        for replacement in (b"", sentinel.replace(b"true", b"false"),
                            sentinel.replace(b"<HexalithVersionsLoaded>", b'<HexalithVersionsLoaded Condition="false">'),
                            sentinel + sentinel):
            with self.subTest(replacement=replacement), self.case():
                self.replace_source(CATALOG, original.replace(sentinel, replacement))
                self.rejected("unconditional HexalithVersionsLoaded=true required")

    def test_owning_import_paths_and_overrides_must_precede_first_import(self):
        for owner, (relative, overrides) in verify.OWNER_CATALOGS.items():
            for key, value in dict(verify.IMPORT_PATHS, **overrides).items():
                declaration = f"<{key}>{value}</{key}>".encode()
                original = self.source_bodies[relative]
                shifted = original.replace(declaration, b"").replace(
                    b"</Project>", b"<PropertyGroup>" + declaration + b"</PropertyGroup></Project>")
                with self.subTest(owner=owner, key=key), self.case():
                    self.replace_source(relative, shifted)
                    self.rejected("owning declaration order changed")

    def test_every_direct_operation_refuses_source_and_transport_substitution(self):
        for index in range(3):
            for field, value in (("source", verify.SOURCES[0]), ("transport", "delegated-owner-query")):
                with self.subTest(index=index, field=field), self.case():
                    self.edit(CONFIG, lambda d: d["projectsReadOnlyOperations"][index].update({field: value}))
                    self.rejected(r"Projects operation .*\." + field)

    def test_delegated_lexical_anchors_do_not_claim_csharp_control_flow_proof(self):
        # A comment-only fixture satisfies lexical anchors. Exact reviewed live
        # source fingerprints and separately qualified behavior remain essential.
        row = verify.DELEGATED_OPERATIONS[0]
        for field in ("source", "transportSource", "queryContractSource", "ownerHandlerSource"):
            self.replace_source(row[field], b"// " + self.source_bodies[row[field]])
        self.validate()

    def test_delegated_consuming_configuration_keeps_exact_defaults_and_unresolved_bindings(self):
        name = "projects-delegated-owner-query"
        for field, value in (("consumingHost", "folders-server"),
                             ("environmentBindings", "invented-environment"), ("secretBindings", "invented-secret")):
            with self.subTest(field=field), self.case():
                self.edit(CONFIG, lambda d: d["moduleConfiguration"][name].update({field: value}))
                self.rejected("Projects delegated")
        for key in verify.MODULE_KEYS[name]:
            for missing in (False, True):
                with self.subTest(key=key, missing=missing), self.case():
                    self.edit(CONFIG, lambda d: d["moduleConfiguration"][name]["values"].pop(key)
                              if missing else d["moduleConfiguration"][name]["values"].update({key: "UNQUALIFIED-PRIVATE-BINDING"}))
                    code, output, errors = self.cli()
                    self.assertEqual(1, code)
                    self.assertNotIn("UNQUALIFIED-PRIVATE-BINDING", output + errors)

    def test_recorded_tool_receipt_requires_acceptance_conclusions_and_manifest_package_bindings(self):
        baseline = json.loads(self.source_bodies[verify.TOOL_RECEIPT])
        paths = [
            ("ciConclusion",), ("releaseConclusion",), ("ciRunId",), ("releaseRunId",),
            ("rollForward",), ("buildsReleaseTag",), ("platformRecordedBuildsGitlink",),
            ("platformToolManifestSha256",), ("restoredVersion",),
            ("freshNuGetOnlyAcceptance", "emptyCliHomeAndPackageCache"),
            ("freshNuGetOnlyAcceptance", "commands"), ("freshNuGetOnlyAcceptance", "unchanged"),
            ("freshNuGetOnlyAcceptance", "restoredPackageSha256"),
            ("platformKitTests", "class"), ("platformKitTests", "total"), ("platformKitTests", "failed"),
        ]
        for package, hashes in verify.TOOL_PACKAGE_HASHES.items():
            paths.extend(("packageHashes", package, field) for field in hashes)
        for path in paths:
            for missing in (False, True):
                with self.subTest(path=path, missing=missing), self.case():
                    data = json.loads(json.dumps(baseline))
                    parent = data
                    for key in path[:-1]:
                        parent = parent[key]
                    if missing:
                        parent.pop(path[-1])
                    else:
                        parent[path[-1]] = "failed-or-substituted"
                    self.replace_source(verify.TOOL_RECEIPT, json.dumps(data).encode())
                    self.rejected("recorded tool acceptance")
        # Expected diagnostic rejections and an unperformed human SM-2 stay
        # limitations; they do not negate the recorded tool-release acceptance.
        self.validate()

    def test_tool_manifest_command_binding_cannot_be_substituted_after_rebinding(self):
        data = json.loads(self.source_bodies[verify.TOOL_MANIFEST])
        data["tools"]["hexalith.builds.module.cli"]["commands"] = ["another-command"]
        self.replace_source(verify.TOOL_MANIFEST, json.dumps(data).encode())
        self.rejected("recorded tool command binding")

    def test_attempt_lock_is_a_sealed_entry_precondition_of_branch_selection(self):
        plan = json.loads(self.packet_bodies[PLAN])
        self.assertIn("already qualified", plan["checks"][1]["preconditions"][-1])
        self.assertIn("acquired", plan["checks"][1]["preconditions"][-1])
        self.edit(PLAN, lambda d: d["checks"][1]["preconditions"].pop())
        self.rejected("staging safeguards")

    def test_effective_catalog_proposal_cannot_hide_override_or_owner_substitution(self):
        for owner in verify.OWNER_CATALOGS:
            for field, value in (("propertyOverrides", {"HexalithFoldersVersion": "99.0.0"}),
                                 ("packageOverrides", {"Hexalith.Folders.Client": "99.0.0"}),
                                 ("owningCatalog", CATALOG),
                                 ("importedCatalog", "unreviewed.props"),
                                 ("foldersClientContractsSelection", "1.2.1")):
                with self.subTest(owner=owner, field=field), self.case():
                    self.edit(CONFIG, lambda d: d["effectiveCatalogSelections"][owner].update({field: value}))
                    self.rejected("effective catalog " + owner)

    def test_effective_catalog_source_changes_fail_even_with_matching_fingerprints(self):
        for owner, (relative, _) in verify.OWNER_CATALOGS.items():
            original = self.source_bodies[relative]
            for body, message in (
                (original.replace(b"</PropertyGroup>", b"<HexalithFoldersVersion>1.2.1</HexalithFoldersVersion></PropertyGroup>"), "reviewed local properties|unsupported or duplicate local property|observed overrides"),
                (original.replace(b"</Project>", b'<ItemGroup><PackageVersion Update="Hexalith.Folders.Client" Version="1.2.1" /></ItemGroup></Project>'), "unsupported local catalog construct"),
                (original.replace(b"../Hexalith.Builds/", b"../Other.Builds/"), "reviewed local properties|import path changed"),
                (original.replace(b"$(Hexalith1BuildPackageProps)", b"$(Hexalith3BuildPackageProps)", 1), "guarded imports"),
                (original.replace(b" And ", b" Or "), "guarded imports"),
            ):
                with self.subTest(owner=owner, message=message), self.case():
                    self.replace_source(relative, body)
                    self.rejected(message)
        relative = verify.OWNER_CATALOGS["Folders"][0]
        original = self.source_bodies[relative]
        for body in (original.replace(b"<HexalithEventStoreVersion>3.127.0</HexalithEventStoreVersion>", b""),
                     original.replace(b"<HexalithEventStoreVersion>3.127.0", b"<HexalithEventStoreVersion>3.126.1")):
            with self.subTest(folders_override=body), self.case():
                self.replace_source(relative, body)
                self.rejected("reviewed local properties|unsupported or duplicate local property|observed overrides")

    def test_nested_catalog_appearance_is_refused_before_reading_its_contents(self):
        alternate = self.sources / "references/Hexalith.Projects/references/Hexalith.Builds/Props/Directory.Packages.props"
        alternate.parent.mkdir(parents=True)
        alternate.write_bytes(b"PRIVATE-ALTERNATE-CATALOG")
        original_read = Path.read_bytes
        def bounded_read(candidate):
            self.assertNotEqual(alternate, candidate, "unreviewed alternate catalog must never be read")
            return original_read(candidate)
        with patch.object(Path, "read_bytes", bounded_read):
            code, output, errors = self.cli()
        self.assertEqual(1, code)
        self.assertIn("earlier nested catalog substitution refused", errors)
        self.assertNotIn("PRIVATE-ALTERNATE-CATALOG", output + errors)

    def test_conditional_duplicate_and_import_suppression_properties_are_refused(self):
        for owner, (relative, _) in verify.OWNER_CATALOGS.items():
            original = self.source_bodies[relative]
            for body in (
                original.replace(b"<PropertyGroup>", b'<PropertyGroup Condition="false">'),
                original.replace(b"</PropertyGroup>", b"<HexalithVersionsLoaded>true</HexalithVersionsLoaded></PropertyGroup>"),
                original.replace(b"</PropertyGroup>", b"<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup>"),
            ):
                with self.subTest(owner=owner, body=body), self.case():
                    self.replace_source(relative, body)
                    self.rejected("conditional property group refused|unsupported or duplicate local property")

    def test_catalog_enclosing_conditions_cannot_hide_effective_default_or_package_pair(self):
        original = self.source_bodies[CATALOG]
        for body, message in (
            (original.replace(b"<PropertyGroup>", b'<PropertyGroup Condition="false">'), "unsupported or conditional catalog group"),
            (original.replace(b"<ItemGroup>", b'<ItemGroup Condition="false">'), "unsupported or conditional catalog group"),
            (original.replace(b'Include="Hexalith.Folders.Contracts"', b'Include="Hexalith.Folders.Contracts" Condition="false"'), "unconditional catalog package binding"),
        ):
            with self.subTest(message=message), self.case():
                self.replace_source(CATALOG, body)
                self.rejected(message)

    def test_effective_client_contracts_pair_requires_shared_catalog_property(self):
        for package in ("Hexalith.Folders.Client", "Hexalith.Folders.Contracts"):
            with self.subTest(package=package), self.case():
                original = self.source_bodies[CATALOG]
                old = ('Include="' + package + '" Version="$(HexalithFoldersVersion)"').encode()
                new = ('Include="' + package + '" Version="1.3.1"').encode()
                self.replace_source(CATALOG, original.replace(old, new))
                self.rejected("catalog package binding|Client exact package binding|Client/Contracts pair binding changed")

    def test_delegated_operation_declaration_cannot_be_removed_replaced_or_made_mutating(self):
        for field, value in (("operation", "InvokeAny"), ("queryType", "UnreviewedQuery"),
                             ("gatewayPath", "/commands"), ("ownerPath", "/admin"),
                             ("ownerDomain", "projects"), ("method", "DELETE"), ("mutates", True),
                             ("source", verify.SOURCES[0]), ("transportSource", verify.SOURCES[0]),
                             ("queryContractSource", verify.SOURCES[0]), ("ownerHandlerSource", verify.SOURCES[0]),
                             ("selection", "Every caller can use delegated authority"),
                             ("outcomes", "Failures are successful"), ("hostedQualification", "accepted")):
            with self.subTest(field=field), self.case():
                self.edit(CONFIG, lambda d: d["projectsDelegatedReadOperations"][0].update({field: value}))
                self.rejected("Projects delegated operation")
        for value in ([], verify.DELEGATED_OPERATIONS * 2):
            with self.subTest(value=value), self.case():
                self.edit(CONFIG, lambda d: d.update(projectsDelegatedReadOperations=value))
                self.rejected("Projects delegated operation")

    def test_delegated_lexical_anchors_are_checked_independently_of_fingerprints(self):
        row = verify.DELEGATED_OPERATIONS[0]
        for field in ("source", "transportSource", "queryContractSource", "ownerHandlerSource"):
            with self.subTest(field=field), self.case():
                self.replace_source(row[field], b"An unrelated read-only implementation")
                self.rejected("Projects delegated source declaration changed")

    def test_recorded_tool_and_story_inputs_cannot_fabricate_current_acceptance(self):
        for relative in (verify.TOOL_MANIFEST, verify.TOOL_RANGE, verify.TOOL_RECEIPT):
            with self.subTest(relative=relative), self.case():
                self.replace_source(relative, self.source_bodies[relative].replace(b"4.35.2", b"4.32.0"))
                self.rejected("recorded tool")
        with self.case():
            self.replace_source(verify.PLATFORM_TRACKER, self.source_bodies[verify.PLATFORM_TRACKER].replace(b"done", b"backlog"))
            self.rejected("recorded Platform story status changed")
        with self.case():
            self.edit(CONFIG, lambda d: d["catalogCoherenceObservation"].update(acceptanceEvidence="hosted-qualified"))
            self.rejected("configuration requirements")

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

    def test_current_proposal_binds_all_52_real_sources(self):
        config = json.loads((DIRECTORY / CONFIG).read_bytes())
        self.assertEqual(52, len(verify.SOURCES))
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
            self.assertEqual(52, len(config["sourceInputs"]))


if __name__ == "__main__":
    unittest.main()
