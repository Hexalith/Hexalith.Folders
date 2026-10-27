"""One-shot, private Debug source qualification for the separately approved spec.

Importing this module runs no commands. Guards use explicit exceptions so python -O
does not remove them. A stopped attempt is retained and cannot be retried by this
plan. No application entrypoint, ComposeRun or target constructor is invoked.
"""
import argparse
import datetime
import ctypes
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import signal
import stat
import struct
import subprocess
import sys
import tarfile
import tempfile
import time
from time import sleep as cleanup_sleep
import xml.etree.ElementTree as ET
import zipfile
import uuid

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
RECORDS = ROOT / "_bmad-output/implementation-artifacts"
EVIDENCE = RECORDS / "evidence"
PREFIX = "story-1-17-current-builds-host-2026-10-26"
SPEC = RECORDS / "spec-1-17-current-builds-host-qualification.md"
INVESTIGATION = RECORDS / "story-1-17-current-builds-host-investigation-2026-10-26.json"
OLD_INPUTS = EVIDENCE / "story-1-17-cli-4-38-loader-proposal-2026-10-26/inputs.json"
EXCLUDED = {"references", "_bmad-output", ".agents", ".claude", "tests", "test", "docs", "evidence"}
CONTROLS = dict(parentStatus="in-progress", tracker="backlog", routing="V1Only", T0=None,
                activationAuthority=None, stagingChecksExecuted=0, stagingChecksTotal=14,
                ordinaryMapping=None, ContentHash=None, currentTupleAccepted=False,
                packageAccepted=False, apiAccepted=False, hostedAccepted=False)
PROJECTS = [
    "references/Hexalith.Builds/src/hosts/Hexalith.Builds.Module.AppHost/Hexalith.Builds.Module.AppHost.csproj",
    "references/Hexalith.Builds/src/libraries/Hexalith.Builds.Module.Cli/Hexalith.Builds.Module.Cli.csproj",
    "references/Hexalith.Platform/src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj",
]
STALE_INPUTS = {
    "references/Hexalith.EventStore/src/" + project + "/.artifacts/ui-test-obj/" + name
    for project in ("Hexalith.EventStore.Admin.Abstractions", "Hexalith.EventStore.Admin.UI",
                    "Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults", "Hexalith.EventStore.SignalR")
    for name in (project + ".csproj.nuget.dgspec.json", project + ".csproj.nuget.g.props",
                 project + ".csproj.nuget.g.targets", "project.assets.json")
}


class GateFailure(RuntimeError):
    """A failed gate always stops dependent execution."""


def require(condition, detail):
    if not condition:
        raise GateFailure(detail)


def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def binding(path, relative_to=None):
    path = Path(path)
    require(path.is_file() and not path.is_symlink(), "Not a regular input: " + str(path))
    return dict(path=str(path.relative_to(relative_to)) if relative_to else str(path),
                sha256=sha(path), bytes=path.stat().st_size)


def same(path, expected):
    actual = binding(path)
    require(actual["sha256"] == expected["sha256"] and actual["bytes"] == expected["bytes"],
            "Input drift: " + str(path))


def write(path, data):
    Path(path).write_text(json.dumps(data, indent=2, sort_keys=True) + "\n")


def git(repo, *args):
    env = dict(os.environ, GIT_OPTIONAL_LOCKS="0")
    return subprocess.check_output(["git", "-C", str(repo), *args], env=env, timeout=30)


def state(root, repositories):
    result = {}
    for row in repositories:
        rel = row["path"]
        repo = root / rel
        index = Path(git(repo, "rev-parse", "--path-format=absolute", "--git-path", "index").decode().strip())
        result[rel] = dict(revision=git(repo, "rev-parse", "HEAD").decode().strip(),
            stageEntriesSha256=hashlib.sha256(git(repo, "ls-files", "--stage")).hexdigest(),
            indexSha256=sha(index), indexFlagsSha256=hashlib.sha256(git(repo, "ls-files", "-v")).hexdigest(),
            branch=git(repo, "branch", "--show-current").decode().strip(),
            remotes=git(repo, "remote", "-v").decode(), history=git(repo, "log", "-5", "--oneline").decode(),
            status=git(repo, "status", "--porcelain=v2", "--untracked-files=all").decode())
    return result


def recording_path(path):
    return (path.startswith("eng/story117-current-builds-host/")
            or path.startswith("_bmad-output/implementation-artifacts/evidence/story-1-17-current-builds-host-")
            or path in {
                ".gitattributes",
                "_bmad-output/implementation-artifacts/spec-1-17-current-builds-host-qualification.md",
                "_bmad-output/implementation-artifacts/story-1-17-current-builds-host-investigation-2026-10-26.json",
                "_bmad-output/implementation-artifacts/story-1-17-current-builds-host-results.md"})


def sdk_build_dependency_paths(sdk_directory):
    """Fixed SDK build/task closure, including dynamically loaded managed/native tasks."""
    suffixes = (".dll", ".exe", ".so", ".deps.json", ".runtimeconfig.json")
    result = sorted(path for path in sdk_directory.rglob("*")
                    if path.is_file() and path.name.endswith(suffixes))
    for name in ("Microsoft.Build.dll", "Microsoft.Build.Tasks.Core.dll", "NuGet.Build.Tasks.dll"):
        require(sdk_directory / name in result, "Incomplete fixed SDK build dependency closure: " + name)
    return result


def verify_repository_predicate(root, plan):
    # Protected raw bytes are checked before considering a recording-only descendant.
    for item in plan["protectedFiles"]:
        same(root / item["path"], item)
    selected = plan["selectedCatalog"]
    catalog = git(root / selected["repository"], "show", selected["revision"] + ":Props/Directory.Packages.props")
    require(hashlib.sha256(catalog).hexdigest() == selected["blobSha256"], "Selected catalog blob drift")
    actual = state(root, plan["repositories"])
    for expected in plan["repositories"]:
        rel = expected["path"]
        row = actual[rel]
        if rel != ".":
            require(row["revision"] == expected["revision"], "Revision drift: " + rel)
            require(row["stageEntriesSha256"] == expected["stageEntriesSha256"], "Index drift: " + rel)
            require(row["status"] == "", "Dirty dependency: " + rel)
        elif row["revision"] == expected["revision"]:
            require(row["stageEntriesSha256"] == expected["stageEntriesSha256"], "Root index drift")
        else:
            require(subprocess.run(["git", "-C", str(root), "merge-base", "--is-ancestor",
                                   expected["revision"], row["revision"]], timeout=30).returncode == 0,
                    "Root is not a recording descendant")
            changed = git(root, "diff", "--name-only", expected["revision"], row["revision"]).decode().splitlines()
            require(all(recording_path(p) for p in changed), "Operational root descendant")
    dirty = git(root, "diff", "--name-only", "HEAD").decode().splitlines()
    untracked = git(root, "ls-files", "--others", "--exclude-standard").decode().splitlines()
    require(all(recording_path(p) for p in dirty + untracked), "Unapproved root working bytes")
    return actual


def source_files(root, repositories):
    result = []
    for row in repositories:
        rel = row["path"]
        if rel in (".", "references/Hexalith.AI.Tools"):
            continue
        for record in git(root / rel, "ls-files", "--stage", "-z").split(b"\0"):
            if not record:
                continue
            metadata, raw_path = record.split(b"\t", 1)
            mode = metadata.split()[0]
            name = raw_path.decode()
            if mode == b"160000" or PurePosixPath(name).parts[0] in EXCLUDED:
                continue
            if rel + "/" + name in STALE_INPUTS:
                continue
            if mode == b"120000" and name in (".clinerules", ".cursorrules", ".windsurfrules"):
                continue  # Instruction links are not compiler/build inputs; never follow them.
            require(mode in (b"100644", b"100755"), "Special tracked input: " + rel + "/" + name)
            require(not {"bin", "obj"}.intersection(PurePosixPath(name).parts), "Tracked stale output: " + name)
            result.append(binding(root / rel / name, root))
    # The snapshot root has no Folders build props: only sibling module graphs are evaluated.
    result.append(binding(root / "global.json", root))
    return sorted(result, key=lambda item: item["path"])


def filter_bytes():
    require(os.uname().machine == "x86_64", "No non-x86_64 seccomp fallback")
    rows = [(0x20, 0, 0, 4), (0x15, 1, 0, 0xc000003e), (0x06, 0, 0, 0x80000000),
            (0x20, 0, 0, 0), (0x35, 0, 1, 0x40000000), (0x06, 0, 0, 0x80000000)]
    for syscall in (41, 42, 53, 425):  # socket, connect, socketpair, io_uring_setup
        rows.extend([(0x15, 0, 1, syscall), (0x06, 0, 0, 0x00050001)])
    rows.append((0x06, 0, 0, 0x7fff0000))
    return b"".join(struct.pack("<HBBI", *row) for row in rows)


def claim_once(path, data):
    # This prevents accidental reuse while the retained claim exists, not deliberate removal.
    with Path(path).open("x") as stream:
        json.dump(data, stream, indent=2)
        stream.write("\n")


