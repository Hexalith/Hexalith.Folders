#!/usr/bin/env python3
"""Replay retained bytes only. This module never executes a recorded command."""
import argparse
import hashlib
import json
import struct
from pathlib import Path, PurePosixPath
import tarfile
from collections import Counter

PREFIX = "story-1-17-current-debug-distribution-2026-10-29"
OUTPUT_ROLES = dict(CLI="Hexalith.Builds.Module.Cli", Tooling="Hexalith.Builds.Tooling", AppHost="Hexalith.Builds.Module.AppHost",
                    EventStoreHost="Hexalith.Builds.Module.EventStoreHost", UiHost="Hexalith.Builds.Module.UiHost", Model="Hexalith.Platform.Composition")
RUNNER_FILES = ("Hexalith.Builds.Module.Tests", "Hexalith.Builds.Module.Tests.dll", "Hexalith.Builds.Module.Tests.pdb",
                "Hexalith.Builds.Module.Tests.deps.json", "Hexalith.Builds.Module.Tests.runtimeconfig.json", "Hexalith.Builds.Tooling.dll")


def require(condition, detail):
    if not condition:
        raise ValueError(detail)


def digest(path):
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(chunk)
    return value.hexdigest()


def safe_path(name):
    path = PurePosixPath(name)
    require(bool(name) and path.as_posix() == name and not path.is_absolute() and ".." not in path.parts and "\\" not in name, "Unsafe evidence path")
    return path


def bound(base, item, kind):
    safe_path(item["path"])
    path = base / item["path"]
    require(not path.is_symlink() and path.is_file(), kind + " is missing or linked")
    require(path.stat().st_size == item["bytes"] and digest(path) == item["sha256"], kind + " SHA-256 mismatch")
    return path


def verify_case_identities(tests, discovery):
    """xUnit XML names contain JSON-escaped display text, including theory values."""
    expected = [(row["Class"], row["Method"], row["DisplayName"]) for row in discovery]
    actual = [(test.get("type"), test.get("method"), json.loads('"' + test.get("name", "") + '"')) for test in tests]
    require(len(expected) == len(set(expected)) and len(actual) == len(set(actual)), "Duplicate discovered/executed case identity")
    require(Counter(actual) == Counter(expected), "Discovered full case identities were not all executed")


def managed_pe(payload):
    """Check PE/CLI metadata structure only; this is not signature attestation."""
    try:
        require(payload[:2] == b"MZ", "Selected output is not a managed PE")
        pe = struct.unpack_from("<I", payload, 0x3c)[0]
        require(payload[pe:pe + 4] == b"PE\0\0", "Selected output lacks PE signature")
        count = struct.unpack_from("<H", payload, pe + 6)[0]
        optional_size = struct.unpack_from("<H", payload, pe + 20)[0]
        optional = pe + 24
        magic = struct.unpack_from("<H", payload, optional)[0]
        require(magic in {0x10b, 0x20b}, "Invalid PE optional header")
        directories = optional + (96 if magic == 0x10b else 112)
        require(optional_size >= directories - optional + 15 * 8, "Missing CLI data directory")
        require(struct.unpack_from("<I", payload, directories - 4)[0] >= 15, "Undeclared CLI data directory")
        sections = optional + optional_size
        require(count > 0 and sections + 40 * count <= len(payload), "Invalid PE section table")
        def locate(rva, size):
            for index in range(count):
                virtual_size, address, raw_size, pointer = struct.unpack_from("<IIII", payload, sections + 40 * index + 8)
                if address <= rva and rva + size <= address + min(virtual_size, raw_size):
                    offset = pointer + rva - address
                    require(offset + size <= len(payload), "PE data exceeds file")
                    return offset
            raise ValueError("PE data is outside mapped sections")
        clr_rva, clr_size = struct.unpack_from("<II", payload, directories + 14 * 8)
        require(clr_rva and clr_size >= 72, "Missing managed CLI header")
        clr = locate(clr_rva, clr_size)
        require(struct.unpack_from("<I", payload, clr)[0] >= 72, "Invalid managed CLI header")
        metadata_rva, metadata_size = struct.unpack_from("<II", payload, clr + 8)
        metadata = locate(metadata_rva, metadata_size)
        require(metadata_size >= 20 and payload[metadata:metadata + 4] == b"BSJB", "Missing managed metadata")
        version_size = struct.unpack_from("<I", payload, metadata + 12)[0]
        cursor = (metadata + 16 + version_size + 3) & ~3
        require(version_size > 0 and cursor + 4 <= metadata + metadata_size, "Invalid managed metadata version")
        streams = struct.unpack_from("<H", payload, cursor + 2)[0]
        require(streams > 0, "Managed metadata streams absent")
        cursor += 4
        tables = None
        for _ in range(streams):
            require(cursor + 8 <= metadata + metadata_size, "Truncated managed metadata stream header")
            offset, size = struct.unpack_from("<II", payload, cursor)
            end = payload.index(b"\0", cursor + 8, min(cursor + 40, metadata + metadata_size))
            name = payload[cursor + 8:end]
            require(offset + size <= metadata_size, "Managed metadata stream exceeds root")
            if name in {b"#~", b"#-"}:
                tables = metadata + offset, size
            cursor = (end + 4) & ~3
        require(tables is not None and tables[1] >= 24, "Managed metadata tables absent")
        valid = struct.unpack_from("<Q", payload, tables[0] + 8)[0]
        require(valid & 1 and valid & (1 << 32), "Managed module/assembly tables absent")
        require(tables[1] >= 24 + 4 * valid.bit_count(), "Managed table row counts truncated")
        assembly_index = (valid & ((1 << 32) - 1)).bit_count()
        require(struct.unpack_from("<I", payload, tables[0] + 24)[0] == 1
                and struct.unpack_from("<I", payload, tables[0] + 24 + 4 * assembly_index)[0] == 1, "Managed module/assembly row counts invalid")
    except (IndexError, struct.error) as error:
        raise ValueError("Truncated managed PE") from error


