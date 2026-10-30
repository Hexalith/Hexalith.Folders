#!/usr/bin/env python3
"""Qualify current source Debug artifacts without starting applications or replaying loaders."""
import argparse
import ast
import hashlib
import json
import os
from pathlib import Path
import resource
import re
import shutil
import signal
import stat
import subprocess
import sys
import tarfile
import tempfile
import time
import xml.etree.ElementTree as ET
from collections import Counter

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
BUILDS = ROOT / "references/Hexalith.Builds"
EVIDENCE = ROOT / "_bmad-output/implementation-artifacts/evidence"
PREFIX = "story-1-17-current-debug-distribution-2026-10-29"
SDK_VERSION = "10.0.402"
SERIAL = ["-m:1", "-nr:false", "-p:BuildInParallel=false", "-p:RestoreBuildInParallel=false", "-p:UseSharedCompilation=false"]
IMPORTS = ("CustomBeforeDirectoryBuildProps", "CustomAfterMicrosoftCommonProps", "CustomBeforeDirectoryBuildTargets", "CustomAfterDirectoryBuildTargets", "AfterMicrosoftNETSdkTargets", "HEXALITH_SOURCE_MAPPING_HASH", "Configuration")
PRIVATE = ("DOTNET_CLI_HOME", "NUGET_PACKAGES", "NUGET_HTTP_CACHE_PATH", "NUGET_PLUGINS_CACHE_PATH", "NUGET_SCRATCH", "TMPDIR", "TEMP", "TMP")
CONTROLS = {"DOTNET_GENERATE_ASPNET_CERTIFICATE": "false", "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1", "DOTNET_NOLOGO": "1", "DOTNET_ADD_GLOBAL_TOOLS_TO_PATH": "false", "DOTNET_CLI_USE_MSBUILD_SERVER": "0", "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE": "true", "MSBUILDDISABLENODEREUSE": "1"}
AUTHORITY = dict(parentStory="in-progress", tracker="backlog", routingPolicy="V1Only", T0=None, activationAuthority=None, stagingExecuted=0, stagingRequired=14)
CLASSES = ("CompositionCommandOptionsTests", "CompositionSdkProcessTests", "Workspace.PlatformModelCatalogTests", "Workspace.WorkspaceMsBuildTests", "Workspace.CatalogVersionTests", "Workspace.WorkspaceAppHostTests")


class GateFailure(RuntimeError):
    """A failed gate stops all dependent qualification."""


def require(condition, detail):
    if not condition:
        raise GateFailure(detail)