def package_feed_layout(packages):
    """Bind a discoverable flat feed using canonical ID.version.nupkg names."""
    result = {}
    for item in packages:
        same(item["path"], item)
        with zipfile.ZipFile(item["path"]) as archive:
            names = [name for name in archive.namelist() if "/" not in name and name.endswith(".nuspec")]
            require(len(names) == 1, "Ambiguous package metadata: " + item["path"])
            metadata = ET.fromstring(archive.read(names[0]))
            fields = {node.tag.rsplit("}", 1)[-1]: node.text for node in metadata.iter()}
        identity = fields.get("id", "")
        version = fields.get("version", "")
        require(re.fullmatch(r"[A-Za-z0-9_.-]+", identity) is not None, "Unsafe package identity")
        require(re.fullmatch(r"[A-Za-z0-9_.+-]+", version) is not None, "Unsafe package version")
        require(not identity.lower().startswith("hexalith."), "Hexalith offline package fallback")
        key = identity.lower() + "/" + version.lower()
        row = dict(item, packageId=identity, packageVersion=version,
                   feedPath=identity.lower() + "." + version.lower() + ".nupkg")
        if key in result:
            previous = result[key]
            require((row["sha256"], row["bytes"]) == (previous["sha256"], previous["bytes"]),
                    "Conflicting bytes for package identity/version: " + key)
        else:
            result[key] = row
    rows = [result[key] for key in sorted(result)]
    require(len({row["feedPath"] for row in rows}) == len(rows), "Conflicting canonical feed filenames")
    return rows


def validate_feed_directory(directory, layout):
    """A local flat source discovers canonical archives directly at the source root."""
    actual = sorted(str(p.relative_to(directory)) for p in directory.rglob("*") if p.is_file())
    expected = sorted(row["feedPath"] for row in layout)
    require(all(len(PurePosixPath(name).parts) == 1 and name.endswith(".nupkg") for name in expected),
            "The qualified local feed must be flat canonical nupkg files")
    require(actual == expected, "Private feed discovery/membership mismatch")
    for row in layout:
        require(row["feedPath"] == row["packageId"].lower() + "." + row["packageVersion"].lower() + ".nupkg",
                "Noncanonical local feed package filename")
        same(directory / row["feedPath"], row)


def validate_serial_restore(argv):
    require("-m:1" in argv and all(arg == "-m:1" for arg in argv if arg.startswith("-m:")), "Restore must use one MSBuild node")
    require("--disable-parallel" in argv and "-nr:false" in argv, "Restore parallelism/node reuse gate")
    for prop in ("RestoreBuildInParallel", "BuildInParallel"):
        values = [arg for arg in argv if arg.startswith("-p:" + prop + "=")]
        require(values == ["-p:" + prop + "=false"], "Restore parallelism property drift: " + prop)


def catalog_metadata_binding(package, nuspec, source_revision):
    """Bind only the selected catalog nuspec, without supplying package assemblies."""
    package_input = binding(package)
    nuspec_input = binding(nuspec)
    with zipfile.ZipFile(package) as archive:
        members = [name for name in archive.namelist() if "/" not in name and name.lower().endswith(".nuspec")]
        require(len(members) == 1, "Ambiguous catalog package metadata")
        raw = archive.read(members[0])
    require(raw == Path(nuspec).read_bytes(), "Cached catalog nuspec differs from nupkg member")
    document = ET.fromstring(raw)
    metadata = next(node for node in document if node.tag.rsplit("}", 1)[-1] == "metadata")
    fields = {node.tag.rsplit("}", 1)[-1]: node for node in metadata}
    require(fields["id"].text == "Hexalith.EventStore.Aspire" and fields["version"].text == "3.128.0",
            "Only the exact selected EventStore.Aspire 3.128.0 catalog metadata is permitted")
    commit = fields["repository"].attrib.get("commit")
    require(commit == source_revision, "Catalog package-declared repository revision differs from source")
    return dict(packageId=fields["id"].text, packageVersion=fields["version"].text,
                nupkg=package_input, nuspec=nuspec_input, memberName=members[0],
                privatePath="hexalith.eventstore.aspire/3.128.0/hexalith.eventstore.aspire.nuspec",
                repositoryCommit=commit, sourceRevision=source_revision,
                scope="Package-declared metadata provenance only; no proof of full package payload/source equivalence.")


def seed_catalog_metadata(record, cache):
    actual = catalog_metadata_binding(record["nupkg"]["path"], record["nuspec"]["path"], record["sourceRevision"])
    require(actual == record, "Catalog metadata provenance drift")
    destination = cache / record["privatePath"]
    require(not destination.exists(), "Preexisting private catalog metadata")
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(Path(record["nuspec"]["path"]).read_bytes())
    same(destination, record["nuspec"])
    require([path.name for path in destination.parent.iterdir()] == [destination.name], "Catalog metadata cache contains package payload")
    return destination


def sdk_feed_configuration(project, snapshot, expected_feed):
    """Inspect the effective project-ancestor sources before external SDK evaluation."""
    ancestors = list(reversed(project.parent.parents)) + [project.parent]
    effective = {}
    configs = []
    for ancestor in ancestors:
        if not ancestor.is_relative_to(snapshot):
            continue  # The private root below clears any earlier machine/user source.
        for candidate in sorted(ancestor.iterdir()):
            if not candidate.is_file() or candidate.name.lower() != "nuget.config":
                continue
            configs.append(binding(candidate))
            for entry in ET.parse(candidate).getroot().findall("./packageSources/*"):
                if entry.tag == "clear":
                    effective.clear()
                elif entry.tag == "add":
                    effective[entry.attrib["key"]] = entry.attrib["value"]
                elif entry.tag == "remove":
                    effective.pop(entry.attrib["key"], None)
    require(effective == {"private-offline": str(expected_feed)}, "External SDK resolver source configuration")
    require(configs and Path(configs[0]["path"]).parent == snapshot, "Missing clearing private root NuGet configuration")
    return dict(configs=configs, effectivePackageSources=effective,
                boundary="Private root packageSources clear replaces earlier machine/user sources; project ancestors were inspected before SDK evaluation.")


def validate_assets(data, private_root):
    for name, row in data.get("libraries", {}).items():
        require(not (row.get("type") == "package" and name.lower().startswith("hexalith.")),
                "Hexalith package fallback: " + name)
        if row.get("type") == "project":
            project = (private_root / "unused" / row.get("path", "")).resolve()
            # The actual restore graph is checked separately using absolute project paths.
            require(row.get("path", "").endswith(".csproj"), "Invalid project asset: " + str(project))
    for framework in data.get("project", {}).get("restore", {}).get("frameworks", {}).values():
        for path in framework.get("projectReferences", {}):
            require(Path(path).resolve().is_relative_to(private_root.resolve()), "External project reference: " + path)


def planned_project(project, snapshot, plan):
    project = Path(project).resolve()
    require(project.is_relative_to(snapshot.resolve()), "Source project escaped private snapshot")
    rel = str(project.relative_to(snapshot.resolve()))
    expected = next((row for row in plan["sources"] if row["path"] == rel), None)
    require(expected is not None and project.suffix == ".csproj", "Unbound source project")
    same(project, expected)
    return project


def proof_path(directory, output):
    return directory / (hashlib.sha256(str(Path(output).absolute()).encode()).hexdigest() + ".json")


def validate_compiler_references(lines, snapshot, plan, proofs):
    """Reject direct binary references unless a verified private source output proves them."""
    accepted = []
    for line in lines:
        if not line.startswith("REF|"):
            continue
        fields = line.split("|")
        require(len(fields) == 4, "Malformed compiler reference capture")
        _, raw, fusion, original = fields
        path = Path(raw)
        hexalith = path.name.startswith("Hexalith.") or fusion.startswith("Hexalith.") or original.startswith("Hexalith.")
        if not hexalith:
            continue
        require(path.is_absolute() and path.resolve() == path and path.is_relative_to(snapshot.resolve()),
                "External/direct Hexalith compiler reference: " + raw)
        proof = proofs.get(str(path))
        require(proof is not None, "Hexalith reference lacks verified source output: " + raw)
        project = planned_project(proof["project"], snapshot, plan)
        require(path == project.parent / "bin/Debug/net10.0" / (project.stem + ".dll"),
                "Hexalith reference is not its source project's Debug output")
        require(proof["output"]["path"] == str(path), "Source output proof path mismatch")
        same(path, proof["output"])
        accepted.append(dict(path=raw, sourceProject=str(project), sha256=proof["output"]["sha256"]))
    return accepted


def audit_reference_file(reference_file, project, snapshot, plan_path, directory):
    plan = json.loads(plan_path.read_text())
    project = planned_project(project, snapshot, plan)
    proofs = {}
    for path in directory.glob("*.json"):
        row = json.loads(path.read_text())
        if "output" in row:
            proofs[row["output"]["path"]] = row
    accepted = validate_compiler_references(reference_file.read_text().splitlines(), snapshot, plan, proofs)
    write(directory / (project.stem + ".reference-audit.json"),
          dict(project=str(project), planSha256=sha(plan_path), accepted=accepted, referenceCapture=binding(reference_file)))
    return accepted


