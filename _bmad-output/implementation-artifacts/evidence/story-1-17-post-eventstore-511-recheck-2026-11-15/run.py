#!/usr/bin/env python3
"""Story 1.17 local recheck after the EventStore 5.1.1 publication.

Run only as ``run-with-temp python3 run.py``. Every build, restore, test and
generation runs inside a private copy beneath run-with-temp's TMPDIR; the
owning checkouts, their Git indexes and the user package cache are only read.
Restores use a local feed of already cached archives, so no network source is
contacted. Results are written next to this script.
"""

from __future__ import annotations

import configparser
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import xml.etree.ElementTree as ET


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
EVIDENCE = HERE
PENDING = "_bmad-output/implementation-artifacts/evidence/story-12-2-v2-source-requalification-pending-2026-11-12.yaml"
COMMANDS: list[dict] = []
PROTECTED_PATTERNS = [
    "_bmad-output/planning-artifacts", "docs/contract", "docs/governance", "docs/runbooks",
    "_bmad-output/implementation-artifacts/sprint-status.yaml",
    "_bmad-output/implementation-artifacts/story-1-17-first-install-decision-2026-10-18.md",
    "_bmad-output/implementation-artifacts/story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md",
    "_bmad-output/implementation-artifacts/story-1-17-publication-handoff-2026-10-25.json",
    "_bmad-output/implementation-artifacts/story-1-17-publication-handoff-2026-11-15.json",
    "_bmad-output/implementation-artifacts/evidence/story-12-2-v2-source-requalification-pending.yaml",
    PENDING,
    "eng/story117-platform-qualification-current", ".gitmodules", "global.json", "Directory.*",
    ".github/workflows", "tools/release-packages.json", "AGENTS.md", "CLAUDE.md",
    ".github/copilot-instructions.md", "scripts", "src/Hexalith.Folders.Server/FoldersApiRouting.cs",
    "src/Hexalith.Folders.Server/FoldersServerHostComposition.cs",
    "src/Hexalith.Folders.Server/Pd10V2CandidateCompatibilitySeam.cs",
]


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def utc(moment: float | None = None) -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(moment))


def git(root: Path, *args: str) -> str:
    return subprocess.check_output(["git", "-C", str(root), *args], text=True).strip()


def write(name: str, value: object) -> None:
    (EVIDENCE / name).write_text(json.dumps(value, indent=2, sort_keys=True) + "\n")


def declared_paths() -> list[str]:
    lines = subprocess.check_output(
        ["git", "config", "-f", str(ROOT / ".gitmodules"), "--get-regexp", "path"], text=True).splitlines()
    return ["."] + [line.split(" ", 1)[1] for line in lines]


def snapshot() -> dict:
    repositories = {}
    for relative in declared_paths():
        root = ROOT / relative
        changed = subprocess.check_output(
            ["git", "-C", str(root), "diff", "HEAD", "--name-only", "--ignore-submodules=all"], text=True).split()
        repositories[relative] = {
            "head": git(root, "rev-parse", "HEAD"),
            "branch": git(root, "branch", "--show-current"),
            "index_entries_sha256": hashlib.sha256(subprocess.check_output(
                ["git", "-C", str(root), "ls-files", "--stage", "-z"])).hexdigest(),
            "staged": git(root, "diff", "--cached", "--name-only"),
            "changed_files": {name: sha(root / name) for name in changed if (root / name).is_file()},
            "root_gitlink": git(ROOT, "ls-tree", "HEAD", relative) if relative != "." else None,
        }
    protected = subprocess.check_output(["git", "-C", str(ROOT), "ls-files", *PROTECTED_PATTERNS],
                                        text=True).splitlines()
    return {"utc": utc(), "repositories": repositories,
            "protected": {path: sha(ROOT / path) for path in protected if (ROOT / path).is_file()}}


