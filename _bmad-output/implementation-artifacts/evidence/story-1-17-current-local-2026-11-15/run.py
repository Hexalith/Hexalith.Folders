#!/usr/bin/env python3
"""Capture a local-only qualification attempt in run-with-temp's private workspace."""

from __future__ import annotations

import configparser
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import time
import xml.etree.ElementTree as ET


EVIDENCE = Path(__file__).resolve().parent
ROOT = EVIDENCE.parents[3]
COMMANDS: list[dict] = []


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def git(root: Path, *args: str) -> str:
    return subprocess.check_output(["git", "-C", str(root), *args], text=True).strip()


def write(name: str, value: object) -> None:
    (EVIDENCE / name).write_text(json.dumps(value, indent=2, sort_keys=True) + "\n")


def snapshot() -> dict:
    paths = ["."] + subprocess.check_output(
        ["git", "config", "-f", str(ROOT / ".gitmodules"), "--get-regexp", "path"], text=True
    ).splitlines()
    repositories = {}
    for value in paths:
        relative = value.split(" ", 1)[1] if value != "." else value
        root = ROOT / relative
        repositories[relative] = {
            "head": git(root, "rev-parse", "HEAD"),
            "branch": git(root, "branch", "--show-current"),
            "index_entries_sha256": hashlib.sha256(subprocess.check_output(
                ["git", "-C", str(root), "ls-files", "--stage", "-z"])).hexdigest(),
            "tracked_status": git(root, "status", "--porcelain=v1", "--untracked-files=no"),
            "root_gitlink": git(ROOT, "ls-tree", "HEAD", relative) if relative != "." else None,
        }
    protected_paths = subprocess.check_output([
        "git", "-C", str(ROOT), "ls-files", "_bmad-output/planning-artifacts", "docs/contract",
        "docs/governance", "_bmad-output/implementation-artifacts/sprint-status.yaml",
        "eng/story117-platform-qualification-current", ".gitmodules", "global.json", "Directory.*",
        ".github/workflows/release.yml", "tools/release-packages.json", "AGENTS.md", "CLAUDE.md",
        ".github/copilot-instructions.md", "scripts/pd10-v2-story-owned-paths.txt",
        "scripts/pd10-v2-coverage-roots.txt", "scripts/generate-pd10-v2-conformance-set.py",
        "src/Hexalith.Folders.Server/FoldersApiRouting.cs",
    ], text=True).splitlines()
    return {"utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
            "repositories": repositories, "protected": {p: sha(ROOT / p) for p in protected_paths}}


def command(label: str, cwd: Path, arguments: list[str], timeout: int = 600) -> int:
    start = time.time()
    log = EVIDENCE / f"{label}.log"
    print(f"START {label}", flush=True)
    with log.open("w") as output:
        try:
            result = subprocess.run(arguments, cwd=cwd, stdout=output, stderr=subprocess.STDOUT,
                                    timeout=timeout, check=False)
            code = result.returncode
        except subprocess.TimeoutExpired:
            code = 124
            output.write("\nQUALIFICATION: finite command timeout; result is incomplete.\n")
    COMMANDS.append({"label": label, "cwd": str(cwd), "argv": arguments,
                     "started_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(start)),
                     "finished_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                     "elapsed_seconds": round(time.time() - start, 2), "exit_code": code,
                     "log": log.name, "log_sha256": sha(log)})
    write("commands.json", COMMANDS)
    print(f"END {label} exit={code} elapsed={time.time() - start:.1f}s", flush=True)
    return code