def record_source_output(project, output, snapshot, plan_path, directory):
    plan = json.loads(plan_path.read_text())
    project = planned_project(project, snapshot, plan)
    output = Path(output).resolve()
    require(output == project.parent / "bin/Debug/net10.0" / (project.stem + ".dll"), "Unexpected source output")
    audit = json.loads((directory / (project.stem + ".reference-audit.json")).read_text())
    require(audit["project"] == str(project) and audit["planSha256"] == sha(plan_path), "Source compilation lacks matching reference gate")
    write(proof_path(directory, output), dict(project=str(project), projectInput=binding(project), output=binding(output),
        referenceGate=binding(directory / (project.stem + ".reference-audit.json"))))


def validate_seal(root, seal):
    actual = sorted(str(p.relative_to(root)) for p in root.rglob("*") if p.is_file())
    require(actual == sorted(item["path"] for item in seal), "Sealed output membership drift")
    for item in seal:
        same(root / item["path"], item)


def validate_probe_events(events, exit_code, sealed_root, seal, mode):
    """Classify fatal gates and bind each emitted target resolution to sealed bytes."""
    require(events and events[0]["kind"] == "context", "Loader did not reach context observation")
    context = events[0]["data"]
    require(context["framework"] == ".NET 10.0.13", "Fatal runtime binding drift")
    tpa = context.get("trustedPlatformAssemblies", "")
    require(all("/10.0.13/" in path for path in tpa.split(os.pathsep) if "/shared/Microsoft." in path),
            "Fatal runtime framework path drift")
    require(exit_code in (0, 10), "Fatal probe exit; no next case")
    require(events[-1]["kind"] == ("complete" if exit_code == 0 else "failure"), "Loader exit/event outcome mismatch")
    if exit_code == 10:
        require(events[-1]["data"].get("phase") in {
            "host-baseline", "model-load-and-ComposeRun-metadata", "bound-Redis-demands",
            "optional-ServiceDefaults-demand", "model-EventStore-demand"}, "Fatal probe preflight/runtime failure")
    indexed = {str((sealed_root / row["path"]).absolute()): row for row in seal}
    resolved = []
    model_seen = False
    metadata_seen = False
    for event in events:
        target_load = event["kind"] == "assembly-load" and (
            event["data"]["identity"].split(",")[0].startswith("Hexalith.")
            or event["data"]["identity"].split(",")[0] in ("Aspire.Hosting", "StackExchange.Redis"))
        if event["kind"] in ("resolved", "model") or target_load:
            assembly = event["data"]["assembly"] if event["kind"] == "resolved" else event["data"]
            path = assembly["location"]
            require(path in indexed and str(Path(path).resolve()) == path, "Resolution path is not a sealed input: " + path)
            row = indexed[path]
            same(path, row)
            require(assembly["sha256"] == row["sha256"], "Resolved hash differs from seal: " + path)
            require(assembly["context"] == "Default" and assembly["identity"].split(",")[0] == Path(path).stem,
                    "Resolved identity/context differs from sealed filename")
            require(str(uuid.UUID(assembly["mvid"])) == assembly["mvid"], "Invalid observed MVID")
            if event["kind"] == "resolved":
                resolved.append(event["data"]["label"])
            elif event["kind"] == "model":
                require(path == str(sealed_root / "model/Hexalith.Platform.Composition.dll"), "Unexpected model path")
                model_seen = True
        elif event["kind"] == "method-metadata-only":
            data = event["data"]
            require(data["Name"] == "ComposeRun" and data["returnType"] == "System.Void"
                    and data["signature"] == ["Aspire.Hosting.IDistributedApplicationBuilder",
                        "Hexalith.Builds.Tooling.Runtime.CompositionRunPlan", "System.String", "System.String"],
                    "Unexpected ComposeRun metadata")
            metadata_seen = True
    if exit_code == 0:
        for label in ("host Aspire", "matching source Tooling", "host Tooling Redis", "model transitive Redis", "optional ServiceDefaults", "model EventStore Aspire"):
            require(any(value.startswith(label) for value in resolved), "Missing successful demand: " + label)
        require(model_seen and metadata_seen, "Successful probe lacks model/method evidence")
        require(mode != "host-first" or "deliberate host-first EventStore" in resolved, "Missing host-first demand")
    return dict(bindingSucceeded=exit_code == 0, resolvedObservationCount=len(resolved), fatalGate=False)


def processes():
    table = {}
    for path in Path("/proc").iterdir():
        if not path.name.isdigit():
            continue
        try:
            raw = (path / "stat").read_text()
            fields = raw[raw.rfind(")") + 2:].split()
            table[int(path.name)] = dict(pid=int(path.name), ppid=int(fields[1]), pgid=int(fields[2]),
                                         state=fields[0], startTime=fields[19])
        except (FileNotFoundError, ProcessLookupError, PermissionError):
            pass
    return table


def process_identity(pid):
    raw = (Path("/proc") / str(pid) / "stat").read_text()
    fields = raw[raw.rfind(")") + 2:].split()
    return dict(pid=pid, ppid=int(fields[1]), pgid=int(fields[2]), state=fields[0], startTime=fields[19])


def owned_rows(pid, seen):
    table = processes()
    owned = {key for key, row in table.items() if (key, row["startTime"]) in seen}
    root = table.get(pid)
    if root and not any(key == pid for key, _ in seen):
        owned.add(pid)
    while True:
        group_owned = any(table[key]["pgid"] == pid for key in owned)
        expanded = owned | {key for key, row in table.items() if row["ppid"] in owned or (group_owned and row["pgid"] == pid)}
        if expanded == owned:
            break
        owned = expanded
    rows = [table[key] for key in sorted(owned)]
    seen.update((row["pid"], row["startTime"]) for row in rows)
    return rows


def observe(pid, seen):
    return dict(atUtc=now(), rootPid=pid, processGroup=pid, rows=owned_rows(pid, seen))


def child_subreaper(enable):
    """Adopt owned orphan descendants so their termination can be reaped."""
    library = ctypes.CDLL(None, use_errno=True)
    previous = ctypes.c_int()
    require(library.prctl(37, ctypes.byref(previous), 0, 0, 0) == 0, "Cannot inspect child subreaper")
    require(library.prctl(36, int(enable), 0, 0, 0) == 0, "Cannot set child subreaper")
    return previous.value


def cleanup_owned(process, seen, record):
    """Terminate the original group and every still-matching observed PID identity."""
    cleanup = dict(observations=[], signals=[], reaped=[], errors=[])
    record["termination"] = cleanup

    def sample():
        try:
            observation = observe(process.pid, seen)
            cleanup["observations"].append(observation)
            table = processes()
            return [row for row in table.values() if (row["pid"], row["startTime"]) in seen]
        except BaseException as error:
            cleanup["errors"].append(dict(type=type(error).__name__, detail=str(error)))
            rows = owned_rows(process.pid, seen)
            cleanup["observations"].append(dict(atUtc=now(), rootPid=process.pid, rows=rows, observer="direct fallback"))
            return rows

    def reap(rows):
        if process.poll() is not None:
            process.wait()
        for row in rows:
            if row["pid"] == process.pid:
                continue
            try:
                pid, status = os.waitpid(row["pid"], os.WNOHANG)
                if pid:
                    cleanup["reaped"].append(dict(pid=pid, startTime=row["startTime"], waitStatus=status))
            except ChildProcessError:
                pass

    rows = sample()
    cleanup["liveBeforeCleanup"] = [row for row in rows if row["state"] != "Z"]
    for signum, grace in ((signal.SIGTERM, 5.0), (signal.SIGKILL, 2.0)):
        # Check each identity immediately before signalling, including descendants
        # that moved into their own session/process group after observation.
        current = processes()
        live = [row for row in current.values() if row["state"] != "Z" and (row["pid"], row["startTime"]) in seen]
        if process.poll() is None or any(row["pgid"] == process.pid for row in live):
            try:
                os.killpg(process.pid, signum)
                cleanup["signals"].append(dict(group=process.pid, signal=int(signum), atUtc=now()))
            except ProcessLookupError:
                pass
        if process.poll() is None:
            # Popen still owns this unreaped child even when /proc observation fails.
            process.send_signal(signum)
        for row in live:
            fresh = processes().get(row["pid"])
            if fresh and fresh["startTime"] == row["startTime"]:
                try:
                    os.kill(row["pid"], signum)
                    cleanup["signals"].append(dict(pid=row["pid"], startTime=row["startTime"], signal=int(signum), atUtc=now()))
                except ProcessLookupError:
                    pass
        deadline = time.perf_counter() + grace
        while True:
            rows = sample()
            reap(rows)
            if not rows or time.perf_counter() >= deadline:
                break
            cleanup_sleep(0.02)
    try:
        record["exitCode"] = process.wait(timeout=1)
    except subprocess.TimeoutExpired:
        cleanup["errors"].append(dict(type="TimeoutExpired", detail="Owned root did not terminate"))
    rows = sample()
    reap(rows)
    final = sample()
    cleanup["endedAtUtc"] = now()
    cleanup["remainingIdentities"] = final
    record["observedSurvivors"] = final  # Zombies that could not be reaped are not silently dropped.