def verify_provenance(result, documents, inventory, workspace):
    def retained(item, detail):
        require(inventory.get(item["path"]) == item, detail)
    commands = result.get("commands")
    require(commands and json.loads(documents["commands.json"]) == commands, "Command provenance absent or inconsistent")
    require(all(row.get("launched") and row.get("exitCode") == 0 and not row.get("timedOut") and row.get("argv") for row in commands), "Required command did not succeed")
    for row in commands:
        retained(row["log"], "Successful command log binding absent")
    labels = {row["label"] for row in commands}
    require(len(labels) == len(commands), "Duplicate required command labels")
    require({"model-compilation-api", "explicit-host-selection", "metadata-only-inspection"} <= labels, "Required qualification command absent")
    reused = result.get("reusedSourceEvidence")
    if reused:
        require(reused.get("sharedSdkControlsUnchanged") is True and reused.get("originalCommands") and reused.get("sourcePreservation"), "Explicit source reuse proof absent")
        require(all(row.get("launched") and row.get("exitCode") == 0 and not row.get("timedOut") for row in reused["originalCommands"]), "Reused command did not succeed")
        require(all(reused.get(name, {}).get("sha256") for name in ("receipt", "archive", "memberManifest")), "Reused receipt bindings absent")
        refresh = result.get("originalGraphRefresh", {})
        require(refresh.get("criticalInputsUnchanged") is True and refresh.get("actualCompilations", 0) > 0
                and {"resume-sdk-version", "resume-production-mapping", "original-debug-graph-refresh"} <= labels, "Reused graph preservation proof absent")
        for name in ("before", "after", "compilerCommands", "changes"):
            retained(refresh[name], "Graph reuse inventory absent")
        before = json.loads(documents[refresh["before"]["path"]])
        after = json.loads(documents[refresh["after"]["path"]])
        require(before and len(before) == len(after) == refresh["boundFiles"], "Graph reuse inventory denominator mismatch")
        old = {row["path"]: row for row in before}
        new = {row["path"]: row for row in after}
        require(len(old) == len(before) and old.keys() == new.keys(), "Graph reuse input set changed")
        compiler = json.loads(documents[refresh["compilerCommands"]["path"]])
        require(compiler.get("exactArgumentEquality") is True and compiler.get("commands") and not compiler.get("intentionalArgumentDifferences"), "Reused compiler vectors not exact")
        require(len(compiler["commands"]) == refresh["actualCompilations"], "Reused compilation denominator mismatch")
        required = {"Hexalith.Builds.Module.Cli", "Hexalith.Builds.Tooling", "Hexalith.Builds.Module.AppHost", "Hexalith.Builds.Module.EventStoreHost", "Hexalith.Builds.Module.UiHost", "Hexalith.Builds.Module.Tests"}
        require(required <= {Path(path).stem for path in compiler["commands"]}, "Reused actual CLI/host/test compilations absent")
        critical = {row["source"]["path"] for row in result.get("originalTestRunnerBindings", [])}
        for record in json.loads(documents["compiler-inputs.json"]):
            critical.update(row["file"]["path"] for row in record["inputs"] if row["file"]["path"] in old)
        critical.update(row["file"]["path"] for row in json.loads(documents["compiler-auxiliary-inputs.json"])["inputs"] if row["file"]["path"] in old)
        critical.update(path for path in old if "/original/bin/Hexalith.Builds.Module.Tests/" in path or "/original/bin/DistributionProbe/" in path)
        require(critical and all(path in old and old[path] == new[path] for path in critical), "Reused test/compiler inputs changed")
    else:
        require({"sdk-version", "sdk-info", "bootstrap-build", "production-mapping", "original-debug-build", "current-api-helper-build"} <= labels, "Original graph command provenance absent")
        require(all("class-" + name.removeprefix("Hexalith.Builds.ModuleTool.Tests.") in labels
                    and "discover-" + name.removeprefix("Hexalith.Builds.ModuleTool.Tests.") in labels for name in result["classDiscoveries"]), "Original test command provenance absent")
    sdk = result.get("sdkSelection", {})
    require(sdk.get("sdkVersion") == "10.0.402" and sdk.get("installedHost") and sdk.get("privateHost") and sdk.get("links"), "SDK selection provenance absent")
    retained(result["sdkBindings"], "SDK binding inventory absent")
    sdk_rows = json.loads(documents[result["sdkBindings"]["path"]])
    require(sdk_rows and sdk["privateHost"] in sdk_rows and any("/sdk/10.0.402/" in row["path"] for row in sdk_rows), "Selected SDK/compiler bindings absent")
    private = dict(sdk["privateHost"], path=str(Path(sdk["privateHost"]["path"]).relative_to(workspace)))
    retained(private, "Selected private SDK host bytes absent")
    require(json.loads(documents["source-baseline.json"]), "Source provenance absent")
    runners = result.get("originalTestRunnerBindings", [])
    require({Path(row["retained"]["path"]).name for row in runners} == set(RUNNER_FILES) and len(runners) == len(RUNNER_FILES), "Complete original runner provenance absent")
    require(all(row["retained"]["path"] == "original-test-runner/" + Path(row["source"]["path"]).name for row in runners), "Noncanonical runner retention path")
    managed_pe(documents["original-test-runner/Hexalith.Builds.Module.Tests.dll"])
    managed_pe(documents["original-test-runner/Hexalith.Builds.Tooling.dll"])
    require(result.get("compilerAuxiliaryCapture", {}).get("status") == "bound" and result.get("compilerAuxiliaryInputs"), "Compiler provenance absent")
    auxiliary = json.loads(documents["compiler-auxiliary-inputs.json"])
    require(auxiliary.get("commands") and auxiliary.get("inputs"), "Compiler command/input provenance absent")
    require(result["compilerAuxiliaryCapture"].get("commands") == len(auxiliary["commands"])
            and result["compilerAuxiliaryCapture"].get("files") == len(auxiliary["inputs"]), "Compiler provenance denominator mismatch")
    require(any(Path(row["project"]).stem == "Hexalith.Platform.Composition" for row in auxiliary["commands"]), "Actual model compiler provenance absent")
    require(all(row.get("arguments") and row.get("inputs") and not any(arg.startswith("@") for arg in row["arguments"]) for row in auxiliary["commands"]), "Incomplete compiler vector provenance")
    require(any(row["kind"] == "compiler" for row in auxiliary["inputs"]), "Compiler executable input absent")
    inputs = json.loads(documents["compiler-inputs.json"])
    require(inputs and all(record.get("inputs") for record in inputs) and {"source", "reference"} <= {row["kind"] for record in inputs for row in record["inputs"]}, "Source/reference compiler provenance absent")
    for record in inputs:
        retained(record["capture"], "Compiler source/reference vector absent")


