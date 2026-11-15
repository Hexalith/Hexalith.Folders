#!/usr/bin/env python3
"""Prepare private Story 1.17 packages and retain honest, isolated qualification evidence.

Run through run-with-temp. This runner never publishes, stages, commits, pushes,
initializes submodules, updates trackers, or changes the owning source checkout.
Use a new --evidence-name for each run; source snapshots include tracked edits.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import signal
import shutil
import subprocess
import tarfile
import time
import traceback
import zipfile
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[2]
EVIDENCE_PARENT = Path("_bmad-output/implementation-artifacts/evidence")
VERSION = "5.1.1"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def git(source: Path, *args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=source, text=True).strip()


class Job:
    def __init__(self, args: argparse.Namespace):
        self.args = args
        self.scratch = Path(os.environ["TMPDIR"]).resolve()
        if self.scratch == Path("/tmp") or self.scratch == Path("/"):
            raise ValueError("Use run-with-temp to provide a private TMPDIR.")
        self.env = os.environ.copy()
        self.env.update({"NUGET_PACKAGES": str(self.scratch / "packages"),
                         "NUGET_HTTP_CACHE_PATH": str(self.scratch / "http-cache"),
                         "DOTNET_CLI_HOME": str(self.scratch / "dotnet-home"),
                         "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
                         "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
                         "DOTNET_CLI_USE_MSBUILD_SERVER": "0",
                         "UseSharedCompilation": "false",
                         "MSBUILDDISABLENODEREUSE": "1"})
        self.workspace = self.scratch / "folders"
        self.commands: dict[str, list[dict]] = {}
        write(self.evidence("folders") / (args.phase + "-runner.json"), {
            "path": str(Path(__file__).resolve()), "sha256": sha(Path(__file__)),
            "argv": __import__("sys").argv, "private_tmpdir": str(self.scratch)})
        shutil.copyfile(Path(__file__), self.evidence("folders") / (args.phase + "-runner.py"))

    def evidence(self, owner: str) -> Path:
        source = ROOT if owner == "folders" else ROOT / "references" / owner
        path = source / EVIDENCE_PARENT / self.args.evidence_name
        path.mkdir(parents=True, exist_ok=True)
        attributes = path / ".gitattributes"
        if not attributes.exists():
            attributes.write_text("* -text -whitespace\n", encoding="utf-8")
        return path

    def run(self, owner: str, label: str, argv: list[str], cwd: Path, *,
            timeout: int = 1800, extra_env: dict[str, str] | None = None) -> int:
        evidence = self.evidence(owner)
        logfile = evidence / (label + ".log")
        if logfile.exists():
            raise FileExistsError(f"Refusing to overwrite previous evidence: {logfile}")
        env = self.env | (extra_env or {})
        start = time.time()
        print(f"START {owner}/{label}", flush=True)
        with logfile.open("wb") as log:
            log.write((json.dumps(argv) + "\n").encode())
            log.flush()
            process = subprocess.Popen(argv, cwd=cwd, env=env, stdout=log, stderr=subprocess.STDOUT,
                                       start_new_session=True)
            try:
                result = process.wait(timeout=timeout)
            except subprocess.TimeoutExpired:
                # Every command has its own process group. Never signal another
                # workspace job; wait for this command before scratch cleanup.
                os.killpg(process.pid, signal.SIGTERM)
                try:
                    process.wait(timeout=30)
                except subprocess.TimeoutExpired:
                    os.killpg(process.pid, signal.SIGKILL)
                    process.wait()
                # The group leader may exit before a noncooperative child.
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                result = 124
                log.write(f"\nTIMEOUT after {timeout} seconds\n".encode())
            finally:
                # Also cover interruption and any command-owned background worker.
                # The process group was created by this Popen, never by another job.
                try:
                    os.killpg(process.pid, signal.SIGTERM)
                except ProcessLookupError:
                    pass
                if process.poll() is None:
                    try:
                        process.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        os.killpg(process.pid, signal.SIGKILL)
                        process.wait()
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
        row = {"label": label, "argv": argv, "cwd": str(cwd), "exit_code": result,
               "started_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(start)),
               "elapsed_seconds": round(time.time() - start, 2), "log": logfile.name,
               "log_sha256": sha(logfile), "environment": {key: env[key] for key in
                   ("NUGET_PACKAGES", "NUGET_HTTP_CACHE_PATH", "DOTNET_CLI_HOME", "RestoreConfigFile",
                    "IncludeSymbols", "SymbolPackageFormat", "HexalithEventStoreVersion", "UseSharedCompilation",
                    "DOTNET_CLI_USE_MSBUILD_SERVER", "MSBUILDDISABLENODEREUSE") if key in env}}
        self.commands.setdefault(owner, []).append(row)
        write(evidence / (self.args.phase + "-commands.json"), self.commands[owner])
        print(f"DONE {owner}/{label} exit={result} seconds={row['elapsed_seconds']}", flush=True)
        return result

    def snapshot(self, owner: str, target: Path, *, evidence_owner: str | None = None) -> Path:
        source = ROOT if owner == "folders" else ROOT / "references" / owner
        target.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run(["git", "clone", "--shared", "--no-hardlinks", "--no-recurse-submodules",
                        "--quiet", str(source), str(target)], check=True)
        changed = subprocess.check_output(["git", "diff", "HEAD", "--name-only", "-z"], cwd=source).decode().split("\0")
        for relative in filter(None, changed):
            original, copied = source / relative, target / relative
            if original.is_file():
                copied.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(original, copied)
            elif not original.exists() and copied.is_file():
                copied.unlink()
        evidence = self.evidence(evidence_owner or owner)
        if evidence_owner:
            evidence = evidence / "snapshots" / owner
            evidence.mkdir(parents=True, exist_ok=True)
        patch = evidence / (self.args.phase + "-source.patch")
        patch.write_bytes(subprocess.check_output(["git", "diff", "HEAD", "--binary"], cwd=source))
        inputs = {}
        for relative in subprocess.check_output(["git", "ls-files", "-z"], cwd=source).decode().split("\0"):
            path = source / relative
            if relative and path.is_file():
                inputs[relative] = sha(path)
        write(evidence / (self.args.phase + "-source.json"), {
            "source": str(source), "commit": git(source, "rev-parse", "HEAD"),
            "tree": git(source, "rev-parse", "HEAD^{tree}"), "tracked_worktree_files": inputs,
            "patch": patch.name, "patch_sha256": sha(patch), "snapshot": str(target),
            "submodules_initialized": False})
        return target

    def config(self, *feeds: Path) -> Path:
        import xml.etree.ElementTree as ET
        config = ET.Element("configuration")
        sources = ET.SubElement(config, "packageSources")
        ET.SubElement(sources, "clear")
        for ordinal, feed in enumerate(feeds):
            ET.SubElement(sources, "add", key=f"private-{ordinal}", value=str(feed))
        ET.SubElement(sources, "add", key="nuget.org", value="https://api.nuget.org/v3/index.json")
        audits = ET.SubElement(config, "auditSources")
        ET.SubElement(audits, "clear")
        ET.SubElement(audits, "add", key="nuget.org", value="https://api.nuget.org/v3/index.json")
        path = self.scratch / "NuGet.Config"
        ET.ElementTree(config).write(path, encoding="utf-8", xml_declaration=True)
        for owner in ["folders", "Hexalith.EventStore", "Hexalith.Projects", "Hexalith.Builds"]:
            shutil.copyfile(path, self.evidence(owner) / (self.args.phase + "-NuGet.Config"))
        return path

    def build(self, owner: str, label: str, project: str, cwd: Path, *, restore: bool = True,
              config: Path | None = None) -> int:
        if restore:
            argv = ["dotnet", "restore", project, "-p:Configuration=Release",
                    "-p:UseHexalithProjectReferences=false", "-m:1", "--disable-build-servers"]
            if config:
                argv += ["--configfile", str(config)]
            self.run(owner, label + "-restore", argv, cwd)
        return self.run(owner, label + "-build", ["dotnet", "build", project, "--configuration", "Release",
            "-p:UseHexalithProjectReferences=false", "-m:1", "-warnaserror", "--disable-build-servers", "--no-restore"], cwd)

    def tests(self, owner: str, label: str, project: str, classes: list[str], cwd: Path) -> int:
        assembly = cwd / "tests" / project / "bin/Release/net11.0" / (project + ".dll")
        if not assembly.is_file():
            write(self.evidence(owner) / (label + "-blocked.json"), {"status": "blocked",
                  "reason": "Fresh test assembly does not exist; previous binaries were not substituted.",
                  "assembly": str(assembly), "classes": classes})
            return 125
        argv = ["dotnet", str(assembly), "-noLogo", "-noColor"]
        for name in classes:
            argv += ["-class", name]
        argv += ["-result-xml", str(self.evidence(owner) / (label + ".xml"))]
        return self.run(owner, label, argv, cwd)

    def retain_runtime(self, owner: str, cwd: Path, projects: list[str]) -> None:
        evidence = self.evidence(owner)
        inventory = {}
        for project in projects:
            directory = cwd / "tests" / project / "bin/Release/net11.0"
            if not directory.is_dir():
                continue
            for path in sorted(directory.rglob("*")):
                if path.is_file():
                    inventory[str(path.relative_to(cwd))] = sha(path)
            archive = evidence / (project + "-runtime.tar.gz")
            with tarfile.open(archive, "w:gz") as tar:
                tar.add(directory, arcname=project)
            inventory[archive.name] = sha(archive)
        write(evidence / "runtime-artifacts.json", inventory)

    def verify_public_hexalith(self, owner: str, cwd: Path, module) -> None:
        """Authenticate every selected public Hexalith archive, not a cache label."""
        selected = {}
        for assets in cwd.rglob("obj/project.assets.json"):
            if "references" in assets.relative_to(cwd).parts:
                continue
            data = json.loads(assets.read_text())
            for identity, library in data.get("libraries", {}).items():
                if library.get("type") != "package" or not identity.startswith("Hexalith."):
                    continue
                if identity.startswith("Hexalith.EventStore.") and identity.endswith("/" + VERSION):
                    continue
                path = Path(self.env["NUGET_PACKAGES"]) / library["path"] / (identity.replace("/", ".").lower() + ".nupkg")
                if not path.is_file():
                    raise ValueError(f"Missing restored public archive: {identity}: {path}")
                selected[identity] = path
        directory = self.evidence(owner) / "verified-public-dependencies"
        directory.mkdir()
        rows = []
        write(self.evidence(owner) / "verified-public-dependencies.json", rows)
        for identity, restored in sorted(selected.items()):
            package_id, version = identity.rsplit("/", 1)
            record = module.official_archive(package_id, version, directory / f"{package_id}.{version}.nupkg", restored)
            if record["sha256"] != sha(restored):
                raise ValueError(f"Restored public archive differs from NuGet authority: {identity}")
            record["restored_archive_sha256"] = sha(restored)
            record["restored_archive"] = str(restored)
            rows.append(record)
            write(self.evidence(owner) / "verified-public-dependencies.json", rows)

    def assert_runtime_packages(self, owner: str, cwd: Path, projects: list[str], archives: list[Path]) -> None:
        members = {}
        for path in archives:
            with zipfile.ZipFile(path) as archive:
                for member in archive.namelist():
                    if member.startswith("lib/net11.0/") and member.endswith(".dll"):
                        filename = Path(member).name
                        row = {"archive": str(path), "archive_sha256": sha(path), "member": member,
                               "assembly_sha256": hashlib.sha256(archive.read(member)).hexdigest()}
                        if filename in members and members[filename]["assembly_sha256"] != row["assembly_sha256"]:
                            raise ValueError(f"Ambiguous package runtime assembly: {filename}")
                        members[filename] = row
        rows = []
        module = "Folders" if owner == "folders" else "Projects"
        required = [f"Hexalith.{module}.{name}.dll" for name in ["Server", "Client", "Contracts"]]
        required += [f"Hexalith.EventStore.{name}.dll" for name in ["Server", "Client", "Contracts"]]
        if owner == "Hexalith.Projects":
            required += [f"Hexalith.{family}.{name}.dll" for family in ["Folders", "Conversations"] for name in ["Client", "Contracts"]]
        for project in projects:
            directory = cwd / "tests" / project / "bin/Release/net11.0"
            if project == f"Hexalith.{module}.Server.Tests":
                for filename in [project + ".dll", *required]:
                    if not (directory / filename).is_file():
                        raise ValueError(f"Missing required fresh runtime artifact: {directory / filename}")
            for path in sorted(directory.glob("*.dll")):
                if path.name not in members:
                    continue
                row = members[path.name] | {"runtime_path": str(path.relative_to(cwd)), "runtime_sha256": sha(path)}
                row["matches_package_member"] = row["runtime_sha256"] == row["assembly_sha256"]
                if not row["matches_package_member"]:
                    raise ValueError(f"Runtime assembly differs from selected package: {path}")
                rows.append(row)
        write(self.evidence(owner) / "runtime-package-bindings.json", rows)


def producer(job: Job) -> None:
    event = job.snapshot("Hexalith.EventStore", job.workspace / "references/Hexalith.EventStore")
    job.snapshot("Hexalith.Builds", job.workspace / "references/Hexalith.Builds")
    for dependency in ["Hexalith.Tenants", "Hexalith.Commons"]:
        job.snapshot(dependency, job.workspace / "references" / dependency, evidence_owner="Hexalith.EventStore")
    config = job.config()
    # The packer restores its own projects: explicitly supply the same authorized
    # sources through the MSBuild environment, retaining normal audit and gates.
    job.env["RestoreConfigFile"] = str(config)
    # Producer compilation retains the published coherent dependency cohort. The
    # packer's explicit global Version stamps the new fourteen-family candidate.
    # Consumers independently select 5.1.1 from its exact private feed afterwards.
    job.env["HexalithEventStoreVersion"] = "5.1.0"
    evidence = job.evidence("Hexalith.EventStore")
    triage_origin = ROOT / "references/Hexalith.EventStore" / EVIDENCE_PARENT / "story-1-17-release-prerequisites-2026-11-15"
    for filename in ["source-ci-triage.json", "source-ci.json", "source-ci-job.log"]:
        original = triage_origin / filename
        if original.is_file() and evidence != triage_origin:
            shutil.copyfile(original, evidence / filename)
    write(evidence / "source-ci-triage-origin.json", {"path": str(triage_origin),
          "triage_sha256": sha(triage_origin / "source-ci-triage.json")})
    job.run("Hexalith.EventStore", "sdk", ["dotnet", "--info"], event)
    tool = ["dotnet", "run", "tools/routing-catalog.cs", "-c", "Release",
            "-p:UseHexalithProjectReferences=false", "-p:UseNuGetDeps=true", "--"]
    golden = "deploy/dapr/eventstore-routing-catalog.json"
    if job.run("Hexalith.EventStore", "development-catalog-regenerate", [*tool, "development", ".", golden], event) == 0:
        output = evidence / "generated" / golden
        output.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(event / golden, output)
        job.run("Hexalith.EventStore", "development-catalog-check", [*tool, "check-development", ".", golden], event)
        job.run("Hexalith.EventStore", "development-runtime-boundary", [*tool, "check-development-runtime", "."], event)
    (evidence / "prepared-source.patch").write_bytes(subprocess.check_output(["git", "diff", "HEAD", "--binary"], cwd=event))
    with tarfile.open(evidence / "prepared-source-inputs.tar.gz", "w:gz") as tar:
        for relative in subprocess.check_output(["git", "ls-files", "-z"], cwd=event).decode().split("\0"):
            path = event / relative
            if relative and path.is_file() and not relative.startswith(("_bmad-output/", ".hexalith/")):
                tar.add(path, arcname=relative, recursive=False)
    projects = ["Hexalith.EventStore.Client.Tests", "Hexalith.EventStore.Server.Tests"]
    for project in projects:
        label = "client" if ".Client." in project else "server"
        if job.build("Hexalith.EventStore", label, f"tests/{project}/{project}.csproj", event, config=config) == 0:
            classes = (["Hexalith.EventStore.Client.Tests.Streams.*", "Hexalith.EventStore.Client.Tests.Routing.DevelopmentRouteCatalogBuilderTests"]
                if label == "client" else ["Hexalith.EventStore.Server.Tests.Controllers.NativeSourceScannerAuthorizationTests",
                    "Hexalith.EventStore.Server.Tests.Controllers.SyntheticOriginalSourceReceiverTests",
                    "Hexalith.EventStore.Server.Tests.Streams.*", "Hexalith.EventStore.Server.Tests.Routing.TechnicalHttp*",
                    "Hexalith.EventStore.Server.Tests.Routing.HeldMetadataDispatchTests",
                    "Hexalith.EventStore.Server.Tests.Routing.DevelopmentGatewayIdentityTests",
                    "Hexalith.EventStore.Server.Tests.Routing.PublishedTenantQuerySelectorAbiTests",
                    "Hexalith.EventStore.Server.Tests.DomainServices.DevelopmentRouteCatalogParityTests"])
            job.tests("Hexalith.EventStore", label + "-native-and-affected", project, classes, event)
    output = job.scratch / "candidate-packages"
    job.run("Hexalith.EventStore", "pack-all-fourteen", ["python3", "tools/pack-release-packages.py", str(output), VERSION], event,
            extra_env={"IncludeSymbols": "true", "SymbolPackageFormat": "snupkg"}, timeout=3600)
    job.run("Hexalith.EventStore", "validate-all-fourteen", ["python3", "tools/validate-release-packages.py", str(output), VERSION], event)
    durable = evidence / "candidate-packages"
    durable.mkdir(exist_ok=False)
    if output.is_dir():
        for path in output.iterdir():
            if path.is_file():
                shutil.copyfile(path, durable / path.name)
    write(evidence / "candidate-inventory.json", {path.name: {"sha256": sha(path), "size": path.stat().st_size}
          for path in sorted(durable.iterdir())})
    api = job.scratch / "api-compatibility"
    job.run("Hexalith.EventStore", "api-all-fourteen", ["python3", "tools/verify-release-api-compatibility.py", str(output), VERSION,
        "--candidate-commit", git(event, "rev-parse", "HEAD"), "--package-catalog",
        str(job.workspace / "references/Hexalith.Builds/Props/Directory.Packages.props"),
        "--work-directory", str(api)], event, timeout=7200)
    if api.is_dir():
        shutil.copytree(api, evidence / "api-compatibility", ignore=shutil.ignore_patterns("scratch"))
    import sys
    sys.path.insert(0, str(event / "tools"))
    spec = importlib.util.spec_from_file_location("release_api", event / "tools/verify-release-api-compatibility.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    job.verify_public_hexalith("Hexalith.EventStore", event, module)
    job.retain_runtime("Hexalith.EventStore", event, projects)


def consumers(job: Job) -> None:
    folders = job.snapshot("folders", job.workspace)
    builds = job.snapshot("Hexalith.Builds", folders / "references/Hexalith.Builds")
    projects = job.snapshot("Hexalith.Projects", folders / "references/Hexalith.Projects")
    job.snapshot("Hexalith.EventStore", folders / "references/Hexalith.EventStore")
    # Preserve the actual umbrella layout for resource declarations and source
    # inspection tests, without initializing any nested submodule.
    for relative in git(ROOT, "config", "-f", ".gitmodules", "--get-regexp", "path").splitlines():
        path = relative.split(" ", 1)[1]
        dependency = Path(path).name
        if dependency not in {"Hexalith.Builds", "Hexalith.Projects", "Hexalith.EventStore"}:
            job.snapshot(dependency, folders / path, evidence_owner="folders")
    candidate = ROOT / "references/Hexalith.EventStore" / EVIDENCE_PARENT / job.args.producer_evidence / "candidate-packages"
    private = job.scratch / "private-feed"
    shutil.copytree(candidate, private)
    published = job.evidence("Hexalith.Projects") / "published-packages"
    published.mkdir(exist_ok=False)
    # Reuse the owner API gate's authenticated NuGet download lane: official
    # catalog SHA512, nuspec source binding, and dotnet nuget verify --all.
    verifier = folders / "references/Hexalith.EventStore/tools/verify-release-api-compatibility.py"
    import sys
    sys.path.insert(0, str(verifier.parent))
    spec = importlib.util.spec_from_file_location("release_api", verifier)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    provenance = []
    for family, version, source in [("Conversations", "2.0.0", "bd6265afe8aee2e555b637bd973653331c284c66"),
                                    ("Folders", "1.4.0", None)]:
        module.REPOSITORY_URL = f"https://github.com/Hexalith/Hexalith.{family}"
        for suffix in ["Client", "Contracts"]:
            identity = f"Hexalith.{family}.{suffix}"
            destination = published / f"{identity}.{version}.nupkg"
            row = module.official_archive(identity, version, destination, source=source)
            import xml.etree.ElementTree as ET
            with zipfile.ZipFile(destination) as archive:
                nuspec = next(name for name in archive.namelist() if name.endswith(".nuspec"))
                repository = ET.fromstring(archive.read(nuspec)).find("{*}metadata/{*}repository")
                if repository is None or repository.get("url") != module.REPOSITORY_URL:
                    raise ValueError(f"Unexpected published repository: {identity}")
                row["repository"] = repository.attrib
                row["runtime_libraries"] = {name: hashlib.sha256(archive.read(name)).hexdigest()
                    for name in archive.namelist() if name.endswith(".dll")}
            provenance.append(row)
            write(job.evidence("Hexalith.Projects") / "published-package-provenance.json", provenance)
            shutil.copyfile(destination, private / destination.name)
    config = job.config(private)
    job.env["RestoreConfigFile"] = str(config)
    # Verify the repaired producer fixtures on current tracked source, separately
    # from immutable candidate archives. The source edits affect only tests and
    # the unactivated Development catalog, never the packaged runtime libraries.
    event = folders / "references/Hexalith.EventStore"
    client = "Hexalith.EventStore.Client.Tests"
    if job.build("Hexalith.EventStore", "repaired-client", f"tests/{client}/{client}.csproj", event, config=config) == 0:
        job.tests("Hexalith.EventStore", "native-scanner", client,
                  ["Hexalith.EventStore.Client.Tests.Streams.NativeSourceScannerTests"], event)
        job.tests("Hexalith.EventStore", "native-admission-isolated", client,
                  ["Hexalith.EventStore.Client.Tests.Streams.GatewayNativeSourceMetadataAdmissionTests"], event)
        job.tests("Hexalith.EventStore", "repaired-client-native-and-catalog", client,
                  ["Hexalith.EventStore.Client.Tests.Streams.*", "Hexalith.EventStore.Client.Tests.Routing.DevelopmentRouteCatalogBuilderTests"], event)
    server = "Hexalith.EventStore.Server.Tests"
    if job.build("Hexalith.EventStore", "repaired-server", f"tests/{server}/{server}.csproj", event, config=config) == 0:
        triage = ROOT / "references/Hexalith.EventStore" / EVIDENCE_PARENT / job.args.producer_evidence / "source-ci-triage.json"
        affected = {entry["test"].removeprefix("failed ").split("(", 1)[0].rsplit(".", 1)[0].strip()
                    for entry in json.loads(triage.read_text())["failed_tests"]
                    if entry["test"].startswith("failed Hexalith.EventStore.Server.Tests.")}
        job.tests("Hexalith.EventStore", "repaired-server-native-and-ci-failures", server, sorted(affected | {
            "Hexalith.EventStore.Server.Tests.Controllers.NativeSourceScannerAuthorizationTests",
            "Hexalith.EventStore.Server.Tests.Controllers.SyntheticOriginalSourceReceiverTests",
            "Hexalith.EventStore.Server.Tests.Streams.*"}), event)
    composition(job, event=event, config=config)
    job.retain_runtime("Hexalith.EventStore", event, [client, server, "Hexalith.EventStore.DomainService.Tests"])
    for script in ["test-authoritative-package-catalog.ps1", "validate-platform-version-catalog.ps1"]:
        job.run("Hexalith.Builds", script.removesuffix(".ps1"), ["pwsh", "-NoLogo", "-NoProfile", "-File", f"Tools/{script}"], builds, timeout=1800)
    job.build("Hexalith.Projects", "complete-release", "Hexalith.Projects.CI.slnx", projects, config=config)
    project_tests = "Hexalith.Projects.Server.Tests"
    if job.build("Hexalith.Projects", "server-focused", f"tests/{project_tests}/{project_tests}.csproj", projects, restore=False) == 0:
        job.tests("Hexalith.Projects", "conversations-folders-adapters", project_tests, [
            "Hexalith.Projects.Server.Tests.ProjectFolderDirectoryTests", "Hexalith.Projects.Server.Tests.ProjectFileReferenceDirectoryTests",
            "Hexalith.Projects.Server.Tests.DelegatedProjectDirectoryRegistrationTests", "Hexalith.Projects.Server.Tests.Conversations.*",
            "Hexalith.Projects.Server.Tests.Folders.*", "Hexalith.Projects.Server.Tests.ProjectConversationAssignmentEndpointTests"], projects)
    job.build("folders", "complete-release", "Hexalith.Folders.CI.slnx", folders, config=config)
    folder_tests = ["Hexalith.Folders.Contracts.Tests", "Hexalith.Folders.Client.Tests", "Hexalith.Folders.Server.Tests",
                    "Hexalith.Folders.IntegrationTests", "Hexalith.Folders.Cli.Tests", "Hexalith.Folders.Mcp.Tests"]
    for project in folder_tests:
        job.build("folders", project, f"tests/{project}/{project}.csproj", folders, restore=False)
    job.tests("folders", "native-adapter-and-authority", "Hexalith.Folders.Server.Tests", [
        "Hexalith.Folders.Server.Tests.NativeSourceHeadReaderTests", "Hexalith.Folders.Server.Tests.FoldersNativeGatewayBearerTests",
        "Hexalith.Folders.Server.Tests.WorkspaceContentProductionTests", "Hexalith.Folders.Server.Tests.WorkspaceContentRejectionMappingTests",
        "Hexalith.Folders.Server.Tests.WorkspaceContentErasureGrantTests"], folders)
    job.tests("folders", "routing", "Hexalith.Folders.IntegrationTests", [
        "Hexalith.Folders.IntegrationTests.Routing.FoldersApiRoutingModeTests", "Hexalith.Folders.IntegrationTests.WorkspaceContentProcessWiringTests"], folders)
    job.run("folders", "parity-all-twelve", ["pwsh", "-NoLogo", "-NoProfile", "-File", "tests/tools/run-contract-parity-ci-gates.ps1",
        "-SkipRestoreBuild", "-OutputReportPath", str(job.evidence("folders") / "parity-report.json")], folders)
    job.run("folders", "governance", ["pwsh", "-NoLogo", "-NoProfile", "-File", "tests/tools/run-governance-completeness-gates.ps1", "-SkipRestoreBuild"], folders)
    report = folders / "_bmad-output/gates/governance-completeness/latest.json"
    if report.is_file():
        shutil.copyfile(report, job.evidence("folders") / "governance-report.json")
    for owner, cwd in [("folders", folders), ("Hexalith.Projects", projects)]:
        resolved = {}
        for assets_path in sorted(cwd.rglob("obj/project.assets.json")):
            if "references" in assets_path.relative_to(cwd).parts:
                continue
            assets = json.loads(assets_path.read_text())
            rows = []
            for identity, library in assets.get("libraries", {}).items():
                if library.get("type") != "package":
                    continue
                archive = Path(job.env["NUGET_PACKAGES"]) / library["path"] / (identity.replace("/", ".").lower() + ".nupkg")
                metadata = archive.parent / ".nupkg.metadata"
                row = {"identity": identity, "archive_sha256": sha(archive) if archive.is_file() else None,
                       "restore_origin": json.loads(metadata.read_text()) if metadata.is_file() else None}
                if identity.startswith("Hexalith.EventStore.") and identity.endswith("/" + VERSION):
                    original = candidate / (identity.replace("/", ".") + ".nupkg")
                    row["exact_candidate_bytes"] = archive.is_file() and original.is_file() and sha(archive) == sha(original)
                    if not row["exact_candidate_bytes"]:
                        raise ValueError(f"Restored EventStore archive differs from exact candidate: {identity}")
                rows.append(row)
            resolved[str(assets_path.relative_to(cwd))] = rows
        write(job.evidence(owner) / "resolved-package-identities.json", resolved)
        job.verify_public_hexalith(owner, cwd, module)
    job.assert_runtime_packages("folders", folders, folder_tests, sorted(candidate.glob("*.nupkg")))
    job.assert_runtime_packages("Hexalith.Projects", projects, [project_tests],
                                [*sorted(candidate.glob("*.nupkg")), *sorted(published.glob("*.nupkg"))])
    job.retain_runtime("folders", folders, folder_tests)
    job.retain_runtime("Hexalith.Projects", projects, [project_tests])


def composition(job: Job, *, event: Path | None = None, config: Path | None = None) -> None:
    standalone = event is None
    if event is None:
        event = job.snapshot("Hexalith.EventStore", job.workspace / "references/Hexalith.EventStore")
        job.snapshot("Hexalith.Builds", job.workspace / "references/Hexalith.Builds")
    if config is None:
        candidate = ROOT / "references/Hexalith.EventStore" / EVIDENCE_PARENT / job.args.producer_evidence / "candidate-packages"
        feed = job.scratch / "candidate-feed"
        shutil.copytree(candidate, feed)
        config = job.config(feed)
    job.env["RestoreConfigFile"] = str(config)
    project = "Hexalith.EventStore.DomainService.Tests"
    if job.build("Hexalith.EventStore", "native-composition", f"tests/{project}/{project}.csproj", event, config=config) != 0:
        raise ValueError("Native composition test build failed; no earlier binaries substituted.")
    if job.tests("Hexalith.EventStore", "native-composition", project,
                 ["Hexalith.EventStore.DomainService.Tests.NativeSourceScannerCompositionTests"], event) != 0:
        raise ValueError("Native composition tests failed.")
    assembly = ET.parse(job.evidence("Hexalith.EventStore") / "native-composition.xml").getroot().find("assembly")
    if assembly is None or assembly.get("total") != "5" or assembly.get("passed") != "5" or assembly.get("skipped") != "0":
        raise ValueError("Expected all five native composition cases without skips.")
    if standalone:
        job.retain_runtime("Hexalith.EventStore", event, [project])


def main() -> None:
    def cancelled(signum, _frame):
        raise SystemExit(128 + signum)

    signal.signal(signal.SIGTERM, cancelled)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--phase", choices=["producer", "consumers", "composition"], required=True)
    parser.add_argument("--evidence-name", required=True)
    parser.add_argument("--producer-evidence", help="Exact successful producer evidence; required for consumers.")
    args = parser.parse_args()
    if args.phase in {"consumers", "composition"} and not args.producer_evidence:
        parser.error("--producer-evidence is required for consumer/composition phases; historical candidates are never selected implicitly.")
    job = Job(args)
    try:
        if args.phase == "producer":
            producer(job)
        elif args.phase == "composition":
            composition(job)
        else:
            consumers(job)
    except Exception as error:
        write(job.evidence("folders") / (args.phase + "-failure.json"),
              {"error": str(error), "traceback": traceback.format_exc(), "phase": args.phase})
        raise


if __name__ == "__main__":
    main()
