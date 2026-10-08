#!/usr/bin/env python3
"""Read-only audit of retained Story 1.17 release identity evidence."""

import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import zipfile

import yaml


def sha(content):
    return hashlib.sha256(content).hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repository-root', type=Path, default=Path(__file__).resolve().parents[3])
    parser.add_argument('--record', type=Path, default=Path(__file__).with_name('story-1-17-release-identity-2026-10-08.json'))
    args = parser.parse_args()
    root = args.repository_root.resolve()
    record = json.loads(args.record.read_text())

    def read(relative):
        return (root / relative).read_bytes()

    def git_blob(revision, relative):
        return subprocess.check_output(['git', 'show', f'{revision}:{relative}'], cwd=root)

    for relative, digest in record['preservation']['protected_file_hashes'].items():
        require(sha(read(relative)) == digest, f'Protected input differs: {relative}')
    for repository, revision in record['root_dependency_revisions'].items():
        current = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root / repository, text=True).strip()
        require(current == revision, f'Root revision differs: {repository}')
    planning = yaml.safe_load(read('_bmad-output/planning-artifacts/planning-story-manifest.yaml'))
    for row in planning['provenance']:
        require(sha(read(row['path'])) == row['sha256'], f'Current binding differs: {row["path"]}')

    inventory_path = '_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml'
    current_raw = read(inventory_path)
    current = yaml.safe_load(current_raw)
    comparison = record['source_comparison']
    require(sha(current_raw) == comparison['current_manifest_sha256'], 'Current inventory differs')
    release_revision = record['released_pair']['packages'][0]['repository']['commit']
    released_raw = git_blob(release_revision, inventory_path)
    released = yaml.safe_load(released_raw)
    require(sha(released_raw) == comparison['released_manifest_sha256'], 'Released inventory differs')
    for row in released['artifacts']:
        require(sha(git_blob(release_revision, row['path'])) == row['sha256'], f'Released source differs: {row["path"]}')
    changes = []
    for row in current['artifacts']:
        digest = sha(read(row['path']))
        require(digest == row['sha256'], f'Candidate input differs: {row["path"]}')
        old = subprocess.run(['git', 'show', f'{release_revision}:{row["path"]}'], cwd=root, capture_output=True)
        previous = sha(old.stdout) if old.returncode == 0 else None
        if previous != digest:
            changes.append({'path': row['path'], 'released_sha256': previous, 'current_sha256': digest})
    require(changes == comparison['changed_artifacts'], 'Source comparison differs')

    for row in record['released_pair']['packages']:
        nuget = root / row['retained_nuget_path']
        github = root / row['retained_github_asset_path']
        require(sha(nuget.read_bytes()) == row['sha256'], f'Released NuGet archive differs: {row["id"]}')
        require(sha(github.read_bytes()) == row['github_asset_sha256'], f'GitHub asset differs: {row["id"]}')
        with zipfile.ZipFile(nuget) as feed, zipfile.ZipFile(github) as asset:
            names = set(feed.namelist()) - {'.signature.p7s'}
            require(names == set(asset.namelist()), f'Released payload inventory differs: {row["id"]}')
            require(all(feed.read(name) == asset.read(name) for name in names), f'Released payload differs: {row["id"]}')

    for row in record['prepared_packages']:
        require(sha(read(row['path'])) == row['sha256'], f'Prepared archive differs: {row["path"]}')
        require(row['repository']['commit'] == record['folders_revision'], 'Prepared source metadata differs')
    for report in record['gate_reports'].values():
        require(sha(read(report['path'])) == report['sha256'], f'Gate report differs: {report["path"]}')
    for test in record['tests'].values():
        require(sha(read(test['retained_log_path'])) == test['log_sha256'], 'Retained test log differs')
    for original, row in record['projects_candidate_build']['build_files'].items():
        require(sha(read(row['retained_path'])) == row['sha256'], f'Retained consumer file differs: {original}')

    bundle = record['projects_candidate_build']['bundle_manifest']
    require(sha(read(bundle['path'])) == bundle['sha256'], 'Consumer bundle manifest differs')
    manifest = json.loads(read(bundle['path']))
    bundle_root = root / manifest['bundle_root']
    paths = sorted(p.relative_to(bundle_root).as_posix() for p in bundle_root.rglob('*') if p.is_file())
    require(paths == [row['path'] for row in manifest['files']], 'Consumer bundle inventory differs')
    for row in manifest['files']:
        content = (bundle_root / row['path']).read_bytes()
        require(len(content) == row['bytes'] and sha(content) == row['sha256'], f'Consumer bundle file differs: {row["path"]}')
    require(sha(read(record['migration_entry']['census_path'])) == record['migration_entry']['census_sha256'], 'Dated census differs')
    print(f'PASS: {len(released["artifacts"])} released hashes; {len(current["artifacts"])} candidate hashes; '
          f'{len(changes)} changed/new paths; ten archives; retained payloads/logs/consumer bundle; '
          'current bindings and protected evidence. No publication or live deployment check.')


if __name__ == '__main__':
    try:
        main()
    except (OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        print(f'FAIL: {error}', file=sys.stderr)
        raise SystemExit(1)
