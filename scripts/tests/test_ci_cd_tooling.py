from __future__ import annotations

import os
import pathlib
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


if __name__ == "__main__":
    unittest.main()
