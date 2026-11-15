#!/usr/bin/env python3
"""Rerun only the broad Debug/source CI-solution build with PowerShell resolvable on PATH.

The final run's solution build stopped when Hexalith.Builds.Tooling invoked ``pwsh`` and the agent
PATH lacked ~/.dotnet/tools (exit 127). This job prepends only that directory, then repeats the same
private-copy restore and build commands. Run as ``run-with-temp python3 -I solution_build.py``.
"""

from __future__ import annotations

import importlib.util
import json
import os
from pathlib import Path
import shutil
import sys

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("recheck_run", HERE / "run.py")
run = importlib.util.module_from_spec(spec)
spec.loader.exec_module(run)


def main() -> int:
    scratch = Path(os.environ["TMPDIR"])
    if "hexalith-job." not in str(scratch):
        raise SystemExit("Run under run-with-temp")
    tools = Path.home() / ".dotnet/tools"
    os.environ["PATH"] = f"{tools}{os.pathsep}{os.environ['PATH']}"
    run.EVIDENCE = HERE / "final-solution-with-pwsh-path"
    run.EVIDENCE.mkdir()
    before = run.snapshot()
    run.write("before.json", before)
    workspace = scratch / "folders"
    closure: dict = {}
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
    pwsh = shutil.which("pwsh")
    source = ["-p:UseHexalithProjectReferences=true", "-p:UseNuGetDeps=false", "-m:1", "--disable-build-servers"]
    solution = {"path_prepended": str(tools), "pwsh": pwsh, "copied_inputs": len(closure),
                "restore": run.command("source-solution-restore", workspace, ["dotnet", "restore", "Hexalith.Folders.CI.slnx",
                                       "-p:Configuration=Debug", "--configfile", str(config), *source])}
    if solution["restore"] == 0:
        solution["build"] = run.command("source-solution-build", workspace, ["dotnet", "build", "Hexalith.Folders.CI.slnx",
                                        "--configuration", "Debug", "--no-restore", "-warnaserror", *source], timeout=3600)
    after = run.snapshot()
    run.write("after.json", after)
    solution["owning_workspace_unchanged"] = (after["repositories"] == before["repositories"]
                                              and after["protected"] == before["protected"])
    run.write("source-solution.json", solution)
    print(json.dumps(solution), flush=True)
    return 0 if solution.get("build") == 0 and solution["owning_workspace_unchanged"] else 1


if __name__ == "__main__":
    sys.exit(main())
