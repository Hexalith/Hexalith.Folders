#!/usr/bin/env python3
"""Run the focused Debug/source fallback without changing the owning workspace."""
from __future__ import annotations

import importlib.util
import json
import os
from pathlib import Path
import shutil


HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("qualification_run", HERE / "run.py")
run = importlib.util.module_from_spec(spec)
spec.loader.exec_module(run)


def main() -> None:
    scratch = Path(os.environ["TMPDIR"])
    workspace = scratch / "folders"
    before = run.snapshot()
    run.write("source-before.json", before)
    closure = {}
    print("Copying source fallback inputs", flush=True)
    run.copy_repository(run.ROOT, workspace, closure)
    for relative in before["repositories"]:
        if relative != ".":
            run.copy_repository(run.ROOT / relative, workspace / relative, closure)
    run.write("source-copied-inputs.json", closure)
    run.write("source-after-copy.json", run.snapshot())
    feed = scratch / "feed"
    feed.mkdir()
    for directory in (Path.home() / ".nuget/packages").glob("*/*"):
        archives = sorted(directory.glob("*.nupkg"))
        if not archives:
            continue
        filename = f"{directory.parent.name}.{directory.name}.nupkg"
        archive = directory / filename
        (feed / filename).symlink_to(archive if archive.is_file() else archives[0])
    config = scratch / "nuget.config"
    config.write_text('<configuration><packageSources><clear/><add key="nuget.org" value="' +
                      str(feed) + '"/></packageSources><packageSourceMapping><packageSource key="nuget.org">'
                      '<package pattern="*"/></packageSource></packageSourceMapping></configuration>')
    common = ["-p:UseHexalithProjectReferences=true", "-p:UseNuGetDeps=false", "-m:1", "--disable-build-servers"]
    passed = []
    for suffix, classes in (
        ("Server.Tests", ["Hexalith.Folders.Server.Tests.WorkspaceContentProductionTests"]),
        ("IntegrationTests", ["Hexalith.Folders.IntegrationTests.Routing.FoldersApiRoutingModeTests"]),
        ("Contracts.Tests", ["Hexalith.Folders.Contracts.Tests.OpenApi.AuthorizationMatrixContractTests",
            "Hexalith.Folders.Contracts.Tests.OpenApi.Pd10V2CandidateContractTests",
            "Hexalith.Folders.Contracts.Tests.OpenApi.ContractSpineCiGateTests"]),
    ):
        name = "Hexalith.Folders." + suffix
        project = f"tests/{name}/{name}.csproj"
        label = "source-" + suffix.lower().replace(".", "-")
        run.command(label + "-restore", workspace, ["dotnet", "restore", project, "-p:Configuration=Debug", "--configfile", str(config), *common])
        result = run.command(label + "-build", workspace, ["dotnet", "build", project, "--configuration", "Debug", "--no-restore", "-warnaserror", *common])
        if result:
            passed.append(False)
            continue
        assembly = workspace / f"tests/{name}/bin/Debug/net11.0/{name}.dll"
        args = ["dotnet", str(assembly), "-noLogo", "-noColor"]
        for selected in classes:
            args += ["-class", selected]
        passed.append(run.command(label + "-behavior", workspace, args) == 0)
    if passed == [True, True, True]:
        outputs = [scratch / "first.yaml", scratch / "second.yaml"]
        for index, output in enumerate(outputs):
            run.command(f"source-generate-{index + 1}", workspace, ["python3", "scripts/generate-pd10-v2-conformance-set.py", "--output", str(output)])
        if all(path.is_file() for path in outputs) and outputs[0].read_bytes() == outputs[1].read_bytes():
            shutil.copy2(outputs[0], HERE / "candidate-generated.yaml")
            target = workspace / "_bmad-output/implementation-artifacts/evidence/story-12-2-v2-source-requalification-pending-2026-11-12.yaml"
            shutil.copy2(outputs[0], target)
            run.command("source-conformance", workspace, ["dotnet", "tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net11.0/Hexalith.Folders.Contracts.Tests.dll", "-noLogo", "-noColor", "-class", "Hexalith.Folders.Contracts.Tests.OpenApi.Pd10ConformanceSetTests"])
    run.write("source-commands.json", run.COMMANDS)
    run.write("source-after.json", run.snapshot())
    print("Source fallback finished; private outputs will be removed", flush=True)


if __name__ == "__main__":
    main()
