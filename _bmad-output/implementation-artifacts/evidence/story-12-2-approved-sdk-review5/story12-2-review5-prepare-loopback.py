from pathlib import Path
import json,hashlib,subprocess,tempfile,shutil,re
root=Path('/home/administrator/projects/hexalith/folders');sdk=root/'references/Hexalith.EventStore';base=root/'_bmad-output/implementation-artifacts';owner=sdk/'_bmad-output/implementation-artifacts/spec-folders-12-2-expiry-and-scratch-recovery.md';recpath=base/'review-12-2-approved-sdk-round5-20261020.json';record=json.loads(recpath.read_text());sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
# Classifications exist before grouping; eight independent actual roots, no deduplication.
assert len(record['findings'])==12 and record['groups'] is None
groups=[dict(id='R5-G'+str(i+1),members=[ident],route=next(x['route'] for x in record['findings'] if x['id']==ident)) for i,ident in enumerate(['R5-B1','R5-B2','R5-B3','R5-B4','R5-B7','R5-B10','R5-E1','R5-V1'])];record['groups']=groups
s=owner.read_text();assert 'review_loop_iteration: 4' in s and 'status: in-review' in s
frozen=s[s.index('<frozen-after-approval'):s.index('</frozen-after-approval>')+len('</frozen-after-approval>')]
# Capture exactly reviewed ownership, not all differences introduced by a concurrent external merge.
v=json.loads(Path('/tmp/story12-2-review5-external-owned-overlaps.json').read_text());plans=json.loads(Path('/tmp/story12-2-review5-external-restore-plan.json').read_text());planmap={x['path']:x for x in plans['files']}
for x in plans['files']:assert sha(root/x['path'])==x['current_sha256'],x['path']+' changed since merge planning'
# Resolve the single text overlap by retaining the incoming single-owner validation.
for x in plans['files']:
 if x['merge_exit']:
  p=Path(x['plan']);text=p.read_text();m=re.search(r'^<<<<<<< .*\n(.*?)^=======\n.*?^>>>>>>> .*\n',text,re.M|re.S);assert m
  incoming=m.group(1).replace(' && marker.ScratchCompensationKey is null','')
  text=text[:m.start()]+incoming+text[m.end():];assert '<<<<<<<' not in text;p.write_text(text);x['merge_exit']=0;x['resolution']='Preserved incoming single-owner boundary guard; removed only this story\'s legacy-compensation/expiry additions during ordinary restore. Final positive snapshot retains both.'
# Snapshot all92 owned files before any ordinary restoration.
keep=Path(tempfile.mkdtemp(prefix='story12-2-sdk-review5-keep-'));records=[]
for repo,baseline,paths,prefix in [(sdk,'d4d05f871991ec2af305267f59dcefeae3f127ed',[x.removeprefix('references/Hexalith.EventStore/') for x in v['sdk_owned']],Path('references/Hexalith.EventStore')),(root,'ec3523ada1b32362bf9f31c555b95c312b8df4b1',v['root_owned'],Path())]:
 for rel in sorted(paths):
  p=repo/rel;assert p.is_file(),rel;combined=str(prefix/rel);dest=keep/'approved-working-tree'/prefix/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest);b=subprocess.run(['git','show',baseline+':'+rel],cwd=repo,capture_output=True)
  restore=Path(planmap[combined]['plan']).read_bytes() if combined in planmap else b.stdout if b.returncode==0 else None
  rpath=keep/'ordinary-restore-tree'/prefix/rel
  if restore is not None:rpath.parent.mkdir(parents=True,exist_ok=True);rpath.write_bytes(restore)
  records.append(dict(repository=str(repo),path=rel,baseline=baseline,baseline_exists=b.returncode==0,sha256=sha(p),snapshot=str(dest),restore_path=str(rpath) if restore is not None else None,restore_sha256=hashlib.sha256(restore).hexdigest() if restore is not None else None,external_overlap=combined in planmap))
