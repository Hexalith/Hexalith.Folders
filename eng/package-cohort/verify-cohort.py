#!/usr/bin/env python3
"""Verify producer inputs and protected bytes without changing any repository.

Protection selects paths from each recorded baseline's `git ls-tree -r -z`,
then removes only policy exclusions. Each retained path contributes its baseline
Git mode/OID and SHA-256 of actual worktree bytes (null for gitlinks/directories).
The aggregate is SHA-256 of UTF-8 json.dumps(entries, sort_keys=True,
separators=(',', ':')). The separately authorized deferred-work append must match
its receipt exactly; only its verified original prefix enters the aggregate.
Recorded owner commits may capture only the exact qualified paths and bytes.
Root descendants may change only qualification artifacts, the verified append,
and those exact cohort gitlinks. Platform retains its exact baseline check.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
from pathlib import Path


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def git(repo: Path, *args: str) -> bytes:
    return subprocess.check_output(['git', *args], cwd=repo)


def read(path: Path):
    return json.loads(path.read_text())


def committed_owners(receipts: Path) -> dict:
    path = receipts / 'owner-commits.json'
    return {row['repository']: row for row in read(path)} if path.exists() else {}


def producers(root: Path, receipts: Path) -> dict:
    sources = read(receipts / 'sources.json')
    committed = committed_owners(receipts)
    for row in sources:
        repo = root / row['repository']
        head = git(repo, 'rev-parse', 'HEAD').decode().strip()
        binding = committed.get(row['repository'])
        captured = binding is not None and head == binding['committedRevision']
        assert head == row['committedRevision'] or captured, row['repository'] + ': revision drift'
        assert sha((receipts / row['patch']).read_bytes()) == row['patchSha256'], row['patch'] + ': patch drift'
        expected = row['workingFileSha256']
        if captured:
            assert binding['qualifiedBaseRevision'] == row['committedRevision'], 'Qualified baseline changed'
            committed_paths = set(git(repo, 'diff', row['committedRevision'], head, '--name-only', '-z').decode().rstrip('\0').split('\0')) - {''}
            assert committed_paths == set(expected), row['repository'] + ': committed snapshot differs'
        changed = set(git(repo, 'diff', 'HEAD', '--name-only', '-z').decode().rstrip('\0').split('\0')) - {''}
        changed.update(set(git(repo, 'ls-files', '--others', '--exclude-standard', '-z').decode().rstrip('\0').split('\0')) - {''})
        assert changed == (set() if captured else set(expected)), row['repository'] + ': unexpected working paths'
        for name, digest in expected.items():
            assert sha((repo / name).read_bytes()) == digest, row['repository'] + '/' + name + ': source drift'
    return {'status': 'pass', 'producers': len(sources), 'sourcesSha256': sha((receipts / 'sources.json').read_bytes())}


def protected(root: Path, receipts: Path) -> dict:
    producers(root, receipts)
    committed = committed_owners(receipts)
    policy = read(receipts / 'protection-policy.json')
    append = read(receipts / 'deferred-work-review-append.json')
    data = (root / append['path']).read_bytes()
    assert append['appendOnly'] and len(data) > append['beforeBytes'], 'Deferred-work append missing'
    assert sha(data[:append['beforeBytes']]) == append['beforeSha256'], 'Deferred-work historical prefix changed'
    assert sha(data) == append['afterSha256'], 'Deferred-work append differs from receipt'
    results = []
    for row in policy['repositories']:
        repo = root / row['repository']
        head = git(repo, 'rev-parse', 'HEAD').decode().strip()
        binding = committed.get(row['repository'])
        if binding is not None:
            assert binding['qualifiedBaseRevision'] == row['baseline'] and head == binding['committedRevision'], row['repository'] + ': HEAD changed'
        elif row['repository'] == '.' and head != row['baseline']:
            subprocess.run(['git', 'merge-base', '--is-ancestor', row['baseline'], head], cwd=repo, check=True)
            allowed = {'.gitattributes', append['path'], '_bmad-output/implementation-artifacts/spec-folders-canonical-package-cohort.md', *committed}
            changes = set(git(repo, 'diff', row['baseline'], head, '--name-only', '-z').decode().rstrip('\0').split('\0')) - {''}
            assert all(name in allowed or name.startswith('eng/package-cohort/') for name in changes), 'Root production inputs changed'
            for name, owner in committed.items():
                assert git(repo, 'rev-parse', 'HEAD:' + name).decode().strip() == owner['committedRevision'], name + ': committed gitlink changed'
        else:
            assert head == row['baseline'], row['repository'] + ': HEAD changed'
        excluded = set(row['excludedPaths'])
        entries = {}
        for item in git(repo, 'ls-tree', '-r', '-z', '--full-tree', row['baseline']).decode().split('\0'):
            if not item:
                continue
            header, name = item.split('\t', 1)
            if name in excluded:
                continue
            mode, kind, oid = header.split()
            file = repo / name
            digest = sha(file.read_bytes()) if file.is_file() else None
            if row['repository'] == '.' and name == append['path']:
                digest = append['beforeSha256']
            if kind == 'commit':
                staged = git(repo, 'ls-files', '-s', '--', name).decode().split()
                permitted = [oid]
                if row['repository'] == '.' and name in committed:
                    permitted = [committed[name]['committedRevision']]
                    if head == row['baseline']:
                        permitted.append(oid)
                assert staged[0] == mode and staged[1] in permitted, row['repository'] + '/' + name + ': gitlink changed'
            entries[name] = {'mode': mode, 'oid': oid, 'sha256': digest}
        aggregate = sha(json.dumps(entries, sort_keys=True, separators=(',', ':')).encode())
        assert aggregate == row['baselineAggregateSha256'], row['repository'] + ': protected aggregate changed'
        results.append({'repository': row['repository'], 'baseline': row['baseline'], 'headAfter': head, 'protectedTrackedPaths': len(entries),
                        'sha256Before': row['baselineAggregateSha256'], 'sha256After': aggregate})
    return {'status': 'pass', 'policySha256': sha((receipts / 'protection-policy.json').read_bytes()),
            'initialComparisonSha256': sha((receipts / 'protected-comparison.initial.json').read_bytes()),
            'authorizedAppend': append, 'authorizedOwnerCommits': list(committed.values()), 'repositories': results}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('check', choices=['producers', 'protected'])
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--receipts', type=Path, default=Path(__file__).resolve().with_name('qualification'))
    parser.add_argument('--write', action='store_true', help='Write a new current protected comparison; initial receipt is preserved.')
    args = parser.parse_args()
    result = (producers if args.check == 'producers' else protected)(args.root, args.receipts)
    if args.write:
        assert args.check == 'protected', '--write applies only to protected checks'
        (args.receipts / 'protected-comparison.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result, sort_keys=True))


if __name__ == '__main__':
    main()
