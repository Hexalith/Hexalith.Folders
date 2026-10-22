from pathlib import Path
import datetime, hashlib, json, subprocess

out = Path(__file__).resolve().parent
freeze = json.loads((out / 'extended-source-runtime-freeze.json').read_text())
errors = []
for group in ('files_sha256', 'assemblies_sha256'):
    for name, expected in freeze[group].items():
        path = Path(name)
        actual = hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None
        if actual != expected:
            errors.append(dict(group=group, path=name, expected=expected, actual=actual))
historical = json.loads((out / 'historical-evidence-freeze.json').read_text())
historical_errors = []
for name, expected in historical.items():
    path = Path(name)
    actual = hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None
    if actual != expected:
        historical_errors.append(dict(path=name, expected=expected, actual=actual))
result = dict(
    utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    source_config_inputs=len(freeze['files_sha256']),
    runtime_artifacts=len(freeze['assemblies_sha256']),
    errors=errors,
    historical_files=len(historical),
    historical_errors=historical_errors,
    passed=not errors and not historical_errors,
)
(out / 'final-preservation-audit.json').write_text(json.dumps(result, indent=2) + '\n')
print({key: len(value) if isinstance(value, list) else value for key, value in result.items()})
assert result['passed']
root = Path('/home/administrator/projects/hexalith/folders')
repositories = {}
for name, path in [('folders', root), ('eventstore', root.parent / 'eventstore')]:
    repositories[name] = {
        key: subprocess.check_output(['git', *arguments], cwd=path, text=True).strip()
        for key, arguments in [('head', ['rev-parse', 'HEAD']), ('status', ['status', '--short'])]
    }
(out / 'final-repositories.json').write_text(json.dumps(repositories, indent=2) + '\n')
