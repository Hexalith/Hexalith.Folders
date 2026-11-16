"""Replay the dated post-landing packet and its unchanged workspace inputs."""

import argparse
import gzip
import hashlib
import json
from pathlib import Path
import subprocess


PACKET = Path(__file__).resolve().parent
ROOT = PACKET.parents[3]
NAMES = {
    "projects_runs", "projects_jobs", "projects_artifacts", "folders_runs",
    "platform_tuple_run", "platform_tuple_jobs",
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def read(path):
    return json.loads(path.read_text())


def git(repo, *args):
    return subprocess.check_output(["git", "-C", str(repo), *args])


def verify(packet_only):
    manifest = {}
    for line in (PACKET / "SHA256SUMS").read_text().splitlines():
        digest, name = line.split("  ", 1)
        require(name not in manifest and not Path(name).is_absolute()
                and ".." not in Path(name).parts, "Invalid packet member")
        manifest[name] = digest
        require(sha((PACKET / name).read_bytes()) == digest, "Packet hash differs: " + name)
    actual = {str(p.relative_to(PACKET)) for p in PACKET.rglob("*")
              if p.is_file() and p.name != "SHA256SUMS"}
    require(set(manifest) == actual, "Packet inventory differs")

    before = read(PACKET / "before.json")
    source = read(PACKET / "source-bindings.json")
    handoff = read(PACKET / "handoff.json")
    previous = read(PACKET / "prior-handoff.json")
    require(sha((PACKET / "prior-handoff.json").read_bytes())
            == handoff["previous_handoff_sha256"] == before["protected"][handoff["previous_handoff"]],
            "Previous handoff snapshot differs")
    for field in ["controls", "tracking", "completed_do_not_repeat", "mutating_v2_admission"]:
        require(handoff[field] == previous[field], "Inherited control differs: " + field)
    controls = handoff["controls"]
    require(controls["route_mode"] == "V1Only" and controls["migration_t0"] is None
            and controls["activation_authority"] is None and controls["staging_checks_executed"] == 0
            and controls["staging_checks_total"] == 14 and controls["parent_status"] == "in-progress"
            and controls["tracker"] == "backlog", "Historical control invariants differ")
    pair = handoff["released_pair_reference"]
    require(pair["handoff"] == handoff["previous_handoff"]
            and pair["sha256"] == handoff["previous_handoff_sha256"]
            and pair["version"] == previous["released_pair"]["version"]
            and pair["source"] == previous["released_pair"]["source"], "Released pair projection differs")
    historical = handoff["historical_local_qualification"]
    summary = read(PACKET / "historical-test-summary.json")
    summary_path = previous["projects_package_alignment"]["test_summary"]
    require(sha((PACKET / "historical-test-summary.json").read_bytes())
            == before["protected"][summary_path], "Historical count snapshot differs")
    require(historical["source"] == previous["projects_package_alignment"]["source_head"]
            and historical["receipt"] == str(Path(summary_path).with_name("receipt.json"))
            and historical["result"] == {
                "passed": summary["passed"], "failed": summary["failed"], "skipped": summary["skipped"],
                "focused_fixtures_passed": summary["focused_fixtures"]["passed"],
                "isolated_package_api_consumers_passed": summary["isolated_package_api_consumers"]["passed"]},
            "Historical qualification projection differs")
    require(summary["existing_skip"]["reason"] == "Requires the isolated Story 6.1 Dapr persistence lane."
            and historical["skip"] == "Existing isolated Story 6.1 Dapr persistence lane; not an executed hosted/runtime acceptance.",
            "Historical skip qualification differs")
    queries = read(PACKET / "queries.json")
    require(len(queries) == len(NAMES) and {q["name"] for q in queries} == NAMES,
            "Query inventory differs")
    observed = {}
    for query in queries:
        archive = (PACKET / query["archive"]).read_bytes()
        data = gzip.decompress(archive)
        require(sha(archive) == query["sha256"] and sha(data) == query["uncompressed_sha256"],
                "Query hashes differ")
        wrapper = json.loads(data)
        require(wrapper["name"] == query["name"] and wrapper["url"] == query["url"]
                and wrapper["observed_at_utc"] == query["observed_at_utc"], "Query provenance differs")
        observed[query["name"]] = wrapper

    project_runs = observed["projects_runs"]["body"]["workflow_runs"]
    ci = [r for r in project_runs if r["name"] == "CI"]
    require(len(ci) == 1 and ci[0]["head_sha"] == source["projects_head"], "CI subject differs")
    ci = ci[0]
    require(handoff["projects"]["ci"] == {k: ci[k] for k in handoff["projects"]["ci"]},
            "CI result projection differs")
    for name, field in [("projects_jobs", "jobs_observation"),
                        ("projects_artifacts", "artifacts_observation")]:
        require(handoff["projects"][field] == {
            "observed_at_utc": observed[name]["observed_at_utc"],
            "total_count": observed[name]["body"]["total_count"]}, "CI collection projection differs")
    require(ci["status"] == "pending" and ci["conclusion"] is None
            and not handoff["projects"]["current_product_acceptance"], "Pending CI acquired acceptance")
    require(ci["referenced_workflows"][0]["sha"] == "6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b",
            "Reusable CI execution revision differs")
    require(handoff["projects"]["ci_observed_at_utc"] == observed["projects_runs"]["observed_at_utc"],
            "Projects CI observation time differs")
    folders = observed["folders_runs"]
    projection_keys = ("id", "name", "head_sha", "status", "conclusion", "html_url", "run_attempt", "referenced_workflows")
    require(handoff["folders_ci_observation"] == {
        "observed_at_utc": folders["observed_at_utc"],
        "runs": [{k: r[k] for k in projection_keys} for r in folders["body"]["workflow_runs"]]},
        "Folders CI projection differs")

    hosted = observed["platform_tuple_run"]["body"]
    expected_jobs = {"resolve-sources", "tuple", "tuple-verdict"}
    jobs = observed["platform_tuple_jobs"]["body"]["jobs"]
    require(hosted["head_sha"] == source["platform_hosted_run_subject"]
            and hosted["status"] == "completed" and hosted["conclusion"] == "success", "Hosted tuple subject/result differs")
    require(len(jobs) == 3 and {j["name"] for j in jobs} == expected_jobs
            and all(j["status"] == "completed" and j["conclusion"] == "success" for j in jobs),
            "Hosted tuple jobs differ")
    require(handoff["platform"]["historical_hosted_tuple"]["run"] == {
        k: hosted[k] for k in handoff["platform"]["historical_hosted_tuple"]["run"]}, "Hosted result projection differs")
    require(all(j["run_id"] == hosted["id"] and j["head_sha"] == hosted["head_sha"]
                and j["run_attempt"] == hosted["run_attempt"] for j in jobs), "Hosted job subject differs")
    require(handoff["platform"]["historical_hosted_tuple"]["jobs"] == [
        {k: j[k] for k in ("id", "name", "conclusion")} for j in jobs], "Hosted job projection differs")
    require(handoff["platform"]["historical_hosted_tuple"]["observed_at_utc"]
            == observed["platform_tuple_run"]["observed_at_utc"]
            and handoff["platform"]["historical_hosted_tuple"]["jobs_observed_at_utc"]
            == observed["platform_tuple_jobs"]["observed_at_utc"], "Hosted observation times differ")
    require(handoff["story_closed"] is False and handoff["platform"]["candidate_acceptance"] is False
            and handoff["hosted_projects_artifact"] is None and handoff["hosted_folders_artifact"] is None,
            "Reconciliation acquired operational authority")
    if packet_only:
        print("PASS: sealed dated observations; no current workspace or operational acceptance.")
        return

    for name, expected in before["protected"].items():
        require(sha((ROOT / name).read_bytes()) == expected, "Protected input differs: " + name)
    require(sha((ROOT / handoff["previous_handoff"]).read_bytes()) == handoff["previous_handoff_sha256"],
            "Previous handoff differs")
    for field in ["controls", "tracking", "completed_do_not_repeat", "mutating_v2_admission"]:
        require(handoff[field] == previous[field], "Inherited control differs: " + field)
    require({r["id"] for r in handoff["remaining"]} == {r["id"] for r in previous["remaining"]}
            and len(handoff["remaining"]) == len(previous["remaining"]), "Remaining gate inventory differs")
    for old in previous["remaining"]:
        if old["id"] not in {"projects-artifact", "platform-mapping-tool-intake"}:
            require(old == next(r for r in handoff["remaining"] if r["id"] == old["id"]),
                    "Unchanged operational gate differs")
    for path, state in before["repositories"].items():
        repo = ROOT / path
        require(git(repo, "rev-parse", "HEAD").decode().strip() == state["head"], "Repository HEAD differs: " + path)
        require(sha(git(repo, "ls-files", "--stage", "-z")) == state["index_sha256"], "Repository index differs: " + path)
        require(not git(repo, "status", "--porcelain", "--untracked-files=no").strip(), "Tracked source differs: " + path)
    require(handoff["source_revisions"] == {k: v["head"] for k, v in before["repositories"].items()},
            "Handoff source tuple differs")
    require(handoff["projects"]["source"] == source["projects_head"]
            == before["repositories"]["references/Hexalith.Projects"]["head"]
            and handoff["projects"]["repair_commit"] == source["repair_commit"]
            and handoff["projects"]["repair_landed"] is True and source["repair_is_ancestor"] is True
            and handoff["platform"]["source"] == source["platform_head"]
            == before["repositories"]["references/Hexalith.Platform"]["head"], "Duplicated source identity differs")

    projects = ROOT / "references/Hexalith.Projects"
    subprocess.run(["git", "-C", str(projects), "merge-base", "--is-ancestor",
                    source["repair_commit"], source["projects_head"]], check=True)
    comment = b"    <!-- Consume the .NET 11 SDK snapshot limits and tenant access recovery contracts. -->\r\n"
    for path, binding in source["repair_files"].items():
        data = (projects / path).read_bytes()
        blob = git(projects, "show", source["projects_head"] + ":" + path)
        landed = git(projects, "show", source["repair_commit"] + ":" + path)
        require(sha(data) == binding["worktree_sha256"] and sha(blob) == binding["committed_blob_sha256"]
                and sha(landed) == binding["repair_commit_blob_sha256"] and blob == landed,
                "Committed repair bytes differ: " + path)
        if path == "Directory.Packages.props":
            require(data.count(comment) == 1, "Comment delta differs")
            data = data.replace(comment, b"")
        require(sha(data) == binding["retained_qualified_sha256"], "Historical repair binding differs: " + path)

    platform = ROOT / "references/Hexalith.Platform"
    require(git(platform, "diff", "--name-only", source["platform_hosted_run_subject"],
                source["platform_head"]).decode().splitlines() == source["platform_delta_from_hosted_subject"],
            "Platform source comparison differs")
    require(handoff["platform"]["delta_from_hosted_subject"] == source["platform_delta_from_hosted_subject"],
            "Platform comparison projection differs")
    for repo, field in [(projects, "projects_inputs"), (platform, "platform_inputs")]:
        for path, expected in source[field].items():
            require(sha((repo / path).read_bytes()) == expected, "Selected source input differs: " + path)
    for repo, field in [(projects, "projects_declared_gitlinks"), (platform, "platform_declared_gitlinks")]:
        paths = [line.split(None, 1)[1] for line in git(repo, "config", "-f", ".gitmodules", "--get-regexp", "path").decode().splitlines()]
        actual_pins = {p: git(repo, "rev-parse", "HEAD:" + p).decode().strip() for p in paths}
        require(source[field] == actual_pins, "Declared gitlink map differs: " + field)
    require(handoff["projects"]["declared_builds_pin"]
            == source["projects_declared_gitlinks"]["references/Hexalith.Builds"], "Declared Builds projection differs")
    missing = {
        "folders_root_module_absent": not (ROOT / "module").exists(),
        "projects_platform_handoff_absent": not (projects / "module/platform-handoff.md").exists(),
        "projects_smoke_surfaces": read(projects / "module/hexalith.module-manifest.v2.json")["modules"][0]["lifecycle"]["smokeSurfaces"],
    }
    require(source["missing_owner_inputs"] == missing, "Owner input observations differ")
    tuple_ = read(platform / "eng/builds-tuple.json")
    require(handoff["platform"]["producer"] == tuple_["producer"]
            and handoff["platform"]["candidate_acceptance"] == tuple_["candidateAcceptance"],
            "Platform producer projection differs")
    require(handoff["platform"]["tool_version"] == read(platform / ".config/dotnet-tools.json")["tools"]["hexalith.builds.module.cli"]["version"]
            and handoff["platform"]["tool_range"] == read(platform / "hexalith-platform.json")["toolVersionRange"],
            "Platform tool projection differs")
    for packet_name in ["story-1-17-closure-preparation-2026-11-16", "story-1-17-projects-ci-repair-2026-11-16",
                        "story-1-17-projects-package-alignment-2026-11-16"]:
        old = PACKET.parent / packet_name
        for line in (old / "SHA256SUMS").read_text().splitlines():
            expected, name = line.split("  ", 1)
            require(sha((old / name).read_bytes()) == expected, "Historical packet member differs")
    checks = read(ROOT / "eng/story117-platform-qualification-current/staging-checks.json")["checks"]
    require(len(checks) == 14 and all(r["status"] == "unexecuted" for r in checks), "Staging controls differ")
    print("PASS: packet, six dated remote responses, committed repairs, source comparison, thirteen repositories, prior packets and controls.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packet-only", action="store_true")
    args = parser.parse_args()
    try:
        verify(args.packet_only)
    except (ValueError, KeyError, OSError, subprocess.CalledProcessError) as error:
        parser.exit(1, "FAIL: " + str(error) + "\n")