def copy_repository(source: Path, target: Path, closure: dict) -> None:
    target.mkdir(parents=True, exist_ok=True)
    files = subprocess.check_output(["git", "-C", str(source), "ls-files", "-z"]).decode().split("\0")
    for relative in files:
        if not relative or relative.startswith(("references/", ".agents/", ".claude/", ".codex/")):
            continue
        original = source / relative
        if not original.is_file():
            continue
        destination = target / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        # Large immutable historical evidence is read-only input, never a build output.
        if original.stat().st_size > 2_000_000 and "/evidence/" in relative:
            destination.symlink_to(original)
        else:
            shutil.copy2(original, destination)
        if original.suffix in {".cs", ".csproj", ".props", ".targets", ".slnx"} or relative in {
            "global.json", "nuget.config", ".editorconfig", ".gitattributes", "Directory.Packages.props"}:
            closure[str(destination.relative_to(target.parents[1] if source != ROOT else target))] = sha(destination)
    # Private Git metadata preserves version/source-link observations; no Git command mutates the originals.
    gitdir = Path(git(source, "rev-parse", "--absolute-git-dir"))
    private = target / ".git"
    (private / "objects" / "info").mkdir(parents=True)
    (private / "objects" / "info" / "alternates").write_text(str(gitdir / "objects") + "\n")
    for name in ("HEAD", "index", "packed-refs", "shallow"):
        if (gitdir / name).is_file():
            shutil.copy2(gitdir / name, private / name)
    if (gitdir / "refs").is_dir():
        shutil.copytree(gitdir / "refs", private / "refs")
    config = configparser.RawConfigParser(strict=False)
    config.read(gitdir / "config")
    if config.has_option("core", "worktree"):
        config.remove_option("core", "worktree")
    with (private / "config").open("w") as output:
        config.write(output)


