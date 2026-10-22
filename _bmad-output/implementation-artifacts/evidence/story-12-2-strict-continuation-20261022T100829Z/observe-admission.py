from pathlib import Path
import datetime, hashlib, json, re, subprocess, time

out = Path(__file__).resolve().parent
snapshots = out / 'admission-snapshots'
snapshots.mkdir(exist_ok=True)
# Read only disposable fixture keys. Export selected status fields, never replay bodies,
# raw idempotency keys, protected digests, credentials or arbitrary persisted values.
script = """
local c='0'; local a={};
repeat
 local r=redis.call('SCAN',c,'COUNT',1000); c=r[1];
 for _,k in ipairs(r[2]) do
  if (string.find(k,'IdempotencyAdmissionActor',1,true) and string.sub(k,-9)=='admission') or string.sub(k,-7)==':status' then
   if redis.call('TYPE',k).ok=='hash' then
    local raw=redis.call('HGET',k,'data');
    if raw then
     local ok,v=pcall(cjson.decode,raw);
     if ok and type(v)=='table' then
      local row={key=k,kind=string.sub(k,-7)==':status' and 'command-status' or 'admission'};
      local fields={'SchemaVersion','State','TenantPartition','FirstConsumedAt','LastObservedAt','ReplayExpiresAt','FencingToken','ExecutionMessageId','ExecutionCorrelationId','Status','Timestamp','AggregateId','EventCount','RejectionEventType','FailureReason','TimeoutDuration','MessageId','CorrelationId','Retryable','RecoveryReasonCode','DrainAttemptCount','CommittedEventSequence','Domain'};
      for _,f in ipairs(fields) do
       local camel=string.lower(string.sub(f,1,1))..string.sub(f,2);
       if v[f]~=nil then row[f]=v[f] elseif v[camel]~=nil then row[f]=v[camel] end;
      end;
      table.insert(a,row);
     end;
    end;
   end;
  end;
 end;
until c=='0'; return cjson.encode(a);
"""
while not (out / 'stop-admission-observer').exists():
    log = out / 'strict-live.log'
    ids = set(re.findall(r'app_id=(?:folders|eventstore|tenants|memories)[^ ]*-([0-9a-f]{16})(?:[ ,]|$)', log.read_text())) if log.exists() else set()
    names = subprocess.check_output(['docker', 'ps', '--format', '{{.Names}}'], text=True).splitlines()
    for ident in ids:
        container = 'folders-durability-' + ident
        if container not in names:
            continue
        started = datetime.datetime.now(datetime.timezone.utc).isoformat()
        command = ['docker', 'exec', container, 'redis-cli', '--raw', 'EVAL', script, '0']
        result = subprocess.run(command, text=True, capture_output=True, timeout=20)
        rows = json.loads(result.stdout) if result.returncode == 0 and result.stdout.strip().startswith('[') else []
        stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
        record = dict(started_utc=started, container=container, exit_code=result.returncode,
                      rows=rows, stderr=result.stderr, query_sha256=hashlib.sha256(script.encode()).hexdigest())
        (snapshots / (stamp + '.json')).write_text(json.dumps(record, indent=2) + '\n')
    time.sleep(5)
print('Read-only admission observer stopped.')
