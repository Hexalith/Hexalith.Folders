#!/usr/bin/env python3
"""Build-only production model qualification. No wrappers, loaders or application startup."""
import argparse
import json
import os
from pathlib import Path
import resource
import re
import shlex
import shutil
import signal
import subprocess
import tarfile
import tempfile
import threading
import time
import xml.etree.ElementTree as ET

import qualify as q

PREFIX = "story-1-17-production-source-model-2026-10-30"
CLASSES = ("Workspace.PlatformModelCatalogTests", "CompositionSdkProcessTests", "CompositionProcessTests")


def preflight(environment):
    """Ambient imports and executable redirection cannot repair the production child."""
    forbidden = ("AlternateCommonProps", "MSBuildSDKsPath", "MSBUILD_EXE_PATH", "DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR",
                 "HEXALITH_TEST_DOTNET_EXECUTABLE", "HEXALITH_TEST_SDK_INTERCEPTION_REQUIRED", *q.IMPORTS)
    q.require(not any(environment.get(key) for key in forbidden), "Unsafe ambient import/SDK controls")
    sdk = Path(shutil.which("dotnet", path=environment.get("PATH")) or "").resolve()
    q.require(sdk.is_file() and (sdk.parent / "sdk/10.0.403/dotnet.dll").is_file(), "Installed Builds SDK 10.0.403 is required")
    q.require(json.loads((q.BUILDS / "global.json").read_text())["sdk"]["version"] == "10.0.403", "Builds SDK pin advanced; reassess")
    return sdk


def validate_environment(environment, work, preserved_home):
    """Refuse unsafe launch controls without repairing the selected command."""
    q.require(all(environment.get(key) == value for key, value in q.CONTROLS.items()), "Unsafe SDK prevention/serialization environment")
    q.require(environment.get("HOME") == preserved_home, "HOME must be preserved")
    for key in q.PRIVATE:
        path = Path(environment.get(key, ""))
        q.require(path.is_absolute() and path.resolve().is_relative_to(work), "SDK cache escapes private workspace: " + key)
    q.require(not any(environment.get(key) for key in ("AlternateCommonProps", "MSBuildSDKsPath", "MSBUILD_EXE_PATH", "DOTNET_STARTUP_HOOKS", "HEXALITH_TEST_DOTNET_EXECUTABLE", "HEXALITH_TEST_SDK_INTERCEPTION_REQUIRED")), "Injected SDK/import controls")


def source_inputs():
    """Bind owning build inputs; unrelated planning documents are observed separately."""
    paths = []
    for repository in q.repositories():
        names = q.git(repository, "ls-files", "--cached", "--others", "--exclude-standard", "-z").split(b"\0")
        for name in names:
            if not name:
                continue
            relative = Path(os.fsdecode(name))
            if relative.parts[0] in {"_bmad-output", "evidence", "docs", ".agents", ".claude", ".github", "archives"}:
                continue
            path = repository / relative
            if path.is_file() and not path.is_symlink():
                paths.append(path)
    return sorted(set(paths))


def test_result(path, full_name, discovery):
    from verify import verify_case_identities
    assembly = ET.parse(path).getroot().find("assembly")
    q.require(assembly is not None, "Missing original-assembly test report")
    tests = assembly.findall(".//test")
    total = int(assembly.get("total", "0"))
    q.require(total == len(discovery) == len(tests) > 0 and int(assembly.get("passed", "0")) == total, "Incomplete original class: " + full_name)
    q.require(all(int(assembly.get(key, "0")) == 0 for key in ("errors", "failed", "skipped", "not-run")), "Failed/skipped/unrun class")
    q.require(all(test.get("result") == "Pass" and test.get("type") == full_name for test in tests), "Wrong selected class")
    verify_case_identities(tests, discovery)
    return dict(name=full_name, total=total, passed=total, failed=0, errors=0, skipped=0, notRun=0, discovered=len(discovery), report=q.binding(path, path.parent))


