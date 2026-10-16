#!/usr/bin/env python3
"""Recheck retained Story 1.17 candidate, preservation and cached-package identities."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile


def digest(content: bytes) -> str:
    return hashlib.sha256(content).hexdigest()


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository-root", type=Path, default=Path(__file__).resolve().parents[3])
    args = parser.parse_args()
    root = args.repository_root.resolve()
    record = json.loads((root / "_bmad-output/implementation-artifacts/evidence/story-1-17-local-recheck-2026-10-15.json").read_text())
    operational = record["operational_discovery"]
    require(digest((root / operational["path"]).read_bytes()) == operational["sha256"], "Retained operational snapshot changed")
    for check in record["checks"]:
        log = root / check["retained_log_path"]
        require(digest(log.read_bytes()) == check["raw_log_sha256"], "Retained command output changed: " + check["id"])
    for path, expected in record["preservation"]["default_gate_reports_sha256"].items():
        require(digest((root / path).read_bytes()) == expected, "Historical default gate report changed: " + path)
    candidate_path = root / record["candidate"]["path"]
    candidate = candidate_path.read_bytes()
    require(digest(candidate) == record["candidate"]["raw_sha256"], "Candidate raw digest changed")
    entries = re.findall(r'  - path: ("[^\n]+")\n    kind: file\n    bytes: (\d+)\n    sha256: ([0-9a-f]{64})\n', candidate.decode())
    require(len(entries) == record["candidate"]["artifact_count"], "Candidate inventory count changed")
    material = []
    for path_json, length, expected in entries:
        path = json.loads(path_json)
        content = (root / path).read_bytes()
        require(len(content) == int(length) and digest(content) == expected, f"Candidate artifact changed: {path}")
        material.append(f"{path}\0{expected}\n")
    require(digest("".join(material).encode()) == record["candidate"]["candidate_set_sha256"], "Candidate set digest changed")

    manifest_path = root / "_bmad-output/planning-artifacts/planning-story-manifest.yaml"
    manifest = manifest_path.read_bytes()
    require(digest(manifest) == record["candidate"]["planning_manifest_raw_sha256"], "Current planning manifest changed")
    provenance_pattern = r"(?ms)^provenance:\n.*?(?=^post_a8_finalization:)"
    provenance = re.search(provenance_pattern, manifest.decode())
    require(provenance is not None, "Missing top-level provenance")
    bindings = re.findall(r"(?m)^- path: (.+)\n  sha256: ([0-9a-f]{64})$", provenance[0])
    require(len(bindings) == record["candidate"]["current_binding_count"], "Current provenance count changed")
    for path, expected in bindings:
        require(digest((root / path).read_bytes()) == expected, f"Current binding changed: {path}")
    previous = subprocess.check_output(["git", "show", record["source"]["repositories"]["folders"]["commit"] + ":" + str(manifest_path.relative_to(root))], cwd=root).decode()
    require(re.sub(provenance_pattern, "", previous) == re.sub(provenance_pattern, "", manifest.decode()), "Historical manifest or lifecycle fields changed")

    with tempfile.TemporaryDirectory(prefix="story117-verify-") as temporary:
        for ordinal in (1, 2):
            output = Path(temporary) / f"candidate-{ordinal}.yaml"
            subprocess.run(["python3", "scripts/generate-pd10-v2-conformance-set.py", "--output", str(output)], cwd=root, check=True)
            require(output.read_bytes() == candidate, "Isolated candidate generation differs")

    for path, expected in record["preservation"]["historical_and_control_file_hashes"].items():
        require(digest((root / path).read_bytes()) == expected, f"Historical/control file changed: {path}")
    spec = (root / "_bmad-output/implementation-artifacts/spec-1-17-complete-pd10-v2-migration-and-closure.md").read_bytes()
    frozen = re.search(rb"<frozen-after-approval\b.*?</frozen-after-approval>", spec, re.S)
    require(frozen is not None and digest(frozen[0]) == record["preservation"]["spec_frozen_sha256"], "Frozen spec changed")
    require(b"baseline_commit: '37d348945b35620da6ba4e524a09bee202712460'" in spec, "Original spec baseline changed")
    require(digest((root / "references/Hexalith.Builds/Props/Directory.Packages.props").read_bytes()) == record["source"]["central_catalog_sha256"], "Central package catalog changed")
    for path, expected in record["source"]["source_inventory_sha256"].items():
        require(digest((root / path).read_bytes()) == expected, f"Inventoried source changed: {path}")

    assets = json.loads((root / "references/Hexalith.Projects/src/Hexalith.Projects.Server/obj/project.assets.json").read_text())
    runtime = root / "references/Hexalith.Projects/tests/Hexalith.Projects.Server.Tests/bin/Release/net10.0"
    for package in record["source"]["released_pair"]:
        identity = package["id"] + "/" + package["version"]
        library = assets["libraries"][identity]
        require(library["type"] == "package", f"Consumer does not use package: {identity}")
        filename = package["id"].lower() + "." + package["version"] + ".nupkg"
        archive = next(Path(folder) / library["path"] / filename for folder in assets["packageFolders"] if (Path(folder) / library["path"] / filename).is_file())
        require(digest(archive.read_bytes()) == package["cached_archive_sha256"], f"Cached archive changed: {identity}")
        with zipfile.ZipFile(archive) as zipped:
            nuspec = ET.fromstring(zipped.read(next(name for name in zipped.namelist() if name.endswith(".nuspec"))))
            repository = next(node for node in nuspec.iter() if node.tag.endswith("repository"))
            require(repository.attrib["commit"] == package["repository_commit"], f"Package source changed: {identity}")
            dll = zipped.read("lib/net10.0/" + package["id"] + ".dll")
            require(digest(dll) == package["runtime_dll_sha256"] and dll == (runtime / (package["id"] + ".dll")).read_bytes(), f"Runtime payload changed: {identity}")
    for path in ("src/Hexalith.Folders.Server/obj/project.assets.json", "references/Hexalith.Projects/src/Hexalith.Projects.Server/obj/project.assets.json"):
        libraries = json.loads((root / path).read_text())["libraries"]
        selected = {key: value for key, value in libraries.items() if key.startswith("Hexalith.EventStore.")}
        require(bool(selected) and all(key.endswith("/3.121.0") and value["type"] == "package" for key, value in selected.items()), f"EventStore package profile changed: {path}")
    for path, expected in record["source"]["test_runtime_sha256"].items():
        require(digest((root / path).read_bytes()) == expected, f"Verified executable changed: {path}")
    print(json.dumps({"candidate_artifacts": len(entries), "current_bindings": len(bindings), "isolated_generations": 2, "preserved_control_files": len(record["preservation"]["historical_and_control_file_hashes"]), "cached_package_payloads": 2, "result": "pass"}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
