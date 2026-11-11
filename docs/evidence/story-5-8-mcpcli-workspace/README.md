# Story 5.8 workspace McpCli prerequisite observation

**Result: incomplete; prerequisite gate blocked on 2026-11-11.** This packet
records a read-only inspection of the approved November Story 5.8 spec and both
files in its frontmatter `context`. It supplies no deployed acceptance evidence.
The spec requires stopping dependent work when accepted prerequisites are absent.

The user's approval authorizes the scoped Story 5.8 work. It does not supply
accepted prerequisite delivery or external McpCli enrollment. The manifest was
inspected without alteration; its `held` status is recorded below alongside that
approval. No additional user approval was requested.

## Observed gates

| Gate | Observation | Remaining evidence |
| --- | --- | --- |
| Story 4.19 | Manifest and sprint tracker: backlog; manifest execution held and delivery open. | Accepted deployed durable workspace preparation/lock evidence, including its prerequisites. |
| Stories 5.13 and 5.14 | Manifest and sprint tracker: backlog; manifest execution held and delivery open. | Gateway command admission/authorization/translation and workspace query delivery acceptance. |
| External enrollment | `EXT-MCPCLI-FOLDERS-ENROLLMENT.acceptance_status: pending`; release `pending-platform-publication`. | Accepted enrolled release, exact Contracts version, AD-15 closure, AD-21 Catalog digest/set/type gates and approved execution vectors. |
| Inventory | Approved inventory 1.0.0 explicitly retains every legacy row as `defer / exclude`; C09/C11/C12 and Q22-Q26 are future candidates. | Owner-approved replacement/inclusion revision for the eight workspace operations. |
| Common heads | The observed McpCli production project enrolls only Parties.Contracts and Tenants.Contracts. | Released common CLI/MCP heads enrolling the exact Folders Contracts package. |
| Folders query routing | Only three owner/file `IDomainQueryHandler` registrations occur in the current server registration file. | Story 5.14 workspace inspection routing and acceptance. |

Story 5.12 is marked done in the sprint tracker; its manifest row still says
backlog/held/open. Its owner declaration is a handoff and explicitly leaves live
Gateway readiness, inventory re-disposition and external enrollment unresolved.
This observation neither reopens Story 5.12 nor treats local declarations as
external acceptance. The independently missing 4.19, 5.13, 5.14 and enrollment
evidence blocks Story 5.8 regardless of that tracking drift.

## Binding and limits

The inspection ran from the Folders root on `main`, at
`fb3187bfcc31a03d2febb3a6ddc41532cf3670b6`. The observed McpCli checkout was
`ec458664b0218d07357091b5a5ea12897154db2a`; this differs from the manifest's
historical observation and is not asserted to be an enrolled release.
The spec, epic context and sprint tracker already had local changes before this
inspection. Those changes were preserved.

| Required execution binding | Observed value |
| --- | --- |
| Enrolled McpCli release | Unavailable; manifest says `pending-platform-publication`. |
| Exact enrolled Contracts package | Unavailable; owner handoff describes Contracts 1.3.3, without enrollment proof. |
| Runtime Catalog digest | Unavailable; no enrolled runtime Catalog observed. |
| Accepted inventory | 1.0.0, deferred/excluded scope only; file hash below. |
| Approved execution vector hashes | Unavailable; no accepted enrollment vector set supplied. |
| Selected deployed acceptance environment | Unavailable; only local source inspection performed. |
| Local observation | `2026-11-11T10:06:39Z`; Linux `6.6.87.2-microsoft-standard-WSL2`, x86_64; .NET SDK `10.0.403`. |
| Persisted event/projection observations, host restart and replay | Not executed because the prerequisite gate failed. |

The bounded `mcpcli_operation` schema/generator/oracle changes, process helper,
AppHost project changes and lifecycle matrix tests remain unimplemented. Accepted
enrollment inputs are absent, so generating mappings would invent dispositions.
No code edits, dependency changes, publication, retirement, seeding or simulated
enrollment occurred. No build, xUnit or Aspire acceptance run was performed; no
skipped scenario is counted as a pass. All Story 5.8 acceptance tasks remain open.

When prerequisites are accepted, the execution must preserve the approved 5.12
losses: explicit uppercase ULID caller keys independent of message identity,
McpCli profiles, JSON and operational exit 2. Caller profiles must allow `taskId`
while retaining existing allowed extensions, and admission must reconcile task
payload/extension context. This packet configures no profile or credential.

## Exact read-only prerequisite check

Run from the Folders root. This inspection uses the available Python 3/PyYAML
environment and returns exit 2 for the observed blocked state. Its success branch
only permits further evidence validation; it does not accept a deployment.