def retain_git_authority(repository, revision, paths, work):
    """Retain native Git object bytes so portable replay can prove the selected pin."""
    objects = {}
    def retain(kind, oid):
        raw = q.git(repository, "cat-file", kind, oid)
        destination = work / "git-objects" / oid
        destination.parent.mkdir(exist_ok=True)
        if not destination.exists():
            destination.write_bytes(raw)
        objects[oid] = dict(kind=kind, oid=oid, file=q.binding(destination, work))
        return raw
    commit = retain("commit", revision)
    root = commit.split(b"\n", 1)[0].decode().removeprefix("tree ")
    selections = {}
    for path in paths:
        oid, kind = root, "tree"
        for part in Path(path).parts:
            q.require(kind == "tree", "Authority path traverses non-tree")
            raw = retain(kind, oid)
            offset, entries = 0, {}
            while offset < len(raw):
                space = raw.index(b" ", offset)
                zero = raw.index(b"\0", space)
                mode, name = raw[offset:space], os.fsdecode(raw[space+1:zero])
                entries[name] = (raw[zero+1:zero+21].hex(), "tree" if mode == b"40000" else "commit" if mode == b"160000" else "blob")
                offset = zero + 21
            q.require(part in entries, "Missing Git authority path: " + path)
            oid, kind = entries[part]
        if kind != "commit":
            retain(kind, oid)
        selections[path] = dict(kind=kind, oid=oid)
    return dict(revision=revision, paths=selections, objects=list(objects.values()))


def observe_processes(group, stop, observations):
    """Read only /proc. Never intercept, replace, or modify a child."""
    seen = set()
    while not stop.is_set():
        for entry in Path("/proc").iterdir():
            if not entry.name.isdigit():
                continue
            try:
                status = (entry / "stat").read_text().rsplit(")", 1)[1].split()
                if int(status[2]) != group:
                    continue
                argv = [os.fsdecode(word) for word in (entry / "cmdline").read_bytes().split(b"\0") if word]
                key = (entry.name, tuple(argv))
                if not argv or key in seen:
                    continue
                seen.add(key)
                environment = dict(os.fsdecode(word).split("=", 1) for word in (entry / "environ").read_bytes().split(b"\0") if b"=" in word)
                row = dict(pid=int(entry.name), argv=argv, cwd=os.readlink(entry / "cwd"),
                           executable=os.readlink(entry / "exe"), environment=environment)
                responses = []
                for argument in argv:
                    if argument.startswith("@"):
                        path = Path(argument[1:])
                        if not path.is_absolute():
                            path = Path(row["cwd"]) / path
                        responses.append(dict(file=q.binding(path), text=path.read_text()))
                if responses:
                    row["responses"] = responses
                observations.append(row)
            except (OSError, ValueError, ProcessLookupError, q.GateFailure):
                continue
        stop.wait(.01)


