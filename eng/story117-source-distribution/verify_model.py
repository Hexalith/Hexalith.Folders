#!/usr/bin/env python3
"""Replay portable production-model evidence as data only. No command is executed."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import tarfile
import xml.etree.ElementTree as ET

from verify import bound, managed_pe, require, safe_path, verify_case_identities

AUTHORITY = dict(parentStory="in-progress", tracker="backlog", routingPolicy="V1Only", T0=None, activationAuthority=None, stagingExecuted=0, stagingRequired=14)
CONTROLS = {"DOTNET_GENERATE_ASPNET_CERTIFICATE": "false", "MSBUILDDISABLENODEREUSE": "1", "DOTNET_CLI_USE_MSBUILD_SERVER": "0"}
PRIVATE = ("DOTNET_CLI_HOME", "NUGET_PACKAGES", "NUGET_HTTP_CACHE_PATH", "NUGET_PLUGINS_CACHE_PATH", "NUGET_SCRATCH", "TMPDIR", "TEMP", "TMP")
FULL = "Hexalith.Builds.ModuleTool.Tests."
REQUIRED_CLASSES = {FULL + name for name in ("Workspace.PlatformModelCatalogTests", "CompositionSdkProcessTests", "CompositionProcessTests", "McpCliRunBuildTests")}


def dotnet_json(value):
    text = json.dumps(value, ensure_ascii=True, separators=(",", ":")).replace('\\"', '\\u0022')
    text = re.sub(r"[<>&'+`]", lambda match: "\\u" + format(ord(match[0]), "04X"), text)
    return re.sub(r"\\u([a-fA-F0-9]{4})", lambda match: "\\u" + match[1].upper(), text).encode()


def git_tree(raw):
    offset, entries = 0, {}
    while offset < len(raw):
        space, zero = raw.index(b" ", offset), raw.index(b"\0", offset)
        mode, name = raw[offset:space], raw[space+1:zero].decode()
        require(name and name not in entries and zero + 21 <= len(raw), "Malformed Git authority tree")
        entries[name] = (raw[zero+1:zero+21].hex(), "tree" if mode == b"40000" else "commit" if mode == b"160000" else "blob")
        offset = zero + 21
    return entries


def verify_git_path(authority, path, documents, inventory):
    objects = {}
    for item in authority["objects"]:
        require(inventory.get(item["file"]["path"]) == item["file"], "Git object inventory mismatch")
        raw = documents[item["file"]["path"]]
        require(hashlib.sha1(item["kind"].encode() + b" " + str(len(raw)).encode() + b"\0" + raw).hexdigest() == item["oid"], "Git object identity mismatch")
        require(item["oid"] not in objects, "Duplicate authority object")
        objects[item["oid"]] = (item["kind"], raw)
    kind, commit = objects[authority["revision"]]
    require(kind == "commit" and commit.startswith(b"tree "), "Invalid revision authority")
    oid, kind = commit.split(b"\n", 1)[0].decode()[5:], "tree"
    for part in safe_path(path).parts:
        require(kind == "tree" and objects[oid][0] == "tree", "Git authority traverses non-tree")
        oid, kind = git_tree(objects[oid][1])[part]
    require(authority["paths"][path] == dict(kind=kind, oid=oid), "False Git path authority")
    return oid, kind, objects.get(oid, (None, None))[1]


def private_path(value, workspace):
    """Validate recorded private paths lexically, without consulting a replay machine."""
    return (isinstance(value, str) and Path(value).is_absolute() and not any(part in {".", ".."} for part in value.split("/"))
            and Path(value).is_relative_to(workspace))


def verify_child(child, mapping, workspace, sdk, helper_tooling):
    source_root = mapping["Root"]
    project = mapping["PlatformIdentity"]["PlatformPath"] + "/src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj"
    catalog = workspace + "/workspace/source-mapping/pinned-tree/Props/Directory.Packages.props"
    expected = ["dotnet", "build", project, "--configuration", "Debug", "--output", workspace + "/workspace/hosts/platform-model",
                "--artifacts-path", workspace + "/workspace/build/platform-model", "-p:BuildsToolingAssemblyPath=" + helper_tooling,
                "-p:NuGetAudit=false", "-p:MinVerVersionOverride=1.0.0", "-nodeReuse:false", "-p:UseSharedCompilation=false",
                "-m:1", "-p:BuildInParallel=false", "-p:RestoreBuildInParallel=false",
                "-p:AlternateCommonProps=" + workspace + "/workspace/source-mapping/SourceMapping.model-common.props",
                "-p:ImportDirectoryPackagesProps=false", "-p:CentralPackageVersionsFileImported=true", "-p:DirectoryPackagesPropsPath=" + catalog]
    require(child["argv"] == expected, "Production child arguments were changed or qualification controls injected")
    require(child["executable"] == sdk and child["cwd"] == source_root + "/references/Hexalith.Platform", "Substituted executable or wrong working directory")
    environment = child["environment"]
    require(all(environment.get(key) == value for key, value in CONTROLS.items()), "Unsafe production SDK controls")
    require(not any(key in environment for key in ("AlternateCommonProps", "MSBuildSDKsPath", "HEXALITH_TEST_DOTNET_EXECUTABLE", "HEXALITH_TEST_SDK_INTERCEPTION_REQUIRED")), "Injected model environment control")
    require(all(private_path(environment.get(key), workspace) for key in PRIVATE), "Shared or unbound model cache")


def verify_commands(result, documents, inventory, workspace):
    commands = result["commands"]
    require(commands == json.loads(documents["commands.json"]), "Command ledger disagrees")
    labels = []
    for row in commands:
        require(row["launched"] and row["log"] == inventory.get(row["log"]["path"]), "Unbound command log")
        require(row["processes"] == inventory.get(row["processes"]["path"]), "Missing real process observation")
        argv = row["argv"]
        label = row["label"]
        labels.append(label)
        require(argv and Path(argv[0]).is_absolute() and row["timeoutSeconds"] in range(1, 901), "Unbounded/relative command")
        require(row["cwd"].endswith("/references/Hexalith.Builds"), "Wrong command owner")
        require(all(row["environment"].get(key) == value for key, value in CONTROLS.items()), "Unsafe outer SDK controls")
        require(row["environment"].get("HOME") == result["environment"]["HOME"], "HOME changed")
        require(all(private_path(row["environment"].get(key), workspace) for key in PRIVATE), "Outer SDK cache escapes private workspace")
        if label in {"helper-build", "cli-build", "original-test-build"}:
            require(argv[1] == "build" and argv[argv.index("--configuration") + 1] == "Debug" and "-m:1" in argv, "Non-Debug or parallel graph build")
            require(private_path(argv[argv.index("--artifacts-path") + 1], workspace), "Shared graph output")
        elif label == "prepare-build":
            manifest = result.get("productionMapping", {}).get("Root", "") + "/references/Hexalith.Parties/module/hexalith.module-manifest.v2.json"
            if "productionMapping" not in result:
                manifest = result["proposedOrdinaryInvocation"]["manifest"]["path"]
            require(argv[2:] == ["prepare-build", "--manifest", manifest, "--workspace", workspace + "/workspace", "--mode", "source", "--output", "json"], "Preparation did not use the genuine supported Parties command")
        elif label == "production-model":
            require(argv[2:] == ["production-model", workspace + "/workspace/source-mapping/mapping.json", result["productionMapping"]["ContentHash"], workspace + "/workspace"], "Wrong model API invocation")
        elif label == "production-controls":
            require(argv[2] == "production-controls" and len(argv) == 4, "Wrong process preflight")
        elif label == "sdk-version":
            require(argv[1:] == ["--version"], "Wrong SDK observation")
        elif label in {"explicit-host-selection", "metadata-only-inspection"}:
            require(argv[1] == ("select" if label == "explicit-host-selection" else "inspect"), "Unexpected helper operation")
        elif label.startswith(("discover-", "class-")) or label == "mcpcli-guards":
            require("Hexalith.Builds.Module.Tests.dll" in argv[1], "Tests did not use original assembly")
            require("-parallel" in argv and argv[argv.index("-parallel") + 1] == "none", "Unserialized original tests")
            require("-class" in argv or "-method" in argv, "Unfiltered test execution")
        else:
            raise ValueError("Unexpected command: " + label)
        require(not any(argument in {"debug", "run", "publish", "--validate-platform"} for argument in argv[1:]), "Forbidden startup/publication command")
    require(len(labels) == len(set(labels)), "Duplicate command labels")
    if result["qualification"] == "passed":
        require(all(row["exitCode"] == 0 and not row["timedOut"] for row in commands), "Passing command ledger contains failure")
        require({"sdk-version", "helper-build", "cli-build", "prepare-build", "production-model", "original-test-build"} <= set(labels), "Missing current production gate")


def verify_authority(result, documents, inventory):
    mapping = json.loads(documents["workspace/source-mapping/mapping.json"])
    require(mapping == result["productionMapping"] and mapping["Mode"] == "source" and mapping["HasGit"], "False source mapping")
    canonical = {key: mapping[key] for key in ("Mode", "Root", "ActiveModule", "Entries", "HasGit", "PlatformIdentity", "IdentityFindings")}
    require(hashlib.sha256(dotnet_json(canonical)).hexdigest() == mapping["ContentHash"], "Source mapping content hash mismatch")
    repos = json.loads(documents["repository-baseline.json"])
    require(repos == result["initialRepositories"] and len(repos) == 13, "Repository tuple mismatch")
    require(all(re.fullmatch("[a-f0-9]{40}", row["revision"]) for row in repos), "Abbreviated revision")
    require(all(row["revision"] == row["indexedGitlink"] == row["rootGitlink"] for row in repos[1:]), "Root HEAD/index/checkout drift")
    origins = {row["path"]: row["revision"] for row in repos[1:]}
    require(len(mapping["Entries"]) == 12 and {entry["Path"] for entry in mapping["Entries"]} == {mapping["Root"] + "/" + path for path in origins}
            and all(entry["Origin"] == "source" for entry in mapping["Entries"]), "Source fallback or foreign reference")
    identity = mapping["PlatformIdentity"]
    require(identity["CheckoutCommit"] == origins["references/Hexalith.Platform"], "False Platform authority")
    snapshot = json.loads(documents["workspace/source-mapping/catalog-versions.json"])
    require(snapshot["MappingHash"] == mapping["ContentHash"] and snapshot["CatalogCommit"] == identity["CatalogCommit"], "False catalog authority")
    authority = json.loads(documents["git-authority.json"])
    require(authority["platform"]["revision"] == identity["CheckoutCommit"] and authority["catalog"]["revision"] == identity["CatalogCommit"], "Git authority revision drift")
    oid, kind, _ = verify_git_path(authority["platform"], "references/Hexalith.Builds", documents, inventory)
    require(oid == identity["CatalogCommit"] and kind == "commit", "Catalog differs from genuine Platform gitlink")
    for path, digest in snapshot["PinnedFiles"].items():
        safe_path(path)
        name = "workspace/source-mapping/pinned-tree/" + path
        oid, kind, raw = verify_git_path(authority["catalog"], path, documents, inventory)
        require(kind == "blob" and raw.removeprefix(b"\xef\xbb\xbf") == documents[name] and inventory[name]["sha256"] == digest, "Pinned catalog/import differs from committed authority")
    require(snapshot["CatalogFileHash"] == snapshot["PinnedFiles"]["Props/Directory.Packages.props"], "Catalog file hash mismatch")
    return mapping


def verify_complete(result, documents, inventory, workspace):
    mapping = verify_authority(result, documents, inventory)
    require(result["sdkVersion"] == "10.0.403" and documents["logs/sdk-version.log"].strip() == b"10.0.403", "Wrong installed SDK")
    source_rows = json.loads(documents["source-baseline.json"])
    require(source_rows and len({row["path"] for row in source_rows}) == len(source_rows) == result["preservation"]["boundSourceCount"], "Source inventory mismatch")
    require(result["preservation"]["repositoryCount"] == 13, "Repository preservation count mismatch")
    require(result["historicalPreservation"]["files"] == len(json.loads(documents["historical-baseline.json"])), "Historical preservation count mismatch")
    require(len(result["protectedCopies"]) == 4, "Protected authority bytes absent")
    protected = []
    for item in result["protectedCopies"]:
        retained = item["retained"]
        require(inventory.get(retained["path"]) == retained and retained["sha256"] == item["source"]["sha256"] and retained["bytes"] == item["source"]["bytes"], "Protected authority byte drift")
        protected.append(documents[retained["path"]])
    require(re.search(rb"^status: ['\"]?in-progress['\"]?\s*$", protected[0], re.MULTILINE), "Parent status changed")
    require(re.search(rb"^\s+1-17-publish-the-pd10-v2-authorization-contract-spine: backlog\s*$", protected[1], re.MULTILINE), "Tracker status changed")
    for raw in protected[2:]:
        controls = json.loads(raw)["productionControl"]
        require(controls["routeMode"] == "V1Only" and controls["T0"] is None and controls["activationAuthority"] is None, "Activation authority changed")
    checks = json.loads(protected[3])["checks"]
    require(len(checks) == 14 and all(row["status"] == "unexecuted" and row["result"] is None for row in checks), "Staging activity changed")
    require(result["apiInputBindings"], "Unbound API producer")
    for item in result["apiInputBindings"]:
        retained = inventory[item["retainedPath"]]
        require(retained["sha256"] == item["source"]["sha256"] and retained["bytes"] == item["source"]["bytes"], "Executed API/CLI input drift")
    child = result["modelChild"]
    model_observations = json.loads(documents["processes/production-model.json"])
    require(sum(row == child for row in model_observations) == 1, "Production child is not in actual observation")
    helper_tooling = workspace + "/artifacts/original/bin/DistributionProbe/debug/Hexalith.Builds.Tooling.dll"
    verify_child(child, mapping, workspace, result["commands"][0]["argv"][0], helper_tooling)
    require(all(child["environment"].get(key) == value for key, value in result["productionEnvironment"].items()), "Production mapping environment lost")
    require(child["environment"]["HOME"] == result["environment"]["HOME"], "Child HOME changed")
    early = ET.fromstring(documents["workspace/source-mapping/SourceMapping.model-common.props"])
    require(early.tag == "Project" and not early.attrib and len(early) == 2 and all(node.tag == "Import" and set(node.attrib) == {"Project"} for node in early), "Modified production early import")
    require([node.get("Project") for node in early] == [workspace + "/workspace/source-mapping/pinned-tree/Props/Directory.Packages.props", "$(MSBuildExtensionsPath)/$(MSBuildToolsVersion)/Microsoft.Common.props"], "Wrong genuine import order")
    targets = documents["workspace/source-mapping/SourceMapping.targets"]
    require(b"UsingTask" in targets and b"SourceMappingValidationTask" in targets and b"SourceMapping.values.targets" in targets, "Genuine mapping task import lost")
    values = documents["workspace/source-mapping/SourceMapping.values.targets"]
    require(all(name in values for name in (b"HXW006", b"HexalithVerifySourceMappingSelection", b"HexalithValidateSourceMapping", b"HexalithValidateSourceProjectsBeforeRestore")), "Mapping guards lost")
    preparation = json.loads(documents["logs/prepare-build.log"])
    require(preparation == result["preparation"] and preparation["status"] == "prepared" and preparation["buildPreparation"]["environment"] == result["productionEnvironment"], "False preparation authority")
    runner = inventory.get("distribution/Tests/Hexalith.Builds.Module.Tests.dll")
    require(runner is not None and runner["sha256"] == result["originalRunner"]["sha256"] and runner["bytes"] == result["originalRunner"]["bytes"], "Original runner byte identity missing")
    relocation = result["runnerRelocation"]["files"]
    require(relocation and any(row["destination"] == result["originalRunner"] for row in relocation), "Real runner location binding absent")
    require(all(row["source"]["sha256"] == row["destination"]["sha256"] and row["source"]["bytes"] == row["destination"]["bytes"] for row in relocation), "Runner relocation changed bytes")
    reports = result["testClasses"]
    require({row["name"] for row in reports} == REQUIRED_CLASSES and len(reports) == len(REQUIRED_CLASSES), "Required original class missing")
    total = 0
    for row in reports:
        discovery = result["classDiscoveries"][row["name"]]
        require(inventory.get(discovery["file"]["path"]) == discovery["file"] and json.loads(documents[discovery["file"]["path"]]) == discovery["cases"], "Discovery binding mismatch")
        require(row["total"] == row["passed"] == row["discovered"] == len(discovery["cases"]) > 0 and all(row[key] == 0 for key in ("failed", "errors", "skipped", "notRun")), "Incomplete original class")
        require(all(case["Assembly"] == result["originalRunner"]["path"] and not case.get("Explicit") and not case.get("SkipReason") for case in discovery["cases"]), "Wrong discovery identity")
        require(inventory.get(row["report"]["path"]) == row["report"], "Test report binding mismatch")
        assembly = ET.fromstring(documents[row["report"]["path"]]).find("assembly")
        require(assembly is not None and int(assembly.get("total")) == row["total"] and int(assembly.get("passed")) == row["total"], "Original XML denominator mismatch")
        tests = assembly.findall(".//test")
        require(all(test.get("result") == "Pass" for test in tests), "Nonpassing original case")
        verify_case_identities(tests, discovery["cases"])
        total += row["total"]
    sdk_cases = result["classDiscoveries"][FULL + "CompositionSdkProcessTests"]["cases"]
    require(any(case["Method"] == "ProductionBareDotnetChildUsesExplicitControlsAsync" for case in sdk_cases), "Fresh production SDK assertion absent")
    require(not any(case["Method"] == "QualificationCatalogProbeInterceptionIsEstablishedAsync" for case in sdk_cases)
            and result["excludedHistoricalInterception"]["credit"] is False, "Historical interception grants current credit")
    for label in ("modelAssets", "originalAssets"):
        require(result[label], "Restored assets absent")
        for row in result[label]:
            require(inventory.get(row["retained"]["path"]) == row["retained"] and not row["hexalithPackages"], "Unbound/fallback source assets")
            assets = json.loads(documents[row["retained"]["path"]])
            require(not any(name.lower().startswith("hexalith.") and item["type"] == "package" for name, item in assets["libraries"].items()), "Hexalith package fallback")
    commands = json.loads(documents[result["modelCompiler"]["path"]])
    require(inventory.get(result["modelCompiler"]["path"]) == result["modelCompiler"] and len(commands) == 1, "Actual model compiler absent")
    compiler = commands[0]
    require(compiler["process"] in model_observations and Path(compiler["output"]).name == "Hexalith.Platform.Composition.dll", "Wrong model compiler")
    require({"source", "reference", "compiler"} <= {item["kind"] for item in compiler["inputs"]}, "Incomplete source/reference compiler evidence")
    require(all(not argument.startswith("@") for argument in compiler["arguments"]), "Unexpanded compiler response")
    for item in compiler["inputs"]:
        retained = item["retained"]
        require(inventory.get(retained["path"]) == retained and retained["sha256"] == item["file"]["sha256"] and retained["bytes"] == item["file"]["bytes"], "Compiler input bytes missing")
    seal = json.loads(documents["output-seal.json"])
    sealed = {row["path"]: row for row in seal}
    require(len(sealed) == len(seal) and all(inventory.get(path) == row for path, row in sealed.items()), "Output seal drift")
    require(set(result["outputs"]) == {"CLI", "Tooling", "AppHost", "EventStoreHost", "UiHost", "Model"}, "Missing or unexpected production output role")
    for role, item in result["outputs"].items():
        require(sealed.get(item["path"]) == item, "Selected output drift")
        managed_pe(documents[item["path"]])
    metadata = {row["path"]: row for row in json.loads(documents["pe-metadata.json"])}
    references = result["modelReferenceIdentities"]
    require(references["model"] == workspace + "/" + result["outputs"]["Model"]["path"], "Wrong PE model")
    require(metadata[references["model"]]["references"] == references["references"], "PE reference evidence mismatch")
    expected = [dict(reference=item, output=str(Path(references["model"]).parent / (item["name"] + ".dll"))) for item in references["references"] if item["name"].startswith("Hexalith.")]
    require(expected and references["hexalithMatches"] == expected, "Partial Hexalith PE reference credit")
    for item in expected:
        relative = str(Path(item["output"]).relative_to(workspace))
        require(relative in sealed and metadata[item["output"]]["identity"] == item["reference"], "Incompatible source dependency identity")
    return total


def verify_correction(result, documents, inventory):
    """Only a same-tuple runner-location correction may reuse production observations."""
    correction = result.get("currentCorrection")
    if not correction:
        return
    require(correction["kind"] == "runner-location-only" and correction["productionCommandsReexecuted"] == 0, "Unknown continuation scope")
    prior = json.loads(documents["prior-phase/receipt.json"])
    require(prior["result"] == json.loads(documents["prior-phase/result.json"]), "Prior result changed")
    prior_members_raw = documents["prior-phase/members.json"]
    require(hashlib.sha256(prior_members_raw).hexdigest() == prior["memberManifest"]["sha256"] and len(prior_members_raw) == prior["memberManifest"]["bytes"], "Prior member manifest changed")
    for item in json.loads(prior_members_raw):
        alias = "prior-phase/" + item["path"] if item["path"] in {"result.json", "commands.json"} else item["path"]
        require(alias in inventory and inventory[alias]["sha256"] == item["sha256"] and inventory[alias]["bytes"] == item["bytes"], "Preserved prior-phase bytes changed: " + alias)
    old = {row["path"]: row for row in json.loads(documents["source-baseline.json"])}
    current = {row["path"]: row for row in json.loads(documents[correction["sourceBaseline"]["path"]])}
    require(set(old) == set(current), "Source inventory changed during location correction")
    changes = [dict(before=old[name], after=current[name]) for name in sorted(old) if old[name] != current[name]]
    require(changes == correction["sourceChanges"] and all(item["before"]["path"] in {"eng/story117-source-distribution/qualify_model.py", "eng/story117-source-distribution/verify_model.py"} for item in changes), "Production/test source changed during location correction")
    for key in ("productionMapping", "productionEnvironment", "modelChild", "modelAssembly", "modelCompiler", "modelAssets", "apiInputBindings", "initialRepositories"):
        require(result[key] == prior["result"][key], "Reused production observation changed: " + key)
    prior_commands = prior["result"]["commands"]
    require(result["commands"][:len(prior_commands)] == prior_commands and all(row["exitCode"] == 0 and not row["timedOut"] for row in prior_commands), "Reused command ledger changed or contains failure")
    require({"production-model", "original-test-build"} <= {row["label"] for row in prior_commands}, "Required completed production gates absent")


def replay(receipt_path):
    receipt_path = Path(receipt_path)
    receipt = json.loads(receipt_path.read_text())
    base = receipt_path.parent
    archive = bound(base, receipt["archive"], "Archive")
    members = json.loads(bound(base, receipt["memberManifest"], "Manifest").read_text())
    inventory = {row["path"]: row for row in members}
    require(len(inventory) == len(members) == receipt["memberCount"], "Manifest count mismatch")
    documents, seen = {}, set()
    with tarfile.open(archive, "r:gz") as capsule:
        for member in capsule:
            safe_path(member.name)
            require(member.isfile() and member.name not in seen and member.name in inventory, "Unexpected/duplicate/linked member")
            seen.add(member.name)
            item = inventory[member.name]
            require(member.size == item["bytes"], "Member size mismatch")
            value = hashlib.sha256()
            keep = (member.name.endswith((".json", ".xml", ".props", ".targets")) or member.name.startswith("protected/") or member.name.startswith("git-objects/")
                    or (Path(member.name).name.startswith("discover-") and member.name.endswith(".log")) or member.name in {"logs/prepare-build.log", "logs/sdk-version.log"}
                    or (member.name.startswith("distribution/") and member.name.endswith(".dll") and Path(member.name).name.startswith("Hexalith.")))
            chunks = []
            with capsule.extractfile(member) as stream:
                while chunk := stream.read(1024 * 1024):
                    value.update(chunk)
                    if keep:
                        chunks.append(chunk)
            require(value.hexdigest() == item["sha256"], "Member SHA-256 mismatch: " + member.name)
            if keep:
                documents[member.name] = b"".join(chunks)
    require(seen == set(inventory), "Missing archive member")
    result = json.loads(documents["result.json"])
    require(result == receipt["result"], "Receipt/result mismatch")
    require(result["protectedAuthority"] == AUTHORITY, "Protected authority changed")
    require(all(result[key] == "pending" for key in ("ordinaryStartup", "signedDistribution", "operationalAcceptance")), "False startup/release/operational credit")
    require(result["qualification"] in {"passed", "incomplete"}, "Unknown outcome")
    require(result["historicalEvidence"]["status"] == "not reused", "Historical evidence used as current authority")
    if result.get("commands"):
        verify_commands(result, documents, inventory, receipt["privateWorkspace"])
    verify_correction(result, documents, inventory)
    total = 0
    if result["qualification"] == "passed":
        require(result["firstFailure"] is None and result["preservation"]["status"] == result["historicalPreservation"]["status"] == "verified", "Passing preservation claim is false")
        total = verify_complete(result, documents, inventory, receipt["privateWorkspace"])
    else:
        require(result.get("firstFailure") is not None or result["preservation"]["status"] != "verified", "Incomplete evidence has no failed gate")
        if result.get("independentDiagnosticGates") == "passed":
            require(result.get("currentCorrection", {}).get("kind") == "runner-location-only", "Unbound independent diagnostic claim")
            total = verify_complete(result, documents, inventory, receipt["privateWorkspace"])
    return dict(qualification=result["qualification"], verifiedMembers=len(seen), originalAssemblyTests=total, commandExecutionCount=0,
                startupCredit=False, runtimeCredit=False, acceptedToolCredit=False, signedReleaseCredit=False, operationalCredit=False)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--receipt", required=True, type=Path)
    args = parser.parse_args()
    try:
        print(json.dumps(replay(args.receipt)))
        return 0
    except (ValueError, KeyError, OSError, TypeError, IndexError, tarfile.TarError, ET.ParseError) as error:
        print("Production model replay refused: " + str(error))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