def sha(path):
    digest = hashlib.sha256()
    with open(path, "rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def binding(path, base=None):
    path = Path(path)
    require(not path.is_symlink() and path.is_file(), "Missing or linked regular input: " + str(path))
    return dict(path=str(path.relative_to(base) if base else path), bytes=path.stat().st_size, sha256=sha(path))


def same(item, base=None):
    path = Path(base) / item["path"] if base else Path(item["path"])
    require(binding(path, base) == item, "Input identity drift: " + str(path))


def write(path, value):
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    Path(path).write_text(json.dumps(value, indent=2) + "\n")


def git(repository, *arguments):
    environment = dict(os.environ, GIT_OPTIONAL_LOCKS="0")
    result = subprocess.run(["git", "-C", str(repository), *arguments], env=environment, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=30)
    require(result.returncode == 0, "Git observation failed: " + repr(arguments) + ": " + result.stderr.decode(errors="replace"))
    return result.stdout


def repositories(root=ROOT):
    declared = git(root, "config", "--file", ".gitmodules", "--get-regexp", r"^submodule\..*\.path$").decode().splitlines()
    paths = [line.split(None, 1)[1] for line in declared]
    require(len(paths) == 12 and len(set(paths)) == 12, "Expected twelve unique root-declared references")
    require(all(Path(path).parts[:1] == ("references",) and len(Path(path).parts) == 2 for path in paths), "Unexpected root reference path")
    return [root, *(root / path for path in paths)]


def repository_state(root=ROOT):
    rows = []
    for repository in repositories(root):
        require((repository / ".git").exists(), "Uninitialized root reference; initialization forbidden: " + str(repository))
        revision = git(repository, "rev-parse", "HEAD").decode().strip()
        index = Path(git(repository, "rev-parse", "--path-format=absolute", "--git-path", "index").decode().strip())
        row = dict(path=str(repository.relative_to(root)), revision=revision, index=binding(index),
                   stageEntriesSha256=hashlib.sha256(git(repository, "ls-files", "--stage", "-z")).hexdigest(),
                   indexFlagsSha256=hashlib.sha256(git(repository, "ls-files", "-v", "-z")).hexdigest())
        if repository != root:
            relative = row["path"]
            row["rootGitlink"] = git(root, "rev-parse", "HEAD:" + relative).decode().strip()
            stage = git(root, "ls-files", "--stage", "--", relative).decode().split()
            require(stage[0] == "160000" and stage[2] == "0", "Root reference requires a stage-zero gitlink: " + relative)
            row["indexedGitlink"] = stage[1]
            sync_path = root / "_bmad-output/implementation-artifacts/evidence" / (PREFIX + "-submodule-sync.json")
            synchronization = json.loads(sync_path.read_text()) if sync_path.is_file() else None
            validate_gitlink(row, synchronization)
        rows.append(row)
    return rows


def validate_gitlink(row, synchronization=None):
    require(row["revision"] == row["indexedGitlink"], "Checkout/index gitlink misalignment: " + row["path"])
    if row["rootGitlink"] != row["indexedGitlink"]:
        require(synchronization is not None, "Index ahead of HEAD without approved synchronization evidence")
        authorized = [item for item in synchronization["repositories"] if item["path"] == row["path"]]
        require(len(authorized) == 1 and authorized[0]["committedRootGitlink"] == row["rootGitlink"]
                and authorized[0]["indexedRootGitlink"] == row["indexedGitlink"], "Root gitlink differs from approved synchronization tuple: " + row["path"])


def source_bindings(root=ROOT):
    """Bind tracked bytes including pre-existing dirty bytes, plus existing untracked inputs."""
    rows = []
    for repository in repositories(root):
        entries = git(repository, "ls-files", "--stage", "-z").split(b"\0")
        for entry in entries:
            if not entry:
                continue
            metadata, name = entry.split(b"\t", 1)
            if metadata.startswith(b"160000 "):
                continue
            path = repository / os.fsdecode(name)
            if path.is_symlink():
                rows.append(dict(path=str(path.relative_to(root)), link=os.readlink(path)))
            elif path.exists():
                rows.append(binding(path, root))
            else:
                rows.append(dict(path=str(path.relative_to(root)), missing=True))
        for name in git(repository, "ls-files", "--others", "--exclude-standard", "-z").split(b"\0"):
            if name:
                path = repository / os.fsdecode(name)
                require(not path.is_symlink(), "Untracked linked input: " + str(path))
                rows.append(binding(path, root))
    return sorted(rows, key=lambda row: row["path"])


def source_input_paths(root=ROOT, exclusions=()):
    """Enumerate the same tracked/untracked input scope without hashing its bytes."""
    paths = set()
    for repository in repositories(root):
        for entry in git(repository, "ls-files", "--stage", "-z").split(b"\0"):
            if entry:
                metadata, name = entry.split(b"\t", 1)
                if not metadata.startswith(b"160000 "):
                    paths.add(repository / os.fsdecode(name))
        paths.update(repository / os.fsdecode(name) for name in git(repository, "ls-files", "--others", "--exclude-standard", "-z").split(b"\0") if name)
    return {str(path.relative_to(root)) for path in paths
            if not any(path.is_relative_to(Path(exclusion)) for exclusion in exclusions)}


def check_sources(rows, root=ROOT, *, complete=False, exclusions=()):
    if complete:
        expected = {row["path"] for row in rows if not any((root / row["path"]).is_relative_to(Path(exclusion)) for exclusion in exclusions)}
        require(source_input_paths(root, exclusions) == expected, "Source input path set changed; added or removed inputs")
    for item in rows:
        path = root / item["path"]
        if "link" in item:
            require(path.is_symlink() and os.readlink(path) == item["link"], "Source link drift: " + str(path))
        elif item.get("missing"):
            require(not path.exists(), "Missing source appeared: " + str(path))
        else:
            same(item, root)


def reusable_source_changes(rows, permitted, root=ROOT):
    changes = []
    for row in rows:
        try:
            check_sources([row], root)
        except GateFailure:
            require(row["path"] in permitted, "Source input changed outside the model wrapper/guards: " + row["path"])
            changes.append(dict(before=row, after=binding(root / row["path"], root)))
    return changes


def check_reuse_additions(previous, current):
    additions = {row["path"] for row in current} - {row["path"] for row in previous}
    prefix = "_bmad-output/implementation-artifacts/evidence/" + PREFIX
    require(all(path.startswith(prefix) and path.endswith(".json") for path in additions),
            "New source/import inputs prevent reuse; only owned qualification JSON recordings may be added")


def shared_sdk_control_ast(source):
    """Bind shared control code and its transitive module dependencies."""
    module = ast.parse(source)
    model_test = ast.dump(ast.parse('config.get("stage") == "model"', mode="eval").body)

    class ModelBlocks(ast.NodeTransformer):
        def visit_If(self, node):
            if ast.dump(node.test) == model_test:
                node.body = [ast.Pass()]
                return node
            return self.generic_visit(node)

    definitions = {}
    for node in module.body:
        if isinstance(node, (ast.FunctionDef, ast.ClassDef)) and getattr(node, "name", None) != "Qualification":
            definitions[node.name] = node
        elif isinstance(node, ast.Assign):
            for target in node.targets:
                if isinstance(target, ast.Name):
                    definitions[target.id] = node
        elif isinstance(node, (ast.Import, ast.ImportFrom)):
            for alias in node.names:
                definitions[alias.asname or alias.name.split(".")[0]] = node
        elif isinstance(node, ast.ClassDef) and node.name == "Qualification":
            for method in node.body:
                if isinstance(method, ast.FunctionDef) and method.name in {"prepare", "build_arguments"}:
                    definitions["Qualification." + method.name] = method
    pending = {"controlled_child", "CONTROLS", "SERIAL", "PRIVATE", "IMPORTS", "SDK_VERSION", "require", "same", "binding", "sha", "Qualification.prepare", "Qualification.build_arguments"}
    bound = {}
    while pending:
        name = pending.pop()
        if name not in definitions or name in bound:
            continue
        node = ModelBlocks().visit(definitions[name])
        bound[name] = ast.dump(node, include_attributes=False)
        pending.update(child.id for child in ast.walk(node) if isinstance(child, ast.Name) and isinstance(child.ctx, ast.Load) and child.id in definitions and child.id not in bound)
    return sorted(bound.items())


def configuration_values(arguments):
    values = []
    iterator = iter(arguments)
    for argument in iterator:
        lower = argument.lower()
        if lower in {"-c", "--configuration", "-p", "/p", "-property", "/property", "--property"}:
            value = next(iterator, None)
            require(value is not None, "Missing configuration/property argument")
            if lower in {"-c", "--configuration"}:
                values.append(value)
                continue
            argument = "-p:" + value
            lower = argument.lower()
        match = re.match(r"^(?:-c|--configuration)[:=](.*)$", argument, re.IGNORECASE)
        if match:
            values.append(match.group(1))
        elif re.match(r"^(?:[-/](?:p|property)|--property)[:=]", lower):
            properties = re.split(r"[:=]", argument, maxsplit=1)[1]
            values.extend(match.group(1) for match in re.finditer(r"(?:^|[;,])\s*configuration\s*=\s*([^;,]*)", properties, re.IGNORECASE))
    return [value.strip().strip('"\'').lower() for value in values]


def packs(arguments):
    if arguments[0] == "pack":
        return True
    return any(match and "pack" in re.split(r"[;,]", match.group(1).lower())
               for argument in arguments[1:]
               for match in [re.match(r"^[-/](?:t|target)[:=](.*)$", argument, re.IGNORECASE)])


def store_metadata():
    """Observe real certificate-store names, modes, sizes and mtimes without reading certificates."""
    rows = []
    home = Path.home()
    for directory in (home / ".dotnet/corefx/cryptography", home / ".aspnet/https", home / ".pki"):
        rows.append(dict(path=str(directory), exists=directory.exists()))
        if directory.exists():
            for path in sorted(directory.rglob("*")):
                info = path.lstat()
                rows.append(dict(path=str(path), mode=info.st_mode, bytes=info.st_size, mtimeNs=info.st_mtime_ns))
    return rows


def protected_authority(root=ROOT):
    parent = root / "_bmad-output/implementation-artifacts/spec-1-17-complete-pd10-v2-migration-and-closure.md"
    tracker = root / "_bmad-output/implementation-artifacts/sprint-status.yaml"
    configuration = root / "eng/story117-platform-qualification-current/configuration.proposed.json"
    staging = root / "eng/story117-platform-qualification-current/staging-checks.json"
    require(re.search(r"^status: ['\"]?in-progress['\"]?\s*$", parent.read_text(), re.MULTILINE), "Parent must remain in-progress")
    require(re.search(r"^\s+1-17-publish-the-pd10-v2-authorization-contract-spine: backlog\s*$", tracker.read_text(), re.MULTILINE), "Tracker must remain backlog")
    documents = [json.loads(path.read_text()) for path in (configuration, staging)]
    for document in documents:
        require(document["productionControl"]["routeMode"] == "V1Only" and document["productionControl"]["T0"] is None
                and document["productionControl"]["activationAuthority"] is None, "Protected activation authority changed")
    checks = documents[1]["checks"]
    require(len(checks) == 14 and all(row["status"] == "unexecuted" and row["result"] is None for row in checks), "Staging must remain 0/14")
    return dict(values=AUTHORITY, files=[binding(path, root) for path in (parent, tracker, configuration, staging)])


def controlled_child(config, kind, arguments, incoming):
    """Fail closed before execution when mandatory qualification controls are unavailable."""
    require(kind in {"sdk", "pwsh"}, "Unknown controlled executable")
    environment = config.get("environment", {})
    for key, value in CONTROLS.items():
        require(environment.get(key) == value, "Missing mandatory qualification control: " + key)
    work = Path(config["work"]).resolve()
    for key in PRIVATE:
        value = Path(environment.get(key, ""))
        require(value.is_absolute() and value.resolve().is_relative_to(work), "Private SDK directory missing or outside workspace: " + key)
    require(environment.get("HOME") == config["preservedHome"], "HOME must be preserved")
    effective = dict(incoming)
    effective.update(environment)
    # Fixtures may explicitly choose another run-owned cache; retain that selection.
    # Ambient/shared directories never supersede qualification-owned defaults.
    for key in PRIVATE:
        selected = Path(incoming.get(key, ""))
        if selected.is_absolute() and selected.resolve().is_relative_to(work):
            effective[key] = str(selected)
    argv = list(arguments)
    if config.get("stage") == "model":
        mapping = config.get("mappingEnvironment", {})
        require(set(IMPORTS) <= set(mapping), "Model child lacks mandatory production mapping controls")
        require(mapping["Configuration"] == "Debug" and bool(mapping["HEXALITH_SOURCE_MAPPING_HASH"]), "Invalid model source mapping controls")
        for key in IMPORTS:
            if key not in {"Configuration", "HEXALITH_SOURCE_MAPPING_HASH"}:
                require(Path(mapping[key]).is_file(), "Model mapping import unavailable: " + key)
        effective.update(mapping)
    if kind == "sdk" and arguments and arguments[0] in {"build", "restore", "msbuild", "pack"}:
        if config.get("stage") == "model" or packs(arguments):
            values = configuration_values(arguments[1:])
            require(not any(value.startswith("@") for value in arguments[1:]) and all(value == "debug" for value in values), "Release/conflicting configuration qualification/packaging is forbidden; Debug is required")
            if not values:
                argv.append("-p:Configuration=Debug")
        argv.extend(SERIAL)
        if arguments[0] == "restore":
            argv.append("--disable-parallel")
        if config.get("stage") == "model":
            pinned = config.get("modelPinnedCatalog")
            require(pinned is not None, "Missing mandatory model pinned catalog binding")
            same(pinned)
            catalog_arguments = [value.split("=", 1)[1] for value in arguments if value.lower().startswith("-p:custombeforedirectorybuildprops=")]
            directory_arguments = [value.split("=", 1)[1] for value in arguments if value.lower().startswith("-p:directorypackagespropspath=")]
            require(catalog_arguments == [pinned["path"]] and directory_arguments == [pinned["path"]], "Model API did not select the exact bound pinned catalog")
            common_props = config.get("modelCommonProps")
            require(common_props is not None, "Missing mandatory model common-props composition")
            same(common_props)
            # SDK.props delegates to this private wrapper before Common.props imports
            # owner props. The wrapper imports the exact pin, then genuine Common.props;
            # that file imports the genuine mapping hook before unchanged owner props.
            effective["AlternateCommonProps"] = common_props["path"]
            argv.extend(["-p:AlternateCommonProps=" + common_props["path"],
                         "-p:CustomBeforeDirectoryBuildProps=" + config["mappingEnvironment"]["CustomBeforeDirectoryBuildProps"]])
            argv.extend(["-p:RestoreConfigFile=" + config["nugetConfig"], "-p:CustomAfterMicrosoftCommonTargets=" + config["captureTargets"],
                         "-p:VersionSuffix=" + config["versionSuffix"],
                         "-v:diag", "-bl:" + str(work / "logs/model-sdk.binlog")])
    for item in config.get("gateBindings", []):
        same(item)
    return effective, {key: value for key, value in effective.items() if incoming.get(key) != value}, argv


def assets_audit(paths):
    require(bool(paths), "No restored assets were captured")
    rows = []
    for path in sorted(paths):
        document = json.loads(path.read_text())
        packages = [name for name, value in document.get("libraries", {}).items() if name.lower().startswith("hexalith.") and value["type"] == "package"]
        require(not packages, "Hexalith package fallback in " + str(path) + ": " + repr(packages))
        rows.append(dict(file=binding(path), sourceProjects=[name for name, value in document.get("libraries", {}).items() if value["type"] == "project"], hexalithPackages=packages))
    return rows


def class_result(path, full_name, minimum, discovery=None):
    assembly = ET.parse(path).getroot().find("assembly")
    require(assembly is not None, "Missing original-assembly test report")
    tests = assembly.findall(".//test")
    total = int(assembly.get("total", "0"))
    require(total >= minimum and len(tests) == total and int(assembly.get("passed", "0")) == total, "Required assertions did not all run: " + full_name)
    require(all(int(assembly.get(key, "0")) == 0 for key in ("errors", "failed", "skipped", "not-run")), "Failed, skipped or unrun required class: " + full_name)
    require(all(test.get("result") == "Pass" and test.get("type") == full_name for test in tests), "Wrong class or non-passing assertion")
    if discovery is not None:
        require(total == len(discovery), "Executed denominator differs from current discovery: " + full_name)
        from verify import verify_case_identities
        try:
            verify_case_identities(tests, discovery)
        except ValueError as error:
            raise GateFailure(str(error)) from error
    if full_name.endswith("CompositionSdkProcessTests"):
        require(any(test.get("method") == "QualificationCatalogProbeInterceptionIsEstablishedAsync" for test in tests), "Required interception assertion absent")
    return dict(name=full_name, total=total, passed=total, failed=0, errors=0, skipped=0, notRun=0, discovered=len(discovery) if discovery is not None else None, report=binding(path))


def verify_pe_references(rows, model):
    by_path = {row["path"]: row for row in rows}
    require(str(model) in by_path, "Model metadata is absent")
    selected = by_path[str(model)]
    matches = []
    for reference in selected["references"]:
        if not reference["name"].startswith("Hexalith."):
            continue
        candidate = str(Path(model).parent / (reference["name"] + ".dll"))
        require(candidate in by_path, "Model reference output missing: " + reference["name"])
        require(by_path[candidate]["identity"] == reference, "Incompatible model reference identity: " + reference["name"])
        matches.append(dict(reference=reference, output=candidate))
    require(bool(matches), "No Hexalith model references were inspected")
    return dict(model=str(model), references=selected["references"], hexalithMatches=matches,
                scope="PE metadata identities only; no application assembly was loaded; runtime compatibility remains pending")


class Qualification:
    def __init__(self, work):
        self.work = work
        self.logs = work / "logs"
        self.logs.mkdir()
        self.commands = []
        self.seal = []
        self.failure = None
        self.result = dict(schemaVersion=1, qualification="incomplete", protectedAuthority=AUTHORITY,
                           ordinaryStartup="pending", signedDistribution="pending", operationalAcceptance="pending", commands=self.commands)

    def prepare(self):
        self.baseline_repos = repository_state()
        self.baseline_sources = source_bindings()
        self.baseline_stores = store_metadata()
        self.result["protectedAuthorityEvidence"] = protected_authority()
        self.artifacts = BUILDS / ".artifacts" / self.work.name
        self.artifacts.mkdir(parents=True, mode=0o700)
        self.result["privateArtifacts"] = str(self.artifacts)
        sync_path = EVIDENCE / (PREFIX + "-submodule-sync.json")
        if sync_path.is_file():
            self.result["authorizedSynchronization"] = binding(sync_path)
            shutil.copy2(sync_path, self.work / "authorized-synchronization.json")
        write(self.work / "repository-baseline.json", self.baseline_repos)
        write(self.work / "source-baseline.json", self.baseline_sources)
        write(self.work / "certificate-stores-before.json", self.baseline_stores)
        for index, repository in enumerate(repositories()):
            (self.work / ("baseline-diff-" + str(index) + ".patch")).write_bytes(git(repository, "diff", "--binary", "--no-ext-diff", "HEAD"))
        installed_sdk = Path(shutil.which("dotnet") or "").resolve()
        pwsh = Path(shutil.which("pwsh") or "").resolve()
        require(installed_sdk.is_file() and pwsh.is_file(), "Absolute dotnet and PowerShell executables are required")
        require((installed_sdk.parent / "sdk" / SDK_VERSION / "dotnet.dll").is_file(), "Required installed SDK is unavailable: " + SDK_VERSION)
        # latestPatch may select a newer installed SDK. A private host layout exposes exactly
        # the approved, already-installed SDK; no repository pin or installed tool is changed.
        sdk_root = self.work / "sdk-layout"
        (sdk_root / "sdk").mkdir(parents=True)
        sdk = sdk_root / "dotnet"
        shutil.copy2(installed_sdk, sdk)
        (sdk_root / "sdk" / SDK_VERSION).symlink_to(installed_sdk.parent / "sdk" / SDK_VERSION, target_is_directory=True)
        for name in ("host", "shared", "packs", "sdk-manifests"):
            if (installed_sdk.parent / name).is_dir():
                (sdk_root / name).symlink_to(installed_sdk.parent / name, target_is_directory=True)
        self.result["sdkSelection"] = dict(installedHost=binding(installed_sdk), privateHost=binding(sdk), sdkVersion=SDK_VERSION,
            sdkDirectory=str(installed_sdk.parent / "sdk" / SDK_VERSION), mechanism="Private copied dotnet host with only the existing 10.0.402 SDK directory exposed; runtime/reference-pack directories remain read-only inputs")
        self.layout_links = {str(path): os.readlink(path) for path in sdk_root.rglob("*") if path.is_symlink()}
        self.result["sdkSelection"]["links"] = self.layout_links
        self.sdk = str(sdk)
        environment = dict(CONTROLS, HOME=str(Path.home()), LANG="C.UTF-8", DOTNET_ROOT=str(sdk.parent), DOTNET_HOST_PATH=str(sdk),
                           HEXALITH_TEST_DOTNET_EXECUTABLE=str(sdk), HEXALITH_TEST_SDK_INTERCEPTION_REQUIRED="true",
                           PATH=str(self.work / "bin") + ":" + str(sdk.parent) + ":" + str(pwsh.parent) + ":/usr/local/bin:/usr/bin:/bin")
        for key in PRIVATE:
            path = self.work / key.lower()
            path.mkdir(mode=0o700)
            environment[key] = str(path)
        for key, name in (("XDG_CACHE_HOME", "xdg-cache"), ("XDG_CONFIG_HOME", "xdg-config"), ("PSModuleAnalysisCachePath", "powershell-cache/ModuleAnalysisCache")):
            path = self.work / name
            (path.parent if key == "PSModuleAnalysisCachePath" else path).mkdir(parents=True, exist_ok=True, mode=0o700)
            environment[key] = str(path)
        (self.work / "bin").mkdir()
        shutil.copytree(HERE, self.work / "harness", ignore=shutil.ignore_patterns("__pycache__"))
        self.config = dict(work=str(self.work), sdk=str(sdk), pwsh=str(pwsh), sdkSha256=sha(sdk), pwshSha256=sha(pwsh),
                           environment=environment, preservedHome=str(Path.home()), stage="bootstrap", childLog=str(self.work / "sdk-children.jsonl"), gateBindings=[])
        self.config["versionSuffix"] = time.strftime("%Y%m%d%H%M%S", time.gmtime())
        self.result["qualificationVersionSuffix"] = self.config["versionSuffix"]
        self.gates = [binding(sdk), binding(pwsh)]
        for path in sorted((sdk.parent / "sdk" / SDK_VERSION).glob("**/*")):
            if path.is_file() and not path.is_symlink() and path.suffix in {".dll", ".json", ".targets", ".props"}:
                self.gates.append(binding(path))
        require(len(self.gates) > 100, "Selected SDK installation is incomplete")
        for directory in (sdk.parent / "host/fxr", sdk.parent / "shared/Microsoft.NETCore.App", sdk.parent / "shared/Microsoft.AspNetCore.App"):
            require(directory.is_dir(), "Required runtime installation unavailable: " + str(directory))
            self.gates.extend(binding(path) for path in sorted(directory.rglob("*")) if path.is_file() and not path.is_symlink())
        write(self.work / "sdk-bindings.json", self.gates)
        self.config["gateBindings"] = [binding(sdk), binding(pwsh)]
        self.config_path = self.work / "config.json"
        write(self.config_path, self.config)
        for name, kind in (("dotnet", "sdk"), ("pwsh", "pwsh")):
            # Shell-free launchers retain argv exactly and never import a mutable workspace module.
            launcher = self.work / "bin" / name
            launcher.write_text("#!/usr/bin/python3\nimport os\nos.execv('/usr/bin/python3', ['/usr/bin/python3', " + repr(str(self.work / "harness/sdk_child.py")) + ", " + repr(str(self.config_path)) + ", " + repr(kind) + ", *__import__('sys').argv[1:]])\n")
            launcher.chmod(0o700)
        self.nuget = self.work / "NuGet.Config"
        self.nuget.write_text('<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources></configuration>\n')
        self.config["nugetConfig"] = str(self.nuget)
        self.capture = self.work / "capture.targets"
        self.capture.write_text('<Project><Target Name="QualificationCaptureCompilerInputs" BeforeTargets="CoreCompile"><MakeDir Directories="$(QualificationCompilerCapture)" /><WriteLinesToFile File="$(QualificationCompilerCapture)/$(MSBuildProjectName).$(TargetFramework).txt" Lines="@(Compile-&gt;\'source|%(FullPath)\');@(ReferencePathWithRefAssemblies-&gt;\'reference|%(FullPath)\')" Overwrite="true" /></Target></Project>\n')
        self.config["captureTargets"] = str(self.capture)
        write(self.config_path, self.config)
        helper = self.work / "helper"
        helper.mkdir()
        shutil.copy2(HERE / "Probe.cs", helper / "Program.cs")
        self.helper_project = helper / "DistributionProbe.csproj"
        self.helper_project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><ProjectReference Include="' + str(BUILDS / 'src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj') + '" /></ItemGroup></Project>\n')
        self.result["initialRepositories"] = self.baseline_repos
        self.result["environment"] = environment
        self.result["sdkBindings"] = binding(self.work / "sdk-bindings.json", self.work)
        self.result["proposedOrdinaryInvocation"] = self.ordinary_invocation()

    def inputs(self):
        require(repository_state() == self.baseline_repos, "Repository revision/index/gitlink drift; dependent execution stopped")
        check_sources(self.baseline_sources, complete=True, exclusions=(self.work, self.artifacts))
        for path, target in self.layout_links.items():
            require(Path(path).is_symlink() and os.readlink(path) == target, "Private SDK selection link drift: " + path)
        for item in self.gates:
            same(item)
        for item in self.seal:
            same(item, self.work)

    def execute(self, label, argv, environment=None, timeout=900):
        self.inputs()
        environment = dict(self.config["environment"], **(environment or {}))
        log = self.logs / (label + ".log")
        row = dict(label=label, argv=[str(arg) for arg in argv], cwd=str(BUILDS), environment=environment,
                   timeoutSeconds=timeout, launched=False, exitCode=None, timedOut=False)
        self.commands.append(row)
        write(self.work / "commands.json", self.commands)
        print(json.dumps(dict(stage=label, status="starting")), flush=True)
        started = time.monotonic()
        try:
            with log.open("wb") as output:
                process = subprocess.Popen(row["argv"], cwd=BUILDS, env=environment, stdin=subprocess.DEVNULL, stdout=output, stderr=subprocess.STDOUT,
                                           start_new_session=True, preexec_fn=lambda: resource.setrlimit(resource.RLIMIT_CORE, (0, 0)))
                row["launched"] = True
                row["pid"] = process.pid
                try:
                    process.wait(timeout=timeout)
                except subprocess.TimeoutExpired:
                    row["timedOut"] = True
                finally:
                    # Terminate any owned descendant left after success or timeout before another step runs.
                    try:
                        os.killpg(process.pid, signal.SIGKILL)
                    except ProcessLookupError:
                        pass
                    process.wait(timeout=10)
                row["exitCode"] = process.returncode
        finally:
            row["seconds"] = round(time.monotonic() - started, 3)
            if log.is_file():
                row["log"] = binding(log, self.work)
            write(self.work / "commands.json", self.commands)
        print(json.dumps({key: row[key] for key in ("label", "exitCode", "timedOut", "seconds")}), flush=True)
        require(row["exitCode"] == 0 and not row["timedOut"], "Command failed: " + label + "; argv=" + repr(row["argv"]) + "; result=" + str(row["exitCode"]) + "; log=" + str(log))
        self.inputs()
        return log

    def build_arguments(self, project, artifacts, capture):
        return [self.sdk, "build", str(project), "--configuration", "Debug", "--artifacts-path", str(artifacts),
                "-p:RestoreConfigFile=" + str(self.nuget), "-p:NuGetAudit=false", "-p:MinVerVersionOverride=1.0.0",
                "-p:VersionSuffix=" + self.config["versionSuffix"],
                "-p:CustomAfterMicrosoftCommonTargets=" + str(self.capture), "-p:QualificationCompilerCapture=" + str(capture),
                "-p:HexalithG4FixtureArtifacts=" + str(self.artifacts / "fixture") + "/", *SERIAL, "-v:diag",
                "-bl:" + str(self.logs / (artifacts.name + "-" + Path(project).stem + ".binlog"))]

    def ordinary_invocation(self):
        runner = self.work / "distribution/CLI/Hexalith.Builds.Module.Cli.dll"
        apphost = self.work / "distribution/AppHost/Hexalith.Builds.Module.AppHost.dll"
        manifest = ROOT / "references/Hexalith.Parties/module/hexalith.module-manifest.v2.json"
        return dict(environment={"HEXALITH_G4_APPHOST": str(apphost)},
            argv=[self.sdk, str(runner), "debug", "--manifest", str(manifest), "--mode", "source", "--output", "json"],
            manifest=binding(manifest), status="blocked proposal; planned artifacts are unmaterialized until all qualification gates pass; not executed",
            prerequisites=["Preserve and recheck the approved synchronized checkout/index tuple and the separately recorded committed root gitlinks",
                           "Complete the original source Debug graph, required original-assembly tests, compilation-only model and sealed factory-selection gates",
                           "Recheck all repository, source, catalog, SDK and sealed artifact identities",
                           "Approve and qualify ordinary startup and actual tool/catalog/composition/module intake",
                           "Qualify runtime/API compatibility, target/data, hosted identity, observation and recovery; obtain exact signed distribution and operational acceptance"])

    def run(self):
        version = self.execute("sdk-version", [self.sdk, "--version"], timeout=30).read_text().strip()
        require(version == SDK_VERSION, "SDK must be exactly " + SDK_VERSION + "; observed=" + version)
        self.execute("sdk-info", [self.sdk, "--info"], timeout=30)
        bootstrap = self.artifacts / "bootstrap"
        self.execute("bootstrap-build", self.build_arguments(self.helper_project, bootstrap, self.work / "compiler/bootstrap"))
        helper = bootstrap / "bin/DistributionProbe/debug/DistributionProbe.dll"
        self.helper = helper
        mapping_workspace = self.work / "workspace"
        manifest = BUILDS / "test/fixtures/module/executable/hexalith.module-manifest.v1.json"
        # Launch apphosts directly: .NET searches its own executable directory before PATH
        # for bare dotnet children when the parent itself is the dotnet host.
        self.execute("production-mapping", [helper.with_suffix(""), "map", manifest, mapping_workspace], timeout=300)
        self.mapping = json.loads((mapping_workspace / "source-mapping/mapping.json").read_text())
        self.mapping_environment = json.loads((mapping_workspace / "environment.json").read_text())
        expected = {str(path) for path in repositories()[1:]}
        require(self.mapping["Root"] == str(ROOT) and self.mapping["HasGit"] is True, "Mapping selected a different workspace")
        require(len(self.mapping["Entries"]) == 12 and {entry["Path"] for entry in self.mapping["Entries"]} == expected
                and all(entry["Origin"] == "source" for entry in self.mapping["Entries"]), "Production mapping selected fallback or non-root source")
        self.gates.extend(binding(path) for path in sorted(mapping_workspace.rglob("*")) if path.is_file())
        self.result["productionMapping"] = self.mapping
        self.result["productionEnvironment"] = self.mapping_environment
        original = self.artifacts / "original"
        project = BUILDS / "test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj"
        self.execute("original-debug-build", self.build_arguments(project, original, self.work / "compiler/original"), self.mapping_environment)
        # Build the diagnostic executable against precisely the Tooling graph used by the original CLI/hosts.
        self.execute("current-api-helper-build", self.build_arguments(self.helper_project, original, self.work / "compiler/helper"), self.mapping_environment)
        self.helper = original / "bin/DistributionProbe/debug/DistributionProbe.dll"
        self.result["originalAssets"] = self.capture_assets([*original.rglob("project.assets.json"), *(self.artifacts / "fixture").rglob("project.assets.json")], "original")
        self.config.update(stage="tests")
        write(self.config_path, self.config)
        assembly = original / "bin/Hexalith.Builds.Module.Tests/debug/Hexalith.Builds.Module.Tests.dll"
        self.result["testClasses"] = []
        self.result["classDiscoveries"] = {}
        self.result["excludedByFrozenBoundary"] = [dict(method="Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceAppHostTests.PlatformPlanDelegatesToThePinnedModelAsync", reason="Loads the actual Platform model and reaches ComposeRun; this spec permits compilation/PE inspection only; no current compatibility credit")]
        for name in CLASSES:
            full_name = "Hexalith.Builds.ModuleTool.Tests." + name
            filters = ["-class", full_name]
            if name == "Workspace.WorkspaceAppHostTests":
                filters.extend(["-method-", full_name + ".PlatformPlanDelegatesToThePinnedModelAsync"])
            discovery_log = self.execute("discover-" + name, [assembly.with_suffix(""), "-list", "full/json", "-preEnumerateTheories", *filters, "-parallel", "none", "-noColor"])
            discovery = json.loads(discovery_log.read_text())
            require(discovery and all(row["Class"] == full_name and not row.get("Explicit") and not row.get("SkipReason") for row in discovery), "Empty, explicit or skipped current discovery: " + full_name)
            self.result["classDiscoveries"][full_name] = dict(file=binding(discovery_log, self.work), cases=discovery)
            report = self.work / (name + ".xml")
            self.execute("class-" + name, [assembly.with_suffix(""), *filters, "-parallel", "none", "-xml", report])
            self.result["testClasses"].append(class_result(report, full_name, 1, discovery))
        self.build_model_and_finish(original, mapping_workspace)

    def build_model_and_finish(self, original, mapping_workspace):
        pinned = mapping_workspace / "source-mapping/pinned-tree/Props/Directory.Packages.props"
        common_props = self.work / "model-common.props"
        composition = ET.Element("Project")
        ET.SubElement(composition, "Import", Project=str(pinned))
        ET.SubElement(composition, "Import", Project="$(MSBuildExtensionsPath)/$(MSBuildToolsVersion)/Microsoft.Common.props")
        ET.ElementTree(composition).write(common_props, encoding="unicode")
        self.config.update(stage="model", mappingEnvironment=self.mapping_environment,
                           modelPinnedCatalog=binding(pinned), modelCommonProps=binding(common_props))
        self.result["modelImportComposition"] = dict(wrapper=binding(common_props, self.work), pinnedCatalog=binding(pinned, self.work),
            order=["SDK.props AlternateCommonProps", "exact bound pinned catalog", "genuine installed Microsoft.Common.props",
                   "genuine production SourceMapping.targets", "unchanged owner Directory.Build.props"],
            scope="Qualification-only SDK import composition; no production imports rewritten or validation suppressed")
        # Capture-only import records actual compiler source/reference inputs; it changes no references.
        self.config["environment"]["QualificationCompilerCapture"] = str(self.work / "compiler/model")
        write(self.config_path, self.config)
        model_log = self.execute("model-compilation-api", [self.helper.with_suffix(""), "model", mapping_workspace / "source-mapping/mapping.json",
                                 self.mapping_environment["HEXALITH_SOURCE_MAPPING_HASH"], mapping_workspace], timeout=900)
        model = Path(json.loads(model_log.read_text())["assembly"])
        self.result["modelAssets"] = self.capture_assets(list((mapping_workspace / "build/platform-model").rglob("project.assets.json")), "model")
        children = [json.loads(line) for line in (self.work / "sdk-children.jsonl").read_text().splitlines()]
        model_children = [row for row in children if row.get("sdkExecuted") and row.get("kind") == "sdk" and str(ROOT / "references/Hexalith.Platform/src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj") in row.get("incomingArguments", [])]
        require(len(model_children) == 1, "Expected exactly one compilation-only model SDK child")
        self.result["modelChildControls"] = model_children[0]
        require(all(model_children[0]["effectiveEnvironment"].get(key) == value for key, value in self.mapping_environment.items()), "Actual model child lost mapping controls")
        self.config.update(stage="seal")
        write(self.config_path, self.config)
        self.seal_outputs(original, model)
        compiler = []
        for path in sorted((self.work / "compiler").rglob("*.txt")):
            if "/bootstrap/" in str(path):
                continue
            inputs = []
            for line in path.read_text().splitlines():
                kind, name = line.split("|", 1)
                inputs.append(dict(kind=kind, file=binding(name)))
            compiler.append(dict(capture=binding(path, self.work), inputs=inputs))
        require(compiler and any("Hexalith.Platform.Composition" in row["capture"]["path"] for row in compiler), "Actual model compiler inputs were not captured")
        write(self.work / "compiler-inputs.json", compiler)
        self.result["compilerInputs"] = binding(self.work / "compiler-inputs.json", self.work)
        self.result["qualification"] = "passed"

    def capture_assets(self, paths, label):
        rows = assets_audit(paths)
        for index, row in enumerate(rows):
            destination = self.work / "assets" / label / str(index) / "project.assets.json"
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(row["file"]["path"], destination)
            row["retained"] = binding(destination, self.work)
        return rows

    def resume_source(self, receipt_path):
        """Reuse verified graph/classes after a model-only wrapper change, with fresh model outputs."""
        import difflib
        from verify import replay
        receipt_path = receipt_path.resolve()
        replay(receipt_path)
        prior = json.loads(receipt_path.read_text())
        previous = Path(prior["privateWorkspace"])
        data = prior["result"]
        require(data["initialRepositories"] == self.baseline_repos, "Cannot reuse a different repository/index tuple")
        require(len(data.get("testClasses", [])) == len(CLASSES) and all(row["passed"] == row["total"] == row["discovered"] > 0
                and all(row[key] == 0 for key in ("failed", "errors", "skipped", "notRun")) for row in data["testClasses"]), "Only fully passing discovered source classes may be reused")
        manifest = json.loads((receipt_path.parent / prior["memberManifest"]["path"]).read_text())
        retained = {row["path"]: row for row in manifest}
        for name in ("source-baseline.json", "sdk-bindings.json"):
            same(retained[name], previous)
        permitted = {"eng/story117-source-distribution/qualify.py", "eng/story117-source-distribution/test_qualification.py"}
        prior_sources = json.loads((previous / "source-baseline.json").read_text())
        check_reuse_additions(prior_sources, self.baseline_sources)
        changes = reusable_source_changes(prior_sources, permitted)
        require(bool(changes), "Resume source lane is only for explicitly recorded model-wrapper corrections")
        require(shared_sdk_control_ast((previous / "harness/qualify.py").read_text()) == shared_sdk_control_ast((HERE / "qualify.py").read_text()), "SDK controls exercised by reused original classes changed")
        for row in json.loads((previous / "sdk-bindings.json").read_text()):
            same(row)
        for key, target in data["sdkSelection"]["links"].items():
            require(Path(key).is_symlink() and os.readlink(key) == target, "Prior selected SDK link drift")
        for row in data["originalTestRunnerBindings"]:
            same(row["source"])
            self.gates.append(row["source"])
        # Bind all reused private graph outputs and compiler inputs before any model work.
        self.artifacts = Path(data["privateArtifacts"])
        for path in sorted((self.artifacts / "original/bin").rglob("*")):
            if path.is_file():
                self.gates.append(binding(path))
        if data.get("compilerAuxiliaryInputs"):
            item = data["compilerAuxiliaryInputs"]
            same(item, previous)
            for row in json.loads((previous / item["path"]).read_text())["inputs"]:
                same(row["file"])
                self.gates.append(row["file"])
        self.result["privateArtifacts"] = str(self.artifacts)
        self.config["versionSuffix"] = data["qualificationVersionSuffix"]
        self.result["qualificationVersionSuffix"] = data["qualificationVersionSuffix"]
        write(self.config_path, self.config)
        self.execute("resume-sdk-version", [self.sdk, "--version"], timeout=30)
        self.helper = self.artifacts / "original/bin/DistributionProbe/debug/DistributionProbe.dll"
        self.gates.extend(binding(path) for path in self.helper.parent.iterdir() if path.is_file())
        mapping_workspace = self.work / "workspace"
        self.execute("resume-production-mapping", [self.helper.with_suffix(""), "map", BUILDS / "test/fixtures/module/executable/hexalith.module-manifest.v1.json", mapping_workspace], timeout=300)
        self.mapping = json.loads((mapping_workspace / "source-mapping/mapping.json").read_text())
        self.mapping_environment = json.loads((mapping_workspace / "environment.json").read_text())
        require(self.mapping == data["productionMapping"], "Production mapping/catalog/identity changed during reuse")
        self.gates.extend(binding(path) for path in sorted(mapping_workspace.rglob("*")) if path.is_file())
        self.result["productionMapping"] = self.mapping
        self.result["productionEnvironment"] = self.mapping_environment
        for row in data["originalAssets"]:
            same(row["file"])
        self.result["originalAssets"] = self.capture_assets([Path(row["file"]["path"]) for row in data["originalAssets"]], "original")
        for path in sorted((previous / "compiler").rglob("*.txt")):
            destination = self.work / path.relative_to(previous)
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, destination)
            for line in path.read_text().splitlines():
                _, source = line.split("|", 1)
                self.gates.append(binding(source))
        for name in ("bootstrap-build.log", "original-debug-build.log", "current-api-helper-build.log"):
            same(retained["logs/" + name], previous)
            shutil.copy2(previous / "logs" / name, self.logs / name)
        self.result["testClasses"] = []
        self.result["classDiscoveries"] = data["classDiscoveries"]
        self.result["excludedByFrozenBoundary"] = data["excludedByFrozenBoundary"]
        for row in data["testClasses"]:
            report_name = Path(row["report"]["path"]).name
            same(retained[report_name], previous)
            shutil.copy2(previous / report_name, self.work / report_name)
            full_name = row["name"]
            discovery = data["classDiscoveries"][full_name]
            same(discovery["file"], previous)
            shutil.copy2(previous / discovery["file"]["path"], self.work / discovery["file"]["path"])
            self.result["testClasses"].append(class_result(self.work / report_name, full_name, 1, discovery["cases"]))
        for name in ("qualify.py", "test_qualification.py"):
            old = (previous / "harness" / name).read_text()
            new = (HERE / name).read_text()
            (self.work / ("reuse-" + name + ".patch")).write_text("".join(difflib.unified_diff(old.splitlines(True), new.splitlines(True), fromfile="previous/" + name, tofile="current/" + name)))
        self.result["reusedSourceEvidence"] = dict(receipt=binding(receipt_path), archive=prior["archive"],
            memberManifest=prior["memberManifest"], privateWorkspace=str(previous), sharedSdkControlsUnchanged=True,
            graphAndClasses="reused unchanged byte-bound observations; not re-executed", permittedModelWrapperChanges=changes,
            fixedVersionSuffix=data["qualificationVersionSuffix"], originalCommands=data["commands"],
            sourcePreservation="all bound inputs except the disclosed model-only wrapper/guards are byte-identical; original compiler files, outputs, SDK and catalog are rechecked")
        self.refresh_original_graph(data, previous, mapping_workspace)
        self.build_model_and_finish(self.artifacts / "original", mapping_workspace)

    def refresh_original_graph(self, data, previous, mapping_workspace):
        """Recompile the graph; reuse cases only when their complete inputs stay exact."""
        original = self.artifacts / "original"
        paths = {path for path in (original / "bin").rglob("*") if path.is_file()}
        critical = {path for name in ("Hexalith.Builds.Module.Tests", "DistributionProbe")
                    for path in (original / "bin" / name).rglob("*") if path.is_file()}
        compiler_captures = list((previous / "compiler").rglob("*.txt"))
        for capture in compiler_captures:
            for line in capture.read_text().splitlines():
                _, name = line.split("|", 1)
                paths.add(Path(name))
                critical.add(Path(name))
        for row in data.get("originalTestRunnerBindings", []):
            same(row["source"])
        if data.get("compilerInputs"):
            same(data["compilerInputs"], previous)
            for row in json.loads((previous / data["compilerInputs"]["path"]).read_text()):
                for item in row["inputs"]:
                    same(item["file"])
                    paths.add(Path(item["file"]["path"]))
                    critical.add(Path(item["file"]["path"]))
        for row in json.loads((previous / data["compilerAuxiliaryInputs"]["path"]).read_text())["inputs"]:
            same(row["file"])
            path = Path(row["file"]["path"])
            paths.add(path)
            critical.add(path)
        before = [binding(path) for path in sorted(paths)]
        write(self.work / "graph-refresh-before.json", before)
        # execute() checks stable source/SDK/catalog inputs around the refresh. These
        # generated outputs are instead compared explicitly afterward, with a retained
        # before/after inventory even if the incremental graph changes them.
        self.gates = [item for item in self.gates if Path(item["path"]) not in paths]
        self.config.update(stage="graph-refresh")
        write(self.config_path, self.config)
        project = BUILDS / "test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj"
        arguments = self.build_arguments(project, original, self.work / "compiler/refresh")
        arguments.append("--no-restore")
        self.execute("original-debug-graph-refresh", arguments, self.mapping_environment)
        after = [binding(path) for path in sorted(paths)]
        write(self.work / "graph-refresh-after.json", after)
        current_paths = {path for path in (original / "bin").rglob("*") if path.is_file()}
        changes = [dict(before=old, after=new) for old, new in zip(before, after) if old != new]
        write(self.work / "graph-refresh-changes.json", changes)
        require(current_paths <= paths, "Graph refresh added outputs; affected gates must rerun")
        require(all(Path(row["before"]["path"]) not in critical
                    and Path(row["before"]["path"]).is_relative_to(original / "bin") for row in changes),
                "Graph refresh changed test/helper or direct/auxiliary compiler inputs; affected cases must rerun")
        from capture_compiler import compiler_command
        def commands(path):
            rows = {}
            with path.open() as stream:
                for line in stream:
                    parsed = compiler_command(line)
                    if parsed:
                        output = next(value.split(":", 1)[1] for value in parsed[1] if value.lower().startswith("/out:"))
                        rows[output] = parsed
            return rows
        prior_commands = commands(previous / "logs/original-debug-build.log")
        refreshed_commands = commands(self.logs / "original-debug-graph-refresh.log")
        require(prior_commands == refreshed_commands, "Graph refresh did not recompile the exact original compiler vectors")
        required = {"Hexalith.Builds.Module.Cli", "Hexalith.Builds.Tooling", "Hexalith.Builds.Module.AppHost",
                    "Hexalith.Builds.Module.EventStoreHost", "Hexalith.Builds.Module.UiHost", "Hexalith.Builds.Module.Tests"}
        require(required <= {Path(path).stem for path in refreshed_commands}, "Actual CLI/host/test Csc execution absent")
        write(self.work / "graph-refresh-compiler-commands.json", dict(priorLog=binding(previous / "logs/original-debug-build.log"),
              refreshLog=binding(self.logs / "original-debug-graph-refresh.log", self.work), commands=refreshed_commands,
              exactArgumentEquality=True, intentionalArgumentDifferences=[]))
        for path in (self.work / "compiler/refresh").glob("*.txt"):
            prior_capture = previous / "compiler/original" / path.name
            require(prior_capture.is_file() and path.read_bytes() == prior_capture.read_bytes(), "Graph refresh changed the source/reference compiler vector: " + path.name)
        self.gates.extend(after)
        self.result["originalGraphRefresh"] = dict(command="original-debug-graph-refresh", restore=False,
            before=binding(self.work / "graph-refresh-before.json", self.work), after=binding(self.work / "graph-refresh-after.json", self.work),
            boundFiles=len(before), criticalFiles=len(critical), criticalInputsUnchanged=True,
            compilerCommands=binding(self.work / "graph-refresh-compiler-commands.json", self.work), actualCompilations=len(refreshed_commands),
            changes=binding(self.work / "graph-refresh-changes.json", self.work), changedNonTestOutputFiles=len(changes),
            cases=str(sum(row["total"] for row in data["testClasses"])) + " previously executed cases reused after exact complete runner/helper/deps/Tooling/direct and auxiliary compiler input equality; no test case re-executed",
            affectedGates="Private application output differences receive fresh model, sealing, selection and PE gates; no runtime/loader credit")
        self.result["reusedSourceEvidence"]["graphAndClasses"] = "original Debug graph actually recompiled with exact compiler arguments; discovered cases reused after exact relevant byte equality"
        self.result["reusedSourceEvidence"]["sourcePreservation"] = "Source/runner/helper/compiler/SDK/catalog bindings are exact; any private application output differences are recorded and freshly sealed"

    def run_focused_factory(self):
        """Independent linked-file diagnostics; these cannot replace original-assembly acceptance."""
        self.result["lane"] = "independent factory diagnostics only; original distribution qualification remains blocked"
        self.failure = dict(type="UnavailablePinnedCatalogObject", owner="Platform/Builds catalog object availability",
            detail="Pinned Builds commit 4ebd4d869fe1d3958d73e586bcaa74bd86e2ce42 is unavailable in the approved root Builds object database; no dependent distribution gate was resumed",
            dependentExecutionStopped=True)
        observations = []
        for arguments in (["cat-file", "-t", "4ebd4d869fe1d3958d73e586bcaa74bd86e2ce42"],
                          ["show", "4ebd4d869fe1d3958d73e586bcaa74bd86e2ce42:Props/Directory.Packages.props"],
                          ["ls-tree", "-z", "4ebd4d869fe1d3958d73e586bcaa74bd86e2ce42", "--", "Props/Directory.Packages.props"]):
            self.inputs()
            argv = ["git", "-C", str(BUILDS), *arguments]
            observed = subprocess.run(argv, env=dict(os.environ, GIT_OPTIONAL_LOCKS="0"), stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=30)
            observations.append(dict(argv=argv, environmentAddition={"GIT_OPTIONAL_LOCKS": "0"}, exitCode=observed.returncode,
                                     stdout=observed.stdout.decode(errors="replace"), stderr=observed.stderr.decode(errors="replace")))
        require(all(row["exitCode"] == 128 for row in observations), "Pinned catalog availability changed; re-assess before qualification")
        self.result["catalogBlocker"] = dict(owner="Platform/Builds catalog object availability", observations=observations,
            requiredRepair="Separately approve restoration of the verified pinned object/import closure in the same root Builds repository, or a distinct proposal to repair invalid pin authority; no fetch, object write, pin change or fallback is authorized here")
        self.result["focusedCatalog"] = dict(file=binding(BUILDS / "Props/Directory.Packages.props"), scope="Live source catalog used only by independent linked-file diagnostics; it does not replace or qualify the unavailable production-pinned catalog")
        require(self.execute("focused-sdk-version", [self.sdk, "--version"], timeout=30).read_text().strip() == SDK_VERSION, "Wrong focused SDK")
        project = self.work / "focused/FactorySelection.csproj"
        project.parent.mkdir()
        tests = BUILDS / "test/Hexalith.Builds.Module.Tests"
        # NuGet must discover the central catalog during SDK props evaluation, before
        # it decides whether central package management applies to this private project.
        (project.parent / "Directory.Packages.props").write_text('<Project><Import Project="' + str(BUILDS / 'Props/Directory.Packages.props') + '" /></Project>\n')
        project.write_text('<Project Sdk="Microsoft.NET.Sdk"><Import Project="' + str(BUILDS / 'Hexalith.Build.props') + '" />'
            + '<PropertyGroup><TargetFramework>net10.0</TargetFramework>'
            + '<AssemblyName>Hexalith.Builds.Module.Tests</AssemblyName><IsTestProject>true</IsTestProject><IsPackable>false</IsPackable></PropertyGroup>'
            + '<ItemGroup><PackageReference Include="Microsoft.NET.Test.Sdk" /><PackageReference Include="Shouldly" /><PackageReference Include="xunit.v3" />'
            + '<ProjectReference Include="' + str(BUILDS / 'src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj') + '" />'
            + ''.join('<Compile Include="' + str(tests / name) + '" Link="' + name + '" />' for name in ('CompositionCommandOptionsTests.cs', 'CompositionTestFiles.cs', 'PlatformManifestTestGrouping.cs'))
            + '</ItemGroup></Project>\n')
        artifacts = self.work / "artifacts/focused"
        self.execute("focused-factory-build", self.build_arguments(project, artifacts, self.work / "compiler/focused"))
        self.result["focusedAssets"] = assets_audit(list(artifacts.rglob("project.assets.json")))
        assembly = artifacts / "bin/FactorySelection/debug/Hexalith.Builds.Module.Tests"
        full_name = "Hexalith.Builds.ModuleTool.Tests.CompositionCommandOptionsTests"
        report = self.work / "focused-factory.xml"
        self.execute("focused-factory-tests", [assembly, "-class", full_name, "-parallel", "none", "-xml", report], timeout=120)
        self.result["supplementaryFactoryTests"] = class_result(report, full_name, 8)
        self.execute("harness-guards", [sys.executable, "-B", "-m", "unittest", "discover", "-s", str(HERE), "-p", "test_*.py", "-v"], timeout=120)
        compiler = []
        for path in sorted((self.work / "compiler/focused").glob("*.txt")):
            inputs = []
            for line in path.read_text().splitlines():
                kind, name = line.split("|", 1)
                inputs.append(dict(kind=kind, file=binding(name)))
            compiler.append(dict(capture=binding(path, self.work), inputs=inputs))
        write(self.work / "compiler-inputs.json", compiler)
        self.result["compilerInputs"] = binding(self.work / "compiler-inputs.json", self.work)

    def seal_outputs(self, original, model):
        projects = {"CLI": "Hexalith.Builds.Module.Cli", "Tooling": "Hexalith.Builds.Tooling", "AppHost": "Hexalith.Builds.Module.AppHost",
                    "EventStoreHost": "Hexalith.Builds.Module.EventStoreHost", "UiHost": "Hexalith.Builds.Module.UiHost"}
        self.result["outputs"] = {}
        for label, project in projects.items():
            source = original / "bin" / project / "debug"
            require((source / (project + ".dll")).is_file(), "Required original output missing: " + project)
            destination = self.work / "distribution" / label
            shutil.copytree(source, destination)
            self.result["outputs"][label] = binding(destination / (project + ".dll"), self.work)
        shutil.copytree(model.parent, self.work / "distribution/Model")
        self.result["outputs"]["Model"] = binding(self.work / "distribution/Model" / model.name, self.work)
        self.seal = [binding(path, self.work) for path in sorted((self.work / "distribution").rglob("*")) if path.is_file()]
        write(self.work / "output-seal.json", self.seal)
        tooling_hashes = {sha(self.work / "distribution" / label / "Hexalith.Builds.Tooling.dll") for label in ("CLI", "Tooling", "AppHost", "Model")}
        require(len(tooling_hashes) == 1, "CLI/host/model Tooling bytes are incompatible")
        runner = self.work / self.result["outputs"]["CLI"]["path"]
        apphost = self.work / self.result["outputs"]["AppHost"]["path"]
        selection_log = self.execute("explicit-host-selection", [self.helper.with_suffix(""), "select", runner, apphost], timeout=30)
        selection = json.loads(selection_log.read_text())
        require(selection["AppHostAssemblyPath"] == str(apphost) and selection["DescriptorChildEntryAssemblyPath"] == str(runner), "Supported factory did not select exact sealed host")
        self.result["selection"] = dict(selection, hostBinding=binding(apphost, self.work), evidence="supported factory API selection only; no public command or host was run")
        paths = [str(path) for path in sorted((self.work / "distribution").rglob("Hexalith.*.dll"))]
        write(self.work / "pe-inputs.json", paths)
        metadata_log = self.execute("metadata-only-inspection", [self.helper.with_suffix(""), "inspect", self.work / "pe-inputs.json"], timeout=60)
        metadata = json.loads(metadata_log.read_text())
        write(self.work / "pe-metadata.json", metadata)
        self.result["modelReferenceIdentities"] = verify_pe_references(metadata, self.work / "distribution/Model" / model.name)
        self.result["proposedOrdinaryInvocation"] = self.ordinary_invocation()
        self.result["proposedOrdinaryInvocation"]["status"] = "sealed diagnostic source artifacts selected; ordinary startup proposal only; not executed"
        self.result["proposedOrdinaryInvocation"]["prerequisites"] = self.result["proposedOrdinaryInvocation"]["prerequisites"][2:]

    def finish(self):
        errors = []
        try:
            self.inputs()
        except Exception as error:
            errors.append(str(error))
        try:
            after_stores = store_metadata()
            write(self.work / "certificate-stores-after.json", after_stores)
            require(after_stores == self.baseline_stores, "Certificate-store metadata drift")
        except Exception as error:
            errors.append(str(error))
        self.result["preservation"] = dict(status="verified" if not errors else "failed-or-unknown", errors=errors,
                                           repositoryCount=len(getattr(self, "baseline_repos", [])), boundSourceCount=len(getattr(self, "baseline_sources", [])))
        if errors or self.failure:
            self.result["qualification"] = "incomplete"
        self.result["firstFailure"] = self.failure
        mapping_path = self.work / "workspace/source-mapping/mapping.json"
        if mapping_path.is_file() and "productionMapping" not in self.result:
            self.result["resolvedMappingBeforeFailure"] = json.loads(mapping_path.read_text())
            self.result["catalogMaterialization"] = "failed; resolved source origins grant no compilation/distribution acceptance"
        self.result["historicalEvidence"] = dict(status="dated evidence only", reason="Current root revisions and source inputs advanced; historical loader/source-host results grant no current distribution/runtime compatibility credit.")
        write(self.work / "result.json", self.result)