def command_record(label, argv, cwd, env, limit):
    return dict(label=label, argv=list(map(str, argv)), cwd=str(cwd), environment=env,
                atUtc=now(), timeoutSeconds=limit, termGraceSeconds=5, observations=[],
                launched=False, launchStatus="not-launched", exitCode=None)


def run_owned(argv, cwd, env, directory, label, limit, passed_fds=(), record=None):
    """Always retain launch status/results and clean owned children on every exit."""
    record = record if record is not None else command_record(label, argv, cwd, env, limit)
    record["argv"] = list(map(str, argv))
    seen = set()
    process = None
    error = None
    previous_subreaper = None
    previous_term_handler = None
    try:
        require(limit in (30, 60, 600), "Invalid deadline")
        previous_subreaper = child_subreaper(True)
        try:
            previous_term_handler = signal.signal(signal.SIGTERM, lambda *_: (_ for _ in ()).throw(InterruptedError("SIGTERM")))
        except ValueError:
            pass  # Only the main Python thread may install a signal handler.
        with (directory / (label + ".stdout")).open("wb") as out, (directory / (label + ".stderr")).open("wb") as err:
            process = subprocess.Popen(record["argv"], cwd=cwd, env=env, stdin=subprocess.DEVNULL,
                stdout=out, stderr=err, start_new_session=True, pass_fds=passed_fds)
            record.update(launched=True, launchStatus="launched", pid=process.pid)
            try:
                root_identity = process_identity(process.pid)
                seen.add((process.pid, root_identity["startTime"]))
                record["rootIdentity"] = root_identity
            except OSError as identity_error:
                record["rootIdentityError"] = str(identity_error)
            deadline = time.monotonic() + limit
            while process.poll() is None:
                record["observations"].append(observe(process.pid, seen))
                if time.monotonic() >= deadline:
                    record["timedOut"] = True
                    raise GateFailure("Timeout: " + label)
                time.sleep(0.2)
            record["exitCode"] = process.wait()
        logs = (directory / (label + ".stdout")).read_text(errors="replace") + (directory / (label + ".stderr")).read_text(errors="replace")
        require("installed an asp.net core https development certificate" not in logs.lower(), "Certificate first-use side effect")
    except BaseException as caught:
        error = caught
        record["error"] = dict(type=type(caught).__name__, detail=str(caught))
    finally:
        if process is not None:
            try:
                cleanup_owned(process, seen, record)
                if record.get("observedSurvivors") or record["termination"]["errors"]:
                    raise GateFailure("Owned process cleanup could not be confirmed: " + label)
            except BaseException as caught:
                record["cleanupError"] = dict(type=type(caught).__name__, detail=str(caught))
                # Observation itself may fail; Popen ownership and recorded
                # identities still require a termination/reap attempt.
                emergency = dict(signals=[], errors=[], observations=[], reaped=[])
                for pid, start in seen:
                    if pid == process.pid:
                        continue
                    try:
                        row = process_identity(pid)
                        if row["startTime"] == start:
                            os.kill(pid, signal.SIGKILL)
                            emergency["signals"].append(dict(pid=pid, startTime=start, signal=int(signal.SIGKILL)))
                    except (ProcessLookupError, FileNotFoundError):
                        pass
                    except BaseException as failure:
                        emergency["errors"].append(str(failure))
                try:
                    if process.poll() is None:
                        try:
                            os.killpg(process.pid, signal.SIGKILL)
                        except ProcessLookupError:
                            pass
                        process.kill()
                    record["exitCode"] = process.wait(timeout=2)
                except BaseException as failure:
                    emergency["errors"].append(str(failure))
                deadline = time.perf_counter() + 2
                remaining = []
                while True:
                    remaining = []
                    for pid, start in seen:
                        try:
                            if pid != process.pid:
                                reaped, status = os.waitpid(pid, os.WNOHANG)
                                if reaped:
                                    emergency["reaped"].append(dict(pid=reaped, waitStatus=status))
                        except ChildProcessError:
                            pass
                        try:
                            row = process_identity(pid)
                            if row["startTime"] == start:
                                remaining.append(row)
                        except (ProcessLookupError, FileNotFoundError):
                            pass
                        except BaseException as failure:
                            emergency["errors"].append(str(failure))
                    emergency["observations"].append(dict(atUtc=now(), rows=remaining))
                    if not remaining or time.perf_counter() >= deadline:
                        break
                    cleanup_sleep(0.02)
                record["observedSurvivors"] = remaining
                record["emergencyTermination"] = emergency
                if error is None:
                    error = caught
        try:
            if previous_term_handler is not None:
                signal.signal(signal.SIGTERM, previous_term_handler)
            if previous_subreaper is not None:
                child_subreaper(previous_subreaper)
        except BaseException as caught:
            record["observerRestoreError"] = dict(type=type(caught).__name__, detail=str(caught))
            if error is None:
                error = caught
        record["endedAtUtc"] = now()
        record["status"] = "failed" if error else "completed"
        record["observationLimit"] = "200ms sampled descendants/group plus recorded PID/start-time cleanup; transient unobserved or escaped processes can be missed. No whole-machine claim."
        for stream in ("stdout", "stderr"):
            path = directory / (label + "." + stream)
            if path.is_file():
                record[stream] = binding(path, directory)
        write(directory / (label + ".command.json"), record)
    if error is not None:
        raise error
    return record


def store_metadata(private):
    result = {}
    for base in (Path("/home/administrator/.dotnet/corefx/cryptography/x509stores"),
                 private / "dotnet-home/.dotnet/corefx/cryptography/x509stores"):
        rows = []
        if base.exists():
            for path in sorted(base.rglob("*")):
                info = path.lstat()
                rows.append(dict(path=str(path.relative_to(base)), mode=info.st_mode, bytes=info.st_size,
                                 mtimeNs=info.st_mtime_ns, ctimeNs=info.st_ctime_ns))
        result[str(base)] = dict(exists=base.exists(), entries=rows)
    return result