class ModelQualification(q.Qualification):
    def prepare(self):
        self.sdk = str(preflight(os.environ))
        self.artifacts = self.work / "artifacts"
        self.artifacts.mkdir()
        self.baseline_repos = q.repository_state()
        self.baseline_sources = [q.binding(path, q.ROOT) for path in source_inputs()]
        self.baseline_stores = q.store_metadata()
        self.layout_links = {}
        pwsh = Path(shutil.which("pwsh") or "").resolve()
        q.require(pwsh.is_file(), "Installed PowerShell is required")
        self.gates = [q.binding(self.sdk), q.binding(pwsh)]
        sdk_root = Path(self.sdk).parent
        for directory in (sdk_root / "sdk/10.0.403", sdk_root / "host", sdk_root / "shared"):
            self.gates.extend(q.binding(path) for path in sorted(directory.rglob("*")) if path.is_file() and not path.is_symlink())
        environment = dict(q.CONTROLS, HOME=str(Path.home()), LANG="C.UTF-8", DOTNET_ROOT=str(sdk_root), DOTNET_HOST_PATH=self.sdk,
                           PATH=str(sdk_root) + ":" + str(pwsh.parent) + ":/usr/local/bin:/usr/bin:/bin")
        for key in q.PRIVATE:
            path = self.work / key.lower()
            path.mkdir(mode=0o700)
            environment[key] = str(path)
        self.config = dict(environment=environment, versionSuffix=time.strftime("%Y%m%d%H%M%S", time.gmtime()))
        self.nuget = self.work / "NuGet.Config"
        self.nuget.write_text('<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources></configuration>\n')
        for relative in ("Current/Microsoft.Common.props", "Sdks/Microsoft.NET.Sdk/Sdk/Sdk.props"):
            destination = self.work / "sdk-inputs" / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(sdk_root / "sdk/10.0.403" / relative, destination)
        self.result.update(schemaVersion=1, lane="unwrapped production model", initialRepositories=self.baseline_repos,
                           environment=environment, privateArtifacts=str(self.artifacts), protectedAuthorityEvidence=q.protected_authority(),
                           historicalEvidence=dict(status="not reused", reason="All current graph, API, fixture and metadata gates run fresh."))
        self.result["protectedCopies"] = []
        for index, item in enumerate(self.result["protectedAuthorityEvidence"]["files"]):
            source = q.ROOT / item["path"]
            destination = self.work / "protected" / (str(index) + source.suffix)
            destination.parent.mkdir(exist_ok=True)
            shutil.copy2(source, destination)
            self.result["protectedCopies"].append(dict(source=item, retained=q.binding(destination, self.work)))
        for name, value in (("repository-baseline.json", self.baseline_repos), ("source-baseline.json", self.baseline_sources),
                            ("certificate-stores-before.json", self.baseline_stores), ("sdk-bindings.json", self.gates)):
            q.write(self.work / name, value)
        shutil.copytree(q.HERE, self.work / "harness", ignore=shutil.ignore_patterns("__pycache__"))
        for index, repository in enumerate(q.repositories()):
            (self.work / ("baseline-diff-" + str(index) + ".patch")).write_bytes(q.git(repository, "diff", "--binary", "--no-ext-diff", "HEAD"))
        helper = self.work / "helper"
        helper.mkdir()
        shutil.copy2(q.HERE / "Probe.cs", helper / "Program.cs")
        self.helper_project = helper / "DistributionProbe.csproj"
        self.helper_project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><ProjectReference Include="' + str(q.BUILDS / 'src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj') + '" /></ItemGroup></Project>\n')
        self.result["proposedOrdinaryInvocation"] = self.ordinary_invocation()
        self.history = [q.binding(path, q.ROOT) for path in sorted(q.EVIDENCE.glob("story-1-17-*")) if path.is_file()]
        q.write(self.work / "historical-baseline.json", self.history)
        parent_baseline = Path("/tmp/story117-model-parent-0t66yin4/baseline.json")
        if parent_baseline.is_file():
            shutil.copy2(parent_baseline, self.work / "parent-baseline.json")

    def inputs(self):
        q.require(q.repository_state() == self.baseline_repos, "Repository/index/gitlink drift")
        q.require({str(path.relative_to(q.ROOT)) for path in source_inputs()} == {row["path"] for row in self.baseline_sources}, "Build input inventory drift")
        q.check_sources(self.baseline_sources)
        for row in self.result["protectedAuthorityEvidence"]["files"]:
            q.same(row, q.ROOT)
        for row in self.gates:
            q.same(row)
        for row in self.seal:
            q.same(row, self.work)

    def build_arguments(self, project, artifacts, capture=None):
        return [self.sdk, "build", str(project), "--configuration", "Debug", "--artifacts-path", str(artifacts),
                "-p:RestoreConfigFile=" + str(self.nuget), "-p:NuGetAudit=false", "-p:MinVerVersionOverride=1.0.0",
                "-p:VersionSuffix=" + self.config["versionSuffix"], "-p:HexalithG4FixtureArtifacts=" + str(self.work / "fixtures") + "/",
                *q.SERIAL, "-v:diag", "-bl:" + str(self.logs / (artifacts.name + "-" + Path(project).stem + ".binlog"))]

    def execute(self, label, argv, environment=None, timeout=900):
        # Monitor the real process group without changing any argv, imports or environment.
        self.inputs()
        selected = dict(self.config["environment"], **(environment or {}))
        validate_environment(selected, self.work, self.config["environment"]["HOME"])
        row = dict(label=label, argv=[str(arg) for arg in argv], cwd=str(q.BUILDS), environment=selected,
                   timeoutSeconds=timeout, launched=False, exitCode=None, timedOut=False)
        self.commands.append(row)
        q.write(self.work / "commands.json", self.commands)
        print(json.dumps(dict(stage=label, status="starting")), flush=True)
        log = self.logs / (label + ".log")
        observations, stop = [], threading.Event()
        started = time.monotonic()
        try:
            with log.open("wb") as output:
                process = subprocess.Popen(row["argv"], cwd=q.BUILDS, env=selected, stdin=subprocess.DEVNULL,
                                           stdout=output, stderr=subprocess.STDOUT, start_new_session=True,
                                           preexec_fn=lambda: resource.setrlimit(resource.RLIMIT_CORE, (0, 0)))
                row.update(launched=True, pid=process.pid)
                monitor = threading.Thread(target=observe_processes, args=(process.pid, stop, observations), daemon=True)
                monitor.start()
                try:
                    process.wait(timeout=timeout)
                except subprocess.TimeoutExpired:
                    row["timedOut"] = True
                finally:
                    try:
                        os.killpg(process.pid, signal.SIGKILL)
                    except ProcessLookupError:
                        pass
                    process.wait(timeout=10)
                    stop.set()
                    monitor.join(timeout=10)
                row["exitCode"] = process.returncode
        finally:
            row.update(seconds=round(time.monotonic() - started, 3), log=q.binding(log, self.work))
            observations_path = self.work / "processes" / (label + ".json")
            q.write(observations_path, observations)
            row["processes"] = q.binding(observations_path, self.work)
            q.write(self.work / "commands.json", self.commands)
        print(json.dumps({key: row[key] for key in ("label", "exitCode", "timedOut", "seconds")}), flush=True)
        q.require(row["exitCode"] == 0 and not row["timedOut"], "Command failed: " + label + "; argv=" + repr(row["argv"]) + "; result=" + str(row["exitCode"]) + "; log=" + str(log))
        self.inputs()
        return log

    def run(self):
        version = self.execute("sdk-version", [self.sdk, "--version"], timeout=30).read_text().strip()
        q.require(version == "10.0.403", "Builds must resolve installed SDK 10.0.403")
        self.result["sdkVersion"] = version
        bootstrap = self.artifacts / "original"
        self.execute("helper-build", self.build_arguments(self.helper_project, bootstrap))
        self.helper = bootstrap / "bin/DistributionProbe/debug/DistributionProbe.dll"
        control_log = self.execute("production-controls", [self.sdk, self.helper, "production-controls", q.BUILDS])
        self.result["productionControlObservation"] = json.loads(control_log.read_text())
        if self.result["productionControlObservation"]["environment"].get("DOTNET_GENERATE_ASPNET_CERTIFICATE") != "false":
            raise q.GateFailure("Pre-repair refusal: production CompositionProcess drops DOTNET_GENERATE_ASPNET_CERTIFICATE; unsafe preparation/model children refused before SDK launch")
        cli_project = q.BUILDS / "src/libraries/Hexalith.Builds.Module.Cli/Hexalith.Builds.Module.Cli.csproj"
        self.execute("cli-build", self.build_arguments(cli_project, bootstrap))
        self.cli = bootstrap / "bin/Hexalith.Builds.Module.Cli/debug/Hexalith.Builds.Module.Cli.dll"
        self.result["apiInputBindings"] = []
        for role, directory in (("Helper", self.helper.parent), ("CLI", self.cli.parent)):
            for path in sorted(directory.iterdir()):
                if path.is_file():
                    item = q.binding(path)
                    self.gates.append(item)
                    self.result["apiInputBindings"].append(dict(role=role, source=item, retainedPath="distribution/" + role + "/" + path.name))
        self.mapping_workspace = self.work / "workspace"
        manifest = q.ROOT / "references/Hexalith.Parties/module/hexalith.module-manifest.v2.json"
        preparation = self.execute("prepare-build", [self.sdk, self.cli, "prepare-build", "--manifest", manifest,
                                                      "--workspace", self.mapping_workspace, "--mode", "source", "--output", "json"])
        document = json.loads(preparation.read_text())
        self.mapping_environment = document["buildPreparation"]["environment"]
        self.mapping = json.loads((self.mapping_workspace / "source-mapping/mapping.json").read_text())
        q.require(len(self.mapping["Entries"]) == 12 and all(entry["Origin"] == "source" for entry in self.mapping["Entries"]), "Production mapping source fallback")
        self.result.update(productionMapping=self.mapping, productionEnvironment=self.mapping_environment, preparation=document)
        identity = self.mapping["PlatformIdentity"]
        snapshot = json.loads((self.mapping_workspace / "source-mapping/catalog-versions.json").read_text())
        authority = dict(platform=retain_git_authority(Path(identity["PlatformPath"]), identity["CheckoutCommit"], ["references/Hexalith.Builds"], self.work),
                         catalog=retain_git_authority(q.BUILDS, identity["CatalogCommit"], list(snapshot["PinnedFiles"]), self.work))
        q.write(self.work / "git-authority.json", authority)
        self.gates.extend(q.binding(path) for path in self.mapping_workspace.rglob("*") if path.is_file())
        self.gates.extend(q.binding(path) for directory in (self.helper.parent, self.cli.parent) for path in directory.iterdir() if path.is_file())
        self.run_model()
        project = q.BUILDS / "test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj"
        self.execute("original-test-build", self.build_arguments(project, bootstrap), self.mapping_environment)
        self.result["testClasses"] = []
        self.result["classDiscoveries"] = {}
        source_runner = bootstrap / "bin/Hexalith.Builds.Module.Tests/debug"
        runner = q.BUILDS / ".artifacts" / self.work.name / "bin" / "runner"
        runner.parent.mkdir(parents=True, mode=0o700)
        q.require(q.git(q.BUILDS, "check-ignore", str(runner)).strip(), "Private runner must be ignored")
        shutil.copytree(source_runner, runner)
        runner.chmod(0o700)
        relocation = []
        for source in sorted(source_runner.rglob("*")):
            if source.is_file():
                destination = runner / source.relative_to(source_runner)
                before, after = q.binding(source), q.binding(destination)
                q.require(before["sha256"] == after["sha256"] and before["bytes"] == after["bytes"], "Runner relocation changed bytes")
                self.gates.extend([before, after])
                relocation.append(dict(source=before, destination=after))
        self.result["runnerRelocation"] = dict(reason="The original RepositoryRoot test helper discovers the real Builds root from AppContext.BaseDirectory; exact private byte-copy only, with no fake markers or assertions changed.", files=relocation)
        assembly = runner / "Hexalith.Builds.Module.Tests.dll"
        self.result["originalRunner"] = q.binding(assembly)
        for name in CLASSES:
            full_name = "Hexalith.Builds.ModuleTool.Tests." + name
            filters = ["-class", full_name]
            if name == "CompositionSdkProcessTests":
                filters.extend(["-method-", full_name + ".QualificationCatalogProbeInterceptionIsEstablishedAsync"])
                self.result["excludedHistoricalInterception"] = dict(method=full_name + ".QualificationCatalogProbeInterceptionIsEstablishedAsync", credit=False, reason="Historical interception mechanism is forbidden in the production lane; separate production bare-dotnet assertion runs fresh.")
            discovery_log = self.execute("discover-" + name, [self.sdk, assembly, "-list", "full/json", "-preEnumerateTheories", *filters, "-parallel", "none", "-noColor"])
            discovery = json.loads(discovery_log.read_text())
            q.require(discovery and all(row["Class"] == full_name and not row.get("Explicit") and not row.get("SkipReason") for row in discovery), "Skipped/empty discovery")
            self.result["classDiscoveries"][full_name] = dict(file=q.binding(discovery_log, self.work), cases=discovery)
            report = self.work / (name + ".xml")
            self.execute("class-" + name, [self.sdk, assembly, *filters, "-parallel", "none", "-xml", report])
            self.result["testClasses"].append(test_result(report, full_name, discovery))
        # Shared McpCli argument and authority assertions never invoke its lifecycle doubles.
        shared = "Hexalith.Builds.ModuleTool.Tests.McpCliRunBuildTests"
        filters = ["-method", shared + ".BuildArgumentsUseDebugRunWorkspaceArtifactsAndThePinnedCatalog", "-method", shared + ".McpCliBuildRefusesMissingOrAlteredCatalogAuthorityAsync"]
        discovery_log = self.execute("discover-mcpcli-guards", [self.sdk, assembly, "-list", "full/json", "-preEnumerateTheories", *filters, "-parallel", "none", "-noColor"])
        discovery = json.loads(discovery_log.read_text())
        self.result["classDiscoveries"][shared] = dict(file=q.binding(discovery_log, self.work), cases=discovery)
        report = self.work / "McpCliRunBuildTests.xml"
        self.execute("mcpcli-guards", [self.sdk, assembly, *filters, "-parallel", "none", "-xml", report])
        self.result["testClasses"].append(test_result(report, shared, discovery))
        self.result["originalAssets"] = self.capture_assets(list(bootstrap.rglob("project.assets.json")), "original")
        self.seal_outputs(bootstrap, self.model)
        self.result["qualification"] = "passed"

    def run_model(self):
        log = self.execute("production-model", [self.sdk, self.helper, "production-model", self.mapping_workspace / "source-mapping/mapping.json",
                                                self.mapping_environment["HEXALITH_SOURCE_MAPPING_HASH"], self.mapping_workspace], timeout=900)
        self.model = Path(json.loads(log.read_text())["assembly"])
        self.result["modelAssembly"] = q.binding(self.model)
        observations = json.loads((self.work / "processes/production-model.json").read_text())
        children = [row for row in observations if row["argv"][1:2] == ["build"] and str(q.ROOT / "references/Hexalith.Platform/src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj") in row["argv"]]
        q.require(len(children) == 1, "Exactly one real production model child must be observed")
        self.result["modelChild"] = children[0]
        self.result["modelAssets"] = self.capture_assets(list((self.mapping_workspace / "build/platform-model").rglob("project.assets.json")), "model")
        self.capture_compiler(observations)

    def capture_compiler(self, observations):
        from capture_compiler import auxiliary_inputs
        commands = []
        for row in observations:
            if Path(row["argv"][0]).name not in {"csc", "csc.exe"}:
                continue
            arguments = []
            for argument in row["argv"][1:]:
                if argument.startswith("@"):
                    response = next(item for item in row.get("responses", []) if item["file"]["path"] == argument[1:])
                    arguments.extend(shlex.split(response["text"]))
                else:
                    arguments.append(argument)
            output = next((argument.split(":", 1)[1] for argument in arguments if argument.startswith("/out:")), None)
            if not output or Path(output).name != "Hexalith.Platform.Composition.dll":
                continue
            project = Path(row["cwd"]) / "Hexalith.Platform.Composition.csproj"
            inputs = auxiliary_inputs(row["executable"], arguments, project)
            for argument in arguments:
                if argument.startswith(("/reference:", "/r:")):
                    inputs.append(dict(kind="reference", path=argument.split(":", 1)[1]))
                elif argument.endswith(".cs") and not re.match(r"^/[a-zA-Z]+:", argument):
                    path = Path(argument)
                    inputs.append(dict(kind="source", path=str(path if path.is_absolute() else Path(row["cwd"]) / path)))
            retained = []
            for item in inputs:
                source = Path(item["path"])
                bound = q.binding(source)
                destination = self.work / "compiler-bytes" / bound["sha256"] / source.name
                destination.parent.mkdir(parents=True, exist_ok=True)
                if not destination.exists():
                    shutil.copy2(source, destination)
                retained.append(dict(kind=item["kind"], file=bound, retained=q.binding(destination, self.work)))
            commands.append(dict(process=row, arguments=arguments, output=output, inputs=retained))
        q.require(len(commands) == 1 and {"source", "reference", "compiler"} <= {item["kind"] for item in commands[0]["inputs"]}, "Actual model compiler inputs unavailable")
        q.write(self.work / "model-compiler.json", commands)
        self.result["modelCompiler"] = q.binding(self.work / "model-compiler.json", self.work)

    def finish(self):
        # Reuse preservation checks, but keep the new lane's no-reuse declaration.
        super().finish()
        try:
            for item in getattr(self, "history", []):
                q.same(item, q.ROOT)
            self.result["historicalPreservation"] = dict(status="verified", files=len(getattr(self, "history", [])))
        except (OSError, q.GateFailure) as error:
            self.result["historicalPreservation"] = dict(status="failed", detail=str(error))
            self.result["qualification"] = "incomplete"
        # Even a failed gate retains the privately rebuilt executable/helper bytes.
        for role, name in {"Helper": "DistributionProbe", "CLI": "Hexalith.Builds.Module.Cli", "Tests": "Hexalith.Builds.Module.Tests"}.items():
            source = self.artifacts / "original/bin" / name / "debug" if hasattr(self, "artifacts") else self.work / "absent"
            destination = self.work / "distribution" / role
            if source.is_dir() and not destination.exists():
                shutil.copytree(source, destination)
        self.result["historicalEvidence"] = dict(status="not reused", reason="Historical wrapper and fixed SDK assumptions are excluded; no historical case earns current credit.")
        q.write(self.work / "result.json", self.result)


