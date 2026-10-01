from __future__ import annotations

import json
import os
import pathlib
import shutil
import stat
import subprocess
import sys
import tempfile
import unittest
import zipfile


ROOT = pathlib.Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "scripts"
sys.path.insert(0, str(SCRIPTS))

from release_package_contract import (  # noqa: E402
    PackageMetadata,
    _validate_archive_paths,
    read_symbol_metadata,
    validate_internal_dependencies,
)


class CoverageValidatorTests(unittest.TestCase):
    def write_coverage(self, directory: pathlib.Path, filename: str = "src/One/A.cs") -> pathlib.Path:
        report = directory / "coverage.cobertura.xml"
        report.write_text(
            f"""<coverage><packages><package><classes><class filename="{filename}"><lines>
<line number="1" hits="1"/><line number="2" hits="0"/>
</lines></class></classes></package></packages></coverage>""",
            encoding="utf-8",
        )
        return report

    def run_validator(self, directory: pathlib.Path, *arguments: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                sys.executable,
                str(SCRIPTS / "validate-coverage.py"),
                "--coverage-root",
                str(directory),
                *arguments,
            ],
            cwd=ROOT,
            text=True,
            capture_output=True,
            check=False,
        )

    def test_accepts_coverage_equal_to_minimum(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = pathlib.Path(temporary)
            self.write_coverage(root)
            result = self.run_validator(
                root,
                "--minimum-line-coverage",
                "50",
                "--required-branch-coverage",
                "0",
                "--line-scope",
                "src/One/",
            )
            self.assertEqual(0, result.returncode, result.stderr)

    def test_rejects_each_missing_declared_scope(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = pathlib.Path(temporary)
            self.write_coverage(root)
            result = self.run_validator(
                root,
                "--minimum-line-coverage",
                "0",
                "--required-branch-coverage",
                "0",
                "--line-scope",
                "src/One/",
                "--line-scope",
                "src/Two/",
            )
            self.assertNotEqual(0, result.returncode)
            self.assertIn("coverage scope(s) not found: src/Two/", result.stderr)

    def test_rejects_positive_branch_threshold_without_branch_records(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = pathlib.Path(temporary)
            self.write_coverage(root)
            result = self.run_validator(
                root,
                "--minimum-line-coverage",
                "0",
                "--required-branch-coverage",
                "1",
                "--line-scope",
                "src/One/",
                "--isolation-auth-target",
                "src/One/A.cs",
            )
            self.assertNotEqual(0, result.returncode)
            self.assertIn("requires parseable branch records", result.stderr)

    def test_rejects_nonfinite_or_out_of_range_thresholds(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = pathlib.Path(temporary)
            self.write_coverage(root)
            for value in ("nan", "-1", "101"):
                with self.subTest(value=value):
                    result = self.run_validator(
                        root,
                        "--minimum-line-coverage",
                        value,
                        "--required-branch-coverage",
                        "0",
                        "--line-scope",
                        "src/One/",
                    )
                    self.assertNotEqual(0, result.returncode)
                    self.assertIn("finite percentage", result.stderr)


class ReleasePackageContractTests(unittest.TestCase):
    def test_rejects_unsafe_archive_entries(self) -> None:
        unsafe_names = ["../payload", "/absolute", "C:/payload", "folder\\payload", "src/project.csproj"]
        with tempfile.TemporaryDirectory() as temporary:
            root = pathlib.Path(temporary)
            for index, unsafe_name in enumerate(unsafe_names):
                with self.subTest(name=unsafe_name):
                    archive_path = root / f"unsafe-{index}.nupkg"
                    with zipfile.ZipFile(archive_path, "w") as archive:
                        archive.writestr(unsafe_name, b"payload")
                    with zipfile.ZipFile(archive_path, "r") as archive:
                        with self.assertRaisesRegex(ValueError, "unsafe archive path"):
                            _validate_archive_paths(archive, archive_path)

    def test_rejects_corrupt_archive_member(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            archive_path = pathlib.Path(temporary) / "corrupt.nupkg"
            with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_STORED) as archive:
                archive.writestr("safe.bin", b"payload")
            with zipfile.ZipFile(archive_path, "r") as archive:
                info = archive.getinfo("safe.bin")
                header_offset = info.header_offset
            content = bytearray(archive_path.read_bytes())
            name_length = int.from_bytes(content[header_offset + 26 : header_offset + 28], "little")
            extra_length = int.from_bytes(content[header_offset + 28 : header_offset + 30], "little")
            data_offset = header_offset + 30 + name_length + extra_length
            content[data_offset] ^= 0xFF
            archive_path.write_bytes(content)
            with zipfile.ZipFile(archive_path, "r") as archive:
                with self.assertRaises((ValueError, zipfile.BadZipFile)):
                    _validate_archive_paths(archive, archive_path)

    def test_rejects_symbol_archive_without_pdb(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            archive_path = pathlib.Path(temporary) / "Package.1.2.3.snupkg"
            nuspec = """<package><metadata><id>Package</id><version>1.2.3</version>
<projectUrl>https://github.com/Hexalith/Hexalith.Folders</projectUrl>
<packageTypes><packageType name="SymbolsPackage"/></packageTypes>
<repository url="https://github.com/Hexalith/Hexalith.Folders" commit="0123456789abcdef0123456789abcdef01234567"/>
</metadata></package>"""
            with zipfile.ZipFile(archive_path, "w") as archive:
                archive.writestr("Package.nuspec", nuspec)
            with self.assertRaisesRegex(ValueError, "nonempty portable PDB"):
                read_symbol_metadata(archive_path, "0123456789abcdef0123456789abcdef01234567")

    def test_rejects_noncanonical_or_wrong_version_internal_dependencies(self) -> None:
        manifest = {"hexalith.folders": "Hexalith.Folders"}
        lowercase = PackageMetadata("Consumer", "1.2.3", (("hexalith.folders", "1.2.3"),))
        with self.assertRaisesRegex(ValueError, "noncanonical"):
            validate_internal_dependencies(lowercase, manifest, "1.2.3")
        wrong_version = PackageMetadata("Consumer", "1.2.3", (("Hexalith.Folders", "1.2.2"),))
        with self.assertRaisesRegex(ValueError, "instead of release version"):
            validate_internal_dependencies(wrong_version, manifest, "1.2.3")

    def test_packer_rejects_repository_root_output(self) -> None:
        result = subprocess.run(
            [
                sys.executable,
                str(SCRIPTS / "pack-release-packages.py"),
                str(ROOT),
                "1.2.3",
                "--source-revision",
                "0123456789abcdef0123456789abcdef01234567",
            ],
            cwd=ROOT,
            text=True,
            capture_output=True,
            check=False,
        )
        self.assertNotEqual(0, result.returncode)
        self.assertIn("repository-owned package directory", result.stderr)


class PublicationPreflightTests(unittest.TestCase):
    def write_executable(self, path: pathlib.Path, content: str) -> None:
        path.write_text(content, encoding="utf-8")
        path.chmod(path.stat().st_mode | stat.S_IXUSR)

    def run_preflight(self, duplicate: bool) -> subprocess.CompletedProcess[str]:
        with tempfile.TemporaryDirectory() as temporary:
            bin_directory = pathlib.Path(temporary)
            self.write_executable(
                bin_directory / "gh",
                """#!/usr/bin/env python3
import sys
if any('/git/ref/heads/main' in value for value in sys.argv):
    print('0123456789abcdef0123456789abcdef01234567')
else:
    print('{"workflow_runs":[{"head_sha":"0123456789abcdef0123456789abcdef01234567","head_branch":"main","event":"push","status":"completed","conclusion":"success"}]}')
""",
            )
            self.write_executable(
                bin_directory / "curl",
                """#!/usr/bin/env python3
import json, os, pathlib, sys
arguments = sys.argv[1:]
output = pathlib.Path(arguments[arguments.index('--output') + 1])
duplicate = os.environ.get('TEST_DUPLICATE') == '1'
output.write_text(json.dumps({'versions':['1.2.3'] if duplicate else []}), encoding='utf-8')
print('200' if duplicate else '404', end='')
""",
            )
            self.write_executable(
                bin_directory / "jq",
                """#!/usr/bin/env python3
import os, sys
query = ' '.join(sys.argv[1:])
if '.packages |' in query:
    print('5')
elif '.workflow_runs' in query:
    sys.stdin.read()
elif '.packages[].id' in query:
    print('Hexalith.Folders.Contracts\\nHexalith.Folders\\nHexalith.Folders.Client\\nHexalith.Folders.Aspire\\nHexalith.Folders.Testing')
elif '.versions' in query:
    raise SystemExit(0 if os.environ.get('TEST_DUPLICATE') == '1' else 1)
else:
    raise SystemExit(2)
""",
            )
            environment = os.environ.copy()
            environment.update(
                {
                    "PATH": f"{bin_directory}{os.pathsep}{environment['PATH']}",
                    "TEST_DUPLICATE": "1" if duplicate else "0",
                    "GITHUB_SHA": "0123456789abcdef0123456789abcdef01234567",
                    "GITHUB_TOKEN": "test-token",
                    "GITHUB_REPOSITORY": "Hexalith/Hexalith.Folders",
                    "HEXALITH_BUILDS_EXECUTION_SHA": "b93e9889e9e7b67036837015b4b2b115e326c4da",
                    "HEXALITH_RELEASE_SOURCE_BRANCH": "main",
                    "HEXALITH_RELEASE_SOURCE_CI_WORKFLOW": "ci.yml",
                    "HEXALITH_RELEASE_ENVIRONMENT": "production",
                    "HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT": "5",
                    "HEXALITH_RELEASE_REQUIRE_AUTHORITY": "false",
                    "HEXALITH_RELEASE_PACKAGE_MANIFEST": "tools/release-packages.json",
                }
            )
            return subprocess.run(
                ["bash", str(SCRIPTS / "validate-publication-preflight.sh"), "1.2.3", "verify"],
                cwd=ROOT,
                env=environment,
                text=True,
                capture_output=True,
                check=False,
            )

    def test_rejects_existing_package_version(self) -> None:
        result = self.run_preflight(duplicate=True)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("already contains", result.stderr)

    def test_accepts_absent_package_versions(self) -> None:
        result = self.run_preflight(duplicate=False)
        self.assertEqual(0, result.returncode, result.stderr)


@unittest.skipUnless(shutil.which("pwsh"), "PowerShell is required to exercise the nightly drift gate")
class NightlyDriftGateTests(unittest.TestCase):
    def run_gate(
        self,
        *,
        configuration: str = "Debug",
        assembly_configuration: str | None = None,
        skip_build: bool = True,
        forgejo_count: int = 10,
        github_count: int = 5,
        skipped: int = 0,
        runner_exit: int = 0,
        forgejo_output: str | None = None,
        github_output: str | None = None,
    ) -> tuple[subprocess.CompletedProcess[str], dict, list[list[str]]]:
        with tempfile.TemporaryDirectory() as temporary:
            root = pathlib.Path(temporary)
            for relative in (
                "tests/contracts/forgejo",
                "tests/tools/forgejo-drift",
            ):
                shutil.copytree(ROOT / relative, root / relative)
            for relative in (
                "tests/tools/run-nightly-drift-gates.ps1",
                "tests/contracts/github/pinned-profile.json",
                "docs/contract/provider-compatibility-catalog.md",
                "references/Hexalith.Builds/Props/Directory.Packages.props",
                "Directory.Packages.props",
            ):
                destination = root / relative
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(ROOT / relative, destination)

            assembly = root / (
                "tests/Hexalith.Folders.Tests/bin/"
                f"{assembly_configuration or configuration}/net10.0/Hexalith.Folders.Tests.dll"
            )
            assembly.parent.mkdir(parents=True)
            assembly.touch()
            bin_directory = root / "test-bin"
            bin_directory.mkdir()
            invocation_log = root / "invocations.jsonl"
            fake_dotnet = bin_directory / "dotnet"
            fake_dotnet.write_text(
                """#!/usr/bin/env python3
import json, os, pathlib, sys
arguments = sys.argv[1:]
with pathlib.Path(os.environ['NIGHTLY_TEST_LOG']).open('a', encoding='utf-8') as log:
    log.write(json.dumps(arguments) + '\\n')
if arguments[0] in ('restore', 'build'):
    raise SystemExit(0)
if arguments[0] == 'test':
    print('Zero tests ran')
    raise SystemExit(5)
if not arguments[0].endswith('.dll') or '-class' not in arguments or '--filter' in arguments:
    raise SystemExit(99)
test_class = arguments[arguments.index('-class') + 1]
counts = json.loads(os.environ['NIGHTLY_TEST_COUNTS'])
provider = 'forgejo' if '.Forgejo.' in test_class else 'github'
count = counts[provider]
skipped = int(os.environ['NIGHTLY_TEST_SKIPPED'])
custom_output = json.loads(os.environ['NIGHTLY_TEST_OUTPUTS'])[provider]
print(custom_output if custom_output is not None else f'Hexalith.Folders.Tests Total: {count}, Errors: 0, Failed: 0, Skipped: {skipped}, Not Run: 0')
raise SystemExit(int(os.environ['NIGHTLY_TEST_EXIT']))
""",
                encoding="utf-8",
            )
            fake_dotnet.chmod(fake_dotnet.stat().st_mode | stat.S_IXUSR)
            environment = os.environ.copy()
            environment.update(
                {
                    "NIGHTLY_TEST_BIN": str(bin_directory),
                    "NIGHTLY_TEST_CONFIGURATION": configuration,
                    "NIGHTLY_TEST_SKIP_BUILD": str(skip_build).lower(),
                    "NIGHTLY_TEST_LOG": str(invocation_log),
                    "NIGHTLY_TEST_COUNTS": json.dumps({"forgejo": forgejo_count, "github": github_count}),
                    "NIGHTLY_TEST_SKIPPED": str(skipped),
                    "NIGHTLY_TEST_EXIT": str(runner_exit),
                    "NIGHTLY_TEST_OUTPUTS": json.dumps({"forgejo": forgejo_output, "github": github_output}),
                }
            )
            # Global-tool installations of pwsh launch through dotnet before the shell starts.
            # Replace PATH inside PowerShell so the stub handles only the gate's SDK calls.
            script_path = str(root / "tests/tools/run-nightly-drift-gates.ps1").replace("'", "''")
            command = [
                "pwsh", "-NoLogo", "-NoProfile", "-Command",
                "$env:PATH = $env:NIGHTLY_TEST_BIN + [IO.Path]::PathSeparator + $env:PATH; "
                "$gateArguments = @{}; "
                "if ($env:NIGHTLY_TEST_CONFIGURATION -ne 'Debug') { $gateArguments.Configuration = $env:NIGHTLY_TEST_CONFIGURATION }; "
                "if ($env:NIGHTLY_TEST_SKIP_BUILD -eq 'true') { $gateArguments.SkipRestoreBuild = $true }; "
                f"& '{script_path}' @gateArguments",
            ]
            result = subprocess.run(
                command, cwd=root, env=environment, text=True, capture_output=True, check=False,
            )
            report_path = root / "_bmad-output/gates/nightly-drift/latest.json"
            self.assertTrue(report_path.exists(), f"exit={result.returncode} {result.stdout}{result.stderr}")
            report = json.loads(report_path.read_text(encoding="utf-8"))
            calls = [json.loads(line) for line in invocation_log.read_text(encoding="utf-8").splitlines()] if invocation_log.exists() else []
            return result, report, calls

    def test_debug_and_release_use_matching_assemblies_and_dependency_modes(self) -> None:
        for configuration in ("Debug", "Release"):
            with self.subTest(configuration=configuration):
                result, report, calls = self.run_gate(configuration=configuration, skip_build=False)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertEqual("passed", report["status"])
                package_mode = "true" if configuration == "Release" else "false"
                solution = "Hexalith.Folders.CI.slnx" if configuration == "Release" else "Hexalith.Folders.slnx"
                self.assertEqual(solution, calls[0][1])
                self.assertEqual(solution, calls[1][1])
                self.assertIn(f"-p:Configuration={configuration}", calls[0])
                self.assertIn(f"-p:UseNuGetDeps={package_mode}", calls[0])
                self.assertIn(f"-p:UseNuGetDeps={package_mode}", calls[1])
                self.assertEqual(configuration, calls[1][calls[1].index("--configuration") + 1])
                for call in calls[2:]:
                    self.assertIn(f"/bin/{configuration}/net10.0/", call[0])
                    self.assertIn("-class", call)
                self.assertEqual(4, len(calls))
                self.assertEqual(["passed", "passed"], [row["hermetic_status"] for row in report["provider_status"]])

    def test_zero_partial_or_excess_cases_fail_for_each_provider(self) -> None:
        for provider, expected in (("forgejo", 10), ("github", 5)):
            for count in (0, expected - 1, expected + 1):
                with self.subTest(provider=provider, count=count):
                    result, report, _ = self.run_gate(**{f"{provider}_count": count})
                    self.assertNotEqual(0, result.returncode)
                    self.assertEqual("failed", report["status"])
                    self.assertIn("zero-or-partial-test-selection", result.stdout + result.stderr)
                    self.assertEqual("failed", next(row["hermetic_status"] for row in report["provider_status"] if row["provider"] == provider))

    def test_skipped_cases_cannot_satisfy_exact_case_count(self) -> None:
        result, report, _ = self.run_gate(skipped=1)
        self.assertNotEqual(0, result.returncode)
        self.assertEqual("failed", report["status"])
        self.assertIn("unsuccessful-test-summary", result.stdout + result.stderr)

    def test_nonzero_runner_exit_fails_even_with_exact_case_count(self) -> None:
        result, report, _ = self.run_gate(runner_exit=2)
        self.assertNotEqual(0, result.returncode)
        self.assertEqual("failed", report["status"])
        self.assertEqual(2, report["results"][-1]["exit_code"])

    def test_other_configuration_assembly_cannot_satisfy_prerequisite(self) -> None:
        result, report, calls = self.run_gate(configuration="Release", assembly_configuration="Debug")
        self.assertNotEqual(0, result.returncode)
        self.assertEqual("failed", report["status"])
        self.assertEqual([], calls)
        self.assertIn("missing-test-assembly", result.stdout + result.stderr)

    def test_diagnostic_totals_cannot_inflate_actual_case_count(self) -> None:
        result, report, _ = self.run_gate(forgejo_output=
            "Diagnostic Total: 1\nHexalith.Folders.Tests Total: 9, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0")
        self.assertNotEqual(0, result.returncode)
        self.assertEqual("failed", report["status"])
        self.assertIn("expected=10 actual=9", result.stdout + result.stderr)

    def test_missing_malformed_or_multiple_summaries_fail(self) -> None:
        valid = "Hexalith.Folders.Tests Total: 10, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0"
        for output in ("", "Total: 10, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0", valid.replace(", Errors: 0", ""), valid + "\n" + valid, valid.replace("Errors: 0", "Errors: 1") + "\n" + valid):
            with self.subTest(output=output):
                result, report, _ = self.run_gate(forgejo_output=output)
                self.assertNotEqual(0, result.returncode)
                self.assertEqual("failed", report["status"])
                self.assertIn("test-summary", result.stdout + result.stderr)

    def test_bad_counters_must_not_be_masked_by_other_lines(self) -> None:
        valid = "Hexalith.Folders.Tests Total: 10, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0"
        for counter in ("Errors", "Failed", "Skipped", "Not Run"):
            with self.subTest(counter=counter):
                output = valid.replace(f"{counter}: 0", f"{counter}: 1") + f"\nDiagnostic {counter}: 0"
                result, report, _ = self.run_gate(forgejo_output=output)
                self.assertNotEqual(0, result.returncode)
                self.assertEqual("failed", report["status"])
                self.assertIn("unsuccessful-test-summary", result.stdout + result.stderr)

    def test_github_summary_failure_keeps_forgejo_pass_separate(self) -> None:
        result, report, _ = self.run_gate(github_output=
            "Hexalith.Folders.Tests Total: 5, Errors: 1, Failed: 0, Skipped: 0, Not Run: 0\nDiagnostic Errors: 0")
        self.assertNotEqual(0, result.returncode)
        self.assertEqual("failed", report["status"])
        self.assertEqual(["passed", "failed"], [row["hermetic_status"] for row in report["provider_status"]])


@unittest.skipUnless(shutil.which("pwsh"), "PowerShell is required to exercise the scheduled policy gate")
class ScheduledPolicyGateTests(unittest.TestCase):
    def run_gate(
        self, *, configuration: str = "Debug", assembly_configuration: str | None = None, skip_build: bool = False,
        child_exit: int = 0, write_child_report: bool = True, stale_child_report: bool = False,
    ) -> tuple[subprocess.CompletedProcess[str], dict, list[list[str]]]:
        with tempfile.TemporaryDirectory() as temporary:
            root = pathlib.Path(temporary)
            shutil.copytree(ROOT / "deploy/dapr/production", root / "deploy/dapr/production")
            for relative in (
                "tests/fixtures/dapr-policy-conformance.yaml",
                "tests/tools/run-scheduled-policy-conformance-gates.ps1",
                "tests/tools/run-dapr-policy-conformance-gates.ps1",
            ):
                destination = root / relative
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(ROOT / relative, destination)
            assembly = root / (
                "tests/Hexalith.Folders.Contracts.Tests/bin/"
                f"{assembly_configuration or configuration}/net10.0/Hexalith.Folders.Contracts.Tests.dll"
            )
            assembly.parent.mkdir(parents=True)
            assembly.touch()
            if stale_child_report:
                stale_report = root / "_bmad-output/gates/dapr-policy-conformance/latest.json"
                stale_report.parent.mkdir(parents=True)
                stale_report.write_text(json.dumps({
                    "gate": "dapr-policy-conformance", "status": "passed",
                    "diagnostic_policy": "metadata-only", "live_dapr_kind_gate": "reference_pending_story_7_8",
                }), encoding="utf-8")
            bin_directory = root / "test-bin"
            bin_directory.mkdir()
            invocation_log = root / "invocations.jsonl"
            for name in ("dotnet", "pwsh"):
                executable = bin_directory / name
                executable.write_text(
                    """#!/usr/bin/env python3
import json, os, pathlib, sys
arguments = sys.argv[1:]
program = pathlib.Path(sys.argv[0]).name
with pathlib.Path(os.environ['POLICY_TEST_LOG']).open('a', encoding='utf-8') as log:
    log.write(json.dumps([program, *arguments]) + '\\n')
if program == 'pwsh':
    if '-SkipRestoreBuild' not in arguments or '-Configuration' not in arguments:
        raise SystemExit(99)
    configuration = arguments[arguments.index('-Configuration') + 1]
    if configuration != os.environ['POLICY_TEST_CONFIGURATION']:
        raise SystemExit(98)
    if os.environ['POLICY_TEST_WRITE_REPORT'] == 'true':
        report_path = pathlib.Path('_bmad-output/gates/dapr-policy-conformance/latest.json')
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(json.dumps({
            'gate': 'dapr-policy-conformance', 'status': 'passed',
            'diagnostic_policy': 'metadata-only', 'live_dapr_kind_gate': 'reference_pending_story_7_8',
        }), encoding='utf-8')
    raise SystemExit(int(os.environ['POLICY_TEST_CHILD_EXIT']))
raise SystemExit(0)
""",
                    encoding="utf-8",
                )
                executable.chmod(executable.stat().st_mode | stat.S_IXUSR)
            environment = os.environ.copy()
            environment.update(
                {
                    "POLICY_TEST_BIN": str(bin_directory),
                    "POLICY_TEST_CONFIGURATION": configuration,
                    "POLICY_TEST_SKIP_BUILD": str(skip_build).lower(),
                    "POLICY_TEST_LOG": str(invocation_log),
                    "POLICY_TEST_CHILD_EXIT": str(child_exit),
                    "POLICY_TEST_WRITE_REPORT": str(write_child_report).lower(),
                }
            )
            script_path = str(root / "tests/tools/run-scheduled-policy-conformance-gates.ps1").replace("'", "''")
            command = [
                "pwsh", "-NoLogo", "-NoProfile", "-Command",
                "$env:PATH = $env:POLICY_TEST_BIN + [IO.Path]::PathSeparator + $env:PATH; "
                "$gateArguments = @{}; "
                "if ($env:POLICY_TEST_CONFIGURATION -ne 'Debug') { $gateArguments.Configuration = $env:POLICY_TEST_CONFIGURATION }; "
                "if ($env:POLICY_TEST_SKIP_BUILD -eq 'true') { $gateArguments.SkipRestoreBuild = $true }; "
                f"& '{script_path}' @gateArguments",
            ]
            result = subprocess.run(command, cwd=root, env=environment, text=True, capture_output=True, check=False)
            report_path = root / "_bmad-output/gates/policy-conformance/latest.json"
            self.assertTrue(report_path.exists(), f"exit={result.returncode} {result.stdout}{result.stderr}")
            report = json.loads(report_path.read_text(encoding="utf-8"))
            calls = [json.loads(line) for line in invocation_log.read_text(encoding="utf-8").splitlines()] if invocation_log.exists() else []
            return result, report, calls

    def test_configuration_selects_solution_dependencies_and_static_gate(self) -> None:
        for configuration in ("Debug", "Release"):
            with self.subTest(configuration=configuration):
                result, report, calls = self.run_gate(configuration=configuration)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertEqual("passed", report["status"])
                self.assertEqual(3, len(calls))
                solution = "Hexalith.Folders.CI.slnx" if configuration == "Release" else "Hexalith.Folders.slnx"
                package_mode = "true" if configuration == "Release" else "false"
                for call in calls[:2]:
                    self.assertEqual(solution, call[2])
                    self.assertIn(f"-p:UseNuGetDeps={package_mode}", call)
                self.assertIn(f"-p:Configuration={configuration}", calls[0])
                self.assertEqual(configuration, calls[1][calls[1].index("--configuration") + 1])
                self.assertEqual("pwsh", calls[2][0])
                self.assertIn("-SkipRestoreBuild", calls[2])
                self.assertEqual(configuration, calls[2][calls[2].index("-Configuration") + 1])

    def test_other_configuration_assembly_is_rejected_before_static_gate(self) -> None:
        result, report, calls = self.run_gate(configuration="Release", assembly_configuration="Debug", skip_build=True)
        self.assertNotEqual(0, result.returncode)
        self.assertEqual("failed", report["status"])
        self.assertEqual([], calls)
        self.assertIn("missing-test-assembly", result.stdout + result.stderr)

    def test_stale_passed_report_cannot_mask_child_startup_failure(self) -> None:
        result, report, _ = self.run_gate(child_exit=7, write_child_report=False, stale_child_report=True)
        self.assertNotEqual(0, result.returncode)
        self.assertEqual("failed", report["status"])
        self.assertEqual(7, report["results"][-1]["exit_code"])

    def test_fresh_passed_report_cannot_override_child_failure_exit(self) -> None:
        result, report, _ = self.run_gate(child_exit=3)
        self.assertNotEqual(0, result.returncode)
        self.assertEqual("failed", report["status"])
        self.assertEqual(3, report["results"][-1]["exit_code"])

    def test_stale_report_cannot_replace_missing_evidence_even_with_success_exit(self) -> None:
        result, report, _ = self.run_gate(write_child_report=False, stale_child_report=True)
        self.assertNotEqual(0, result.returncode)
        self.assertEqual("failed", report["status"])
        self.assertIn("missing-static-gate-report", result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
