#!/usr/bin/env python3
"""Verify retained Story 1.17 clean-source diagnostics and exact-head CI evidence."""
import argparse
import hashlib
import io
import json
import re
from pathlib import Path
import subprocess
import tarfile
import zipfile


def digest(body):
    return hashlib.sha256(body).hexdigest()


def verify(receipt, workspace, builds):
    record = json.loads(receipt.read_bytes())
    assert record['schema'] == 'hexalith.story117.builds-clean-source-qualification.v1'
    assert record['authorization']['publication'] is False
    assert record['source']['clean'] is True
    assert record['decision']['fullLocalTechnicalQualificationPassed'] is True
    assert record['decision']['technicalInventoryReleaseEligible'] is True
    assert record['decision']['stagingChecksExecuted'] == 0
    assert record['decision']['stagingChecksRemaining'] == 14
    assert record['decision']['productionMode'] == 'V1Only'
    assert record['decision']['t0Utc'] is None
    assert record['decision']['storyTracker'] == 'backlog'
    for field in ['releaseAdmission', 'stagingQualification', 'storyClosure']:
        assert record['decision'][field] is False, field
    for name, expected in record['protectedWorkspaceFilesSha256'].items():
        assert digest((workspace / name).read_bytes()) == expected, name
    for name, expected in record['verificationFilesSha256'].items():
        assert digest((workspace / name).read_bytes()) == expected, name
    bundle = record['retainedArtifactBundle']
    assert digest((workspace / bundle['path']).read_bytes()) == bundle['sha256'], 'bundle hash'
    contents = {}
    with tarfile.open(workspace / bundle['path'], 'r:gz') as archive:
        members = [member for member in archive.getmembers() if member.isfile()]
        assert len(members) == len(bundle['membersSha256'])
        assert len({member.name for member in members}) == len(members)
        for member in members:
            body = archive.extractfile(member).read()
            assert digest(body) == bundle['membersSha256'][member.name], member.name
            contents[member.name] = body
    for name, expected in record['protectedWorkspaceFilesSha256'].items():
        assert digest(contents['protected/' + name]) == expected, name
    platform_inputs = json.loads(contents['verification/platform-inputs-preservation.json'])['filesSha256']
    assert len(platform_inputs) == record['preservation']['capturedPlatformFilesUnchanged']
    for name, expected in platform_inputs.items():
        assert name.startswith('references/Hexalith.Platform/'), name
        assert digest((workspace / name).read_bytes()) == expected, name
    synthetic = json.loads(contents['verification/synthetic-expectations-preservation.json'])
    assert synthetic['synthetic'] is True
    assert len(synthetic['unchangedExpectationsSha256']) == 37
    for name, expected in synthetic['unchangedExpectationsSha256'].items():
        assert digest((builds / 'test/fixtures/evidence/acceptance' / name).read_bytes()) == expected, name
    inventory = json.loads(contents['diagnostic/g4-tool-package-inventory.json'])
    assert digest(contents['diagnostic/g4-tool-package-inventory.json']) == record['inventorySha256']
    q = inventory['qualification']
    assert q == record['qualification'], 'qualification receipt differs'
    assert len(inventory['qualificationEvidence']) == record['qualificationEvidenceArtifacts']
    for lane in ['sourceValidation', 'controls']:
        assert q[lane] == {'mode': 'executed', 'result': 'passed'}, lane
    assert q['packageBuild'] == {'mode': 'official', 'result': 'passed'}
    assert q['fixtures']['mode'] == 'repository-tracked'
    assert q['sourceTree'] == {'clean': True, 'revision': record['source']['buildsCommit']}
    assert q['releaseEligible'] is True and q['ineligibilityReasons'] == []
    assert len(inventory['packages']) == 2
    assert {row['id'] for row in inventory['packages']} == {'Hexalith.Builds.Evidence.Cli', 'Hexalith.Builds.Module.Cli'}
    for row in inventory['packages']:
        assert row['version'] == '4.31.2'
        for role in ['nupkg', 'snupkg']:
            artifact = row[role]
            body = contents['diagnostic/' + artifact['file']]
            assert len(body) == artifact['sizeBytes']
            assert digest(body).upper() == artifact['sha256'].upper(), artifact['file']
    recorded_archives = [
        {'file': row[role]['file'], 'sha256': row[role]['sha256'].lower(), 'sizeBytes': row[role]['sizeBytes']}
        for row in inventory['packages'] for role in ['nupkg', 'snupkg']
    ]
    assert recorded_archives == record['packages'], 'archive receipt differs'
    for row in inventory['qualificationEvidence']:
        body = contents['diagnostic/' + row['file']]
        assert digest(body).upper() == row['sha256'].upper(), row['file']
        assert len(body) == row['sizeBytes']
    proof = json.loads(contents['diagnostic/qualification-evidence/packaged-seed-subscriptions.json'])
    assert proof['status'] == 'passed'
    module = contents['diagnostic/Hexalith.Builds.Module.Cli.4.31.2.nupkg']
    assert digest(module).upper() == proof['package']['sha256'].upper()
    with zipfile.ZipFile(io.BytesIO(module)) as archive:
        assert digest(archive.read(proof['seed']['entry'])).upper() == proof['seed']['sha256'].upper()
    assert proof['seed']['loadedSha256'] == proof['seed']['sha256']
    assert proof['seed']['sha256'].lower() == record['packagedSeedSha256']
    assert len(proof['checks']) == 2
    for item, declarative in zip(proof['checks'], [False, True], strict=True):
        assert item['declarative'] is declarative
        assert item['discoveryStatusCode'] == (404 if declarative else 200)
        assert item['receiverPresent'] is True
        assert item['projectionSubscriptionCount'] == (0 if declarative else 1)
        assert item['receiverTopicMetadataCount'] == (0 if declarative else 1)
        for field in ['receiverPubsubName', 'subscriptionPubsubName']:
            assert item[field] == (None if declarative else 'configured-seed-events')
    command = record['completeQualification']
    assert command['exitCode'] == 0
    assert '-RequireControls' in command['command']
    assert '-SkipSourceValidation' not in command['command']
    log = contents['verification/' + command['log']]
    assert digest(log) == command['sha256']
    summaries = re.findall(r'Test run summary: Passed!\s+total: (\d+)\s+failed: (\d+)\s+succeeded: (\d+)\s+skipped: (\d+)', log.decode())
    assert len(summaries) == 3
    counts = {name: dict(zip(['total', 'failed', 'passed', 'skipped'], map(int, row), strict=True))
              for name, row in zip(['Evidence', 'Module', 'Integration'], summaries, strict=True)}
    assert counts == record['sourceTests'], 'source test counts'
    assert all(row['failed'] == 0 for row in counts.values())
    ci = record['exactHeadPushCi']
    assert ci['head_sha'] == record['source']['buildsCommit']
    assert ci['head_branch'] == 'main' and ci['event'] == 'push'
    assert ci['status'] == 'completed' and ci['conclusion'] == 'success'
    retained_runs = json.loads(contents['verification/exact-head-ci-runs.log'])
    assert any(run['id'] == ci['id'] and run['head_sha'] == ci['head_sha']
               and run['head_branch'] == 'main' and run['event'] == 'push'
               and run['status'] == 'completed' and run['conclusion'] == 'success'
               for run in retained_runs['workflow_runs'])
    for field in ['releaseSourceGuard', 'inventoryEligibilityGuard']:
        guard = record[field]
        assert guard['exitCode'] == 0, field
        assert digest(contents['verification/' + guard['log']]) == guard['sha256'], field
    assert 'DISPATCH_SHA=' + record['source']['buildsCommit'] in record['releaseSourceGuard']['command']
    assert record['diagnosticOnly'] is True and record['actualNextReleaseVersion'] is None
    head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=builds, text=True).strip()
    assert head == record['source']['buildsCommit'], 'current Builds commit differs'
    assert subprocess.check_output(['git', 'rev-parse', 'HEAD^{tree}'], cwd=builds, text=True).strip() == record['source']['buildsTree']
    assert subprocess.check_output(['git', 'status', '--porcelain'], cwd=builds, text=True) == '', 'Builds is dirty'
    assert digest((builds / 'Props/Directory.Packages.props').read_bytes()) == record['source']['catalogSha256']
    assert digest((builds / 'Tools/package-version-audit.json').read_bytes()) == record['source']['auditSha256']
    for row in q['fixtures']['files']:
        assert digest((builds / 'test/fixtures' / row['file']).read_bytes()).upper() == row['sha256'].upper(), row['file']
    print(f"Verified clean Builds {head}, successful exact-head push CI, "
          f"{len(members)} retained members, four diagnostic archives, "
          f"{len(inventory['qualificationEvidence'])} qualification artifacts and both seed modes. "
          'Technical qualification passed; publication and acceptance remain unauthorized.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--workspace', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--builds', type=Path)
    parser.add_argument('--receipt', type=Path)
    args = parser.parse_args()
    workspace = args.workspace.resolve()
    builds = args.builds.resolve() if args.builds else workspace / 'references/Hexalith.Builds'
    receipt = args.receipt or workspace / '_bmad-output/implementation-artifacts/evidence/story-1-17-builds-clean-source-qualification-2026-10-16.json'
    try:
        verify(receipt.resolve(), workspace, builds)
    except (AssertionError, KeyError, OSError, ValueError, subprocess.CalledProcessError, tarfile.TarError, zipfile.BadZipFile) as error:
        raise SystemExit(f'Evidence verification failed: {error}') from error
