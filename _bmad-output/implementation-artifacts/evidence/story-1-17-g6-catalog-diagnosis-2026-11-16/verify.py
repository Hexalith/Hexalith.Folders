"""Verify the dated CI diagnosis and preservation without network access."""

import argparse
import gzip
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile


def require(condition, message):
    if not condition:
        raise SystemExit(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packet-only", action="store_true", help="Check dated packet evidence without current-workspace preservation checks")
    options = parser.parse_args()
    packet = Path(__file__).resolve().parent
    root = packet.parents[3]
    for line in (packet / "SHA256SUMS").read_text().splitlines():
        expected, name = line.split("  ", 1)
        require(digest((packet / name).read_bytes()) == expected, "Packet hash changed: " + name)

    def observation(name):
        return json.loads(gzip.decompress((packet / "observations" / (name + ".json.gz")).read_bytes()))

    projects = observation("projects_run")
    folders = observation("folders_run")
    require((projects["id"], projects["head_sha"], projects["run_attempt"], projects["status"], projects["conclusion"]) ==
            (43881420098, "981ceef13a1abc88219925e73c51a23eb9ac50c2", 1, "completed", "failure"), "Projects subject changed")
    require((folders["id"], folders["head_sha"], folders["run_attempt"], folders["status"], folders["conclusion"]) ==
            (43881492421, "dafa51b0866cc19c18d4448300b7931dad9158f0", 1, "completed", "success"), "Folders subject changed")
    for name, run in (("projects_jobs", projects), ("folders_jobs", folders)):
        data = observation(name)
        require(len(data["jobs"]) == data["total_count"], "Incomplete job observation")
        require(all((job["run_id"], job["head_sha"], job["run_attempt"]) ==
                    (run["id"], run["head_sha"], 1) for job in data["jobs"]), "Job subject mismatch")
    expected_jobs = {
        "projects_jobs": {"Validate workflow policy": "success", "G-6 current runtime qualification": "failure",
            "P1R candidate evidence replay": "success", "ci / build-and-test": "success",
            "Projects generated-artifact gates": "success", "ci / performance-tests": "skipped",
            "ci / aspire-tests": "skipped", "Scheduled managed AppHost E2E": "skipped"},
        "folders_jobs": {"e2e-gates": "success", "accessibility-gates": "success",
            "folders-specialized-gates": "success", "ci / build-and-test": "success",
            "ci / performance-tests": "skipped", "ci / aspire-tests": "skipped"},
    }
    for name, expected in expected_jobs.items():
        require({job["name"]: job["conclusion"] for job in observation(name)["jobs"]} == expected,
                "Reported job outcomes differ: " + name)
    jobs = observation("projects_jobs")["jobs"]
    require(any(job["name"] == "ci / build-and-test" and job["conclusion"] == "success" for job in jobs), "Product job did not pass")
    g6 = next(job for job in jobs if job["id"] == 133360595951)
    require(g6["conclusion"] == "failure" and any(step["name"] == "Restore effective G-6 package graphs" and
            step["conclusion"] == "failure" for step in g6["steps"]), "G-6 failed step differs")
    require(any("Audit and qualify" in step["name"] and step["conclusion"] == "skipped" for step in g6["steps"]), "Runtime step was not skipped")

    artifacts = observation("projects_artifacts")
    require(len(artifacts["artifacts"]) == artifacts["total_count"], "Incomplete artifact-list observation")
    artifact = next(item for item in artifacts["artifacts"] if item["id"] == 14234531847)
    require((artifact["workflow_run"]["id"], artifact["workflow_run"]["head_sha"]) ==
            (projects["id"], projects["head_sha"]), "Archive subject differs")
    archive = packet / "g6-source-mapping.zip"
    require("sha256:" + digest(archive.read_bytes()) == artifact["digest"] ==
            "sha256:24d169b2973bd1d16625ca031b96ae0bcfdfd231056758e55dfc89b45ea8a678", "Archive digest mismatch")
    with zipfile.ZipFile(archive) as contents:
        failure = json.loads(contents.read("source-mapping/conversations.json"))
        require(failure["status"] == "failed" and failure["diagnostics"][0]["ruleId"] == "HXW009" and
                "e2bbbd0893f586e4d447ef8358ff747ea3d34435" in failure["diagnostics"][0]["message"], "Catalog diagnostic differs")
        require(failure["outcome"]["phase"] == "Prerequisite" and
                failure["outcome"]["category"] == "PrerequisiteUnavailable" and
                failure["diagnostics"][0]["phase"] == "Prerequisite" and
                failure["diagnostics"][0]["category"] == "PrerequisiteUnavailable", "Diagnostic classification differs")
        build = contents.read("source-mapping/build.stdout.log").decode()
        require(all(text in build for text in ("Build succeeded.", "0 Warning(s)", "0 Error(s)")), "Mapper build did not succeed cleanly")
        require(contents.read("source-mapping/build.stderr.log") == b"" and
                contents.read("source-mapping/prepare.stderr.log") == b"", "Archive stderr was not empty")
    probe = json.loads((packet / "probe-result.json").read_text())
    bindings = json.loads((packet / "source-bindings.json").read_text())
    require(probe["source_builds"] == bindings["mapper"]["revision"] and
            probe["catalog_revision"] == bindings["selected_catalog"]["builds_revision"] and
            probe["head_after_acquisition"] == probe["source_builds"] and
            probe["catalog_object_before_fetch"] == probe["catalog_revision"] + " missing" and
            probe["shallow_read_exit"] == 128 and probe["exact_fetch_read_exit"] == 0 and
            probe["catalog_sha256"] == "d098dab50977041a536508f6df7e934f95678a4a0abedd80b28e9bae19ee0637",
            "Retained probe subjects or outcomes differ")
    initializer_log = (packet / "initializer-job-log.txt").read_text()
    require("Run Hexalith/Hexalith.Builds/Github/initialize-build@" + bindings["initializer"]["revision"] in initializer_log and
            "Submodule path 'references/Hexalith.Builds': checked out '" + bindings["mapper"]["revision"] + "'" in initializer_log,
            "Observed initializer/checkout identity differs")
    if options.packet_only:
        print("PASS: dated packet hashes and original CI/archive/probe subjects; no current-workspace/source or operational acceptance")
        return

    def git(repository, *arguments):
        return subprocess.check_output(["git", "-C", str(root / repository), *arguments])

    before = json.loads((packet / "before.json").read_text())
    for repository, record in before["repositories"].items():
        require(git(repository, "rev-parse", "HEAD").decode().strip() == record["head"], "Repository revision changed: " + repository)
        index = Path(git(repository, "rev-parse", "--git-path", "index").decode().strip())
        if not index.is_absolute():
            index = root / repository / index
        require(digest(index.read_bytes()) == record["index_sha256"], "Git index changed: " + repository)
    tracked = json.loads((packet / "tracked-status.json").read_text())
    require(set(tracked["repositories"]) == set(before["repositories"]), "Tracked-status repository set differs")
    for repository, status in tracked["repositories"].items():
        require(status == "" and git(repository, "--no-optional-locks", "status", "--porcelain=v1", "--untracked-files=no").decode() == status,
                "Tracked working tree changed: " + repository)
    for name, expected in before["protected"].items():
        require(digest((root / name).read_bytes()) == expected, "Protected bytes changed: " + name)
    for key in ("initializer", "mapper", "caller"):
        source = bindings[key]
        require(digest(git(source["repository"], "show", source["revision"] + ":" + source["path"])) == source["sha256"], "Bound source differs: " + key)
        require(git(source["repository"], "rev-parse", source["revision"] + ":" + source["path"]).decode().strip() == source["git_blob"], "Bound Git blob differs: " + key)
    require(bindings["caller"]["revision"] == projects["head_sha"], "Caller differs from observed Projects source")
    for name, expected in (("Hexalith.Builds", bindings["mapper"]["revision"]),
                           ("Hexalith.Platform", bindings["selected_catalog"]["platform_source"])):
        require(git("references/Hexalith.Projects", "rev-parse", projects["head_sha"] + ":references/" + name).decode().strip() == expected,
                "Projects selected gitlink differs: " + name)
    require(git("references/Hexalith.Platform", "rev-parse", bindings["selected_catalog"]["platform_source"] +
                ":references/Hexalith.Builds").decode().strip() == bindings["selected_catalog"]["builds_revision"], "Platform catalog pin differs")
    require(digest(git("references/Hexalith.Builds", "show", probe["catalog_revision"] + ":Props/Directory.Packages.props")) == probe["catalog_sha256"], "Probe catalog bytes differ from bound source")
    print("PASS: packet hashes, original CI/archive/probe subjects, selected source chain, protected files and 13 repository revisions/indices/clean tracked worktrees; no runtime acceptance")


if __name__ == "__main__":
    main()
