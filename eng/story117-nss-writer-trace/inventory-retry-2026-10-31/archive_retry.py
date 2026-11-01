#!/usr/bin/env python3
"""Create and stream-verify additive metadata evidence; never extract or execute."""
import hashlib
import json
import os
from pathlib import Path
import tarfile

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
RUNTIME=Path('/var/tmp/story117-nss-retry.ubgn0d7i')
ARCHIVE=ROOT/'_bmad-output/implementation-artifacts/evidence/story-1-17-nss-inventory-retry-2026-10-31.artifacts.tar.gz'


def digest_file(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):h.update(block)
    return h.hexdigest()


def save(name,value):
    fd=os.open(HERE/name,os.O_CREAT|os.O_EXCL|os.O_WRONLY|os.O_NOFOLLOW,0o600)
    os.fchown(fd,1000,1000)
    with os.fdopen(fd,'w') as stream:json.dump(value,stream,indent=2);stream.write('\n')


files=[]
for path in RUNTIME.rglob('*'):
    if path.is_file() and not path.is_symlink():files.append((path,'runtime/'+str(path.relative_to(RUNTIME))))
for path in HERE.rglob('*'):
    if path.is_file() and not path.is_symlink():files.append((path,'continuation/'+str(path.relative_to(HERE))))
for name in ['collector.py','test_collector.py','trace.bpf.c','README.md','VALIDATION.md','SPEC.md']:
    files.append((HERE.parent/name,'diagnostic/'+name))
previous=HERE.parent/'live-validation-2026-10-31'
for name in ['REPORT.md','summary.json','preservation-before.json','preservation-after.json','archive.json','capsule-verification.json']:
    files.append((previous/name,'previous-evidence/'+name))
manifest={name:{'bytes':path.stat().st_size,'sha256':digest_file(path)} for path,name in files}
fd=os.open(ARCHIVE,os.O_CREAT|os.O_EXCL|os.O_WRONLY|os.O_NOFOLLOW,0o600)
os.fchown(fd,1000,1000)
with os.fdopen(fd,'wb') as stream:
    with tarfile.open(fileobj=stream,mode='w:gz',compresslevel=6) as archive:
        for path,name in files:archive.add(path,arcname=name,recursive=False)
verified=[]
with tarfile.open(ARCHIVE,'r|gz') as archive:
    for member in archive:
        if not member.isfile() or member.name.startswith('/') or '..' in Path(member.name).parts:
            raise ValueError('Unexpected archive member '+member.name)
        h=hashlib.sha256();size=0
        stream=archive.extractfile(member)
        for block in iter(lambda:stream.read(1024*1024),b''):h.update(block);size+=len(block)
        if member.name not in manifest or manifest[member.name]!={'bytes':size,'sha256':h.hexdigest()}:
            raise ValueError('Member mismatch '+member.name)
        if member.name in verified:raise ValueError('Duplicate member '+member.name)
        verified.append(member.name)
if set(verified)!=set(manifest):raise ValueError('Member set mismatch')
save('capsule.members.json',manifest)
save('capsule-verification.json',{'verified':True,'members':len(verified),'total_uncompressed_file_bytes':sum(v['bytes'] for v in manifest.values()),'extracted':False,'commands_executed':0})
save('archive.json',{'path':str(ARCHIVE.relative_to(ROOT)),'bytes':ARCHIVE.stat().st_size,'sha256':digest_file(ARCHIVE),'members':len(verified),'qualification':'incomplete','aggregate_allocated_capture_seconds':899})
print(json.dumps({'archive':str(ARCHIVE),'bytes':ARCHIVE.stat().st_size,'members':len(verified),'verified':True}))
