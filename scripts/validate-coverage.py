#!/usr/bin/env python3
"""Validate merged Cobertura line coverage for declared Folders source scopes."""

from __future__ import annotations

import argparse
import math
import os
import pathlib
import re
import sys
import xml.etree.ElementTree as element_tree


DEFAULT_SCOPES = [
    "src/Hexalith.Folders.Contracts/",
    "src/Hexalith.Folders/",
    "src/Hexalith.Folders.Client/",
    "src/Hexalith.Folders.Aspire/",
    "src/Hexalith.Folders.Testing/",
]


def normalize(path: str, scopes: list[str]) -> str:
    """Normalize collector-relative source paths before merging reports."""

    value = path.replace("\\", "/").lstrip("./")
    if value.startswith("src/"):
        return value
    for scope in scopes:
        without_src = scope.removeprefix("src/")
        if value.startswith(without_src):
            return f"src/{value}"
    return value


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--coverage-root", type=pathlib.Path, required=True)
    parser.add_argument("--minimum-line-coverage", type=float, required=True)
    parser.add_argument("--required-branch-coverage", type=float, required=True)
    parser.add_argument("--line-scope", action="append", default=[])
    parser.add_argument("--isolation-auth-target", action="append", default=[])
    parser.add_argument("--summary-file", type=pathlib.Path)
    args = parser.parse_args()
    for label, threshold in (
        ("minimum line coverage", args.minimum_line_coverage),
        ("required branch coverage", args.required_branch_coverage),
    ):
        if not math.isfinite(threshold) or not 0 <= threshold <= 100:
            raise ValueError(f"{label} must be a finite percentage from 0 through 100")

    scopes = args.line_scope or DEFAULT_SCOPES
    reports = sorted(args.coverage_root.glob("**/coverage.cobertura.xml"))
    if not reports:
        raise ValueError(f"no coverage.cobertura.xml files found under {args.coverage_root}")

    valid: set[tuple[str, str]] = set()
    covered: set[tuple[str, str]] = set()
    branch_by_line: dict[tuple[str, str], tuple[int, int]] = {}
    matched_scopes: set[str] = set()
    matched_targets: set[str] = set()
    for report in reports:
        for class_element in element_tree.parse(report).getroot().findall(".//class"):
            filename = normalize(class_element.attrib.get("filename", ""), scopes)
            lines = class_element.findall(".//line")
            matching_scopes = [scope for scope in scopes if filename.startswith(scope) or f"/{scope}" in filename]
            if matching_scopes:
                matched_scopes.update(matching_scopes)
                for line in lines:
                    key = (filename, line.attrib.get("number", ""))
                    valid.add(key)
                    if int(line.attrib.get("hits", "0")) > 0:
                        covered.add(key)

            target = next(
                (candidate for candidate in args.isolation_auth_target if filename.endswith(normalize(candidate, scopes))),
                None,
            )
            if target is None:
                continue
            matched_targets.add(target)
            for line in lines:
                if line.attrib.get("branch", "").lower() != "true":
                    continue
                match = re.search(r"\((\d+)/(\d+)\)", line.attrib.get("condition-coverage", ""))
                if match is None:
                    continue
                branch_covered, branch_valid = int(match.group(1)), int(match.group(2))
                key = (filename, line.attrib.get("number", ""))
                previous_covered, _ = branch_by_line.get(key, (0, branch_valid))
                branch_by_line[key] = (max(previous_covered, branch_covered), branch_valid)
    if not valid:
        raise ValueError("no coverage data matched the declared Folders source scopes")
    missing_scopes = [scope for scope in scopes if scope not in matched_scopes]
    if missing_scopes:
        raise ValueError(f"coverage scope(s) not found: {', '.join(missing_scopes)}")
    percentage = len(covered) / len(valid) * 100
    if percentage < args.minimum_line_coverage:
        raise ValueError(
            f"line coverage {percentage:.2f}% is below {args.minimum_line_coverage:.2f}%"
        )

    if args.required_branch_coverage > 0 and not args.isolation_auth_target:
        raise ValueError("positive branch coverage requires at least one --isolation-auth-target")
    missing_targets = [target for target in args.isolation_auth_target if target not in matched_targets]
    if missing_targets:
        raise ValueError(f"coverage target(s) not found: {', '.join(missing_targets)}")
    branch_covered = sum(value[0] for value in branch_by_line.values())
    branch_valid = sum(value[1] for value in branch_by_line.values())
    if args.required_branch_coverage > 0 and branch_valid == 0:
        raise ValueError("positive branch coverage requires parseable branch records")
    branch_percentage = 100.0 if branch_valid == 0 else branch_covered / branch_valid * 100
    if branch_percentage < args.required_branch_coverage:
        raise ValueError(
            f"branch coverage {branch_percentage:.2f}% is below {args.required_branch_coverage:.2f}%"
        )

    summary = (
        "## Coverage Gates\n"
        f"- Folders package line coverage: {percentage:.2f}% ({len(covered)}/{len(valid)})\n"
        f"- Isolation/auth branch coverage: {branch_percentage:.2f}% ({branch_covered}/{branch_valid})\n"
    )
    print(summary)
    summary_path = args.summary_file or (pathlib.Path(os.environ["GITHUB_STEP_SUMMARY"]) if "GITHUB_STEP_SUMMARY" in os.environ else None)
    if summary_path is not None:
        with summary_path.open("a", encoding="utf-8") as handle:
            handle.write(summary)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:  # noqa: BLE001 - command-line gate reports a concise failure.
        print(f"Coverage validation failed: {error}", file=sys.stderr)
        raise SystemExit(1)