```bash
python3 - <<'PY'
from pathlib import Path
import json
import sys
import xml.etree.ElementTree as ET
import yaml
root = Path.cwd()
manifest = yaml.safe_load((root / '_bmad-output/planning-artifacts/planning-story-manifest.yaml').read_text())
stories = {str(row['id']): row for row in manifest['stories']}
for story_id in ['4.19', '5.8', '5.13', '5.14']:
    row = stories[story_id]
    print(f"{story_id}: lifecycle={row['story_lifecycle_status']}; execution={row['execution_authorization_status']}; delivery={row['delivery_evidence_status']}")
enrollment = manifest['external_nodes']['EXT-MCPCLI-FOLDERS-ENROLLMENT']
print(f"enrollment: acceptance={enrollment['acceptance_status']}; release={enrollment['release_version_or_commit']}")
inventory_text = (root / enrollment['evidence_paths']['inventory']).read_text()
inventory = yaml.safe_load(inventory_text.split('---', 2)[1])
print(f"inventory: version={inventory['inventory_version']}; all legacy rows defer/exclude={'Every current legacy row is **defer / exclude**' in inventory_text}")
project = ET.parse(root / 'references/Hexalith.McpCli/src/Hexalith.McpCli/Hexalith.McpCli.csproj')
modules = sorted(row.attrib['Include'] for row in project.findall('.//PackageReference') if row.attrib.get('HexalithContracts') == 'true')
print('enrolled Contracts: ' + ', '.join(modules))
blocked = any(stories[key]['delivery_evidence_status'] != 'accepted' for key in ['4.19', '5.13', '5.14']) or enrollment['acceptance_status'] != 'accepted' or 'Hexalith.Folders.Contracts' not in modules
print('Story 5.8 prerequisite gate: ' + ('BLOCKED; dependent work stopped; completion remains incomplete' if blocked else 'READY FOR FURTHER EVIDENCE VALIDATION'))
sys.exit(2 if blocked else 0)
PY
```

Exit code: **2**. Output:

```text
4.19: lifecycle=backlog; execution=held; delivery=open
5.8: lifecycle=backlog; execution=held; delivery=open
5.13: lifecycle=backlog; execution=held; delivery=open
5.14: lifecycle=backlog; execution=held; delivery=open
enrollment: acceptance=pending; release=pending-platform-publication
inventory: version=1.0.0; all legacy rows defer/exclude=True
enrolled Contracts: Hexalith.Parties.Contracts, Hexalith.Tenants.Contracts
Story 5.8 prerequisite gate: BLOCKED; dependent work stopped; completion remains incomplete
```

An initial invocation with `python` returned exit 127,
`/bin/bash: line 1: python: command not found`; the exact successful inspection
above uses `python3` and makes no environment or dependency change.

The following supplemental query-registration check returned exit 0:

```bash
rg -n 'IDomainQueryHandler' src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs
```

```text
81:        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDomainQueryHandler, Queries.FolderOwnerReadQueryHandler>());
83:        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDomainQueryHandler, Queries.FolderFileOwnerReadQueryHandler>());
84:        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDomainQueryHandler, Queries.FolderFileReferenceReadQueryHandler>());
```

## Input digests

`sha256sum` over the following paths returned exit 0 and these exact digests:

```text
1d8c6be38d22fce84c3775afe4dfc3ad1d71ac49e465536542c9bf3c008bffd9  _bmad-output/planning-artifacts/planning-story-manifest.yaml
c9a49953e60c2869bfefb402921508c6333b138b500ca5f4c018e56c9fb2910b  _bmad-output/implementation-artifacts/epic-5-context.md
9ba0063e90fb82b95aa88b8718183d10ba5bee35faeecf62ba902959599dd480  docs/contract/mcpcli-folders-operation-declarations.md
57cf556b8051921bf2bf8a17abe3b82b7d0a3ba8ba5593072f52c12f85650e0b  references/Hexalith.McpCli/_bmad-output/implementation-artifacts/folders-migration-inventory-v1.md
181b26acedfec3526eb22579bfff1329287515511a9302623c48485f2af386fb  references/Hexalith.McpCli/src/Hexalith.McpCli/Hexalith.McpCli.csproj
d7f8b858f8f00107cdb231812427b463110d40bbb903e7573d21ddc48a7f110f  src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs
ad89298e54ffec382ff8be6482062a4842500d7a524e87f63e81e35a1fe7119c  global.json
```

## Evidence disclosure check

Only repository metadata, source declarations and gate statuses were retained.
No authenticated request/result capture or credential file was read. A local
pattern scan of this packet checks private-key headers, common token formats,
authorization bearer values and credential assignments; its exact command and
result are retained below. This limited scan is not the enrolled release's
secret-scan/acceptance gate.

```bash
python3 - <<'PY'
from pathlib import Path
import re
path = Path('docs/evidence/story-5-8-mcpcli-workspace/README.md')
patterns = [
    r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',
    r'gh[pousr]_[A-Za-z0-9]{30,}',
    r'github_pat_[A-Za-z0-9_]{40,}',
    r'(?i)authorization\s*:\s*bearer\s+[A-Za-z0-9._~+/=-]{12,}',
    r'(?i)(?:password|secret|access_token)\s*[:=]\s*[\x22\x27]?[A-Za-z0-9/+]{12,}',
]
findings = sum(len(re.findall(pattern, path.read_text())) for pattern in patterns)
print(f'Local evidence pattern scan: {findings} findings')
raise SystemExit(1 if findings else 0)
PY
```

Exit code: **0**. Output: `Local evidence pattern scan: 0 findings`.