def make_plan(record_id=PREFIX):
    investigation = json.loads(INVESTIGATION.read_text())
    old = json.loads(OLD_INPUTS.read_text())
    for item in investigation["bindings"]:
        same(ROOT / item["path"], item)
    protected_paths = {item["path"] for item in investigation["bindings"]}
    protected_paths.update(item["path"] for item in old["preservedFiles"])
    protected_paths.update(str(path.relative_to(ROOT)) for path in (SPEC, INVESTIGATION, OLD_INPUTS))
    # Every historical 4.38 result/approval/receipt remains a protected input.
    protected_paths.update(str(p.relative_to(ROOT)) for p in RECORDS.glob("*1-17-cli-4-38*") if p.is_file())
    protected_paths.update(str(p.relative_to(ROOT)) for p in EVIDENCE.glob("*1-17-cli-4-38*") if p.is_file())
    protected_paths.update(STALE_INPUTS)
    protected_paths.add(".gitattributes")
    continuation = None
    attempt_number = 1 if record_id == PREFIX else int(record_id.rsplit("-attempt-", 1)[1])
    if attempt_number > 1:
        previous_id = PREFIX if attempt_number == 2 else PREFIX + "-attempt-" + str(attempt_number - 1)
        failed_receipt = EVIDENCE / (previous_id + ".json")
        previous = json.loads(failed_receipt.read_text())
        evaluations = ["sdk-evaluate-AppHost", "evaluate-AppHost", "sdk-evaluate-EventStoreHost", "evaluate-EventStoreHost",
                       "sdk-evaluate-UiHost", "evaluate-UiHost", "sdk-restore-AppHost", "restore-AppHost"]
        expected_commands = {2: [], 3: evaluations[:2], 4: evaluations,
                             5: evaluations + ["sdk-build-AppHost", "build-AppHost"]}[attempt_number]
        require(previous["outcome"]["completedCommandLabels"] == expected_commands
                and previous["outcome"]["caseResults"] == [], "Unexpected previous execution scope")
        for number in range(1, attempt_number):
            name = "plan.json" if number == 1 else "plan-attempt-" + str(number) + ".json"
            protected_paths.add(str((HERE / name).relative_to(ROOT)))
        protected_paths.update(str(p.relative_to(ROOT)) for p in EVIDENCE.glob(PREFIX + "*") if p.is_file())
        interpretations = {
            2: "Parent implementer: Jerome's approval covers implementation/verification of the conditional sequence. The pre-SDK file-copy defect did not consume the still-unexecuted build/loader authority. Correct only preparation, retain the failed attempt unchanged, then continue with a fresh additive claim. No loader case is retried; no new human signature is required for this reversible correction.",
            3: "Parent implementer: preserve attempt 2's failed SDK evaluation/claim/plan. Correct only the private feed-format preparation defect to canonical flat ID.version.nupkg names with discovery tests, then continue the still-unrun build/loader sequence. No new packages, pins, tools, network or target behavior; retain all SDK/source/isolation gates.",
            4: "Parent implementer: enforce the frozen serial/fresh-build requirements with restore -m:1, RestoreBuildInParallel=false, BuildInParallel=false and node reuse disabled, retaining socket denial/deadlines. Preserve failed restore and all 14 worker crash reports. Omit exactly 20 generated ui-test-obj inputs from new snapshots while protecting original hashes. No operational source/dependency/tool/isolation change. Loader cases remain once each after all builds pass.",
            5: "Parent implementer: bind the existing selected EventStore.Aspire 3.128.0 nupkg and cached nuspec, verify nuspec-member byte equality, exact ID/version and package-declared repository commit matching source. Seed ONLY the nuspec into the fresh private cache, with no Hexalith DLL/nupkg in the restore feed or compiled graph. Preserve attempt 4's failed build and run GeneratePlatformVersionCatalog unchanged in a fresh additive attempt. No fetched packages, source edits, pin changes, tool install or catalog-target bypass; source-only package-fallback guards remain enforced. No loader case has run."
        }
        continuation = dict(priorReceipt=str(failed_receipt.relative_to(ROOT)),
            priorSdkCommands={2: 0, 3: 2, 4: 10, 5: 20}[attempt_number],
            commandCountScope="Recorded outer driver commands; SDK calls inside unchanged build helpers are not individually wrapped/countable from this field.",
            priorBuilds=1 if attempt_number == 5 else 0, priorProbeCompilerCommands=0, priorLoaderCases=0,
            scopeInterpretation=interpretations[attempt_number],
            feedFormatAuthority=["https://learn.microsoft.com/en-us/nuget/hosting-packages/local-feeds", "https://github.com/NuGet/Home/wiki/Nupkg-Metadata-File"] if attempt_number >= 3 else [])
    catalog_metadata = None
    if attempt_number >= 5:
        cache = Path("/home/administrator/.nuget/packages/hexalith.eventstore.aspire/3.128.0")
        revision = next(row["revision"] for row in investigation["repositories"] if row["path"] == "references/Hexalith.EventStore")
        catalog_metadata = catalog_metadata_binding(cache / "hexalith.eventstore.aspire.3.128.0.nupkg", cache / "hexalith.eventstore.aspire.nuspec", revision)
        require(catalog_metadata["nupkg"]["sha256"] == "522e68bda69b2881417db790fd7938a30e7fa1f0935e1c14e47f675a69c89bda", "Catalog nupkg differs from independently observed input")
        require(catalog_metadata["nuspec"]["sha256"] == "36d376bab8683eaf72702f4df328b3e4ae839c5b0e06892bb6ae6fd9ab5a06bc", "Catalog nuspec differs from independently observed input")
    sdk = list(old["sdk"]["files"])
    seen = {item["path"] for item in sdk}
    dotnet_root = Path("/home/administrator/.dotnet")
    extra = [*list((dotnet_root / "sdk/10.0.402/Roslyn/bincore").glob("*")),
             *sdk_build_dependency_paths(dotnet_root / "sdk/10.0.402"),
             *list((dotnet_root / "host/fxr/10.0.13").glob("*")),
             dotnet_root / "sdk/10.0.402/MSBuild.dll", dotnet_root / "sdk/10.0.402/MSBuild.runtimeconfig.json",
             dotnet_root / "sdk/10.0.402/MSBuild.deps.json"]
    for path in extra:
        if path.is_file() and str(path) not in seen:
            sdk.append(binding(path))
            seen.add(str(path))
    third_party_packages = []
    for path in sorted(Path("/home/administrator/.nuget/packages").glob("*/*/*.nupkg")):
        if not path.name.lower().startswith("hexalith."):
            third_party_packages.append(binding(path))
    require(third_party_packages, "No offline third-party package inputs")
    prototype = Qualification.__new__(Qualification)
    prototype.work = Path("{private}")
    prototype.snapshot = prototype.work / "snapshot"
    omitted_links = []
    for row in investigation["repositories"]:
        for name in (".clinerules", ".cursorrules", ".windsurfrules"):
            path = ROOT / row["path"] / name
            if path.is_symlink():
                omitted_links.append(dict(path=str(path.relative_to(ROOT)), target=os.readlink(path), followed=False))
    return dict(schemaVersion=2, sdkClosurePolicy="fixed SDK 10.0.402 managed/native build/task dependency closure", recordId=record_id, continuation=continuation, approvalSource=str(SPEC.relative_to(ROOT)),
        authority="Jerome approved the conditional sequence in the frozen spec; no historical approval is reused.",
        repositories=investigation["repositories"], protectedFiles=[binding(ROOT / p, ROOT) for p in sorted(protected_paths)],
        sources=source_files(ROOT, investigation["repositories"]), sdkFiles=sdk,
        frameworkDirectories=old["installedFrameworkDirectories"], selectedCatalog=investigation["selectedCatalog"],
        thirdPartyPackages=third_party_packages, offlineFeedLayout=package_feed_layout(third_party_packages), catalogMetadata=catalog_metadata,
        tools=[binding(Path(shutil.which(name)).resolve()) for name in ("bwrap", "pwsh")],
        harnessFiles=[binding(HERE / name, HERE) for name in ("qualify.py", "source-references.targets", "LoaderProbe.cs", "verify.py", "tests/test_qualification_guards.py", "tests/test_review_guards.py")],
        omittedInstructionLinks=omitted_links, buildPropertyTemplates=prototype.properties(),
        omittedGeneratedInputs=[binding(ROOT / path, ROOT) for path in sorted(STALE_INPUTS)],
        projects=PROJECTS, configuration="Debug", sdkVersion="10.0.402", runtimeVersion="10.0.13",
        seccompSha256=hashlib.sha256(filter_bytes()).hexdigest(), controls=CONTROLS,
        sequence=["protected/revision/index checks", "fresh private snapshots and offline feed", "SDK identity gates",
                  "evaluated references", "restore AppHost; reject Hexalith package assets", "build AppHost",
                  "restore/build CLI", "restore/build matching Platform Composition", "seal host/model bytes",
                  "compile metadata probe", "cold once", "host-first once", "archive and preservation"],
        failurePolicy="First SDK/source/isolation/restore/build failure stops all dependent execution; binding failure permits only the second predeclared case; timeout stops both.",
        exactCommand=["python3", "-I", "-B", str(HERE / "qualify.py"), "--plan", "plan.json" if attempt_number == 1 else "plan-attempt-" + str(attempt_number) + ".json", "--execute"],
        environmentTemplate=environment(Path("{private}")),
        commandTemplates={"sdk": ["{dotnet}", "--version"],
            "evaluate": ["{dotnet}", "msbuild", "{project}", "-nologo", "-getProperty:Configuration,UseHexalithProjectReferences,UseNuGetDeps", "-getItem:PackageReference,ProjectReference", "{properties}"],
            "restore": ["{dotnet}", "restore", "{project}", "--disable-parallel", "-m:1", "-nr:false", "--configfile", "{private}/nuget.config", "{properties}"],
            "build": ["{dotnet}", "build", "{project}", "--no-restore", "--configuration", "Debug", "-m:1", "-v:diag", "-bl:{log}", "{properties}"],
            "case": ["{dotnet}", "exec", "--runtimeconfig", "{host}.runtimeconfig.json", "--depsfile", "{host}.deps.json", "{probe}", "{model}", "cold|host-first"]},
        deadlines=dict(sdk=30, build=600, case=60),
        limits=["Private source Debug bundle, not ordinary mapping or a packaged tool.",
                "Tracked .clinerules/.cursorrules/.windsurfrules instruction links are omitted and never followed; other special snapshot inputs fail closed.",
                "Prior signed 4.38 failure, package layout/API/runtime/hosted limits are unchanged.",
                "Loader mounts external paths read-only and denies network/socket access; no fallback.",
                "No application member or constructor is explicitly invoked; loading can run implicit initialization.",
                "Offline package feed contains only copied, byte-bound existing third-party packages; missing cache inputs stop restore."])


def environment(work):
    return dict(PATH="/home/administrator/.dotnet/tools:/home/administrator/.dotnet:/usr/bin:/bin", LANG="C.UTF-8", LC_ALL="C.UTF-8",
        DOTNET_ROOT="/home/administrator/.dotnet", DOTNET_CLI_HOME=str(work / "dotnet-home"),
        TMPDIR=str(work / "tmp"), TMP=str(work / "tmp"), TEMP=str(work / "tmp"),
        XDG_CACHE_HOME=str(work / "cache"), NUGET_PACKAGES=str(work / "nuget"),
        NUGET_HTTP_CACHE_PATH=str(work / "http-cache"), NUGET_PLUGINS_CACHE_PATH=str(work / "plugin-cache"),
        DOTNET_BUNDLE_EXTRACT_BASE_DIR=str(work / "bundle"), DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1",
        DOTNET_GENERATE_ASPNET_CERTIFICATE="false", DOTNET_SKIP_FIRST_TIME_EXPERIENCE="true",
        DOTNET_EnableDiagnostics="0", DOTNET_EnableDiagnostics_IPC="0", DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE="true",
        DOTNET_CLI_USE_MSBUILD_SERVER="0", MSBUILDDISABLENODEREUSE="1", POWERSHELL_TELEMETRY_OPTOUT="1")


