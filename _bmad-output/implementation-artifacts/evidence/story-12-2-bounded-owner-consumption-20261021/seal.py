from pathlib import Path
import datetime
import hashlib
import json
import sys

out = Path(__file__).parent
manifest = out / 'SHA256SUMS'
mode = sys.argv[1] if len(sys.argv) > 1 else 'verify'
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
if mode == 'verify':
    expected = dict(line.split('  ', 1)[::-1] for line in manifest.read_text().splitlines())
    actual = {str(p.relative_to(out)): sha(p) for p in sorted(out.rglob('*')) if p.is_file() and p != manifest}
    assert actual == expected, {'unlisted': sorted(actual.keys() - expected.keys()),
        'missing': sorted(expected.keys() - actual.keys()),
        'changed': [p for p, h in expected.items() if p in actual and actual[p] != h]}
    print(json.dumps(dict(verified_files=len(actual), manifest_sha256=sha(manifest))))
else:
    assert mode in ('seal', 'refresh')
    assert mode == 'refresh' or not manifest.exists(), 'Use explicit refresh after adding parent review/audit artifacts.'
    qualification = json.loads((out / 'qualification.json').read_text())
    parent = json.loads((out / 'parent-task36-verification.json').read_text())
    assert parent['verification_passed'] is True, parent
    assert parent['task36_qualified'] is False, parent
    assert qualification['task36_source_consumption_qualified'] is False
    assert qualification['current_checkout_qualified'] is False
    assert qualification['whole_story_accepted'] is False
    (out / 'seal-status.json').write_text(json.dumps(dict(
        observed_at_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        command=['python3', str(Path(__file__)), mode],
        status='Byte-sealed incomplete qualification; sealing does not close Task36 or live/review gates.',
        task36_source_consumption_qualified=False, current_checkout_qualified=False, whole_story_accepted=False), indent=2) + '\n')
    files = [p for p in sorted(out.rglob('*')) if p.is_file() and p != manifest]
    manifest.write_text(''.join(f'{sha(p)}  {p.relative_to(out)}\n' for p in files))
    print(json.dumps(dict(sealed_files=len(files), manifest_sha256=sha(manifest))))