def archive(work, record_id):
    """Retain selected immutable evidence and complete sealed distribution, outside Git."""
    result_path = work / "result.json"
    result = json.loads(result_path.read_text())
    def capture_error(detail):
        result.setdefault("evidenceCaptureErrors", []).append(detail)
        result["qualification"] = "incomplete"
        if result.get("firstFailure") is None:
            result["firstFailure"] = dict(owner="evidence capture", detail=detail, dependentExecutionStopped=True)
    if result.get("privateArtifacts"):
        runner = Path(result["privateArtifacts"]) / "original/bin/Hexalith.Builds.Module.Tests/debug"
        if runner.exists():
            retained_runner = []
            for name in ("Hexalith.Builds.Module.Tests", "Hexalith.Builds.Module.Tests.dll", "Hexalith.Builds.Module.Tests.pdb", "Hexalith.Builds.Module.Tests.deps.json", "Hexalith.Builds.Module.Tests.runtimeconfig.json", "Hexalith.Builds.Tooling.dll"):
                source = runner / name
                destination = work / "original-test-runner" / name
                destination.parent.mkdir(exist_ok=True)
                try:
                    shutil.copy2(source, destination)
                    retained_runner.append(dict(source=binding(source), retained=binding(destination, work)))
                except (OSError, GateFailure) as error:
                    capture_error("Runner companion capture failed: " + name + ": " + str(error))
            result["originalTestRunnerBindings"] = retained_runner
    write(result_path, result)
    if result.get("privateArtifacts") and (work / "logs/bootstrap-build.log").is_file():
        from capture_compiler import capture
        try:
            report = capture(work)
            result["compilerAuxiliaryInputs"] = binding(work / "compiler-auxiliary-inputs.json", work)
            result["compilerAuxiliaryCapture"] = dict(status="bound", commands=len(report["commands"]), files=len(report["inputs"]))
        except (OSError, ValueError, KeyError, GateFailure) as error:
            result["compilerAuxiliaryCapture"] = dict(status="failed", detail=str(error))
            capture_error("Compiler-input evidence capture failed: " + str(error))
        write(result_path, result)
    members = []
    selected = []
    excluded = {"artifacts", "xdg-cache", "xdg-config", "powershell-cache", *[name.lower() for name in PRIVATE]}
    for path in sorted(work.rglob("*")):
        relative = path.relative_to(work)
        # Runtime caches may contain CLR FIFOs/sockets. Exclude them before inspecting
        # file kinds; evidence outside these private cache roots still fails closed.
        if relative.parts[0] in excluded:
            continue
        if path.is_dir():
            continue
        require(not path.is_symlink() and stat.S_ISREG(path.lstat().st_mode), "Cannot archive non-regular evidence: " + str(path))
        if relative.parts[:2] == ("workspace", "build"):
            # Retain actual assets, generated catalogs and compiler references separately below.
            if path.name != "project.assets.json":
                continue
        selected.append(path)
    # Retain compiler inputs themselves, including generated sources and ref assemblies.
    compiler_path = work / "compiler-inputs.json"
    copies = work / "compiler-bytes"
    if compiler_path.is_file():
        for record in json.loads(compiler_path.read_text()):
            for item in record["inputs"]:
                source = Path(item["file"]["path"])
                try:
                    same(item["file"])
                    destination = copies / item["file"]["sha256"] / source.name
                    if not destination.exists():
                        destination.parent.mkdir(parents=True, exist_ok=True)
                        shutil.copyfile(source, destination)
                        selected.append(destination)
                except (OSError, GateFailure) as error:
                    capture_error("Compiler input copy failed: " + str(error))
    write(result_path, result)
    archive_path = EVIDENCE / (record_id + ".artifacts.tar.gz")
    require(not archive_path.exists(), "Evidence archive already exists; select a new run suffix")
    with tarfile.open(archive_path, "x:gz") as output:
        for path in sorted(set(selected)):
            item = binding(path, work)
            output.add(path, arcname=item["path"], recursive=False)
            same(item, work)
            members.append(item)
    manifest_path = EVIDENCE / (record_id + ".members.json")
    write(manifest_path, members)
    result = json.loads((work / "result.json").read_text())
    receipt = dict(schemaVersion=1, recordId=record_id, archive=binding(archive_path, EVIDENCE), memberManifest=binding(manifest_path, EVIDENCE),
                   memberCount=len(members), result=result, privateWorkspace=str(work),
                   archivePolicy="Ignored local archive per archives-out-of-git.md. No external copy/upload; copy to this filename before replay elsewhere.",
                   retention="Complete sealed CLI/Tooling/three-host/model outputs, selected assets, actual compiler source/reference bytes, catalog/imports, commands and full logs/binlogs. Third-party caches and unrelated build intermediates are omitted.")
    write(EVIDENCE / (record_id + ".json"), receipt)
    return receipt