(keep/'files.json').write_text(json.dumps(records,indent=2)+'\n');(keep/'external-restore-plan.json').write_text(json.dumps(plans,indent=2)+'\n')
old=Path('/tmp/story12-2-sdk-review4-keep-111x6kq_/README.md').read_text();(keep/'README.md').write_text('''# Positive preservation for SDK review loop5

Hash-verify and positively reuse ALL78 SDK source/test/guide files in approved-working-tree, then correct
review5. Parent owns all14 Folders files and restores them only after final SDK handoff. Never copy
planning/evidence or Git metadata from here. All earlier KEEP/Spec Change Log constraints remain binding.

CURRENT HISTORICAL QUALIFICATION: SDK review4 has315 artifacts,3838 source inputs, Client1828+2 optional
skips, Domain1061,66 unique required live cases,20 workers/20PIDs/10 exited pairs (six production named,
four primitive late-installer epoch controls). Parent independently audited31PDBs/5272documents, raw named
Open/Aborted states, all matrix/XML names and eight zero-key fixture namespaces. Folders Stage10 packet
has49 artifacts,1663 source inputs,62assembly/PDB files,4201 passes+2 optional Domain skips, live1/1 zero
skips in334.315s; six actual root/Server/manual x selected/inherited clock cases first RED then GREEN.
KEEP fixed consumer clock registration, original public constructor, internal adapter and all14 files.
Current historical p95: acknowledgement4784.157ms (over1000),Workers601.608ms,status120.761ms; all earlier
observations/failed live attempts remain retained and unattributed. All TEN canonical stages are exact.

CONCURRENT USER CHANGES MUST SURVIVE: another session committed/merged source while review5 ran.
SDK head c9c56cbc041d56b46721d35b933615e00591548e; root182fbddf6ab81ac32b57f511c81708d3ebb9693f.
The positive snapshot includes three incoming SDK hunks (custom owner-selection key; retained single-owner
selection allowance; strict single-owner anchor validation) and three new query-handler registration
lines in the root Server extension. These four files were ordinary-restored by three-way blob merging,
not blindly replaced. All other incoming source/config/test files are untouched, including SDK globals,
.gitattributes, owner/source authority changes and Folders file-identity/query features. The original
baseline commits are preserved only as ownership origins; do not restore or replace incoming changes.
Use CURRENT repository configuration and qualify the integrated current source; old Stage10 binaries
are historical only. Do not assume old total test/source counts still describe merged source. If new
unrelated changes appear, preserve them. No Git/index/history/gitlink/dependency mutation is authorized.

KEEP review4's validated retained participant proofs, distinct Completed/Aborted fences, exact-scope
indexes, physical own expired-envelope settlement, bounded legacy compensation, accepted-owner retired
inventory and actual repeated named Abort reconciliation after terminal cleanup flags. Keep full pending
and accepted cases, component/live metadata guards and zero required skips. Fixes must preserve all
existing foreign/newer/tenant/epoch/operation/CAS guards, physical TTL and absence, ordinary delivery and
fresh named rebuild, not merely primitive epoch cycling. Documentation must keep full tenant metadata
retention/legal-hold/offboarding/restore/drained-writer purge disposition. Production Redis stays
Resumable; forced TransactionQualified partial writes/refusal are unsupported, not atomic rollback.

The following older positive instructions remain historical constraints, with the current facts above
superseding their then-current counters and snapshot pointers:

'''+old)
# Full SHA guard before any repository code write.
for x in records:assert sha(Path(x['snapshot']))==x['sha256'] and sha(Path(x['repository'])/x['path'])==x['sha256']
for x in records:
 p=Path(x['repository'])/x['path']
 if x['restore_path']:
  q=Path(x['restore_path']);assert sha(q)==x['restore_sha256'];p.write_bytes(q.read_bytes())
 else:p.unlink()
