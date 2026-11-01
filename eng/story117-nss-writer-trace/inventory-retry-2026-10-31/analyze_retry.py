#!/usr/bin/env python3
"""Read retained metadata only; never execute archive or fixture commands."""
import collections
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys

HERE=Path(__file__).resolve().parent
RUNTIME=Path(sys.argv[1])
sys.path.insert(0,str(HERE.parent))
import collector as c


def load(path): return json.loads(path.read_text())


def save(path,value):
    path.parent.mkdir(mode=0o700,parents=True,exist_ok=True)
    path.parent.chown(1000,1000) if hasattr(path.parent,'chown') else None
    fd=os.open(path,os.O_CREAT|os.O_EXCL|os.O_WRONLY|os.O_NOFOLLOW,0o600)
    os.fchown(fd,1000,1000)
    with os.fdopen(fd,'w') as stream:json.dump(value,stream,indent=2);stream.write('\n')


def task_key(row):return tuple(row[k] for k in ('pid','tid','group_leader_start_ticks','task_start_ticks'))


def inventory(path,fixture=None):
    counts=collections.Counter();errors=[];bad_refs=[];descriptors={};maps=set();tasks=set();seen={}
    if not path.exists():return {'present':False}
    for line in path.open():
        row=json.loads(line);kind=row['kind'];counts[kind]+=1
        if kind=='snapshot_error':errors.append(row)
        if kind=='task':tasks.add(task_key(row))
        if kind=='shared_mapping_inventory' and row['complete']:
            maps.add((task_key(row),row['started_monotonic_ns'],row['completed_monotonic_ns']))
        if kind=='descriptor':descriptors[(task_key(row),row['fd'],row['observation_id'],row['observed_monotonic_ns'])]=row
        if kind=='descriptor_reference':
            ref=row['reference'];key=(task_key(ref),ref['fd'],ref['observation_id'],ref['observed_monotonic_ns'])
            if key not in descriptors:bad_refs.append({'kind':kind,'reference':ref})
            else:row={**descriptors[key],**row}
        if kind in ('descriptor_table_reference','shared_mapping_reference'):
            ref=row['reference'];source=(task_key(ref),ref['started_monotonic_ns'],ref['completed_monotonic_ns'])
            if (task_key(ref) not in tasks or task_key(row) not in tasks or
                    not ref['completed_monotonic_ns'] <= row['proof_before_monotonic_ns'] <= row['proof_after_monotonic_ns'] <= row['observed_monotonic_ns'] or
                    (kind=='shared_mapping_reference' and source not in maps)):
                bad_refs.append({'kind':kind,'reference':ref})
        if fixture and kind in ('descriptor','descriptor_reference'):
            owner=fixture['identity'];private=fixture['thread_private']
            if row['pid']==owner['pid'] and row['group_leader_start_ticks']==owner['start_ticks']:
                if row['tid']==owner['tid'] and row['fd']==fixture['preexisting_fd'] and row['metadata']['inode']==fixture['file']['inode']:
                    seen['preexisting_descriptor']=True
                if private.get('unshared') and row['tid']==private['identity']['tid'] and row['task_start_ticks']==private['identity']['task_start_ticks'] and row['fd']==private['fd'] and row['metadata']['inode']==private['file']['inode']:
                    seen['private_thread_descriptor']=True
    return {'present':True,'bytes':path.stat().st_size,'record_counts':counts,'errors':errors,
            'reference_validation_errors':bad_refs,'fixture_inventory_observations':seen}


