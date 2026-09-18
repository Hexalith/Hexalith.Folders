#!/usr/bin/env python3
"""Manifest and sealed NuGet archive contract for Folders releases."""

from __future__ import annotations

import json
import pathlib
import re
import xml.etree.ElementTree as element_tree
import zipfile
from dataclasses import dataclass


ROOT = pathlib.Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "tools" / "release-packages.json"
PACKAGE_ID_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")
DRIVE_PATH_PATTERN = re.compile(r"^[A-Za-z]:")


@dataclass(frozen=True)
class ManifestPackage:
    """One validated package manifest entry."""

    package_id: str
    project: str
    project_path: pathlib.Path


@dataclass(frozen=True)
class PackageMetadata:
    """Identity and dependency metadata extracted from one primary package."""

    package_id: str
    version: str
    dependencies: tuple[tuple[str, str], ...]


def reject_duplicate_keys(pairs: list[tuple[str, object]]) -> dict[str, object]:
    """Build one object while failing closed on duplicate JSON properties."""

    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON property '{key}'")
        result[key] = value
    return result


def load_manifest(path: pathlib.Path = MANIFEST) -> list[ManifestPackage]:
    """Load and normalize the authoritative package inventory."""

    with path.open("r", encoding="utf-8") as handle:
        payload = json.load(handle, object_pairs_hook=reject_duplicate_keys)
    packages = payload.get("packages") if isinstance(payload, dict) else None
    if not isinstance(packages, list) or not packages:
        raise ValueError(f"{path} must contain a non-empty 'packages' array")

    source_root = (ROOT / "src").resolve()
    seen_ids: set[str] = set()
    seen_projects: set[pathlib.Path] = set()
    normalized: list[ManifestPackage] = []
    for index, item in enumerate(packages, start=1):
        if not isinstance(item, dict):
            raise ValueError(f"package entry #{index} must be an object")
        package_id = item.get("id")
        project = item.get("project")
        if not isinstance(package_id, str) or not isinstance(project, str):
            raise ValueError(f"package entry #{index} must define string 'id' and 'project' values")
        if not PACKAGE_ID_PATTERN.fullmatch(package_id) or not package_id.startswith("Hexalith.Folders"):
            raise ValueError(f"package id is outside the Folders namespace: {package_id}")

        project_parts = pathlib.PurePosixPath(project.replace("\\", "/"))
        if project_parts.is_absolute() or ".." in project_parts.parts or project_parts.suffix != ".csproj":
            raise ValueError(f"project must be a normalized relative .csproj path: {project}")
        project_path = (ROOT / project_parts).resolve()
        try:
            project_path.relative_to(source_root)
        except ValueError as error:
            raise ValueError(f"project is outside the root-owned src directory: {project}") from error
        normalized_project = project_path.relative_to(ROOT).as_posix()
        if project != normalized_project or not project_path.is_file():
            raise ValueError(f"package project is missing or not normalized: {project}")

        id_key = package_id.casefold()
        if id_key in seen_ids or project_path in seen_projects:
            raise ValueError(f"duplicate package identity or project at entry #{index}")
        seen_ids.add(id_key)
        seen_projects.add(project_path)
        normalized.append(ManifestPackage(package_id, project, project_path))

    return normalized


def _validate_archive_paths(archive: zipfile.ZipFile, path: pathlib.Path) -> None:
    """Reject traversal, rooted paths, project files, and corrupt entries."""

    corrupt = archive.testzip()
    if corrupt is not None:
        raise ValueError(f"{path.name} contains corrupt entry {corrupt}")
    for name in archive.namelist():
        normalized = name.replace("\\", "/")
        parts = pathlib.PurePosixPath(normalized).parts
        if (
            name != normalized
            or pathlib.PurePosixPath(normalized).is_absolute()
            or pathlib.PureWindowsPath(normalized).is_absolute()
            or DRIVE_PATH_PATTERN.match(normalized) is not None
            or ".." in parts
            or normalized.casefold().endswith((".csproj", ".fsproj", ".vbproj"))
        ):
            raise ValueError(f"{path.name} contains an unsafe archive path: {name}")


