#!/usr/bin/env python3
"""Rerun the independent package-profile gates after the pending-only reconciliation."""
from __future__ import annotations

import importlib.util
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
    closure = {}
    run.write("final-before.json", run.snapshot())
    run.copy_repository(run.ROOT, workspace, closure)
    run.copy_repository(run.ROOT / "references/Hexalith.Builds", workspace / "references/Hexalith.Builds", closure)
    feed = scratch / "feed"
    feed.mkdir()
    for directory in (Path.home() / ".nuget/packages").glob("*/*"):
        archives = sorted(directory.glob("*.nupkg"))
        if archives:
            filename = f"{directory.parent.name}.{directory.name}.nupkg"
            preferred = directory / filename
            (feed / filename).symlink_to(preferred if preferred.is_file() else archives[0])
    config = scratch / "nuget.config"
    config.write_text('<configuration><packageSources><clear/><add key="nuget.org" value="' + str(feed) +
        '"/></packageSources><packageSourceMapping><packageSource key="nuget.org"><package pattern="*"/>'
        '</packageSource></packageSourceMapping></configuration>')
    project = "tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj"
    common = ["-p:UseHexalithProjectReferences=false", "-m:1", "--disable-build-servers"]
    run.command("final-contracts-restore", workspace, ["dotnet", "restore", project, "-p:Configuration=Release", "--configfile", str(config), *common])
    run.command("final-contracts-build", workspace, ["dotnet", "build", project, "--configuration", "Release", "--no-restore", "-warnaserror", *common])
    binary = "tests/Hexalith.Folders.Contracts.Tests/bin/Release/net11.0/Hexalith.Folders.Contracts.Tests.dll"
    run.command("final-conformance", workspace, ["dotnet", binary, "-noLogo", "-noColor", "-class", "Hexalith.Folders.Contracts.Tests.OpenApi.Pd10ConformanceSetTests"])
    run.command("final-contract-behavior", workspace, ["dotnet", binary, "-noLogo", "-noColor", "-class", "Hexalith.Folders.Contracts.Tests.OpenApi.AuthorizationMatrixContractTests", "-class", "Hexalith.Folders.Contracts.Tests.OpenApi.Pd10V2CandidateContractTests", "-class", "Hexalith.Folders.Contracts.Tests.OpenApi.ContractSpineCiGateTests"])
    oracle = "tests/tools/parity-oracle-generator/Hexalith.Folders.ParityOracleGenerator.csproj"
    run.command("final-oracle-restore", workspace, ["dotnet", "restore", oracle, "-p:Configuration=Release", "--configfile", str(config), *common])
    run.command("final-oracle-build", workspace, ["dotnet", "build", oracle, "--configuration", "Release", "--no-restore", "-warnaserror", *common])
    run.command("final-parity", workspace, ["pwsh", "tests/tools/run-contract-parity-ci-gates.ps1", "-SkipRestoreBuild", "-OutputReportPath", str(HERE / "final-parity-report.json")])
    run.command("final-governance", workspace, ["pwsh", "tests/tools/run-governance-completeness-gates.ps1", "-SkipRestoreBuild"])
    run.write("final-commands.json", run.COMMANDS)
    run.write("final-copied-inputs.json", closure)
    run.write("final-after.json", run.snapshot())
    directory = workspace / Path(binary).parent
    artifacts = []
    for path in sorted(directory.iterdir()):
        if path.is_file() and path.suffix in {".dll", ".pdb", ".json"}:
            relative = None
            if path.name.startswith(("Hexalith.Folders.",)):
                target = HERE / "diagnostic-artifacts" / "final-contracts" / path.name
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(path, target)
                relative = str(target.relative_to(HERE))
            artifacts.append({"file": path.name, "sha256": run.sha(path), "bytes": path.stat().st_size, "retained": relative})
    run.write("final-contracts-artifacts.json", artifacts)
    print("Final independent gates complete", flush=True)


if __name__ == "__main__":
    main()