budget=load(RUNTIME/'budget.json');attempts=[];uid_counts=collections.Counter();operations=collections.Counter();threads=0;deadlines=0;identities=set();parents=[];fork_creators=0
for allocated in budget['attempts']:
    name=allocated['name'];root=RUNTIME/name;capture=root/'capture';metadata=load(capture/'metadata.json');receipt=load(capture/'receipt.json');execution=load(root/'execution.json')
    handshake=load(root/'handshake.json') if (root/'handshake.json').exists() else None
    fixture=load(root/'fixture-before.json') if (root/'fixture-before.json').exists() else None
    before=inventory(capture/'before.jsonl',fixture);after=inventory(capture/'after.jsonl',fixture)
    local_ops=collections.Counter();local_uids=collections.Counter();event_count=0;violations=0;fixture_targets=0
    events_path=capture/'events.jsonl'
    if events_path.exists():
        for line in events_path.open():
            event=json.loads(line);event_count+=1;local_ops[event['operation']]+=1;identity=event['process'];local_uids[identity['uid']]+=1
            threads+=identity['tid']!=identity['pid'];identities.add((identity['pid'],identity['group_leader_start_boottime_ns']))
            parents.append((identity['ppid'],identity['parent_group_leader_start_boottime_ns']))
            fork_creators+=event['fork_creator'] is not None
            if not (metadata['started_monotonic_ns']<=event['entry_monotonic_ns']<=event['return_monotonic_ns']<metadata['deadline_monotonic_ns'] and
                    metadata['started_boottime_ns']<=event['entry_boottime_ns']<=event['return_boottime_ns']<metadata['deadline_boottime_ns']):violations+=1
            fixture_targets+=any('/synthetic-'+name+'/nssdb' in event[key]['mount_relative_path'] for key in ('source_before','destination_before','target_after'))
    uid_counts.update(local_uids);operations.update(local_ops);deadlines+=violations
    row={'name':name,'allocated_seconds':allocated['duration'],'valid_handshake':bool(handshake and handshake['valid']),
         'workload_executed':execution['workload_executed'],'executed_mutation_routes':len(execution.get('outcomes',[])),
         'collector_exit':execution['collector_exit'],'control_case_passed':execution.get('case_passed'),
         'events':event_count,'operation_counts':local_ops,'uid_counts':local_uids,'deadline_violations':violations,
         'synthetic_target_event_candidates':fixture_targets,'before':before,'after':after,'receipt':receipt}
    attempts.append(row)
    destination=HERE/'receipts'/name
    destination.mkdir(mode=0o700,parents=True,exist_ok=True)
    os.chown(HERE/'receipts',1000,1000);os.chown(destination,1000,1000)
    for directory in (root,capture):
        for path in directory.glob('*.json'):save(destination/path.name,load(path))
    for path in root.glob('*.log'):
        target=destination/path.name
        fd=os.open(target,os.O_CREAT|os.O_EXCL|os.O_WRONLY|os.O_NOFOLLOW,0o600);os.fchown(fd,1000,1000)
        with os.fdopen(fd,'wb') as stream:stream.write(path.read_bytes())
summary={'schema':'story117-nss-inventory-retry-v1','frozen':c.FROZEN,'qualification':'incomplete','complete':False,'global_immutability_proven':False,
         'previous_capture_seconds':553,'additional_allocated_capture_seconds':budget['allocated_seconds'],
         'aggregate_allocated_capture_seconds':553+budget['allocated_seconds'],'aggregate_limit_seconds':900,
         'valid_readiness_handshakes':sum(a['valid_handshake'] for a in attempts),'executed_fixture_mutation_routes':sum(a['executed_mutation_routes'] for a in attempts),
         'new_total_events':sum(a['events'] for a in attempts),'new_operation_counts':operations,'new_uid_counts':uid_counts,
         'new_nonleader_thread_records':threads,'new_raw_fork_records_with_creator_identity':fork_creators,
         'ambient_parent_identity_joins':sum(parent in identities for parent in parents),'deadline_violations':deadlines,
         'unsupported':c.UNSUPPORTED,'readiness_blocker':'Host process/descriptor churn remains: each full fixture before snapshot retains 3..7 errors. Substantial ambient submission/delivery losses also prevent complete accounting.',
         'fixture_scope':'Existing main/private thread descriptors and mapping prepared outside real NSS; no post-handshake mutation suite executed.',
         'attempts':attempts,'runtime_root':str(RUNTIME)}
save(HERE/'summary.json',summary);save(HERE/'budget.json',budget);save(HERE/'snapshot-only-result.json',load(RUNTIME/'snapshot-only-result.json'))
# Inspect only exact aliases that these retained receipts identify as owned.
aliases=set();live=[]
for attempt in attempts:
    aliases.update(attempt['receipt']['kretprobe_profile_before_detach'].get('owned_rows',{}))
    identity=load(RUNTIME/attempt['name']/'execution.json');pid=identity['collector_pid']
    try:
        current=c.proc_stat(Path('/proc',str(pid),'stat').read_text())
        if current['start_ticks']==identity.get('collector_start_ticks',identity.get('start_ticks')):live.append(pid)
    except FileNotFoundError:pass
profile=Path('/sys/kernel/tracing/kprobe_profile').read_text();definitions=Path('/sys/kernel/tracing/kprobe_events').read_text()
remaining=[name for name in aliases if any(line.split() and line.split()[0].split('/')[-1]==name for line in definitions.splitlines()) or any(line.split() and line.split()[0]==name for line in profile.splitlines())]
cleanup={'owned_alias_count':len(aliases),'remaining_exact_owned_aliases':remaining,'live_bound_collectors':live,
         'remaining_synthetic_directories':[str(p) for p in RUNTIME.glob('synthetic-*')],
         'prepared_headers_retained':Path('/lib/modules',os.uname().release,'build/include/generated/autoconf.h').is_file(),
         'global_tracing_cleanup_performed':False}
save(HERE/'cleanup-verification.json',cleanup)
print(json.dumps({'attempts':len(attempts),'events':summary['new_total_events'],'uids':uid_counts,'deadlines':deadlines,'valid_handshakes':summary['valid_readiness_handshakes'],'budget':budget['allocated_seconds'],'cleanup':cleanup},indent=2))
