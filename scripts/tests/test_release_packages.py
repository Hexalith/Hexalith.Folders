from __future__ import annotations

import pathlib
import tempfile
import unittest
import zipfile

from scripts.release_package_contract import (
    ROOT,
    PackageMetadata,
    _validate_archive_paths,
    read_symbol_metadata,
    validate_internal_dependencies,
    validate_package_output_directory,
)


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
        with self.assertRaisesRegex(ValueError, "repository-owned package directory"):
            validate_package_output_directory(ROOT)



if __name__ == "__main__":
    unittest.main()