def command(label: str, cwd: Path, arguments: list[str], timeout: int = 2400) -> int:
    start = time.time()
    log = EVIDENCE / f"{label}.log"
    if log.exists():
        raise SystemExit(f"Refusing to overwrite existing evidence {log.name}")
    print(f"START {label}", flush=True)
    with log.open("w") as output:
        try:
            code = subprocess.run(arguments, cwd=cwd, stdout=output, stderr=subprocess.STDOUT,
                                  timeout=timeout, check=False).returncode
        except subprocess.TimeoutExpired:
            code = 124
            output.write("\nQUALIFICATION: finite command timeout; result is incomplete.\n")
    COMMANDS.append({"label": label, "cwd": str(cwd), "argv": arguments, "started_utc": utc(start),
                     "finished_utc": utc(), "elapsed_seconds": round(time.time() - start, 2),
                     "exit_code": code, "log": log.name, "log_sha256": sha(log)})
    write("commands.json", COMMANDS)
    print(f"END {label} exit={code} elapsed={time.time() - start:.1f}s", flush=True)
    return code


def copy_repository(source: Path, target: Path, closure: dict, base: Path) -> None:
    target.mkdir(parents=True, exist_ok=True)
    files = subprocess.check_output(["git", "-C", str(source), "ls-files", "-z"]).decode().split("\0")
    for relative in files:
        if not relative or relative.startswith(("references/", ".agents/", ".claude/", ".codex/")):
            continue
        original = source / relative
        if not original.is_file() or original.is_symlink():
            continue
        destination = target / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        if original.stat().st_size > 2_000_000 and "/evidence/" in relative:
            destination.symlink_to(original)
        else:
            shutil.copy2(original, destination)
        if original.suffix in {".cs", ".csproj", ".props", ".targets", ".slnx", ".json", ".yaml", ".py", ".ps1"}:
            closure[str(destination.relative_to(base))] = sha(destination)
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


def counts(xml_path: Path, selectors: list[str]) -> dict:
    tree = ET.parse(xml_path)
    per_selector = {selector: {"passed": 0, "failed": 0, "skipped": 0, "other": 0} for selector in selectors}
    for test in tree.iter("test"):
        owner = test.get("type", "")
        result = test.get("result", "")
        for selector in selectors:
            if owner == selector:
                key = {"Pass": "passed", "Fail": "failed", "Skip": "skipped"}.get(result, "other")
                per_selector[selector][key] += 1
    totals = {key: sum(value[key] for value in per_selector.values()) for key in ("passed", "failed", "skipped", "other")}
    ok = all(value["passed"] > 0 and value["failed"] == 0 and value["skipped"] == 0 and value["other"] == 0
             for value in per_selector.values())
    return {"selectors": per_selector, "totals": totals, "every_selector_passed_unskipped": ok,
            "xml_sha256": sha(xml_path)}


def run_tests(label: str, workspace: Path, assembly: str, selectors: list[str]) -> bool:
    xml_path = EVIDENCE / f"{label}.xml"
    args = ["dotnet", str(workspace / assembly), "-noLogo", "-noColor"]
    for selector in selectors:
        args += ["-class", selector]
    args += ["-result-xml", str(xml_path)]
    code = command(label, workspace, args)
    summary = {"exit_code": code, **(counts(xml_path, selectors) if xml_path.is_file() else {"xml": "absent"})}
    write(f"{label}-counts.json", summary)
    return code == 0 and summary.get("every_selector_passed_unskipped") is True