def read_metadata(path: pathlib.Path, source_revision: str | None = None) -> PackageMetadata:
    """Validate one primary archive and return its embedded package contract."""

    with zipfile.ZipFile(path, "r") as archive:
        _validate_archive_paths(archive, path)
        nuspec_names = [name for name in archive.namelist() if name.casefold().endswith(".nuspec")]
        if len(nuspec_names) != 1:
            raise ValueError(f"{path.name} must contain exactly one .nuspec")
        root = element_tree.fromstring(archive.read(nuspec_names[0]))
        namespace = root.tag[1:].split("}", 1)[0] if root.tag.startswith("{") else ""
        qualified = lambda name: f"{{{namespace}}}{name}" if namespace else name
        metadata = root.find(qualified("metadata"))
        if metadata is None:
            raise ValueError(f"{path.name} is missing nuspec metadata")

        def text(name: str) -> str:
            return (metadata.findtext(qualified(name)) or "").strip()

        package_id = text("id")
        version = text("version")
        if not package_id or not version:
            raise ValueError(f"{path.name} must declare package id and version")
        if text("authors") != "Hexalith Contributors" or text("projectUrl") != "https://github.com/Hexalith/Hexalith.Folders":
            raise ValueError(f"{path.name} has incomplete authorship or project metadata")
        license_element = metadata.find(qualified("license"))
        if license_element is None or (license_element.text or "").strip() != "MIT":
            raise ValueError(f"{path.name} must declare the MIT license expression")
        readme = text("readme")
        if not readme or readme not in archive.namelist():
            raise ValueError(f"{path.name} must contain its declared package readme")
        repository = metadata.find(qualified("repository"))
        if repository is None or repository.attrib.get("url") != "https://github.com/Hexalith/Hexalith.Folders":
            raise ValueError(f"{path.name} has incomplete repository metadata")
        if source_revision is not None and repository.attrib.get("commit") != source_revision:
            raise ValueError(f"{path.name} does not identify the expected source revision")

        dependencies = tuple(
            (dependency.attrib["id"].strip(), dependency.attrib.get("version", "").strip())
            for dependency in metadata.findall(f".//{qualified('dependency')}")
            if dependency.attrib.get("id", "").strip()
        )
        return PackageMetadata(package_id, version, dependencies)


def read_symbol_metadata(path: pathlib.Path, source_revision: str | None = None) -> tuple[str, str]:
    """Validate symbol identity, provenance, and payload."""

    with zipfile.ZipFile(path, "r") as archive:
        _validate_archive_paths(archive, path)
        nuspec_names = [name for name in archive.namelist() if name.casefold().endswith(".nuspec")]
        if len(nuspec_names) != 1:
            raise ValueError(f"{path.name} must contain exactly one symbol .nuspec")
        root = element_tree.fromstring(archive.read(nuspec_names[0]))
        namespace = root.tag[1:].split("}", 1)[0] if root.tag.startswith("{") else ""
        qualified = lambda name: f"{{{namespace}}}{name}" if namespace else name
        metadata = root.find(qualified("metadata"))
        if metadata is None:
            raise ValueError(f"{path.name} is missing symbol nuspec metadata")
        package_id = (metadata.findtext(qualified("id")) or "").strip()
        version = (metadata.findtext(qualified("version")) or "").strip()
        package_types = metadata.findall(f".//{qualified('packageType')}")
        if not package_id or not version or not any(item.attrib.get("name") == "SymbolsPackage" for item in package_types):
            raise ValueError(f"{path.name} has invalid symbol identity or package type")
        repository = metadata.find(qualified("repository"))
        if repository is None or repository.attrib.get("url") != "https://github.com/Hexalith/Hexalith.Folders":
            raise ValueError(f"{path.name} has incomplete symbol repository metadata")
        if source_revision is not None and repository.attrib.get("commit") != source_revision:
            raise ValueError(f"{path.name} does not identify the expected symbol source revision")
        pdb_entries = [name for name in archive.namelist() if name.casefold().endswith(".pdb")]
        if len(pdb_entries) != 1 or archive.getinfo(pdb_entries[0]).file_size == 0:
            raise ValueError(f"{path.name} must contain exactly one nonempty portable PDB")
        return package_id, version