def verify_outputs(result, documents, inventory):
    outputs = result.get("outputs", {})
    require(set(outputs) == set(OUTPUT_ROLES), "Missing selected output")
    require(all(outputs[role]["path"] == "distribution/" + role + "/" + name + ".dll" for role, name in OUTPUT_ROLES.items()), "Selected output roles need distinct canonical paths")
    seal = json.loads(documents["output-seal.json"])
    sealed = {row["path"]: row for row in seal}
    distribution = {name: row for name, row in inventory.items() if name.startswith("distribution/")}
    require(seal and len(sealed) == len(seal) and sealed == distribution, "Seal/distribution inventory mismatch")
    require(all(PurePosixPath(name).parts[1] in OUTPUT_ROLES for name in distribution), "Unknown sealed distribution role")
    for role, name in OUTPUT_ROLES.items():
        row = outputs[role]
        require(sealed.get(row["path"]) == row, "Selected output binding mismatch")
        managed_pe(documents[row["path"]])
        companions = [".deps.json", ".pdb"] + ([".runtimeconfig.json"] if role in {"CLI", "AppHost", "EventStoreHost", "UiHost"} else [])
        require(all("distribution/" + role + "/" + name + suffix in sealed for suffix in companions), "Selected output companion absent")
    tooling = outputs["Tooling"]
    require(all((row := sealed.get("distribution/" + role + "/Hexalith.Builds.Tooling.dll"))
                and row["sha256"] == tooling["sha256"] and row["bytes"] == tooling["bytes"] for role in ("CLI", "AppHost", "Model")), "Selected Tooling copies disagree")