def main() -> int:
    global EVIDENCE
    if len(sys.argv) not in (2, 3) or not sys.argv[1].replace("-", "").isalnum():
        raise SystemExit("Usage: run-with-temp python3 -I run.py OUTPUT_SUBDIRECTORY [PRIVATE_COPY_PATCH]")
    EVIDENCE = HERE / sys.argv[1]
    EVIDENCE.mkdir()
    patch = Path(sys.argv[2]).resolve() if len(sys.argv) == 3 else None
    scratch = Path(os.environ["TMPDIR"])
    if not str(scratch).startswith(("/tmp/", "/tmp/claude")) or "hexalith-job." not in str(scratch):
        raise SystemExit("Run under run-with-temp")
    before = snapshot()
    write("before.json", before)
    for name in ("spec-1-17-complete-pd10-v2-migration-and-closure.md",
                 "story-1-17-current-candidate-technical-readiness-2026-09-24.md",
                 "story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md"):
        shutil.copy2(ROOT / "_bmad-output/implementation-artifacts" / name, EVIDENCE / (name + ".before"))
    workspace = scratch / "folders"
    closure: dict = {}
    print("Copying root-declared repositories into the private workspace", flush=True)
    copy_repository(ROOT, workspace, closure, workspace)
    for relative in before["repositories"]:
        if relative != ".":
            copy_repository(ROOT / relative, workspace / relative, closure, workspace)
    write("copied-source-inputs.json", closure)
    after_copy = snapshot()
    write("after-copy.json", after_copy)
    if after_copy["repositories"] != before["repositories"] or after_copy["protected"] != before["protected"]:
        print("Source drift during copy; stopping", flush=True)
        return 3
    print(f"Copied {len(closure)} source/build inputs", flush=True)
    if patch is not None:
        # Trial only: the patch changes the private copy, never an owning checkout.
        shutil.copy2(patch, EVIDENCE / "private-copy.patch")
        if command("private-copy-patch", workspace, ["git", "apply", "--verbose", str(patch)]) != 0:
            return 4

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
    write("feed.json", {"cached_archives": len(list(feed.iterdir())), "network_sources": 0})
    command("sdk", workspace, ["dotnet", "--info"])

    source = ["-p:UseHexalithProjectReferences=true", "-p:UseNuGetDeps=false", "-m:1", "--disable-build-servers"]
    built = {}
    for name in ("Hexalith.Folders.Server.Tests", "Hexalith.Folders.IntegrationTests", "Hexalith.Folders.Contracts.Tests"):
        project = f"tests/{name}/{name}.csproj"
        short = name.removeprefix("Hexalith.Folders.").lower().replace(".", "-")
        restored = command(f"source-{short}-restore", workspace, ["dotnet", "restore", project, "-p:Configuration=Debug",
                                                                   "--configfile", str(config), *source]) == 0
        built[name] = restored and command(f"source-{short}-build", workspace, ["dotnet", "build", project,
                                           "--configuration", "Debug", "--no-restore", "-warnaserror", *source]) == 0
    write("source-builds.json", built)

    results = {}
    if built["Hexalith.Folders.Server.Tests"]:
        results["native-authority"] = run_tests("source-native-authority", workspace,
            "tests/Hexalith.Folders.Server.Tests/bin/Debug/net11.0/Hexalith.Folders.Server.Tests.dll", [
                "Hexalith.Folders.Server.Tests.NativeSourceHeadReaderTests",
                "Hexalith.Folders.Server.Tests.FoldersNativeGatewayBearerTests",
                "Hexalith.Folders.Server.Tests.WorkspaceContentProductionTests",
                "Hexalith.Folders.Server.Tests.WorkspaceContentRejectionMappingTests",
                "Hexalith.Folders.Server.Tests.WorkspaceContentErasureGrantTests"])
    if built["Hexalith.Folders.IntegrationTests"]:
        results["routing"] = run_tests("source-routing", workspace,
            "tests/Hexalith.Folders.IntegrationTests/bin/Debug/net11.0/Hexalith.Folders.IntegrationTests.dll", [
                "Hexalith.Folders.IntegrationTests.Routing.FoldersApiRoutingModeTests",
                "Hexalith.Folders.IntegrationTests.WorkspaceContentProcessWiringTests"])
    if built["Hexalith.Folders.Contracts.Tests"]:
        results["contract-behavior"] = run_tests("source-contract-behavior", workspace,
            "tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net11.0/Hexalith.Folders.Contracts.Tests.dll", [
                "Hexalith.Folders.Contracts.Tests.OpenApi.AuthorizationMatrixContractTests",
                "Hexalith.Folders.Contracts.Tests.OpenApi.Pd10V2CandidateContractTests",
                "Hexalith.Folders.Contracts.Tests.OpenApi.ContractSpineCiGateTests"])
        results["conformance"] = run_tests("source-conformance", workspace,
            "tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net11.0/Hexalith.Folders.Contracts.Tests.dll", [
                "Hexalith.Folders.Contracts.Tests.OpenApi.Pd10ConformanceSetTests"])
    pre_generation = ("native-authority", "routing", "contract-behavior", "conformance")
    if built["Hexalith.Folders.Contracts.Tests"]:
        # These classes read Directory.Build.targets and therefore cover the build-configuration edit.
        results["deployment-conformance"] = run_tests("source-deployment-conformance", workspace,
            "tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net11.0/Hexalith.Folders.Contracts.Tests.dll", [
                "Hexalith.Folders.Contracts.Tests.Deployment.ContainerImageConformanceTests",
                "Hexalith.Folders.Contracts.Tests.Deployment.ReleasePackageConformanceTests"])
    write("source-results.json", results)

    generation = {"ran": False}
    if all(results.get(name) is True for name in pre_generation):
        first, second = scratch / "first.yaml", scratch / "second.yaml"
        codes = [command("generate-first", workspace, ["python3", "scripts/generate-pd10-v2-conformance-set.py", "--output", str(first)]),
                 command("generate-second", workspace, ["python3", "scripts/generate-pd10-v2-conformance-set.py", "--output", str(second)])]
        generation = {"ran": True, "exit_codes": codes}
        if first.is_file() and second.is_file():
            shutil.copy2(first, EVIDENCE / "candidate-generated-first.yaml")
            shutil.copy2(second, EVIDENCE / "candidate-generated-second.yaml")
            generation.update({"first_sha256": sha(first), "second_sha256": sha(second),
                               "checked_in_sha256": sha(ROOT / PENDING),
                               "generations_equal": first.read_bytes() == second.read_bytes(),
                               "equals_checked_in": first.read_bytes() == (ROOT / PENDING).read_bytes()})
    write("generation.json", generation)

    # Broad source-profile gate after the focused gates; its result never changes their outcomes.
    solution = {"restore": command("source-solution-restore", workspace, ["dotnet", "restore", "Hexalith.Folders.CI.slnx",
                                   "-p:Configuration=Debug", "--configfile", str(config), *source])}
    if solution["restore"] == 0:
        solution["build"] = command("source-solution-build", workspace, ["dotnet", "build", "Hexalith.Folders.CI.slnx",
                                    "--configuration", "Debug", "--no-restore", "-warnaserror", *source], timeout=3600)
    write("source-solution.json", solution)

    runtime = []
    for relative in ("tests/Hexalith.Folders.Server.Tests/bin/Debug/net11.0",
                     "tests/Hexalith.Folders.IntegrationTests/bin/Debug/net11.0"):
        directory = workspace / relative
        for name in ("Hexalith.EventStore.Client.dll", "Hexalith.Folders.Server.dll",
                     "Hexalith.Folders.Client.dll", "Hexalith.Folders.Contracts.dll"):
            path = directory / name
            if path.is_file():
                runtime.append({"directory": relative, "file": name, "sha256": sha(path), "bytes": path.stat().st_size})
    write("source-runtime-artifacts.json", runtime)
    after = snapshot()
    write("after.json", after)
    unchanged = after["repositories"] == before["repositories"] and after["protected"] == before["protected"]
    write("run-summary.json", {"started_utc": before["utc"], "finished_utc": after["utc"], "copied_inputs": len(closure),
                               "source_builds": built, "source_results": results, "generation": generation,
                               "source_solution": solution, "owning_workspace_unchanged": unchanged})
    print(json.dumps({"builds": built, "results": results, "generation": generation, "solution": solution,
                      "unchanged": unchanged}), flush=True)
    focused = all(built.values()) and len(results) == 5 and all(results.values())
    return 0 if unchanged and focused and generation.get("equals_checked_in") and solution.get("build") == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
