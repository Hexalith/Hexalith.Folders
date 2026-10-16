#!/usr/bin/env python3
"""Verify retained signed Builds publication observations, without accepting Platform."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import tarfile
import zipfile
import xml.etree.ElementTree as ET

if not __debug__:
    raise SystemExit('Integrity verification requires Python assertions; optimization is refused.')

PRIOR_RECEIPT = '_bmad-output/implementation-artifacts/evidence/story-1-17-builds-clean-source-qualification-2026-10-16.json'
PRIOR_SHA256 = '80604829ed24bc571732384ea654004b2d234228de11531c260d7c9e16a398db'
REPOSITORY = 'Hexalith/Hexalith.Builds'
PACKAGE_IDS = {'Hexalith.Builds.Module.Cli', 'Hexalith.Builds.Evidence.Cli'}


def digest(body):
    return hashlib.sha256(body).hexdigest()


def verify(receipt, workspace):
    record = json.loads(receipt.read_bytes())
    prior_bytes = (workspace / PRIOR_RECEIPT).read_bytes()
    assert digest(prior_bytes) == PRIOR_SHA256, 'original qualification anchor'
    prior = json.loads(prior_bytes)
    assert record['priorCleanSourceQualification']['path'] == PRIOR_RECEIPT
    assert record['priorCleanSourceQualification']['sha256'] == PRIOR_SHA256
    assert record['protectedWorkspaceFilesSha256'] == prior['protectedWorkspaceFilesSha256']
    assert len(record['protectedWorkspaceFilesSha256']) == 42
    for name, expected in record['verificationFilesSha256'].items():
        assert digest((workspace / name).read_bytes()) == expected, name
    assert record['schema'] == 'hexalith.story117.builds-publication.v1'
    assert record['authorization']['publication'] is True
    assert record['authorization']['platformAcceptance'] is False
    assert record['authorization']['deployment'] is False and record['authorization']['activation'] is False
    assert record['realAcceptanceEvidenceCreated'] is False
    assert record['decision']['productionMode'] == 'V1Only'
    assert record['decision']['t0Utc'] is None
    assert record['decision']['storyTracker'] == 'backlog'
    assert record['decision']['stagingChecksExecuted'] == 0
    assert record['decision']['stagingChecksRemaining'] == 14
    for field in ['platformAcceptance', 'stagingQualification', 'storyClosure']:
        assert record['decision'][field] is False, field
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
    run = json.loads(contents['release-run.log'])
    assert run['repository']['full_name'] == REPOSITORY
    assert run['path'] == '.github/workflows/build-release.yml'
    assert run['id'] == record['workflowRunId']
    assert run['html_url'] == f"https://github.com/{REPOSITORY}/actions/runs/{run['id']}"
    assert record['workflowUrl'] == run['html_url']
    assert run['status'] == 'completed' and run['conclusion'] == 'success'
    assert run['event'] == 'workflow_dispatch' and run['head_branch'] == 'main'
    assert run['head_sha'] == record['sourceRevision']
    dispatch = json.loads(contents['dispatch-observation.json'])
    assert dispatch == record['dispatchObservation']
    assert dispatch['selectedRun'] == run['id'] and dispatch['selectedSourceRevision'] == record['sourceRevision']
    assert record['newDispatchPerformedByThisAgent'] == dispatch['newDispatchPerformedByThisAgent']
    if dispatch['ownDispatchCancelled']:
        cancelled = json.loads(contents['cancelled-duplicate.log'])
        assert cancelled['id'] == dispatch['ownDispatch'] and cancelled['conclusion'] == 'cancelled'
        assert json.loads(contents['cancelled-duplicate-jobs.log']) == {'total_count': 0, 'jobs': []}

    release = json.loads(contents['release.json'])
    assert release['tag_name'] == 'v' + record['version']
    assert release['html_url'] == f"https://github.com/{REPOSITORY}/releases/tag/{release['tag_name']}"
    assert record['releaseUrl'] == release['html_url']
    assert release['draft'] is False and release['prerelease'] is False
    tag = json.loads(contents['tag-ref.json'])
    assert tag['ref'] == 'refs/tags/v' + record['version']
    assert tag['object']['type'] == 'commit' and tag['object']['sha'] == record['sourceRevision']
    candidate = json.loads(contents['candidate-source.json'])
    assert candidate == record['freshSourceQualification']
    assert candidate['revision'] == record['sourceRevision'] and candidate['clean'] is True
    ci = json.loads(contents['exact-head-ci.log'])
    assert ci['repository']['full_name'] == REPOSITORY
    assert ci['id'] == candidate['ciRunId'] and ci['path'] == '.github/workflows/ci.yml'
    assert ci['head_sha'] == record['sourceRevision'] and ci['head_branch'] == 'main'
    assert ci['event'] == 'push' and ci['status'] == 'completed' and ci['conclusion'] == 'success'
    assert ci['html_url'] == f"https://github.com/{REPOSITORY}/actions/runs/{ci['id']}"
    diagnostic_bytes = contents['source-qualification/g4-tool-package-inventory.json']
    assert digest(diagnostic_bytes) == candidate['inventorySha256']
    diagnostic = json.loads(diagnostic_bytes)
    assert diagnostic['version'] == '4.31.2'  # Observed prior version, local diagnostic only.
    assert diagnostic['configuration'] == 'Release'
    assert len(diagnostic['packages']) == 2 and {row['id'] for row in diagnostic['packages']} == PACKAGE_IDS
    for package in diagnostic['packages']:
        for role in ['nupkg', 'snupkg']:
            artifact = package[role]
            body = contents['source-qualification/' + artifact['file']]
            assert digest(body) == artifact['sha256'].lower() and len(body) == artifact['sizeBytes']
            with zipfile.ZipFile(io.BytesIO(body)) as archive:
                names = [name for name in archive.namelist() if name.endswith('.nuspec')]
                assert len(names) == 1
                spec = ET.fromstring(archive.read(names[0]))
                metadata = next(node for node in spec if node.tag.rsplit('}', 1)[-1] == 'metadata')
                fields = {node.tag.rsplit('}', 1)[-1]: node for node in metadata}
                assert fields['id'].text == package['id'] and fields['version'].text == diagnostic['version']
                assert fields['repository'].attrib['commit'] == record['sourceRevision'], 'diagnostic archive source'
    assert len(diagnostic['qualificationEvidence']) == 55
    assert len({row['file'] for row in diagnostic['qualificationEvidence']}) == 55
    for artifact in diagnostic['qualificationEvidence']:
        body = contents['source-qualification/' + artifact['file']]
        assert digest(body) == artifact['sha256'].lower() and len(body) == artifact['sizeBytes']
    source_command = json.loads(contents[candidate['qualificationCommandFile']])
    assert source_command['exitCode'] == 0
    assert '-RequireControls' in source_command['command'] and '-SkipSourceValidation' not in source_command['command']
    assert digest(contents[source_command['log']]) == source_command['sha256']
    inventory_bytes = contents['release/g4-tool-package-inventory.json']
    inventory = json.loads(inventory_bytes)
    assert digest(inventory_bytes) == record['inventorySha256']
    asset = [row for row in release['assets'] if row['name'] == 'g4-tool-package-inventory.json']
    assert len(asset) == 1 and asset[0]['digest'] == 'sha256:' + digest(inventory_bytes)
    assert inventory['version'] == record['version']
    qualification = inventory['qualification']
    assert diagnostic['qualification'] == qualification
    assert qualification['sourceTree'] == {'clean': True, 'revision': record['sourceRevision']}
    assert qualification['releaseEligible'] is True and qualification['ineligibilityReasons'] == []
    assert qualification['fixtures']['mode'] == 'repository-tracked'
    assert qualification['fixtures']['files'] == prior['qualification']['fixtures']['files']
    for item in qualification['fixtures']['files']:
        body = contents['fixtures/' + item['file']]
        assert digest(body) == item['sha256'].lower(), item['file']
        assert len(body) == item['sizeBytes'], item['file']
    for lane in ['sourceValidation', 'controls']:
        assert qualification[lane] == {'mode': 'executed', 'result': 'passed'}, lane
    assert qualification['packageBuild'] == {'mode': 'official', 'result': 'passed'}
    assert {row['id'] for row in inventory['packages']} == PACKAGE_IDS
    assert len(inventory['packages']) == 2
    workflow_log = contents['release-workflow.log'].decode()
    signed = json.loads(contents['signed-packages.json'])
    assert signed == record['signedPackages']
    assert len(signed['packages']) == 2
    assert {row['id'] for row in signed['packages']} == PACKAGE_IDS
    assert len({row['file'] for row in signed['packages']}) == 2
    for row in signed['packages']:
        assert row['file'] == f"downloads/{row['id'].lower()}.{record['version']}.nupkg"
        body = contents[row['file']]
        assert digest(body) == row['signedSha256']
        original = next(package for package in inventory['packages'] if package['id'] == row['id'])
        assert original['nupkg']['sha256'].lower() == row['qualifiedUnsignedSha256']
        marker = (f"Verified remote '{row['id']}' '{record['version']}' SHA-256 "
                  f"{row['signedSha256']} (qualified local SHA-256 {row['qualifiedUnsignedSha256']}).")
        assert marker.lower() in workflow_log.lower(), 'publisher payload proof: ' + row['id']
        with zipfile.ZipFile(io.BytesIO(body)) as archive:
            assert len(archive.namelist()) == len(set(archive.namelist()))
            assert archive.namelist().count('.signature.p7s') == 1
            actual = {name: digest(archive.read(name)) for name in sorted(archive.namelist())
                      if name != '.signature.p7s'}
            assert actual == row['payloadEntriesSha256']
            names = [name for name in archive.namelist() if name.endswith('.nuspec')]
            assert len(names) == 1
            spec = ET.fromstring(archive.read(names[0]))
            metadata = next(node for node in spec if node.tag.rsplit('}', 1)[-1] == 'metadata')
            fields = {node.tag.rsplit('}', 1)[-1]: node for node in metadata}
            assert fields['id'].text == row['id'] and fields['version'].text == record['version']
            assert fields['repository'].attrib['commit'] == record['sourceRevision']
    for command in record['verificationCommands']:
        assert command['exitCode'] == 0, command['command']
        assert digest(contents[command['log']]) == command['sha256'], command['log']
    signatures = [row for row in record['verificationCommands'] if row['command'][:3] == ['dotnet', 'nuget', 'verify']]
    assert len(signatures) == 2 and all('--all' in row['command'] for row in signatures)
    before = json.loads(contents['before.json'])
    targets = set()
    for command in signatures:
        assert command['cwd'] == before['workspace'] + '/references/Hexalith.Builds'
        matches = [row for row in signed['packages']
                   if before['workspace'] + '/artifacts/story117-builds-publication-20261016/' + row['file'] in command['command']]
        assert len(matches) == 1
        package = matches[0]
        assert package['id'] not in targets
        targets.add(package['id'])
        log = contents[command['log']].decode()
        assert f"Verifying {package['id']}.{record['version']}" in log
        assert 'Signature type: Repository' in log and 'NuGet.org Repository by Microsoft' in log
    assert targets == PACKAGE_IDS
    controls = json.loads(contents['installed-controls.json'])
    assert controls['synthetic'] is True and controls['acceptanceEvidence'] is False
    assert controls['allMatched'] is True and controls['acceptanceControlCount'] == 37
    assert controls['version'] == record['version'] and controls['sourceRevision'] == record['sourceRevision']
    acceptance = [row for row in controls['controls'] if 'fixture' in row]
    assert len(acceptance) == 37
    canonical_bytes = contents['synthetic-expectations-preservation.json']
    assert digest(canonical_bytes) == prior['retainedArtifactBundle']['membersSha256']['verification/synthetic-expectations-preservation.json']
    canonical = json.loads(canonical_bytes)['unchangedExpectationsSha256']
    required = {'test/fixtures/evidence/acceptance/' + name.removesuffix('.expected.json') + '.json'
                for name in canonical}
    assert len(required) == 37 and {row['fixture'] for row in acceptance} == required
    for row in acceptance:
        expected = row['expectation']
        fixture = row['fixture'].removeprefix('test/')
        assert digest(contents[fixture]) == row['fixtureSha256']
        expectation = fixture.removesuffix('.json') + '.expected.json'
        assert digest(contents[expectation]) == row['expectationSha256']
        assert row['expectationSha256'] == canonical[expectation.removeprefix('fixtures/evidence/acceptance/')]
        assert json.loads(contents[expectation]) == expected
        assert row['exitCode'] == expected['exitCode']
        output = contents[row['stdout']['file']]
        assert digest(output) == row['stdout']['sha256']
        assert contents[row['stderr']['file']] == b''
        assert not output.startswith(b'\xef\xbb\xbf') and b'\r' not in output
        assert output.endswith(b'\n') and not output.endswith(b'\n\n')
        result = json.loads(output)
        assert result['status'] == expected['status']
        for name, value in [('exitCode', expected['outcomeExitCode']), ('phase', expected['phase']),
                            ('category', expected['category']), ('ruleId', expected['outcomeRuleId'])]:
            assert type(result['outcome'][name]) is type(value) and result['outcome'][name] == value
        assert [item['ruleId'] for item in result['diagnostics']] == expected['ruleIds']
    proof = json.loads(contents['published-seed-proof.json'])
    assert proof['status'] == 'passed' and len(proof['checks']) == 2
    module = next(row for row in signed['packages'] if row['id'] == 'Hexalith.Builds.Module.Cli')
    assert proof['package']['sha256'].lower() == module['signedSha256']
    with zipfile.ZipFile(io.BytesIO(contents[module['file']])) as archive:
        assert digest(archive.read(proof['seed']['entry'])) == proof['seed']['sha256'].lower()
    assert proof['seed']['loadedSha256'] == proof['seed']['sha256']
    for check, declarative in zip(proof['checks'], [False, True], strict=True):
        assert check['declarative'] is declarative
        assert check['discoveryStatusCode'] == (404 if declarative else 200)
        assert check['receiverPresent'] is True
        for field in ['projectionSubscriptionCount', 'receiverTopicMetadataCount']:
            assert check[field] == (0 if declarative else 1), field
        for field in ['subscriptionPubsubName', 'receiverPubsubName']:
            assert check[field] == (None if declarative else 'configured-seed-events'), field
    for name, expected in record['protectedWorkspaceFilesSha256'].items():
        assert digest((workspace / name).read_bytes()) == expected, name
    platform_bytes = contents['platform-inputs-preservation.json']
    assert digest(platform_bytes) == prior['retainedArtifactBundle']['membersSha256']['verification/platform-inputs-preservation.json']
    platform = json.loads(platform_bytes)['filesSha256']
    assert len(platform) == 4888
    for name, expected in platform.items():
        assert name.startswith('references/Hexalith.Platform/'), name
        assert digest((workspace / name).read_bytes()) == expected, name
    print(f"Verified published Builds {record['version']} at {record['sourceRevision']}, "
          'both signed package identities, 37 exact synthetic controls and both seed modes. '
          'Platform acceptance, staging and Story closure remain open.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--workspace', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--receipt', type=Path)
    args = parser.parse_args()
    workspace = args.workspace.resolve()
    receipt = args.receipt or workspace / '_bmad-output/implementation-artifacts/evidence/story-1-17-builds-publication-2026-10-16.json'
    try:
        verify(receipt.resolve(), workspace)
    except (AssertionError, KeyError, OSError, ValueError, tarfile.TarError, zipfile.BadZipFile, ET.ParseError) as error:
        raise SystemExit(f'Publication evidence verification failed: {error}') from error
