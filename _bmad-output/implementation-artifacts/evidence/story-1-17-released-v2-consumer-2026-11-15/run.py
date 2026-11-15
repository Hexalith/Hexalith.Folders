"""Run the bounded released-pair consumer qualification in disposable copies."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parent
if len(sys.argv) == 2:
    attempt = sys.argv[1]
    require_name = attempt and attempt not in {".", ".."} and all(
        character.isascii() and (character.isalnum() or character in "._-") for character in attempt)
    if not require_name:
        raise SystemExit("Unsafe attempt name")
    OUT = OUT / attempt
    OUT.mkdir()
SCRATCH = Path(os.environ["TMPDIR"]).resolve()
WORKSPACE = SCRATCH / "workspace"
COMMANDS = []
ENV = dict(os.environ)
for key in list(ENV):
    if key.startswith(("Hexalith", "NuGet", "Restore")) or key in {
        "CI", "Configuration", "UseNuGetDeps", "UseHexalithProjectReferences",
        "NUGET_PACKAGES", "NUGET_HTTP_CACHE_PATH", "DOTNET_CLI_HOME",
    }:
        del ENV[key]
ENV.update({
    "PATH": os.environ["PATH"] + ":" + str(Path.home() / ".dotnet/tools"),
    "NUGET_PACKAGES": str(SCRATCH / "packages"),
    "NUGET_HTTP_CACHE_PATH": str(SCRATCH / "http-cache"),
    "DOTNET_CLI_HOME": str(SCRATCH / "dotnet-home"),
    "DOTNET_NOLOGO": "1", "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
    "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_CLI_USE_MSBUILD_SERVER": "0",
    "MSBUILDDISABLENODEREUSE": "1", "UseSharedCompilation": "false",
    "PYTHONDONTWRITEBYTECODE": "1", "NuGetAudit": "true",
    "NuGetAuditMode": "all", "NuGetAuditLevel": "low",
})


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def safe(text):
    return str(text).replace(str(WORKSPACE), "{workspace}").replace(
        str(SCRATCH), "{temp}").replace(str(ROOT), "{source}")


def write(name, value):
    (OUT / name).write_text(json.dumps(value, indent=2) + "\n")


def run(name, argv, cwd=WORKSPACE, allowed=(0,)):
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    clock = time.monotonic()
    print(name + " started", flush=True)
    result = subprocess.run(argv, cwd=cwd, env=ENV, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT, text=True)
    (OUT / (name + ".log")).write_text(safe(result.stdout))
    row = {"name": name, "argv": [safe(arg) for arg in argv], "cwd": safe(cwd),
           "started_utc": started, "elapsed_seconds": round(time.monotonic() - clock, 3),
           "exit_code": result.returncode, "log": name + ".log"}
    COMMANDS.append(row)
    write("commands.json", COMMANDS)
    print(name + " exit " + str(result.returncode), flush=True)
    require(result.returncode in allowed, name + " failed; see its retained log")
    return result


def copy_repository(source, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    run("copy-" + source.name,
        ["git", "-c", "submodule.recurse=false", "clone", "--quiet", "--shared",
         "--no-checkout", str(source), str(destination)], cwd=ROOT)
    subprocess.run(["git", "-C", str(destination), "read-tree", "HEAD"], check=True)
    names = subprocess.check_output(["git", "-C", str(source), "ls-files", "-z"]).split(b"\0")
    files = [name.decode() for name in names if name and (source / name.decode()).is_file()]
    for start in range(0, len(files), 150):
        subprocess.run(["cp", "--reflink=auto", "--parents", "--preserve=mode,timestamps",
                        "--target-directory=" + str(destination), "--", *files[start:start + 150]],
                       cwd=source, check=True)


def verify_public_archives():
    publication = json.loads((Path(__file__).resolve().parent / "publication-verification.retained.json").read_text())
    require(publication["status"] == "verified" and publication["version"] == "2.0.0",
            "Unexpected retained publication")
    (OUT / "publication-verification.retained.json").write_text(json.dumps(publication, indent=2) + "\n")
    downloads = OUT / "packages"
    downloads.mkdir()
    rows = []
    for package in publication["packages"]:
        archive_path = downloads / (package["id"] + ".2.0.0.nupkg")
        with urllib.request.urlopen(package["url"], timeout=60) as response:
            archive_path.write_bytes(response.read())
        require(sha(archive_path) == package["public_archive_sha256"], "Public archive changed")
        with zipfile.ZipFile(archive_path) as archive:
            metadata = ET.fromstring(archive.read(package["id"] + ".nuspec")).find("{*}metadata")
            require(metadata is not None, "Absent package metadata")
            require(metadata.findtext("{*}id") == package["id"] and
                    metadata.findtext("{*}version") == "2.0.0", "Wrong package identity")
            require(metadata.find("{*}repository").attrib["commit"] == publication["source"],
                    "Wrong published source")
            require(".signature.p7s" in archive.namelist(), "Absent repository signature")
            payload = "lib/net11.0/" + package["id"] + ".dll"
            rows.append({"id": package["id"], "version": "2.0.0", "archive": str(archive_path.relative_to(OUT)),
                         "sha256": sha(archive_path), "repository_commit": publication["source"],
                         "payload_member": payload,
                         "payload_sha256": hashlib.sha256(archive.read(payload)).hexdigest()})
    write("public-packages.json", rows)
    print("Five public archives match retained release identities and source.", flush=True)
    return rows


def runtime_bindings(projects, packages):
    rows = []
    for owner, rel in (("server", "src/Hexalith.Projects.Server"),
                       ("tests", "tests/Hexalith.Projects.Server.Tests")):
        project = projects / rel
        assets = json.loads((project / "obj/project.assets.json").read_text())
        selected = {key for key in assets["libraries"] if key.startswith("Hexalith.Folders.")}
        for package in packages:
            if package["id"] not in {"Hexalith.Folders.Client", "Hexalith.Folders.Contracts"}:
                continue
            require(package["id"] + "/2.0.0" in selected, "Released pair absent from restored graph")
            runtime = project / "bin/Release/net11.0" / (package["id"] + ".dll")
            cache = SCRATCH / "packages" / package["id"].lower() / "2.0.0" / (package["id"].lower() + ".2.0.0.nupkg")
            require(sha(runtime) == package["payload_sha256"], "Loaded runtime DLL differs from signed archive")
            require(sha(cache) == package["sha256"], "Restored archive differs from verified public archive")
            rows.append({"project": owner, "package": package["id"] + "/2.0.0",
                         "archive_sha256": sha(cache), "runtime_sha256": sha(runtime),
                         "runtime": str(runtime.relative_to(WORKSPACE)),
                         "payload_member": package["payload_member"]})
    write("runtime-bindings.json", rows)


def source_inputs():
    owners = {}
    for relative in (".", "references/Hexalith.Builds", "references/Hexalith.Projects"):
        private = WORKSPACE / relative
        names = subprocess.check_output(["git", "-C", str(private), "ls-files", "-z"]).split(b"\0")
        files = {}
        for name in names:
            if not name:
                continue
            path = name.decode()
            candidate = private / path
            if not candidate.is_file() or path.startswith(("_bmad-output/implementation-artifacts/evidence/",
                                                           "references/", ".agents/", "_bmad/")):
                continue
            value = sha(candidate)
            require(value == sha(ROOT / relative / path), "Private source drift: " + relative + "/" + path)
            files[path] = value
        owners[relative] = {"head": subprocess.check_output(["git", "-C", str(private), "rev-parse", "HEAD"],
                                                          text=True).strip(), "files": files}
    write("source-inputs.json", owners)


def seed_explicit_downloads():
    """Keep legacy primary-cache HintPaths usable with the read-only fallback."""
    selected = set()
    for owner in (WORKSPACE, WORKSPACE / "references/Hexalith.Projects"):
        names = subprocess.check_output(["git", "-C", str(owner), "ls-files", "-z"]).split(b"\0")
        for name in names:
            if not name or not name.endswith(b".csproj") or name.startswith(b"_bmad-output/"):
                continue
            path = owner / name.decode()
            for item in ET.parse(path).getroot().iter("PackageDownload"):
                version = item.attrib.get("Version", "")
                if item.attrib.get("Include") == "Hexalith.Builds.Module.Cli" and version.startswith("[") and version.endswith("]"):
                    selected.add(version[1:-1])
    for version in sorted(selected):
        source = Path.home() / ".nuget/packages/hexalith.builds.module.cli" / version
        destination = SCRATCH / "packages/hexalith.builds.module.cli" / version
        if source.is_dir():
            destination.parent.mkdir(parents=True, exist_ok=True)
            run("seed-explicit-tooling-" + version, ["cp", "-a", "--reflink=auto", str(source), str(destination)], cwd=ROOT)


def retain_runtime_artifact(projects):
    source = projects / "src/Hexalith.Projects.Server/bin/Release/net11.0"
    members = []
    target = OUT / "projects-server-local-build.zip"
    with zipfile.ZipFile(target, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=1) as archive:
        for path in sorted(source.rglob("*")):
            if not path.is_file() or not (path.suffix.lower() in {".dll", ".pdb", ".so", ".exe"}
                                          or path.name.endswith((".deps.json", ".runtimeconfig.json"))):
                continue
            name = str(path.relative_to(source))
            info = zipfile.ZipInfo(name, (1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(info, path.read_bytes(), compresslevel=1)
            members.append({"path": name, "sha256": sha(path), "bytes": path.stat().st_size})
    write("projects-local-artifact.json", {"archive": target.name, "sha256": sha(target),
                                         "kind": "unpublished local compiled runtime; no hosted image/deployment or immutable release claim",
                                         "configuration": "Release/package", "members": members})


def main():
    require(SCRATCH != ROOT and WORKSPACE.is_relative_to(SCRATCH), "Unsafe scratch root")
    packages = verify_public_archives()
    copy_repository(ROOT, WORKSPACE)
    declarations = subprocess.check_output(["git", "config", "--file", ".gitmodules", "--get-regexp",
                                            r"^submodule\..*\.path$"], cwd=ROOT, text=True)
    for declaration in declarations.splitlines():
        relative = declaration.split(maxsplit=1)[1]
        copy_repository(ROOT / relative, WORKSPACE / relative)
    source_inputs()
    seed_explicit_downloads()
    buildroot = WORKSPACE / "references/Hexalith.Builds"
    projects = WORKSPACE / "references/Hexalith.Projects"
    psh = ["pwsh", "-NoLogo", "-NoProfile", "-File"]
    run("dotnet-info", ["dotnet", "--info"])
    run("catalog-validation", psh + ["Tools/validate-central-package-versions.ps1"], buildroot)
    run("catalog-authority", psh + ["Tools/test-authoritative-package-catalog.ps1"], buildroot)
    run("catalog-platform", psh + ["Tools/validate-platform-version-catalog.ps1"], buildroot)
    run("catalog-exceptions", psh + ["Tools/validate-package-version-exceptions.ps1", "-InventoryPath",
                                   "Tools/package-version-exceptions.json", "-CatalogPath",
                                   "Props/Directory.Packages.props"], buildroot)
    run("audit-precommit-refusal", psh + ["Tools/audit-central-package-versions.ps1", "-ChangedFamily",
                                        "hexalith-folders", "-PriorAuditPath", "Tools/package-version-audit.json",
                                        "-OutputPath", str(SCRATCH / "audit-proposal.json")], buildroot, (1,))
    run("audit-current-refusal", psh + ["Tools/validate-package-version-audit.ps1"], buildroot, (1,))
    props = ["-p:UseHexalithProjectReferences=false", "-p:UseNuGetDeps=true", "-p:NuGetAudit=true",
             "-p:NuGetAuditMode=all", "-p:NuGetAuditLevel=low",
             "-p:RestoreFallbackFolders=" + str(Path.home() / ".nuget/packages"), "-m:1", "--disable-build-servers"]
    run("projects-release-restore", ["dotnet", "restore", "Hexalith.Projects.CI.slnx", "-p:Configuration=Release",
                                    "--configfile", "nuget.config", *props], projects)
    run("projects-release-build", ["dotnet", "build", "Hexalith.Projects.CI.slnx", "--configuration", "Release",
                                  "--no-restore", "-warnaserror", *props], projects)
    classes = [
        "Hexalith.Projects.Server.Tests.ProjectFolderDirectoryTests",
        "Hexalith.Projects.Server.Tests.ProjectFileReferenceDirectoryTests",
        "Hexalith.Projects.Server.Tests.DelegatedProjectDirectoryRegistrationTests",
        "Hexalith.Projects.Server.Tests.Queries.DelegatedProjectAdvisoryConversationDirectoryTests",
        "Hexalith.Projects.Server.Tests.Folders.ProjectFolderValidationOutcomeMapperTests",
        "Hexalith.Projects.Server.Tests.Folders.ProjectFileReferenceValidationOutcomeMapperTests",
    ]
    assembly = "tests/Hexalith.Projects.Server.Tests/bin/Release/net11.0/Hexalith.Projects.Server.Tests.dll"
    for index, selector in enumerate(classes):
        xml_path = OUT / ("projects-" + str(index) + ".xml")
        run("projects-tests-" + str(index), ["dotnet", assembly, "-noLogo", "-noColor", "-class", selector,
                                           "-result-xml", str(xml_path)], projects)
        cases = ET.parse(xml_path).getroot().findall(".//test")
        require(cases and all(case.attrib["result"] == "Pass" for case in cases), "Empty or nonpassing selector")
    runtime_bindings(projects, packages)
    retain_runtime_artifact(projects)
    run("folders-release-restore", ["dotnet", "restore", "Hexalith.Folders.CI.slnx", "-p:Configuration=Release",
                                   "--configfile", "nuget.config", *props])
    run("folders-release-build", ["dotnet", "build", "Hexalith.Folders.CI.slnx", "--configuration", "Release",
                                 "--no-restore", "-warnaserror", *props])
    run("folders-oracle-build", ["dotnet", "build", "tests/tools/parity-oracle-generator/Hexalith.Folders.ParityOracleGenerator.csproj",
                                "--configuration", "Release", "-warnaserror", *props])
    run("folders-parity", psh + ["tests/tools/run-contract-parity-ci-gates.ps1", "-SkipRestoreBuild",
                                 "-OutputReportPath", str(OUT / "parity-report.json")])
    run("folders-governance", psh + ["tests/tools/run-governance-completeness-gates.ps1", "-SkipRestoreBuild"])
    debug_props = ["-p:UseHexalithProjectReferences=true", "-p:UseNuGetDeps=false", "-p:NuGetAudit=true",
                   "-p:NuGetAuditMode=all", "-p:NuGetAuditLevel=low",
                   "-p:RestoreFallbackFolders=" + str(Path.home() / ".nuget/packages"), "-m:1", "--disable-build-servers"]
    run("folders-debug-restore", ["dotnet", "restore", "Hexalith.Folders.CI.slnx", "-p:Configuration=Debug",
                                 "--configfile", "nuget.config", *debug_props])
    run("folders-debug-build", ["dotnet", "build", "Hexalith.Folders.CI.slnx", "--configuration", "Debug",
                               "--no-restore", "-warnaserror", *debug_props])
    write("result.json", {"status": "passed-local-consumer", "published_version": "2.0.0",
                          "package_cache": "existing cache used as read-only fallback; missing versions restored privately from NuGet.org",
                          "audit": "blocked until reviewed catalog declarations are committed in owning Builds repository",
                          "operational_acceptance": False})


if __name__ == "__main__":
    main()
