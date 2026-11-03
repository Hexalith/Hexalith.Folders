"""Extract only closed dispatch timing fields from a private structured runtime log."""
from pathlib import Path
import json,re,sys,collections
root=Path(__file__).parent
label=sys.argv[1]
assert re.fullmatch(r"[a-z0-9-]+",label)
source=root/(label+"-private.log");rows=[]
pattern=re.compile(r"Stage=(\w+), Status=(\w+), ElapsedMilliseconds=(\d+), RequestCanceled=(\w+), OperationCanBeCanceled=(\w+), OperationCanceled=(\w+), TraceId=([a-f0-9]+), SpanId=([a-f0-9]+), AttemptId=([a-f0-9]+)")
for line in source.read_text(errors="replace").splitlines():
 if "Dispatch await:" not in line:continue
 for start in (m.start() for m in re.finditer(r"\{",line)):
  try:
   obj,end=json.JSONDecoder().raw_decode(line[start:])
   if not isinstance(obj,dict) or not isinstance(obj.get("Message"),str):continue
   match=pattern.search(obj["Message"])
   if not match:continue
   timestamp=re.search(r"\d{4}-\d{2}-\d{2}T[0-9:.]+Z",line[:start])
   rows.append({"timestamp":timestamp[0] if timestamp else None,"category":obj.get("Category"),"stage":match[1],"status":match[2],"ms":int(match[3]),"requestCanceled":match[4],"opCanceled":match[6],"traceId":match[7],"spanId":match[8],"attemptId":match[9]});break
  except (ValueError,json.JSONDecodeError):pass
output=root/(label+"-stages-private.json");output.write_text(json.dumps(rows,indent=2)+"\n");output.chmod(0o600)
counts=collections.Counter((r["stage"],r["status"]) for r in rows)
summary={"records":len(rows),"counts":{str(k):v for k,v in sorted(counts.items())},"longestCompletedOrFailed":sorted((r for r in rows if r["status"]!="Started"),key=lambda r:r["ms"],reverse=True)[:15]}
print(json.dumps(summary))