class Qualification:
    """Owns only the newly allocated private output directory."""

    def __init__(self, plan, plan_path=None):
        self.plan = plan
        self.original_plan = binding(plan_path) if plan_path else None
        self.work = Path(tempfile.mkdtemp(prefix="story117-current-builds-host-"))
        self.work.chmod(0o700)
        self.snapshot = self.work / "snapshot"
        self.logs = self.work / "logs"
        for name in ("snapshot", "logs", "tmp", "cache", "http-cache", "plugin-cache", "nuget", "feed", "dotnet-home", "bundle", "graph", "sealed"):
            (self.work / name).mkdir()
        self.env = environment(self.work)
        self.dotnet = Path(self.env["DOTNET_ROOT"]) / "dotnet"
        self.commands = []
        self.snapshot_inputs = []
        self.private_inputs = []
        self.feed_ready = False
        self.case_seal = None
        self.builds_verified = False
        self.case_results = []
        self.fd = None
        self.counter = 0
        self.stop = None

    def inputs(self):
        if self.original_plan:
            same(self.original_plan["path"], self.original_plan)
        verify_repository_predicate(ROOT, self.plan)
        for item in self.plan["sdkFiles"] + self.plan["tools"]:
            same(item["path"], item)
        for path, expected in self.plan["frameworkDirectories"].items():
            require(sorted(p.name for p in Path(path).iterdir() if p.is_dir()) == expected, "Framework directory drift: " + path)
        for item in self.plan["sources"]:
            same(ROOT / item["path"], item)
        for item in self.snapshot_inputs:
            same(self.snapshot / item["path"], item)
        for item in self.private_inputs:
            same(self.work / item["path"], item)
        if getattr(self, "feed_ready", False):
            validate_feed_directory(self.work / "feed", self.plan["offlineFeedLayout"])
        for item in self.plan["harnessFiles"]:
            same(HERE / item["path"], item)
        metadata = self.plan.get("catalogMetadata")
        if metadata:
            require(catalog_metadata_binding(metadata["nupkg"]["path"], metadata["nuspec"]["path"], metadata["sourceRevision"]) == metadata,
                    "Catalog metadata input drift")

    def sandbox(self, argv, readonly=False):
        # Each fresh descriptor begins at offset zero; bwrap consumes it.
        self.fd = os.open(self.work / "deny-sockets.bpf", os.O_RDONLY)
        require(os.lseek(self.fd, 0, os.SEEK_CUR) == 0, "Seccomp descriptor reuse")
        cmd = ["/usr/bin/bwrap", "--unshare-all", "--die-with-parent", "--ro-bind", "/", "/",
               "--tmpfs", "/tmp", "--tmpfs", "/run", "--bind", str(self.work), str(self.work),
               "--ro-bind", str(self.work / "feed"), str(self.work / "feed"),
               "--proc", "/proc", "--dev", "/dev", "--seccomp", str(self.fd)]
        if readonly:
            cmd += ["--ro-bind", str(self.work / "sealed"), str(self.work / "sealed")]
        cmd += ["--chdir", str(self.snapshot), "--", *map(str, argv)]
        return cmd

    def run(self, label, argv, limit, readonly=False, env=None):
        record = command_record(label, argv, self.snapshot, env or self.env, limit)
        record["targetArgv"] = list(map(str, argv))
        self.commands.append(record)
        try:
            self.inputs()
            command = self.sandbox(argv, readonly)
            run_owned(command, self.snapshot, env or self.env, self.logs, label, limit, (self.fd,) if self.fd is not None else (), record)
        except BaseException as error:
            record.setdefault("error", dict(type=type(error).__name__, detail=str(error)))
            record.update(status="failed", endedAtUtc=now())
            write(self.logs / (label + ".command.json"), record)
            raise
        finally:
            if getattr(self, "fd", None) is not None:
                os.close(self.fd)
                self.fd = None
        print(json.dumps(dict(atUtc=now(), gate=label, exitCode=record["exitCode"], private=str(self.work))), flush=True)
        return record

    def sdk_gate(self, label):
        record = self.run("sdk-" + label, [self.dotnet, "--version"], 30)
        output = (self.logs / ("sdk-" + label + ".stdout")).read_text().strip()
        require(record["exitCode"] == 0 and output == self.plan["sdkVersion"], "SDK selection failed: " + output)
        self.inputs()

    def checked(self, label, argv, limit=600):
        self.sdk_gate(label)
        result = self.run(label, argv, limit)
        require(result["exitCode"] == 0, "Command failed: " + label + "; exit " + str(result["exitCode"]))
        return result

    def prepare(self):
        # Retain every executed harness/plan byte before any fallible feed operation.
        write(self.work / "plan.json", self.plan)
        for item in self.plan["harnessFiles"]:
            target = self.work / "harness" / item["path"]
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(HERE / item["path"], target)
            same(target, item)
        for item in self.plan["sources"]:
            source = ROOT / item["path"]
            target = self.snapshot / item["path"]
            same(source, item)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
            same(target, item)
        self.snapshot_inputs = list(self.plan["sources"])
        require(not any((self.snapshot / path).exists() for path in STALE_INPUTS), "Stale generated input reached private snapshot")
        metadata = self.plan.get("catalogMetadata")
        if metadata:
            seeded_metadata = seed_catalog_metadata(metadata, self.work / "nuget")
            write(self.work / "catalog-metadata-provenance.json", metadata)
            (self.work / "catalog-inputs").mkdir()
            shutil.copy2(seeded_metadata, self.work / "catalog-inputs/hexalith.eventstore.aspire.nuspec")
        shutil.copy2(HERE / "source-references.targets", self.work / "source-references.targets")
        layout = package_feed_layout(self.plan["thirdPartyPackages"])
        require(layout == self.plan["offlineFeedLayout"], "Offline package metadata/layout drift")
        for item in layout:
            same(item["path"], item)
            target = self.work / "feed" / item["feedPath"]
            require(not target.exists(), "Conflicting preexisting private feed output: " + item["feedPath"])
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(item["path"], target)
            same(target, item)
        validate_feed_directory(self.work / "feed", layout)
        self.feed_ready = True
        write(self.work / "offline-feed-discovery.json", dict(format="flat canonical ID.version.nupkg",
            packageCount=len(layout), byteAndMembershipChecksPassed=True, source=str(self.work / "feed")))
        (self.work / "deny-sockets.bpf").write_bytes(filter_bytes())
        require(sha(self.work / "deny-sockets.bpf") == self.plan["seccompSha256"], "Socket filter drift")
        (self.work / "nuget.config").write_text('<?xml version="1.0"?><configuration><packageSources><clear/><add key="private-offline" value="' + str(self.work / "feed") + '"/></packageSources></configuration>\n')
        shutil.copy2(self.work / "nuget.config", self.snapshot / "NuGet.Config")
        write(self.work / "sdk-feed-configuration.json", sdk_feed_configuration(
            self.snapshot / self.plan["projects"][0], self.snapshot, self.work / "feed"))
        write(self.work / "environment.json", dict(environment=self.env, inheritance="exact Popen environment; HOME is not repurposed", scope="all SDK/compiler/build/loader targets"))
        write(self.work / "plan.json", self.plan)
        self.private_inputs = [binding(self.work / name, self.work) for name in
            ("source-references.targets", "deny-sockets.bpf", "nuget.config", "snapshot/NuGet.Config",
             "sdk-feed-configuration.json", "environment.json", "plan.json")]
        self.private_inputs.append(binding(self.work / "offline-feed-discovery.json", self.work))
        self.private_inputs.extend(binding(p, self.work) for p in (self.work / "harness").rglob("*") if p.is_file())
        if metadata:
            self.private_inputs.extend([binding(seeded_metadata, self.work), binding(self.work / "catalog-metadata-provenance.json", self.work),
                                        binding(self.work / "catalog-inputs/hexalith.eventstore.aspire.nuspec", self.work)])

    def properties(self):
        refs = self.snapshot / "references"
        props = dict(Configuration="Debug", Story117Qualification="true", UseHexalithProjectReferences="true",
            UseNuGetDeps="false", CustomAfterMicrosoftCommonTargets=str(self.work / "source-references.targets"),
            Story117EvidenceDirectory=str(self.work / "graph"), UseSharedCompilation="false",
            Story117HarnessPath=str(self.work / "harness/qualify.py"), Story117SnapshotRoot=str(self.snapshot),
            Story117BoundPlanPath=str(self.work / "plan.json"),
            RestorePackagesPath=str(self.work / "nuget"), RestoreConfigFile=str(self.work / "nuget.config"),
            RestoreSources=str(self.work / "feed"), RestoreBuildInParallel="false", BuildInParallel="false",
            VersionSuffix="qualification-20261026")
        for name in ("Builds", "EventStore", "FrontComposer", "Commons", "PolymorphicSerializations", "Tenants", "Memories", "Parties", "McpCli"):
            props["Hexalith" + name + "Root"] = str(refs / ("Hexalith." + name))
            if name != "Builds":
                props["Hexalith" + name + "FromSource"] = "true"
        props["HexalithTenantsBasePath"] = str(refs / "Hexalith.Tenants/src")
        for name in ("EventStore", "Tenants", "Parties", "Memories", "Commons"):
            props[name + "Path"] = str(refs / ("Hexalith." + name))
        # These are supported root selectors for older shared import layouts.
        props["Hexalith1BuildBuildProps"] = str(refs / "Hexalith.Builds/Hexalith.Build.props")
        props["Hexalith1BuildPackageProps"] = str(refs / "Hexalith.Builds/Props/Directory.Packages.props")
        return ["-p:" + name + "=" + value for name, value in props.items()]

    def evaluated(self, project, label, extra):
        self.checked("evaluate-" + label, [self.dotnet, "msbuild", project, "-nologo",
            "-getProperty:Configuration,UseHexalithProjectReferences,UseNuGetDeps",
            "-getItem:PackageReference,ProjectReference", *self.properties(), *extra], 30)
        data = json.loads((self.logs / ("evaluate-" + label + ".stdout")).read_text())
        require(data["Properties"]["Configuration"] == "Debug", "Non-Debug evaluation")
        require(data["Properties"]["UseHexalithProjectReferences"] == "true", "Missing source selector")
        for item in data["Items"].get("PackageReference", []):
            require(not item["Identity"].startswith("Hexalith."), "Evaluated Hexalith package fallback: " + item["Identity"])
        for item in data["Items"].get("ProjectReference", []):
            require(Path(item["FullPath"]).resolve().is_relative_to(self.snapshot), "External evaluated source reference")
        return data

    def assets(self):
        paths = sorted(self.snapshot.rglob("project.assets.json"))
        require(paths, "No restored assets")
        result = []
        for path in paths:
            data = json.loads(path.read_text())
            validate_assets(data, self.snapshot)
            result.append(binding(path, self.snapshot))
        write(self.work / "assets-checked.json", result)

    def restore_argv(self, project, extra):
        argv = [self.dotnet, "restore", project, "--disable-parallel", "-m:1", "-nr:false", "--configfile",
                self.work / "nuget.config", *self.properties(), *extra]
        validate_serial_restore(list(map(str, argv)))
        return argv

    def build(self):
        labels = ["AppHost", "CLI", "Platform-Composition"]
        for rel, label in zip(self.plan["projects"], labels):
            project = self.snapshot / rel
            require(not (project.parent / "bin").exists() and not (project.parent / "obj").exists()
                    or label in ("CLI", "Platform-Composition"), "Stale initial outputs")
            extra = []
            if label == "Platform-Composition":
                tooling = self.snapshot / "references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/bin/Debug/net10.0/Hexalith.Builds.Tooling.dll"
                require(tooling.is_file(), "Matching source Tooling is missing")
                extra = ["-p:BuildsToolingAssemblyPath=" + str(tooling)]
            self.evaluated(project, label, extra)
            if label == "AppHost":
                for host in ("EventStoreHost", "UiHost"):
                    child = project.parent.parent / ("Hexalith.Builds.Module." + host) / ("Hexalith.Builds.Module." + host + ".csproj")
                    self.evaluated(child, host, extra)
            self.checked("restore-" + label, self.restore_argv(project, extra))
            self.assets()
            self.checked("build-" + label, [self.dotnet, "build", project, "--no-restore", "--configuration", "Debug", "-m:1", "-v:diag", "-bl:" + str(self.logs / (label + ".binlog")), *self.properties(), *extra])
            self.assets()
        require(not list(self.snapshot.glob("**/bin/Release")), "Release output appeared")
        self.builds_verified = True

    def seal(self):
        require(self.builds_verified, "Cannot seal before all source graph/build gates pass")
        host = self.snapshot / Path(self.plan["projects"][0]).parent / "bin/Debug/net10.0"
        model = self.snapshot / Path(self.plan["projects"][2]).parent / "bin/Debug/net10.0"
        for name, path in (("host", host), ("model", model)):
            require(path.is_dir(), "Missing output directory: " + str(path))
            shutil.copytree(path, self.work / "sealed" / name)
        require(sha(host / "Hexalith.Builds.Tooling.dll") == sha(model / "Hexalith.Builds.Tooling.dll"), "Host/model Tooling bytes differ")
        for directory, names in ((host, ("Hexalith.EventStore.Aspire.dll", "Aspire.Hosting.dll", "StackExchange.Redis.dll")),
                                 (model, ("Hexalith.EventStore.Aspire.dll", "Hexalith.EventStore.ServiceDefaults.dll"))):
            for name in names:
                require((directory / name).is_file(), "Missing bound loader input: " + str(directory / name))
        self.case_seal = [binding(p, self.work / "sealed") for p in sorted((self.work / "sealed").rglob("*")) if p.is_file()]
        write(self.work / "output-seal.json", self.case_seal)
        return host, model

    def probe(self):
        require(self.builds_verified and self.case_seal is not None, "Cannot launch before verified builds and sealed outputs")
        source = self.work / "LoaderProbe.cs"
        shutil.copy2(HERE / "LoaderProbe.cs", source)
        target = self.work / "sealed/host/LoaderProbe.dll"
        references = [item["path"] for item in self.plan["sdkFiles"] if "/packs/Microsoft.NETCore.App.Ref/10.0.13/ref/net10.0/" in item["path"]]
        self.checked("compile-probe", [self.dotnet, "exec", self.dotnet.parent / "sdk/10.0.402/Roslyn/bincore/csc.dll",
            "-noconfig", "-nostdlib+", "-target:exe", "-langversion:14", "-nullable:enable", "-deterministic+",
            "-out:" + str(target), *["-r:" + p for p in references], source], 60)
        # The generated host deps/runtimeconfig files remain byte-for-byte unchanged.
        self.case_seal.append(binding(target, self.work / "sealed"))
        write(self.work / "output-seal.json", self.case_seal)
        for mode in ("cold", "host-first"):
            validate_seal(self.work / "sealed", self.case_seal)
            self.sdk_gate(mode)
            host = self.work / "sealed/host/Hexalith.Builds.Module.AppHost"
            env = dict(self.env, DOTNET_HOST_TRACE="1", DOTNET_HOST_TRACEFILE=str(self.logs / (mode + ".host-trace.log")), DOTNET_HOST_TRACE_VERBOSITY="4")
            record = self.run("case-" + mode, [self.dotnet, "exec", "--runtimeconfig", str(host) + ".runtimeconfig.json",
                "--depsfile", str(host) + ".deps.json", target, self.work / "sealed/model/Hexalith.Platform.Composition.dll", mode], 60, readonly=True, env=env)
            events = [json.loads(line) for line in (self.logs / ("case-" + mode + ".stdout")).read_text().splitlines()]
            validation = validate_probe_events(events, record["exitCode"], self.work / "sealed", self.case_seal, mode)
            self.case_results.append(dict(mode=mode, exitCode=record["exitCode"],
                bindingSucceeded=validation["bindingSucceeded"], finalObservation=events[-1],
                hostAssembly=next(item for item in self.case_seal if item["path"] == "host/Hexalith.Builds.Module.AppHost.dll"),
                modelAssembly=next(item for item in self.case_seal if item["path"] == "model/Hexalith.Platform.Composition.dll"),
                scope="Explicit metadata/identity demands only; no package/API/ordinary/hosted acceptance"))
            write(self.work / "case-results.json", self.case_results)
            validate_seal(self.work / "sealed", self.case_seal)

    def finish(self, before, stores_before):
        """Record observation errors as unknown and always produce archive inputs."""
        errors = []
        drifts = []
        after = None
        stores_after = None
        equality = None
        stores_equal = None
        try:
            self.inputs()
        except BaseException as error:
            drifts.append(str(error))
            errors.append(dict(observation="inputs", type=type(error).__name__, detail=str(error)))
        try:
            after = state(ROOT, self.plan["repositories"])
            if before is not None:
                equality = all(before[rel][key] == after[rel][key] for rel in before
                    for key in ("revision", "stageEntriesSha256", "indexSha256", "indexFlagsSha256", "status"))
        except BaseException as error:
            errors.append(dict(observation="repository", type=type(error).__name__, detail=str(error)))
        try:
            stores_after = store_metadata(self.work)
            if stores_before is not None:
                stores_equal = stores_before == stores_after
        except BaseException as error:
            errors.append(dict(observation="certificateMetadata", type=type(error).__name__, detail=str(error)))
        if before is None or stores_before is None:
            errors.append(dict(observation="baseline", type="UnknownBaseline", detail="Initial preservation observation did not complete"))
        preservation = dict(atUtc=now(), repositoryBefore=before, repositoryAfter=after,
            revisionsIndicesFlagsAndStatusEqual=equality, storesBefore=stores_before, storesAfter=stores_after,
            certificateStoreMetadataEqual=stores_equal, protectedInputDrifts=drifts, observationErrors=errors,
            status="verified" if equality is True and stores_equal is True and not errors else "failed-or-unknown",
            scope="Bound repositories/inputs and certificate metadata only; failed observations prove no preservation.")
        if errors and self.stop is None:
            self.stop = dict(atUtc=now(), type="PreservationObservationFailure", detail="Final preservation is failed or unknown", dependentExecutionStopped=True)
        write(self.work / "preservation.json", preservation)
        attempts = {mode: sum(row.get("launched") is True and row["label"] == "case-" + mode for row in self.commands)
                    for mode in ("cold", "host-first")}
        write(self.work / "outcome.json", dict(atUtc=now(), stop=self.stop, controls=CONTROLS,
            completedCommandLabels=[row["label"] for row in self.commands if row.get("launched") and row.get("exitCode") is not None],
            commands=self.commands, consumedLoaderAttempts=attempts, claimConsumed=getattr(self, "claim_consumed", False),
            caseResults=self.case_results,
            currentSourceBinding="unexecuted" if not any(attempts.values()) else "see per-case results; launched failures remain consumed",
            prerequisite="No implicit retry; inspect the retained failure/claim and remaining authority." if self.stop else "Binding evidence only; ordinary/package/API/hosted acceptance remains separate.",
            privateDirectory=str(self.work), cleanup="owned-child termination observed before archival; private files retained"))
        return preservation


