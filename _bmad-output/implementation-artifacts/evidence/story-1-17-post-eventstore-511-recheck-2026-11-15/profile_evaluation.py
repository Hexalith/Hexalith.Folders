#!/usr/bin/env python3
"""Evaluate (never build) every CI-solution project to show where the Tenants identity carry applies.

Release/package evaluation must leave the carry inert; Debug/source evaluation must carry the root
EventStore selection on every Tenants source reference. Only MSBuild evaluation runs: no restore,
build, or file output in the owning checkout besides this script's JSON result.
"""

from __future__ import annotations

import json
from pathlib import Path
import re
import subprocess
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
PROFILES = {
    "release-package": ["-p:Configuration=Release", "-p:UseHexalithProjectReferences=false"],
    "debug-source": ["-p:Configuration=Debug", "-p:UseHexalithProjectReferences=true", "-p:UseNuGetDeps=false"],
}


def main() -> int:
    projects = re.findall(r'<Project Path="([^"]+)"', (ROOT / "Hexalith.Folders.CI.slnx").read_text())
    tenants_root = str((ROOT / "references/Hexalith.Tenants/src").resolve()) + "/"
    result = {"projects": len(projects), "profiles": {}}
    ok = True
    for name, arguments in PROFILES.items():
        rows = []
        for project in projects:
            completed = subprocess.run(["dotnet", "msbuild", project, "-nologo", *arguments,
                                        "-getProperty:_FoldersCarriesEventStoreVersionToTenants",
                                        "-getProperty:HexalithEventStoreVersion", "-getItem:ProjectReference"],
                                       cwd=ROOT, capture_output=True, text=True, check=False)
            if completed.returncode != 0:
                rows.append({"project": project, "exit_code": completed.returncode, "error": completed.stdout[-500:]})
                ok = False
                continue
            data = json.loads(completed.stdout)
            tenants = []
            for item in data.get("Items", {}).get("ProjectReference", []):
                full = item.get("FullPath") or item.get("Identity", "")
                if full.startswith(tenants_root):
                    tenants.append({"file": Path(full).name, "additional_properties": item.get("AdditionalProperties", "")})
            rows.append({"project": project, "exit_code": 0,
                         "carry": data["Properties"]["_FoldersCarriesEventStoreVersionToTenants"],
                         "eventstore_version": data["Properties"]["HexalithEventStoreVersion"],
                         "direct_tenants_source_references": tenants})
        result["profiles"][name] = rows
    package = result["profiles"]["release-package"]
    source = result["profiles"]["debug-source"]
    # AppHost.Tests keeps a pre-existing non-assembly Tenants host reference in both profiles; inert means
    # the carry flag is unset and no package-profile Tenants edge receives an EventStore version.
    result["package_inert"] = all(row.get("exit_code") == 0 and row.get("carry") == "" and not any(
        "HexalithEventStoreVersion=" in ref["additional_properties"] for ref in row.get("direct_tenants_source_references", []))
        for row in package)
    result["package_tenants_references"] = [{"project": row["project"], **ref} for row in package
                                            for ref in row.get("direct_tenants_source_references", [])]
    carried = [ref for row in source for ref in row.get("direct_tenants_source_references", [])]
    result["source_direct_tenants_references"] = len(carried)
    result["source_all_direct_tenants_references_carried"] = bool(carried) and all(
        "HexalithEventStoreVersion=5.1.1" in ref["additional_properties"] for ref in carried)
    ok &= result["package_inert"] and result["source_all_direct_tenants_references_carried"]
    result["status"] = "pass" if ok else "fail"
    (HERE / "profile-evaluation.json").write_text(json.dumps(result, indent=2, sort_keys=True) + "\n")
    print(json.dumps({key: result[key] for key in ("projects", "package_inert", "source_direct_tenants_references",
                                                   "source_all_direct_tenants_references_carried", "status")}))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