def archive(work, record_id):
    selected = []
    excluded = {"artifacts", *[key.lower() for key in q.PRIVATE]}
    for path in sorted(work.rglob("*")):
        relative = path.relative_to(work)
        if relative.parts[0] in excluded or path.is_dir():
            continue
        if relative.parts[:2] == ("workspace", "build") and path.name != "project.assets.json":
            continue
        selected.append(path)
    archive_path = q.EVIDENCE / (record_id + ".artifacts.tar.gz")
    manifest_path = q.EVIDENCE / (record_id + ".members.json")
    q.require(not archive_path.exists() and not manifest_path.exists(), "Existing evidence is immutable")
    members = []
    with tarfile.open(archive_path, "x:gz") as capsule:
        for path in selected:
            item = q.binding(path, work)
            capsule.add(path, arcname=item["path"], recursive=False)
            q.same(item, work)
            members.append(item)
    q.write(manifest_path, members)
    result = json.loads((work / "result.json").read_text())
    receipt = dict(schemaVersion=1, recordId=record_id, privateWorkspace=str(work), result=result,
                   archive=q.binding(archive_path, q.EVIDENCE), memberManifest=q.binding(manifest_path, q.EVIDENCE), memberCount=len(members),
                   scope="Unwrapped production BuildModelAsync compilation/fixture/PE evidence only; no startup, runtime, accepted-tool, signed-release or operational credit.")
    receipt_path = q.EVIDENCE / (record_id + ".json")
    q.write(receipt_path, receipt)
    return receipt_path, receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--execute", action="store_true", required=True)
    parser.add_argument("--suffix", required=True)
    args = parser.parse_args()
    q.require(args.suffix and all(character.isalnum() or character in "-_" for character in args.suffix), "Unsafe suffix")
    record_id = PREFIX + "-" + args.suffix
    q.require(not (q.EVIDENCE / (record_id + ".json")).exists(), "Existing receipt is immutable")
    work = Path(tempfile.mkdtemp(prefix=record_id + "-"))
    work.chmod(0o700)
    qualification = ModelQualification(work)
    try:
        qualification.prepare()
        qualification.run()
    except Exception as error:
        qualification.failure = dict(type=type(error).__name__, detail=str(error), owner="first failing command/input owner; see exact command and retained log", dependentExecutionStopped=True)
        print(json.dumps(qualification.failure), flush=True)
    finally:
        qualification.finish()
        path, receipt = archive(work, record_id)
        print(json.dumps(dict(receipt=str(path), work=str(work), qualification=receipt["result"]["qualification"])), flush=True)
    return 0 if receipt["result"]["qualification"] == "passed" else 2


if __name__ == "__main__":
    raise SystemExit(main())