def focused_success(result):
    return (result.get("supplementaryFactoryTests", {}).get("passed") == 8
            and result.get("preservation", {}).get("status") == "verified"
            and (result.get("firstFailure") or {}).get("type") == "UnavailablePinnedCatalogObject"
            and not result.get("supplementaryFailure") and not result.get("evidenceCaptureErrors")
            and result.get("compilerAuxiliaryCapture", {}).get("status") != "failed"
            and all(row.get("exitCode") == 0 and not row.get("timedOut") for row in result.get("commands", [])))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    lane = parser.add_mutually_exclusive_group(required=True)
    lane.add_argument("--execute", action="store_true", help="Build/qualify source artifacts only; never start applications")
    lane.add_argument("--focused-factory", action="store_true", help="Run independent factory diagnostics without resuming blocked distribution gates")
    parser.add_argument("--resume-source", type=Path, help="Reuse an exact passing graph/classes receipt after a model-only wrapper correction")
    parser.add_argument("--suffix", default="", help="Unique additive evidence run suffix")
    args = parser.parse_args()
    require(not args.suffix or all(character.isalnum() or character in "-_" for character in args.suffix), "Unsafe evidence suffix")
    record_id = PREFIX + ("-" + args.suffix if args.suffix else "")
    require(not (EVIDENCE / (record_id + ".json")).exists(), "Existing receipt is immutable; choose --suffix")
    work = Path(tempfile.mkdtemp(prefix=record_id + "-"))
    work.chmod(0o700)
    qualification = Qualification(work)
    try:
        qualification.prepare()
        if args.resume_source:
            require(args.execute, "Source reuse requires the build-only execute lane")
            qualification.resume_source(args.resume_source)
        elif args.focused_factory:
            qualification.run_focused_factory()
        else:
            qualification.run()
    except Exception as error:
        failure = dict(type=type(error).__name__, detail=str(error), owner="first failing command/input owner; see exact command and retained log", dependentExecutionStopped=True)
        if qualification.failure is None:
            qualification.failure = failure
        else:
            qualification.result["supplementaryFailure"] = failure
        print(json.dumps(failure), flush=True)
    finally:
        qualification.finish()
        receipt = archive(work, record_id)
        print(json.dumps(dict(receipt=str(EVIDENCE / (record_id + ".json")), work=str(work), qualification=receipt["result"]["qualification"])), flush=True)
    focused_passed = args.focused_factory and focused_success(receipt["result"])
    return 0 if receipt["result"]["qualification"] == "passed" or focused_passed else 2


if __name__ == "__main__":
    raise SystemExit(main())
