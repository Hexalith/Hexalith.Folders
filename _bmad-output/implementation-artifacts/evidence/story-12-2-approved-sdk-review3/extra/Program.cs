using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;
var store=new InMemoryReadModelStore();
var scope=new SharedProjectionScope("statestore","tenant","widget","index",["writer"]);
var c=new SharedProjectionEpochCoordinator(store,store);
var bs=new ReadModelBatchScope("statestore","tenant","widget","index","index","scratch");
var b=new ReadModelBatch(bs,[ReadModelBatchOperation.Write("private",new JsonObject{["padding"]=new string('x',20000)},ReadModelBatchConcurrency.CreateOnly)]){RetainManifestForRecovery=true};
string manifest=ReadModelBatchKeys.ManifestKey(bs.ComputeScopeHash());
store.SeedRaw("statestore",manifest,JsonNode.Parse(ReadModelBatchFingerprint.BuildCanonicalManifest(b))!.AsObject());
Console.WriteLine("orphan known recovery: "+await store.RecoverPendingAsync(bs,bs.ComputeMarkerKey()));
await c.ReclaimScratchReceiptAsync(scope,"scratch");
Console.WriteLine("manifest survives cleanup: "+(store.SnapshotRaw("statestore",manifest)!=null));
var future=DateTimeOffset.UtcNow.AddSeconds(1);
var write=new ReadModelBatch(bs with{BatchId="expiring"},[ReadModelBatchOperation.WriteExpiring("model",new JsonObject{["count"]=1},ReadModelBatchConcurrency.LastWrite,DateTimeOffset.UtcNow.AddDays(-1))]);
// A compacted physical wrapper may remain within provider rounding, or indefinitely in the fake.
store.SeedRaw("statestore","model",JsonNode.Parse(ReadModelBatchExpiringValue.Encode("{\"count\":1}"u8.ToArray(),DateTimeOffset.UtcNow.AddDays(-1)).Span)!.AsObject());
Console.WriteLine("expired read: "+(await store.GetAsync<JsonObject>("statestore","model")).Value);
try{await ReadModelWritePolicy.UpdateAsync<JsonObject>(store,"statestore","model",_=>new JsonObject{["count"]=2});Console.WriteLine("single save succeeded");}catch(Exception ex){Console.WriteLine("single save: "+ex.Message);}
var guarded=new ReadModelBatch(bs with{BatchId="expired-guard"},[ReadModelBatchOperation.WriteExpiring("model",new JsonObject{["count"]=3},ReadModelBatchConcurrency.Match("stale-etag"),DateTimeOffset.UtcNow.AddDays(-1)),ReadModelBatchOperation.Write("checkpoint",new JsonObject{["sequence"]=4},ReadModelBatchConcurrency.LastWrite)]);
Console.WriteLine("expired stale guard: "+await store.ExecuteAsync(guarded));
var foreign=new ReadModelBatch(bs with{BatchId="foreign"},[ReadModelBatchOperation.Write("foreign-target",new JsonObject{["count"]=9},ReadModelBatchConcurrency.LastWrite)]);
Console.WriteLine("foreign staged: "+await store.StageAsync(foreign));
var expiredForeign=new ReadModelBatch(bs with{BatchId="expired-foreign"},[ReadModelBatchOperation.WriteExpiring("foreign-target",new JsonObject{["count"]=3},ReadModelBatchConcurrency.LastWrite,DateTimeOffset.UtcNow.AddDays(-1)),ReadModelBatchOperation.Write("other-checkpoint",new JsonObject{["sequence"]=4},ReadModelBatchConcurrency.LastWrite)]);
Console.WriteLine("expired foreign Execute: "+await store.ExecuteAsync(expiredForeign));
Console.WriteLine("expired foreign marker: "+store.Snapshot<ReadModelBatchMarker>("statestore",expiredForeign.Scope.ComputeMarkerKey())!.Status);
Console.WriteLine("other-checkpoint pending: "+store.HasPendingEnvelope("statestore","other-checkpoint"));