def archive(work, record_id=PREFIX):
    """Retain portable diagnostic inputs/logs, assets, compiler inputs and actual outputs."""
    members = []
    paths = []
    for path in sorted(work.rglob("*")):
        rel = path.relative_to(work)
        crash_report = rel.parts[0] == "tmp" and rel.name.startswith("MSBuild_pid-") and rel.name.endswith(".failure.txt")
        if rel.parts[0] in {"feed", "nuget", "cache", "http-cache", "plugin-cache", "dotnet-home", "bundle", "tmp"} and not crash_report:
            continue
        require(not path.is_symlink(), "Archive symlink: " + str(rel))
        if path.is_file():
            require(stat.S_ISREG(path.lstat().st_mode), "Archive special file: " + str(rel))
            paths.append(path)
            members.append(binding(path, work))
    target = EVIDENCE / (record_id + ".artifacts.tar.gz")
    with tarfile.open(target, "x:gz") as output:
        for path in paths:
            output.add(path, arcname=str(path.relative_to(work)), recursive=False)
    for item in members:
        same(work / item["path"], item)
    write(EVIDENCE / (record_id + ".members.json"), members)
    receipt = dict(recordId=record_id, atUtc=now(), archive=binding(target, EVIDENCE),
        memberManifest=binding(EVIDENCE / (record_id + ".members.json"), EVIDENCE), memberCount=len(members),
        outcome=json.loads((work / "outcome.json").read_text()),
        preservation=json.loads((work / "preservation.json").read_text()),
        omissions="Private third-party feed, extracted package/cache, certificate-store contents and temporary home excluded; exact package input hashes remain in plan.")
    write(EVIDENCE / (record_id + ".json"), receipt)
    return receipt