Path('/tmp/story12-2-review5-keep-path.json').write_text(json.dumps(dict(path=str(keep),sdk_files=78,root_files=14,records=len(records)),indent=2)+'\n')
# Reopen nonfrozen implementation only; keep frozen intent and ownership origins unchanged.
s=s.replace('review_loop_iteration: 4','review_loop_iteration: 5',1).replace('status: in-review','status: in-progress',1)
s+='\n\nRoot-cause groups after all twelve individual verdicts: eight distinct roots, one per surviving finding.\nB1/B7/E1 route bad_spec; B2/B3/B4/B10/V1 direct patches survive as explicit requirements in loop5.\nB5/B6/B8/B9 are rejected on their individual refutations. No intent gap, new approval or defer is needed.\n'
start=s.index('## Tasks & Acceptance');end=s.index('## Review-one implementation corrections',start);s=s[:start]+s[start:end].replace('- [x]','- [ ]')+s[end:]
requirements=f'''## Review-five implementation corrections

Parent classified all twelve final reports individually before grouping. Positively reuse every file in
`{keep}/approved-working-tree/references/Hexalith.EventStore` after SHA verification. Read its README and
files.json first; all78 SDK files and14 parent-owned consumer files were preserved. Three incoming SDK
hunks and one Folders file overlap an external user merge. Their external changes survive the ordinary
three-way restoration; ALL unrelated merged source/config/tests remain untouched. Original frontmatter
baseline d4d05 is an ownership origin, not permission to revert the incoming merged features. The
parent's user-change preservation rule takes precedence over a blind baseline reset. Do not edit Folders.
Use current owning guidance/configuration, including incoming globals/.gitattributes and current tests.
Keep all prior successful behavior, metadata/compatibility decisions, typed owner guards and tests.

- [ ] Make same-action retained scratch retry converge after a benign competing owner advances the
  shared fence ETag. The parent public-coordinator probe pauses A after ordinal0, completes distinct
  owner/key/batch B, then A compensates false and every identical A retry stays false because its
  retained Aborted deterministic identity cannot be reused. Reconcile only proven original terminal
  work and derive/recover/reclaim any necessary bounded deterministic SDK attempt identity coherently.
  Preserve immutable decisions, current operation/epoch/tenant, original concurrency, late old-worker
  and provider ETag-ABA fences. No random per-retry IDs, deleted safety proof, public scratch API or
  consumer protocol. Test original and replacement attempts, repeated competing conflicts, current
  same-action retry, Abort and late original installers, including legacy replacement coexistence;
  actual production named multi-owner Accumulate retry, subsequent ordinary delivery and fresh rebuild
  must work. Qualify the relevant competing-writer race over real Dapr/Redis with zero required skips.
- [ ] Preserve lawful legacy retirement at maximum accepted scope/key escaping. A validated512-byte
  component scope produces a56025-byte two-anchor version1 fence, above its16384-byte limit. Derive
  bounded physical allowances from ALL accepted inputs and apply them coherently to seal/read/discovery
  and compatibility. Keep original100-operation/one-MiB manifest, logical-model, participant-proof and
  canonical validation limits. Add meaningful full legacy recovery regressions at the supported bound,
  malformed/oversized refusal, repeated settlement and provider coverage, not only a private helper test.
- [ ] Validate a logical-key discovery's original complete scope before writing terminal fence/index
  metadata, including terminal receipts with no RecoveryAnchor. The foreign-tenant probe currently
  writes two auxiliary keys then refuses. Keep exact-marker discovery and authentic same-boundary
  discovery working; invalid tenant/store/domain/owner/projection/batch evidence must leave ALL original
  payload, marker, manifest, participant proofs and auxiliary bytes unchanged. Reuse authoritative guards.
- [ ] Make participant-proof anchor matching reject invalid concurrency enum and ETag digest using the
  same canonical typed validation as ReadAsync. Parent public recovery currently resolves a valid terminal
  anchor with mode999/digest malformed. Validate BEFORE auxiliary writes, manifest reclamation or model
  compaction. Preserve valid modes/original guards, digest shape, duplicates/canonical/scope/anchor bounds;
  add actual recovery tests, including unchanged original/auxiliary bytes on malformed evidence.
- [ ] Apply the authoritative canonical ScratchCompensationKey(scopeHash) guard to terminal legacy
  recovery before reading unrelated proof or mutating private data. Parent supplies unrelated-key with
  matching terminal anchors and recovery currently resolves/deletes original envelopes. Keep valid
  resumed legacy compensation, absent already-reclaimed proof and late-worker safety; malformed evidence
  must fail closed without ANY original/foreign/auxiliary mutation.
- [ ] Missing retired owner inventory after cleanup flags advance must produce an explicit unavailable
  or refused cleanup disposition when original evidence is insufficient. Parent production named
  Begin/Accumulate/Abort then missing inventory plus captured late original chunk returns AlreadyCompleted
  with physical scratch still present. Never infer unknown owner counts/chunks, enumerate unrelated keys,
  delete arbitrary private data, or regress the exact Open/selected epoch. Preserve incoming/legacy
  compatibility where provable original inventory remains; cover historical never-created and unavailable/
  corrupt inventory, replacement coordinators, repeated Abort, valid late cleanup and subsequent growth.
- [ ] Extend terminal accepted-owner late-installer qualification to streamed transport fragments/parts
  and the corresponding retained discovery counts, beyond the existing chunk-only case. Use actual named
  production dispatch to accept owner, Abort through terminal cleanup flags, reinstate captured exact
  authenticated original fragment (and relevant streamed/hash-page proof path), replace coordinator,
  repeat Abort, assert PHYSICAL absence plus exact Open preservation, ordinary delivery and fresh named
  rebuild without manual recovery/private-delete calls. Include meaningful component and real-provider
  coverage so deleting the new terminal transport discovery loop fails executed acceptance.
- [ ] Report budget-expired only when the shared maintenance budget token actually cancelled. An
  independent dependency OperationCanceledException at197ms under30000ms currently emits budget-expired.
  Correct the existing closed diagnostic reason without changing budget/gate/retry semantics or public
  surface. Execute separate dependency-cancel and actual budget-expiry cases, and assert emitted tags
  outside exporter callbacks. Preserve bounded scheduled work, caller-only wait cancellation, safe
  exporter failure and successful foreground cleanup/retry.

B5's mixed explicit2020 business/default System store probe is rejected: use one coherently configured
clock as documented; do not mutate opaque externally supplied stores or add public clock enforcement.
B6's foreign Prepared refusal converges when its owner settles, as the parent same-profile Abort control
proves. Preserve that refusal; do not help/delete/classify an undecided foreign envelope as final conflict.
B8 requires no new capacity/progress/performance redesign; cooperative cancellation refuses success and
later noncancelled work revisits all potentially late owners. B9 adds no generic maintenance-budget API:
per-batch ambiguous-dispatch timeout and the bounded generic maintenance fallback remain distinct.

Requalify CURRENT merged Debug/source with exact configured SDK/tool versions; record blockers rather
than altering dependencies/globals. Run full affected Client and serialized Domain suites and all required
live classes/process workers, preserve final-source/PE/PDB/API/XML/matrix provenance, raw TTL/private and
session state, real named behavior and scoped cleanup. Derive counts from final XML, not historical totals.
Keep all earlier failed attempts and every sealed packet; write additive `qualification-review5` only.
All ten Folders canonical stages stay immutable; parent later restores all14 consumer files and qualifies
integrated source, with public API/clock fix preserved. All external applications remain untouched; strip
secret query parameters from CLI URLs before storing/printing. No Git, dependency/pin, release, nested
submodule, public v2, content cleanup, admission/performance redesign or separate >10000 identity work.

'''
s=s.replace('## Verification\n',requirements+'## Verification\n',1)
entry=f'''- 2026-10-20, review loop5: all three final reports arrived before twelve individual verdicts. Added
  retained CAS-conflict retry convergence, full lawful escaped legacy fence capacity and missing historical
  owner-inventory refusal. Retained five direct corrections: pre-write original scope validation, typed
  participant proof validation, canonical legacy compensation pointer, accurate cancellation diagnostics,
  and terminal transport late-installer qualification. Known-bad states are permanently poisoned retry
  identities, refused legal legacy recovery, cross-boundary auxiliary writes, malformed-proof success,
  unsafe noncanonical recovery, false terminal cleanup and misclassified dependency cancellation.
  B5/B6/B8/B9 are rejected on code/contracts and the independent controls; no frozen amendment or defer.
  KEEP every earlier constraint and all92 current owned files in `{keep}/README.md`.
  Stage10/SDKreview4 are historical exact-source packets; ALL ten canonical stages and all previous raw
  failures/packets remain unchanged. Concurrent external commits merged source after qualification:
  preserve current incoming SDK authority/selection and Folders identity/query features. Only the78 SDK
  and14 root owned files are ordinary-restored, using three-way blobs for four overlaps; do not blindly
  restore d4d05/ec352 or unrelated files. Git/history/index, recorded gitlink, globals/dependencies and
  external commits are preserved. Current integrated source must be freshly qualified with truthful counts.
  Parent owns consumer adoption; SDK child must not edit Folders. Approval/scope exclusions remain unchanged.

'''
s=s.replace('## Spec Change Log\n','## Spec Change Log\n\n'+entry,1);assert s[s.index('<frozen-after-approval'):s.index('</frozen-after-approval>')+len('</frozen-after-approval>')]==frozen;owner.write_text(s)
record.update(positive_preservation=str(keep),owner_review_loop_iteration=5,external_merge_preserved=plans,external_source_audit='/tmp/story12-2-review5-concurrent-recording-source-audit.json');recpath.write_text(json.dumps(record,indent=2)+'\n')
# Preserve ten canonical stages and reopen only current nonfrozen owner acceptance.
canonical=base/'evidence/story-12-2-durability.json';d=json.loads(canonical.read_text());assert len(d['stages'])==10;guard=dict(stages=d['stages'],stage_sha256=[hashlib.sha256(json.dumps(x,sort_keys=True,separators=(',',':')).encode()).hexdigest() for x in d['stages']]);Path('/tmp/story12-2-review5-canonical-stage-guard.json').write_text(json.dumps(guard,indent=2)+'\n');d['status']='SDK review5: loop5 corrections in progress over preserved incoming merged source; Stage10 is historical qualification.';d['full_acceptance']=False;d['approved_sdk_review5']=dict(status='implementation-loop5',record=str(recpath.relative_to(root)),preserved_source=str(keep),historical_stages_preserved=10,external_merge_preserved=True);canonical.write_text(json.dumps(d,indent=2)+'\n');assert d['stages']==guard['stages']
main=base/'spec-12-2-durable-projections-and-task-completion-pipeline.md';m=main.read_text();f=m[m.index('<frozen-after-approval'):m.index('</frozen-after-approval>')+len('</frozen-after-approval>')]
for old,new in [('- [x] Round-four retention (frozen decision):','- [ ] Round-four retention (frozen decision):'),('- [x] Round-four tests:','- [ ] Round-four tests:'),('  - [x] Named rebuild persists rows;','  - [ ] Named rebuild persists rows;')]:assert old in m;m=m.replace(old,new,1)
m=m.replace('## Current Stage10 approved SDK source and clock qualification','## Historical Stage10 approved SDK source and clock qualification',1)
m+='''\n\n## Approved SDK review5 follow-up\n\nIndependent scope review identified retained competing-owner CAS retry nonconvergence, lawful legacy\nretirement size refusal, original scope/proof/pointer validation gaps, missing historical owner inventory\nbeing treated as cleanup success, dependency cancellation misclassification and a late transport-fragment\ncoverage gap. Owner loop5 addresses the eight roots without changing frozen intent or approval. All14\nFolders source/test/docs files, including the passing root/Server/manual clock fix, are positively preserved\nfor parent restoration after SDK handoff. All ten canonical stages and prior sealed packets stay exact.\nAnother session merged source changes during review; they are preserved, including external SDK authority/\nselection and Folders file-identity/query changes. Current merged source differs from historical Stage10,\nso it needs fresh source/PE/PDB/component/full live qualification. Do not reuse old counts as current results.\nAlready-approved separately owned >10000 identity consumption, package/PD10, performance and whole-story\nreview remain open. No Git/release/pin/dependency/content/performance redesign is authorized.\n''';assert m[m.index('<frozen-after-approval'):m.index('</frozen-after-approval>')+len('</frozen-after-approval>')]==f;main.write_text(m)
proposal=base/'proposal-12-2-sdk-expiry-and-scratch-recovery.md';proposal.write_text(proposal.read_text()+'''\n\nReview5 reopens the approved prerequisites for bounded retry/validation/cleanup corrections. Stage10 is\nhistorical exact-source qualification; concurrent user merges are preserved and current integrated source\nwill be qualified separately. Existing approval, separate identity ownership and release/Git boundaries\nare unchanged; no renewed approval is needed for these corrections within the frozen intent.\n''')
print(json.dumps(dict(keep=str(keep),sdk_files=78,root_files=14,ordinary_restored=len(records),three_way_overlaps=4,owner_loop=5,git_mutations=False),indent=2))