def verify_current_classes(result, documents):
    totals = dict(total=0, passed=0, failed=0, skipped=0, errors=0, notRun=0)
    for full_name, discovery in result.get("classDiscoveries", {}).items():
        raw = json.loads(documents[discovery["file"]["path"]])
        require(raw == discovery["cases"] and raw, "Current discovery evidence mismatch")
        require(all(row["Class"] == full_name and not row.get("Explicit") and not row.get("SkipReason") for row in raw), "Explicit/skipped current discovery")
        name = full_name.removeprefix("Hexalith.Builds.ModuleTool.Tests.")
        report = next((documents[path] for path in (name + ".xml", "independent-" + name + ".xml") if path in documents), None)
        if report is None:
            # Discovery can precede a process start failure; no execution credit is granted.
            continue
        import xml.etree.ElementTree as ET
        assembly = ET.fromstring(report).find("assembly")
        require(assembly is not None, "Current class report absent")
        tests = assembly.findall(".//test")
        require(len(tests) == len(raw) == int(assembly.get("total", "0")), "Current discovery/execution denominator mismatch")
        require(all(test.get("type") == full_name for test in tests), "Current class report selects another class")
        verify_case_identities(tests, raw)
        values = {key: int(assembly.get("not-run" if key == "notRun" else key, "0")) for key in totals}
        require(values["skipped"] == values["errors"] == values["notRun"] == 0, "Required current assertion skipped, errored or unrun")
        require(values["passed"] + values["failed"] == values["total"], "Current assertion accounting mismatch")
        require(sum(test.get("result") == "Pass" for test in tests) == values["passed"] and sum(test.get("result") == "Fail" for test in tests) == values["failed"], "Current assertion results disagree")
        for key in totals:
            totals[key] += values[key]
    return totals


