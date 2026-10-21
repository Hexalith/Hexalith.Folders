"""Parent verification of the incomplete Task 36 evidence and exact frozen candidate."""
from pathlib import Path
import datetime
import hashlib
import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path('/home/administrator/projects/hexalith/folders')
PACKET = Path(__file__).resolve().parent.parent
read = lambda name: json.loads((PACKET / name).read_text())
digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None
freeze = read('source-freeze.json')
archive = read('final-build7-byte-archive.json')
qualification = read('qualification.json')
errors, source_drift, binary_drift = [], [], []
for relative, expected in freeze['files_sha256'].items():
    if digest(ROOT / relative) == expected:
        continue
    candidates = [PACKET / 'external-drift-original-inputs' / expected]
    if relative in archive:
        candidates.append(PACKET / archive[relative]['archive'])
    recovered = next((path for path in candidates if digest(path) == expected), None)
    source_drift.append({'path': relative, 'expected': expected,
                         'originalArchive': str(recovered.relative_to(PACKET)) if recovered else None})
    if recovered is None:
        errors.append('Missing exact original input: ' + relative)
for relative, expected in freeze['assemblies_sha256'].items():
    if digest(ROOT / relative) != expected:
        binary_drift.append(relative)
    if relative not in archive or digest(PACKET / archive[relative]['archive']) != expected:
        errors.append('Missing exact original PE/PDB archive: ' + relative)
totals = {'passed': 0, 'failed': 0, 'skipped': 0}
for lane, info in qualification['components']['lanes'].items():
    if 'xml' not in info:
        text = (PACKET / info['log']).read_text()
        for key, label in [('passed', 'succeeded'), ('failed', 'failed'), ('skipped', 'skipped')]:
            match = re.search(r'^\s*' + label + r':\s*(\d+)\s*$', text, re.MULTILINE)
            observed = int(match.group(1)) if match else None
            if observed != info[key]:
                errors.append('Integration summary mismatch: ' + key)
            totals[key] += observed or 0
        continue
    assemblies = list(ET.parse(PACKET / info['xml']).getroot().iter('assembly'))
    for key in totals:
        observed = sum(int(a.get(key, '0')) for a in assemblies)
        if observed != info[key]:
            errors.append(lane + ': count mismatch ' + key)
        totals[key] += observed
    large_cases = [test for a in assemblies for test in a.iter('test')
                   if test.get('method') == 'CompleteOriginalOwnersRebuildGrowAndPreserveHistoricalTasksWithBoundedValues']
    if lane == 'domain' and (len(large_cases) != 2 or any(t.get('result') != 'Pass' for t in large_cases)):
        errors.append('Required large-owner component cases did not both pass')
spec_path = '_bmad-output/implementation-artifacts/spec-12-2-durable-projections-and-task-completion-pipeline.md'
spec = (ROOT / spec_path).read_text()
old = subprocess.check_output(['git', 'show', freeze['root_head'] + ':' + spec_path], cwd=ROOT, text=True)
frozen = lambda text: re.search(r'<frozen-after-approval[\s\S]*?</frozen-after-approval>', text).group(0)
checks = {
    'frozen_intent_unchanged': frozen(spec) == frozen(old),
    'original_baseline_preserved': "baseline_commit: 'c22b13137202f7bf8c60b80c095a69748435bd6e'" in spec,
    'story_in_progress': "status: 'in-progress'" in spec,
    'task36_unchecked': '- [ ] Task 36' in spec,
    'no_task36_acceptance_claim': qualification['task36_source_consumption_qualified'] is False,
    'no_current_checkout_claim': qualification['current_checkout_qualified'] is False,
    'no_whole_story_claim': qualification['whole_story_accepted'] is False,
    'sdk_broad_failure_retained': qualification['sdk_reuse']['sdk_broad_gate_qualified'] is False,
    'no_live_acceptance_pass': qualification['live']['complete_case_passes'] == 0,
    'all_frozen_binaries_unchanged': not binary_drift,
    'regression_counts': totals == {'passed': 4249, 'failed': 0, 'skipped': 2},
}
errors.extend(key for key, value in checks.items() if not value)
result = {'command': [sys.executable, str(Path(__file__).resolve())],
          'observed_at_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
          'scope': 'Parent verification of exact frozen candidate and honest incomplete disposition; no independent review or acceptance pass.',
          'source_input_count': len(freeze['files_sha256']), 'source_drift': source_drift,
          'binary_count': len(freeze['assemblies_sha256']), 'binary_drift': binary_drift,
          'executed_component_counts': totals, 'checks': checks, 'errors': errors,
          'verification_passed': not errors, 'task36_qualified': False}
(PACKET / 'parent-task36-verification.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({key: value for key, value in result.items() if key != 'source_drift'}, indent=2))
sys.exit(0 if not errors else 1)
