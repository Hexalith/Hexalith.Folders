"""Verify immutable Story 1.17 execution evidence using stdlib reads only.

Usage: python3 -I -B verify_execution_capsule.py /explicit/evidence/directory
No extraction, writes, subprocesses, archived-code execution, or SDK invocation.
Integrity does not grant authorization or prove unretained historic metadata.
"""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import stat
import sys
import tarfile

STEM = 'story-1-17-cli-4-38-filter-reset-execution-2026-10-26'
EXPECTED = {
    STEM + '.json': (
        217465,
        '83926941c48d04402d107df0e11e9f8f5300144601197d79e55c7bbd6c0a96ef',
    ),
    STEM + '.artifacts.tar.gz': (
        43400158,
        'a7e68bcc283c14c2a48f2262c31e8e52b7b478c828fcb534be4f920a07ec3387',
    ),
    STEM + '.members.json': (
        104228,
        '44f74850e5b83433153552a2f934a83ae55f3eb1e045c2a9501494f0979873c5',
    ),
}
EXPECTED_MEMBER_COUNT = 417


def require(condition, message):
    if not condition:
        raise ValueError(message)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, 'Duplicate JSON object key: ' + key)
        result[key] = value
    return result


def safe_path(name):
    require(isinstance(name, str) and bool(name), 'Empty or non-string member path.')
    require('\x00' not in name and '\\' not in name, 'Unsafe member path: ' + name)
    path = PurePosixPath(name)
    require(not path.is_absolute(), 'Absolute member path: ' + name)
    require(all(part not in ('', '.', '..') for part in name.split('/')),
            'Noncanonical or traversing member path: ' + name)
    require(str(path) == name, 'Noncanonical member path: ' + name)
    require(path.parts[0] in ('filter-reset', 'loader', 'attempt-claim'),
            'Unexpected member prefix: ' + name)
    return name


def verify(evidence_directory):
    require(evidence_directory.is_dir(), 'Evidence directory does not exist.')
    artifacts = {}
    for filename, (expected_size, expected_sha256) in EXPECTED.items():
        path = evidence_directory / filename
        require(stat.S_ISREG(path.lstat().st_mode), 'Artifact is not a regular file: ' + str(path))
        with path.open('rb') as stream:
            digest = hashlib.file_digest(stream, 'sha256').hexdigest()
        require(path.stat().st_size == expected_size, 'Artifact size mismatch: ' + filename)
        require(digest == expected_sha256, 'Artifact SHA256 mismatch: ' + filename)
        artifacts[filename] = {'bytes': expected_size, 'sha256': digest}

    with (evidence_directory / (STEM + '.json')).open('r', encoding='utf-8') as stream:
        receipt = json.load(stream, object_pairs_hook=unique_object)
    with (evidence_directory / (STEM + '.members.json')).open('r', encoding='utf-8') as stream:
        manifest = json.load(stream, object_pairs_hook=unique_object)
    require(manifest.get('format') == 'sha256-file-members-v1', 'Unexpected manifest format.')
    members = manifest.get('members')
    require(isinstance(members, list) and len(members) == EXPECTED_MEMBER_COUNT,
            'Manifest must contain exactly 417 members.')
    expected_members = {}
    for entry in members:
        require(isinstance(entry, dict), 'Invalid manifest member.')
        name = safe_path(entry.get('path'))
        require(name not in expected_members, 'Duplicate manifest member: ' + name)
        size = entry.get('bytes')
        digest = entry.get('sha256')
        require(type(size) is int and size >= 0, 'Invalid manifest member size: ' + name)
        require(isinstance(digest, str) and len(digest) == 64
                and all(character in '0123456789abcdef' for character in digest),
                'Invalid manifest member SHA256: ' + name)
        expected_members[name] = (size, digest)
    for label, suffix in [('archive', '.artifacts.tar.gz'), ('manifest', '.members.json')]:
        expected_size, expected_sha256 = EXPECTED[STEM + suffix]
        require(receipt[label]['bytes'] == expected_size
                and receipt[label]['sha256'] == expected_sha256,
                'Receipt binding mismatch for ' + label)

    verified = set()
    with tarfile.open(evidence_directory / (STEM + '.artifacts.tar.gz'), mode='r:gz') as archive:
        actual_members = archive.getmembers()
        require(len(actual_members) == EXPECTED_MEMBER_COUNT, 'Archive must contain exactly 417 members.')
        for member in actual_members:
            name = safe_path(member.name)
            require(name not in verified, 'Duplicate archive member: ' + name)
            require(member.type in (tarfile.REGTYPE, tarfile.AREGTYPE)
                    and member.isfile() and not member.issparse(),
                    'Archive member is not an ordinary regular file: ' + name)
            require(name in expected_members, 'Unmanifested archive member: ' + name)
            expected_size, expected_sha256 = expected_members[name]
            require(member.size == expected_size, 'Archive member size mismatch: ' + name)
            # extractfile returns a read-only stream; nothing is extracted to disk.
            with archive.extractfile(member) as stream:
                actual_sha256 = hashlib.file_digest(stream, 'sha256').hexdigest()
            require(actual_sha256 == expected_sha256, 'Archive member SHA256 mismatch: ' + name)
            verified.add(name)
    require(verified == set(expected_members), 'Archive and manifest member sets differ.')
    return {
        'result': 'VERIFIED',
        'evidenceDirectory': str(evidence_directory.resolve()),
        'artifacts': artifacts,
        'verifiedMemberCount': len(verified),
        'uniqueSafeRegularMembers': True,
        'exactMemberSetSizesAndSha256': True,
        'operations': 'stdlib reads only; no extraction, writes, subprocesses, or code/SDK execution',
        'limit': 'Integrity only; no renewed authorization or independent proof of unretained historic regular-file metadata.',
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('evidence_directory', type=Path,
                        help='Directory containing the original three execution evidence files.')
    arguments = parser.parse_args()
    try:
        result = verify(arguments.evidence_directory)
    except (OSError, ValueError, KeyError, TypeError, tarfile.TarError) as error:
        print('Verification failed: ' + str(error), file=sys.stderr)
        return 1
    print(json.dumps(result, indent=2))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
