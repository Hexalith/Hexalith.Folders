"""Replay the retained source-host evidence without executing its commands."""

import hashlib
import json
from pathlib import Path
import sys
import tarfile
import xml.etree.ElementTree as ET


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def verify_capsule(base, metadata):
    archive = base / metadata["archive"]
    manifest = base / metadata["manifest"]
    require(archive.stat().st_size == metadata["archiveBytes"], "Archive size mismatch")
    require(manifest.stat().st_size == metadata["manifestBytes"], "Manifest size mismatch")
    require(digest(archive) == metadata["archiveSha256"], "Archive SHA-256 mismatch")
    require(digest(manifest) == metadata["manifestSha256"], "Manifest SHA-256 mismatch")
    entries = json.loads(manifest.read_text())["members"]
    names = [entry["member"] for entry in entries]
    require(len(names) == len(set(names)), "Duplicate manifest member")
    contents = {}
    with tarfile.open(archive, "r:gz") as retained:
        actual = retained.getmembers()
        require(all(member.isfile() for member in actual), "Unexpected non-file member")
        require(len(actual) == len(names), "Archive member count mismatch")
        require(set(member.name for member in actual) == set(names), "Archive member inventory mismatch")
        for entry in entries:
            stream = retained.extractfile(entry["member"])
            require(stream is not None, "Missing retained member")
            payload = stream.read()
            require(len(payload) == entry["bytes"], "Member size mismatch: " + entry["member"])
            require(hashlib.sha256(payload).hexdigest() == entry["sha256"], "Member SHA-256 mismatch: " + entry["member"])
            contents[entry["member"]] = payload
    return contents


def main():
    default = "story-1-17-supported-source-host-qualification-resume-2026-10-28.review-final.json"
    receipt = Path(sys.argv[1]) if len(sys.argv) == 2 else Path(__file__).with_name(default)
    require(len(sys.argv) <= 2, "Usage: verifier.py [receipt.json]")
    wrapper = json.loads(receipt.read_text())
    contents = verify_capsule(receipt.parent, wrapper["portableEvidence"])
    require(len(contents) == wrapper["portableEvidence"]["memberCount"], "Final member denominator mismatch")
    previous = verify_capsule(receipt.parent, wrapper["preReviewEvidence"])
    data = json.loads(contents[wrapper["portableEvidence"]["receiptDataMember"]])
    inert = {"parentStory": "in-progress", "tracker": "backlog", "routingPolicy": "V1Only", "T0": None, "activationAuthority": None, "stagingExecuted": 0, "stagingRequired": 14}
    require(wrapper["protectedAuthority"] == inert and data["protectedAuthority"] == inert, "Protected authority mismatch")
    require(wrapper["storyCompletionClaim"] is False and data["storyCompletionClaim"] is False, "Unexpected parent completion claim")
    require(data["status"] == "reviewed local source-host qualification passed", "Unexpected qualification status")
    require(data["sliceStatus"] == "done" and data["precedingMappingSliceStatus"] == "done", "Incomplete local slices")
    require(not data["finalRepositories"]["problems"], "Repository revision/index prerequisite failed")
    require(not data["trackedPreservation"]["unexpectedChanged"] and not data["trackedPreservation"]["missing"], "Unexpected tracked-file changes")
    require(len(data["commands"]) == 9 and all(command["exitCode"] == 0 and not command["timedOut"] for command in data["commands"]), "Incomplete final command sequence")
    require(len(data["productionMapping"]["mapping"]["Entries"]) == 12, "Mapping denominator mismatch")
    require(all(entry["Origin"] == "source" for entry in data["productionMapping"]["mapping"]["Entries"]), "Non-source mapping origin")
    expected = {"CompositionSdkProcessTests": 17, "Workspace.WorkspaceMsBuildTests": 139, "Workspace.CatalogVersionTests": 34, "Workspace.WorkspaceAppHostTests": 12}
    require(len(data["tests"]) == 4, "Required class inventory mismatch")
    total = 0
    for report in data["tests"]:
        suffix = report["class"].removeprefix("Hexalith.Builds.ModuleTool.Tests.")
        require(suffix in expected, "Unexpected test class")
        required = expected.pop(suffix)
        assembly = ET.fromstring(contents[report["xml"]["member"]]).find("assembly")
        require(assembly is not None, "Missing test assembly report")
        cases = assembly.findall(".//test")
        require(int(assembly.get("total", "0")) == required and int(assembly.get("passed", "0")) == required and len(cases) == required, "Test denominator mismatch")
        require(all(int(assembly.get(name, "0")) == 0 for name in ["errors", "failed", "skipped", "not-run"]), "Failed/skipped/unrun required assertion")
        require(all(case.get("result") == "Pass" and case.get("type") == report["class"] for case in cases), "Unexpected test result/class")
        if suffix == "Workspace.WorkspaceAppHostTests":
            require(sum(case.get("method") == "PlatformPlanDelegatesToThePinnedModelAsync" for case in cases) == 1, "Required Platform assertion missing")
        total += required
    require(not expected and total == 202 and data["totalPassed"] == 202 and wrapper["totalPassed"] == 202, "Final test total mismatch")
    require(len(data["sourceGraph"]["assets"]) == 29 and data["sourceGraph"]["hexalithPackageLibraries"] == [], "Source graph denominator mismatch")
    for binding in data["sourceGraph"]["assets"]:
        assets = json.loads(contents[binding["member"]])
        require(not any(name.lower().startswith("hexalith.") and library.get("type") == "package" for name, library in assets.get("libraries", {}).items()), "Hexalith package fallback in restored graph")
    require(data["review"]["findingCount"] == 15 and data["review"]["correctedMediumFindings"] == 14 and data["review"]["falseFindings"] == 1 and data["review"]["deferred"] == [], "Review disposition mismatch")
    require(all(result["exitCode"] == 0 for result in data["whitespaceChecks"]), "Whitespace verification failed")
    print(json.dumps({"status": "PASS", "finalMembers": len(contents), "preReviewMembers": len(previous), "passed": total, "failed": 0, "skipped": 0, "notRun": 0, "parentStoryComplete": False}, sort_keys=True))


if __name__ == "__main__":
    main()
