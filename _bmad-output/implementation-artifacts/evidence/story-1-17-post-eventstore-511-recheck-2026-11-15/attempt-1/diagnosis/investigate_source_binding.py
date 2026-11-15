#!/usr/bin/env python3
"""Diagnose the Debug/source EventStore assembly-load failure (run under run-with-temp only).

Builds Folders Server.Tests in the same private Debug/source profile as run.py, then records only
metadata: Hexalith.EventStore file hashes/sizes, deps.json entries and assembly identities.
"""

from __future__ import annotations

import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("recheck_run", HERE / "run.py")
run = importlib.util.module_from_spec(spec)
spec.loader.exec_module(run)
run.EVIDENCE = HERE / "diagnosis"


def identities(paths: list[Path]) -> dict:
    script = ("$ErrorActionPreference='Stop'; $out=@{}; foreach($p in $args){ $n=[Reflection.AssemblyName]::GetAssemblyName($p);"
              " $out[$p]=@{name=$n.Name; version=$n.Version.ToString()} };"
              " $out | ConvertTo-Json -Depth 4")
    result = subprocess.run([str(Path.home() / ".dotnet/tools/pwsh"), "-NoLogo", "-NoProfile", "-Command", script, *map(str, paths)],
                            capture_output=True, text=True, check=False)
    return {"exit_code": result.returncode, "stdout": result.stdout, "stderr": result.stderr[-2000:]}


def references(path: Path) -> dict:
    script = ("$ErrorActionPreference='Stop'; Add-Type -AssemblyName System.Reflection.Metadata;"
              " $s=[IO.File]::OpenRead($args[0]); $pe=[Reflection.PortableExecutable.PEReader]::new($s);"
              " $md=[Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe); $r=@();"
              " foreach($h in $md.AssemblyReferences){ $a=$md.GetAssemblyReference($h); $n=$md.GetString($a.Name);"
              " if($n -like 'Hexalith.EventStore*'){ $r+= \"$n $($a.Version)\" } }; $r -join \"`n\"")
    result = subprocess.run([str(Path.home() / ".dotnet/tools/pwsh"), "-NoLogo", "-NoProfile", "-Command", script, str(path)],
                            capture_output=True, text=True, check=False)
    return {"exit_code": result.returncode, "stdout": result.stdout, "stderr": result.stderr[-2000:]}


def main() -> int:
    run.EVIDENCE.mkdir()
    scratch = Path(os.environ["TMPDIR"])
    if "hexalith-job." not in str(scratch):
        raise SystemExit("Run under run-with-temp")
    workspace = scratch / "folders"
    closure: dict = {}
    before = run.snapshot()
    run.copy_repository(run.ROOT, workspace, closure, workspace)
    for relative in before["repositories"]:
        if relative != ".":
            run.copy_repository(run.ROOT / relative, workspace / relative, closure, workspace)
    feed = scratch / "feed"
    feed.mkdir()
    for version_directory in (Path.home() / ".nuget/packages").glob("*/*"):
        archives = sorted(version_directory.glob("*.nupkg"))
        if archives:
            filename = f"{version_directory.parent.name}.{version_directory.name}.nupkg"
            preferred = version_directory / filename
            (feed / filename).symlink_to(preferred if preferred.is_file() else archives[0])
    config = scratch / "nuget.config"
    config.write_text('<configuration><packageSources><clear/><add key="cached" value="' + str(feed) +
                      '"/></packageSources><packageSourceMapping><packageSource key="cached">'
                      '<package pattern="*"/></packageSource></packageSourceMapping></configuration>')
    source = ["-p:UseHexalithProjectReferences=true", "-p:UseNuGetDeps=false", "-m:1", "--disable-build-servers"]
    project = "tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj"
    run.command("diagnose-server-tests-restore", workspace, ["dotnet", "restore", project, "-p:Configuration=Debug", "--configfile", str(config), *source])
    run.command("diagnose-server-tests-build", workspace, ["dotnet", "build", project, "--configuration", "Debug", "--no-restore",
                                                           "-warnaserror", "-v:n", *source])
    output = workspace / "tests/Hexalith.Folders.Server.Tests/bin/Debug/net11.0"
    source_outputs = sorted((workspace / "references/Hexalith.EventStore/src").glob("*/bin/Debug/net11.0/Hexalith.EventStore.*.dll"))
    test_outputs = sorted(output.glob("Hexalith.EventStore*.dll"))
    deps = json.loads((output / "Hexalith.Folders.Server.Tests.deps.json").read_text())
    entries = {}
    for target in deps.get("targets", {}).values():
        for key, value in target.items():
            if key.startswith("Hexalith.EventStore"):
                entries[key] = value
    libraries = {key: value for key, value in deps.get("libraries", {}).items() if key.startswith("Hexalith.EventStore")}
    minver = subprocess.run(["git", "-C", str(workspace / "references/Hexalith.EventStore"), "describe", "--tags", "--long"],
                            capture_output=True, text=True, check=False)
    result = {
        "source_outputs": [{"path": str(p.relative_to(workspace)), "sha256": run.sha(p), "bytes": p.stat().st_size} for p in source_outputs],
        "test_outputs": [{"path": str(p.relative_to(workspace)), "sha256": run.sha(p), "bytes": p.stat().st_size} for p in test_outputs],
        "deps_targets": entries, "deps_libraries": libraries,
        "source_identities": identities(source_outputs), "test_identities": identities(test_outputs),
        "server_tests_references": references(output / "Hexalith.Folders.Server.Tests.dll"),
        "folders_server_references": references(output / "Hexalith.Folders.Server.dll"),
        "eventstore_describe": {"exit_code": minver.returncode, "stdout": minver.stdout.strip(), "stderr": minver.stderr.strip()},
    }
    run.write("diagnose-source-binding.json", result)
    print(json.dumps({"source": len(source_outputs), "test": len(test_outputs)}), flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