def replay(receipt_path):
    receipt = json.loads(receipt_path.read_text())
    base = receipt_path.parent
    archive = bound(base, receipt["archive"], "Archive")
    manifest = bound(base, receipt["memberManifest"], "Manifest")
    expected = json.loads(manifest.read_text())
    expected_by_name = {row["path"]: row for row in expected}
    require(len(expected) == len(expected_by_name) == receipt["memberCount"], "Manifest denominator mismatch")
    for name in expected_by_name:
        safe_path(name)
    documents = {}
    with tarfile.open(archive, "r:gz") as capsule:
        actual = set()
        for member in capsule:
            safe_path(member.name)
            require(member.isfile() and member.name not in actual, "Non-regular or duplicate archive member")
            require(member.name in expected_by_name, "Unexpected archive member")
            actual.add(member.name)
            row = expected_by_name[member.name]
            require(member.size == row["bytes"], "Member size mismatch: " + member.name)
            stream = capsule.extractfile(member)
            value = hashlib.sha256()
            keep = member.name.endswith((".json", ".xml")) or member.name.startswith(("logs/discover-", "logs/independent-discover-")) or (member.name.startswith(("distribution/", "original-test-runner/")) and Path(member.name).name.startswith("Hexalith.") and member.name.endswith(".dll"))
            chunks = []
            while chunk := stream.read(1024 * 1024):
                value.update(chunk)
                if keep:
                    chunks.append(chunk)
            require(value.hexdigest() == row["sha256"], "Member SHA-256 mismatch: " + member.name)
            if keep:
                documents[member.name] = b"".join(chunks)
        require(actual == set(expected_by_name), "Missing archive member")
    result = json.loads(documents["result.json"])
    require(result == receipt["result"], "Receipt and retained result disagree")
    require(result["protectedAuthority"] == dict(parentStory="in-progress", tracker="backlog", routingPolicy="V1Only", T0=None, activationAuthority=None, stagingExecuted=0, stagingRequired=14), "Protected authority changed")
    require(all(result[name] == "pending" for name in ("ordinaryStartup", "signedDistribution", "operationalAcceptance")), "Unqualified acceptance claim")
    require(result["qualification"] in {"passed", "incomplete"}, "Unknown qualification outcome")
    current_tests = verify_current_classes(result, documents)
    for row in result.get("originalTestRunnerBindings", []):
        require(expected_by_name.get(row["retained"]["path"]) == row["retained"] and row["source"]["bytes"] == row["retained"]["bytes"] and row["source"]["sha256"] == row["retained"]["sha256"], "Original test-runner bytes missing")
    if result.get("originalTestRunnerBindings"):
        assembly = next((row["source"]["path"] for row in result["originalTestRunnerBindings"] if row["source"]["path"].endswith("/Hexalith.Builds.Module.Tests.dll")), None)
        require(assembly is not None and all(case["Assembly"] == assembly for discovery in result.get("classDiscoveries", {}).values() for case in discovery["cases"]), "Discovery does not name the retained original test assembly")
    if result.get("compilerAuxiliaryInputs"):
        item = result["compilerAuxiliaryInputs"]
        require(expected_by_name.get(item["path"]) == item, "Compiler auxiliary report binding mismatch")
        auxiliary = json.loads(documents[item["path"]])
        source_result = auxiliary["sourceResult"]
        require(expected_by_name.get(source_result["path"]) == source_result, "Compiler auxiliary source-result snapshot mismatch")
        for row in auxiliary["inputs"]:
            require(expected_by_name.get(row["retained"]["path"]) == row["retained"] and row["file"]["bytes"] == row["retained"]["bytes"] and row["file"]["sha256"] == row["retained"]["sha256"], "Compiler auxiliary input bytes missing")
        for row in auxiliary["commands"]:
            require(expected_by_name.get(row["log"]["path"]) == row["log"], "Original compiler diagnostics binding mismatch")
    if result.get("compiledUnqualifiedOutputs"):
        item = result["compiledUnqualifiedOutputs"]
        require(expected_by_name.get(item["path"]) == item, "Unqualified output report binding mismatch")
        unqualified = json.loads(documents[item["path"]])
        require(unqualified["model"] is None, "Unexecuted model output claimed")
        for row in unqualified["files"]:
            require(expected_by_name.get(row["retained"]["path"]) == row["retained"] and row["source"]["bytes"] == row["retained"]["bytes"] and row["source"]["sha256"] == row["retained"]["sha256"], "Unqualified original output bytes missing")
    if result["qualification"] == "incomplete":
        require(result["firstFailure"] or result["preservation"]["status"] != "verified", "Incomplete qualification lacks a failed gate")
        supplementary = result.get("supplementaryFactoryTests")
        if supplementary:
            import xml.etree.ElementTree as ET
            require(supplementary["passed"] == supplementary["total"] == 8 and all(supplementary[key] == 0 for key in ("failed", "errors", "skipped", "notRun")), "Incomplete supplementary factory assertions")
            require("focused-factory.xml" in documents, "Supplementary factory report missing")
            assembly = ET.fromstring(documents["focused-factory.xml"]).find("assembly")
            require(assembly is not None and int(assembly.get("total", "0")) == 8 and int(assembly.get("passed", "0")) == 8, "Supplementary factory report disagrees")
            tests = assembly.findall(".//test")
            require(len(tests) == 8 and all(test.get("result") == "Pass" and test.get("type") == "Hexalith.Builds.ModuleTool.Tests.CompositionCommandOptionsTests" for test in tests), "Supplementary factory test skipped or absent")
            for record in json.loads(documents["compiler-inputs.json"]):
                for row in record["inputs"]:
                    item = row["file"]
                    name = "compiler-bytes/" + item["sha256"] + "/" + Path(item["path"]).name
                    require(name in expected_by_name and expected_by_name[name]["sha256"] == item["sha256"] and expected_by_name[name]["bytes"] == item["bytes"], "Supplementary compiler input bytes missing")
        return dict(qualification="incomplete", verifiedMembers=len(actual), supplementaryFactoryTests=8 if supplementary else 0, currentOriginalAssemblyTests=current_tests, originalAssemblyAcceptance=False, commandExecutionCount=0)
    require(result["firstFailure"] is None and result["preservation"]["status"] == "verified", "Passing evidence contains a failed gate")
    require(all(result.get(name) for name in ("commands", "sdkSelection", "sdkBindings", "originalTestRunnerBindings", "compilerAuxiliaryInputs", "compilerInputs", "classDiscoveries", "testClasses")), "Mandatory command/SDK/compiler/runner provenance absent")
    require(all(name in documents for name in ("commands.json", "source-baseline.json", "sdk-bindings.json", "compiler-inputs.json", "compiler-auxiliary-inputs.json")), "Mandatory provenance inventory absent")
    verify_provenance(result, documents, expected_by_name, receipt["privateWorkspace"])
    repositories = json.loads(documents["repository-baseline.json"])
    require(len(repositories) == 13 and all(row["revision"] == row["indexedGitlink"] for row in repositories[1:]), "Repository/index gitlink identity mismatch")
    synchronization = json.loads(documents["authorized-synchronization.json"]) if "authorized-synchronization.json" in documents else None
    for row in repositories[1:]:
        if row["rootGitlink"] != row["indexedGitlink"]:
            require(synchronization is not None, "Unapproved index-ahead gitlink")
            matches = [item for item in synchronization["repositories"] if item["path"] == row["path"]]
            require(len(matches) == 1 and matches[0]["committedRootGitlink"] == row["rootGitlink"] and matches[0]["indexedRootGitlink"] == row["indexedGitlink"], "Synchronized committed/indexed gitlink mismatch")
    mapping = json.loads(documents["workspace/source-mapping/mapping.json"])
    require(mapping == result["productionMapping"] and len(mapping["Entries"]) == 12 and all(row["Origin"] == "source" for row in mapping["Entries"]), "Production source mapping mismatch")
    verify_outputs(result, documents, expected_by_name)
    selection = result["selection"]
    require(selection["hostBinding"] == result["outputs"]["AppHost"], "Factory host binding mismatch")
    require(selection["AppHostAssemblyPath"] == receipt["privateWorkspace"] + "/" + result["outputs"]["AppHost"]["path"], "Factory selected another host")
    require(selection["DescriptorChildEntryAssemblyPath"] == receipt["privateWorkspace"] + "/" + result["outputs"]["CLI"]["path"], "Factory selected another runner")
    classes = {row["name"]: row for row in result["testClasses"]}
    for name in ("CompositionCommandOptionsTests", "CompositionSdkProcessTests", "Workspace.PlatformModelCatalogTests", "Workspace.WorkspaceMsBuildTests", "Workspace.CatalogVersionTests", "Workspace.WorkspaceAppHostTests"):
        full_name = "Hexalith.Builds.ModuleTool.Tests." + name
        require(full_name in classes, "Required original-assembly class missing")
        row = classes[full_name]
        discovery = result["classDiscoveries"][full_name]
        require(json.loads(documents[discovery["file"]["path"]]) == discovery["cases"], "Discovery evidence disagrees")
        require(row["passed"] == row["total"] == row["discovered"] == len(discovery["cases"]) > 0 and all(row[key] == 0 for key in ("failed", "errors", "skipped", "notRun")), "Incomplete discovered class")
        import xml.etree.ElementTree as ET
        report_name = name + ".xml"
        assembly = ET.fromstring(documents[report_name]).find("assembly")
        require(assembly is not None and int(assembly.get("total", "0")) == row["total"], "Retained test report disagrees")
        tests = assembly.findall(".//test")
        require(len(tests) == row["total"] and all(test.get("result") == "Pass" and test.get("type") == full_name for test in tests), "Required assertion skipped or absent")
        verify_case_identities(tests, discovery["cases"])
    for rows in (result["originalAssets"], result["modelAssets"]):
        require(rows and all(not row["hexalithPackages"] for row in rows), "Hexalith package fallback")
        for row in rows:
            retained = row["retained"]
            require(expected_by_name.get(retained["path"]) == retained, "Restored asset binding mismatch")
            assets = json.loads(documents[retained["path"]])
            require(not any(name.lower().startswith("hexalith.") and item["type"] == "package" for name, item in assets["libraries"].items()), "Retained assets contain Hexalith package fallback")
    child = result["modelChildControls"]
    require(child["sdkExecuted"] and child["incomingArguments"][0] == "build", "Model child was not a compilation")
    require(all(child["effectiveEnvironment"].get(key) == value for key, value in result["productionEnvironment"].items()), "Model child lost production mapping")
    require(child["effectiveEnvironment"].get("DOTNET_GENERATE_ASPNET_CERTIFICATE") == "false" and child["effectiveEnvironment"].get("MSBUILDDISABLENODEREUSE") == "1", "Model SDK safety controls missing")
    require(all(value in child["effectiveArguments"] for value in ("-m:1", "-nr:false", "-p:BuildInParallel=false", "-p:RestoreBuildInParallel=false", "-p:UseSharedCompilation=false")), "Model SDK serial controls missing")
    require(all(child["effectiveEnvironment"][key] == value for key, value in child["addedEnvironment"].items()), "Added controls disagree with actual child")
    metadata_rows = json.loads(documents["pe-metadata.json"])
    metadata = {row["path"]: row for row in metadata_rows}
    inspected = {receipt["privateWorkspace"] + "/" + name for name in expected_by_name
                 if name.startswith("distribution/") and Path(name).name.startswith("Hexalith.") and name.endswith(".dll")}
    require(len(metadata) == len(metadata_rows) and set(metadata) == inspected, "Inspected PE inventory differs from sealed Hexalith outputs")
    references = result["modelReferenceIdentities"]
    model = receipt["privateWorkspace"] + "/" + result["outputs"]["Model"]["path"]
    require(references["model"] == model, "Inspected model is not the selected Model artifact")
    require(metadata[references["model"]]["references"] == references["references"], "Model metadata mismatch")
    expected_matches = [dict(reference=reference, output=str(Path(model).parent / (reference["name"] + ".dll")))
                        for reference in references["references"] if reference["name"].startswith("Hexalith.")]
    require(expected_matches and len({row["reference"]["name"] for row in expected_matches}) == len(expected_matches), "Missing/duplicate Hexalith model references")
    require(references["hexalithMatches"] == expected_matches, "Entire Hexalith model reference set was not matched")
    for row in expected_matches:
        relative = str(Path(row["output"]).relative_to(receipt["privateWorkspace"]))
        require(relative in expected_by_name and row["output"] in metadata and metadata[row["output"]]["identity"] == row["reference"], "Incompatible adjacent model reference identity")
        managed_pe(documents[relative])
    for record in json.loads(documents["compiler-inputs.json"]):
        for row in record["inputs"]:
            item = row["file"]
            name = "compiler-bytes/" + item["sha256"] + "/" + Path(item["path"]).name
            require(name in expected_by_name and expected_by_name[name]["sha256"] == item["sha256"] and expected_by_name[name]["bytes"] == item["bytes"], "Compiler input bytes missing")
    return dict(qualification="passed", verifiedMembers=len(actual), originalAssemblyTests=sum(row["total"] for row in classes.values()), commandExecutionCount=0)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--receipt", type=Path, default=Path(__file__).resolve().parents[2] / "_bmad-output/implementation-artifacts/evidence" / (PREFIX + ".json"))
    args = parser.parse_args()
    try:
        print(json.dumps(replay(args.receipt)))
        return 0
    except (ValueError, KeyError, OSError, tarfile.TarError) as error:
        print("Evidence replay refused: " + str(error))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