def main() -> None:
    scratch = Path(os.environ["TMPDIR"])
    workspace = scratch / "folders"
    before = snapshot()
    write("before.json", before)
    for name in ("spec-1-17-complete-pd10-v2-migration-and-closure.md",
                 "story-1-17-current-candidate-technical-readiness-2026-09-24.md"):
        shutil.copy2(ROOT / "_bmad-output/implementation-artifacts" / name, EVIDENCE / (name + ".before"))
    closure = {}
    print("Copying declared source roots to private workspace", flush=True)
    copy_repository(ROOT, workspace, closure)
    for relative in before["repositories"]:
        if relative != ".":
            copy_repository(ROOT / relative, workspace / relative, closure)
    write("copied-source-inputs.json", closure)
    write("after-copy.json", snapshot())
    print(f"Copied {len(closure)} source/build inputs", flush=True)
    feed = scratch / "feed"
    feed.mkdir()
    cache = Path.home() / ".nuget/packages"
    for version_directory in cache.glob("*/*"):
        archives = sorted(version_directory.glob("*.nupkg"))
        if not archives:
            continue
        filename = f"{version_directory.parent.name}.{version_directory.name}.nupkg"
        expected = version_directory / filename
        archive = expected if expected.is_file() else archives[0]
        (feed / filename).symlink_to(archive)
    config = scratch / "nuget.config"
    config.write_text('<configuration><packageSources><clear/><add key="nuget.org" value="' +
                      str(feed) + '"/></packageSources><packageSourceMapping><packageSource key="nuget.org">'
                      '<package pattern="*"/></packageSource></packageSourceMapping></configuration>')
    print(f"Offline feed: {len(list(feed.iterdir()))} cached archives", flush=True)
    command("sdk", workspace, ["dotnet", "--info"])
    release = ["--configuration", "Release", "-p:UseHexalithProjectReferences=false", "-m:1", "-warnaserror", "--disable-build-servers"]
    restore = ["-p:Configuration=Release", "-p:UseHexalithProjectReferences=false", "--configfile", str(config), "-m:1", "--disable-build-servers"]
    command("folders-restore", workspace, ["dotnet", "restore", "Hexalith.Folders.CI.slnx", *restore])
    # Focused assemblies may build even when an unrelated solution component is blocked.
    for project in ("Contracts", "Server", "Integration"):
        name = f"Hexalith.Folders.{project}Tests" if project == "Integration" else f"Hexalith.Folders.{project}.Tests"
        relative = f"tests/{name}/{name}.csproj"
        command(f"{project.lower()}-build", workspace, ["dotnet", "build", relative, *release, "--no-restore"])
    selections = {
        "server-authority": ("Server.Tests", ["Hexalith.Folders.Server.Tests.WorkspaceContentProductionTests"]),
        "routing": ("IntegrationTests", ["Hexalith.Folders.IntegrationTests.Routing.FoldersApiRoutingModeTests"]),
        "contracts-behavior": ("Contracts.Tests", ["Hexalith.Folders.Contracts.Tests.OpenApi.AuthorizationMatrixContractTests",
            "Hexalith.Folders.Contracts.Tests.OpenApi.Pd10V2CandidateContractTests",
            "Hexalith.Folders.Contracts.Tests.OpenApi.ContractSpineCiGateTests"]),
    }
    behavior_results = []
    for label, (suffix, classes) in selections.items():
        assembly = workspace / f"tests/Hexalith.Folders.{suffix}/bin/Release/net11.0/Hexalith.Folders.{suffix}.dll"
        args = ["dotnet", str(assembly), "-noLogo", "-noColor"]
        for name in classes:
            args += ["-class", name]
        behavior_results.append(command(label, workspace, args))
    # Regeneration is scratch-only until the pre-generation behavior gates pass.
    if behavior_results == [0, 0, 0]:
        first = scratch / "candidate-first.yaml"
        second = scratch / "candidate-second.yaml"
        for label, output in (("generate-first", first), ("generate-second", second)):
            command(label, workspace, ["python3", "scripts/generate-pd10-v2-conformance-set.py", "--output", str(output)])
        if first.is_file() and second.is_file() and first.read_bytes() == second.read_bytes():
            shutil.copy2(first, EVIDENCE / "candidate-generated.yaml")
            shutil.copy2(first, workspace / "_bmad-output/implementation-artifacts/evidence/story-12-2-v2-source-requalification-pending-2026-11-12.yaml")
    command("folders-release-build", workspace, ["dotnet", "build", "Hexalith.Folders.CI.slnx", *release, "--no-restore"])
    for project in ("Client", "Cli", "Mcp"):
        name = f"Hexalith.Folders.{project}.Tests"
        command(f"{project.lower()}-build", workspace, ["dotnet", "build", f"tests/{name}/{name}.csproj", *release, "--no-restore"])
    command("conformance", workspace, ["dotnet", "tests/Hexalith.Folders.Contracts.Tests/bin/Release/net11.0/Hexalith.Folders.Contracts.Tests.dll", "-noLogo", "-noColor", "-class", "Hexalith.Folders.Contracts.Tests.OpenApi.Pd10ConformanceSetTests"])
    command("parity", workspace, ["pwsh", "tests/tools/run-contract-parity-ci-gates.ps1", "-SkipRestoreBuild", "-OutputReportPath", str(EVIDENCE / "parity-report.json")])
    command("governance", workspace, ["pwsh", "tests/tools/run-governance-completeness-gates.ps1", "-SkipRestoreBuild"])
    projects = workspace / "references/Hexalith.Projects"
    command("projects-restore", projects, ["dotnet", "restore", "Hexalith.Projects.CI.slnx", *restore])
    command("projects-release-build", projects, ["dotnet", "build", "Hexalith.Projects.CI.slnx", *release, "--no-restore"])
    command("projects-adapters", projects, ["dotnet", "tests/Hexalith.Projects.Server.Tests/bin/Release/net11.0/Hexalith.Projects.Server.Tests.dll", "-noLogo", "-noColor", "-class", "Hexalith.Projects.Server.Tests.ProjectFolderDirectoryTests", "-class", "Hexalith.Projects.Server.Tests.ProjectFileReferenceDirectoryTests", "-class", "Hexalith.Projects.Server.Tests.DelegatedProjectDirectoryRegistrationTests"])
    for relative in ("src/Hexalith.Folders.Server/obj/project.assets.json", "references/Hexalith.Projects/src/Hexalith.Projects.Server/obj/project.assets.json"):
        path = workspace / relative
        if path.is_file():
            data = json.loads(path.read_text())
            write(("projects" if relative.startswith("references") else "folders") + "-resolved-packages.json", {
                "assets_sha256": sha(path), "libraries": data.get("libraries"), "frameworks": data.get("project", {}).get("frameworks")})
    write("after.json", snapshot())
    print("Private qualification complete; wrapper removes all scratch outputs", flush=True)


if __name__ == "__main__":
    main()