def validate_internal_dependencies(
    metadata: PackageMetadata,
    manifest_ids_by_key: dict[str, str],
    release_version: str,
) -> None:
    """Require every internal dependency to use canonical identity and the coordinated release version."""

    internal_dependencies = [
        (dependency_id, dependency_version)
        for dependency_id, dependency_version in metadata.dependencies
        if dependency_id.casefold().startswith("hexalith.folders")
    ]
    missing = sorted(
        dependency_id
        for dependency_id, _ in internal_dependencies
        if dependency_id.casefold() not in manifest_ids_by_key
    )
    if missing:
        raise ValueError(f"{metadata.package_id} has unpublished Folders dependencies: {missing}")
    for dependency_id, dependency_version in internal_dependencies:
        canonical_id = manifest_ids_by_key[dependency_id.casefold()]
        if dependency_id != canonical_id:
            raise ValueError(f"{metadata.package_id} uses noncanonical Folders dependency ID {dependency_id}")
        if dependency_version != release_version:
            raise ValueError(
                f"{metadata.package_id} dependency {dependency_id} uses {dependency_version!r} "
                f"instead of release version {release_version}"
            )


def validate_packages(
    package_directory: pathlib.Path,
    expected_version: str | None = None,
    source_revision: str | None = None,
) -> tuple[list[ManifestPackage], str]:
    """Validate exact package/symbol pairs, metadata, and dependency closure."""

    manifest = load_manifest()
    manifest_ids = {package.package_id for package in manifest}
    manifest_ids_by_key = {package.package_id.casefold(): package.package_id for package in manifest}
    primary_archives = sorted(
        path
        for path in package_directory.glob("*.nupkg")
        if ".symbols." not in path.name and not path.name.endswith(".snupkg")
    )
    symbol_archives = sorted(package_directory.glob("*.snupkg"))
    if len(primary_archives) != len(manifest) or len(symbol_archives) != len(manifest):
        raise ValueError(
            f"expected {len(manifest)} .nupkg/.snupkg pairs, found "
            f"{len(primary_archives)} primary and {len(symbol_archives)} symbol packages"
        )

    metadata_rows = [read_metadata(path, source_revision) for path in primary_archives]
    ids = [metadata.package_id for metadata in metadata_rows]
    if len({package_id.casefold() for package_id in ids}) != len(ids) or set(ids) != manifest_ids:
        raise ValueError(f"package output does not match the manifest IDs: {sorted(ids)}")
    versions = {metadata.version for metadata in metadata_rows}
    if len(versions) != 1:
        raise ValueError(f"package output contains multiple versions: {sorted(versions)}")
    version = next(iter(versions))
    if expected_version is not None and version != expected_version:
        raise ValueError(f"package version {version} does not match expected version {expected_version}")

    for metadata, archive in zip(metadata_rows, primary_archives, strict=True):
        expected_name = f"{metadata.package_id}.{version}.nupkg"
        expected_symbol = package_directory / f"{metadata.package_id}.{version}.snupkg"
        if archive.name != expected_name or not expected_symbol.is_file():
            raise ValueError(f"package or symbol archive name drifted for {metadata.package_id}")
        symbol_id, symbol_version = read_symbol_metadata(expected_symbol, source_revision)
        if symbol_id != metadata.package_id or symbol_version != version:
            raise ValueError(f"symbol package identity drifted for {metadata.package_id}")

        validate_internal_dependencies(metadata, manifest_ids_by_key, version)

    return manifest, version