def record_failed_preflight(plan_path, invocation, error):
    """Add an explicit immutable observation without claiming execution authority."""
    descriptor, name = tempfile.mkstemp(prefix=PREFIX + ".failed-preflight-", suffix=".json", dir=EVIDENCE)
    record = dict(atUtc=now(), invocation=invocation, cwd=str(Path.cwd()), planPath=str(plan_path),
                  error=dict(type=type(error).__name__, detail=str(error)),
                  targetLaunchCount=0, claimConsumed=False, controls=CONTROLS)
    if plan_path.is_file() and not plan_path.is_symlink():
        record["observedPlan"] = binding(plan_path)
    with os.fdopen(descriptor, "w") as stream:
        json.dump(record, stream, indent=2)
        stream.write("\n")
    return Path(name)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prepare", action="store_true", help="Bind a new plan; performs no SDK execution")
    parser.add_argument("--check", action="store_true", help="Check bound static inputs; performs no SDK execution")
    parser.add_argument("--execute", action="store_true", help="Consume this spec's single conditional sequence")
    parser.add_argument("--plan", choices=("plan.json", "plan-attempt-2.json", "plan-attempt-3.json", "plan-attempt-4.json", "plan-attempt-5.json"), default="plan.json")
    parser.add_argument("--audit-references", type=Path)
    parser.add_argument("--record-source-output", type=Path)
    parser.add_argument("--source-project", type=Path)
    parser.add_argument("--snapshot", type=Path)
    parser.add_argument("--bound-plan", type=Path)
    parser.add_argument("--proof-directory", type=Path)
    try:
        args = parser.parse_args()
    except SystemExit as error:
        if "--execute" in sys.argv and error.code:
            record_failed_preflight(HERE / "plan.json", list(sys.orig_argv), error)
        raise
    if args.audit_references or args.record_source_output:
        require(not any((args.prepare, args.check, args.execute)) and bool(args.audit_references) != bool(args.record_source_output), "Choose one reference gate")
        require(all((args.source_project, args.snapshot, args.bound_plan, args.proof_directory)), "Missing reference gate inputs")
        if args.audit_references:
            audit_reference_file(args.audit_references, args.source_project, args.snapshot, args.bound_plan, args.proof_directory)
        else:
            record_source_output(args.source_project, args.record_source_output, args.snapshot, args.bound_plan, args.proof_directory)
        return 0
    plan_path = HERE / args.plan
    try:
        require(sum((args.prepare, args.check, args.execute)) == 1, "Choose one operation")
    except GateFailure as error:
        if args.execute:
            record_failed_preflight(plan_path, list(sys.orig_argv), error)
            return 2
        raise
    if args.prepare:
        require(not plan_path.exists(), "Existing plan may not be silently rebound")
        write(plan_path, make_plan(PREFIX if args.plan == "plan.json" else PREFIX + "-attempt-" + args.plan.removeprefix("plan-attempt-").removesuffix(".json")))
        print(str(plan_path))
        return 0
    try:
        plan = json.loads(plan_path.read_text())
        verify_repository_predicate(ROOT, plan)
        for item in plan["sources"]:
            same(ROOT / item["path"], item)
        for item in plan["harnessFiles"]:
            same(HERE / item["path"], item)
        for item in plan["sdkFiles"] + plan["tools"]:
            same(item["path"], item)
        require(package_feed_layout(plan["thirdPartyPackages"]) == plan["offlineFeedLayout"], "Offline feed preflight drift")
        metadata = plan.get("catalogMetadata")
        if metadata:
            require(catalog_metadata_binding(metadata["nupkg"]["path"], metadata["nuspec"]["path"], metadata["sourceRevision"]) == metadata,
                    "Catalog metadata preflight drift")
        if args.execute:
            require(not (EVIDENCE / (plan["recordId"] + ".claim.json")).exists(), "This attempt claim is already consumed")
    except BaseException as error:
        if not args.execute:
            raise
        record_failed_preflight(plan_path, list(sys.orig_argv), error)
        return 2
    if args.check:
        print("Static input/revision/index/SDK-byte checks passed; no SDK invoked.")
        return 0
    record_id = plan["recordId"]
    claim_path = EVIDENCE / (record_id + ".claim.json")
    try:
        qualification = Qualification(plan, plan_path)
    except BaseException as error:
        record_failed_preflight(plan_path, list(sys.orig_argv), error)
        return 2
    before = None
    stores_before = None
    try:
        before = state(ROOT, plan["repositories"])
        stores_before = store_metadata(qualification.work)
        qualification.prepare()
        qualification.inputs()
        claim_once(claim_path, dict(atUtc=now(), specSha256=sha(SPEC), planSha256=sha(plan_path),
                                   authority=plan["authority"], continuation=plan["continuation"], privatePreparationPassed=True))
        qualification.claim_consumed = True
        before = state(ROOT, plan["repositories"])
        qualification.build()
        qualification.seal()
        qualification.probe()
    except BaseException as error:
        qualification.stop = dict(atUtc=now(), type=type(error).__name__, detail=str(error), dependentExecutionStopped=True)
        print(json.dumps(qualification.stop), flush=True)
    finally:
        preservation = qualification.finish(before, stores_before)
        receipt = archive(qualification.work, record_id)
        print(json.dumps(dict(receipt=str(EVIDENCE / (record_id + ".json")), members=receipt["memberCount"], private=str(qualification.work))), flush=True)
    return 2 if qualification.stop or preservation["revisionsIndicesFlagsAndStatusEqual"] is not True or preservation["certificateStoreMetadataEqual"] is not True or preservation["protectedInputDrifts"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
