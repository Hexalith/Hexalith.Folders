using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Tests.Projections;
using Microsoft.Extensions.Logging.Abstractions;

using var client = new RecordingDaprClient { PersistTypedState = true };
var store = new DaprReadModelStore(client);
var scope = new SharedProjectionScope("statestore", "tenant", "widget", "legacy-overlap", ["writer"]);
var coordinator = new SharedProjectionEpochCoordinator(store, store);
await coordinator.RegisterWriterAsync(scope, "writer");
await coordinator.BeginAsync(scope, "legacy", "inventory", new Dictionary<string,long>());
var entry = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey);
var old = new ReadModelBatch(new(scope.StoreName,scope.TenantId,scope.Domain,scope.Family,scope.Family,"scratch"),
[ReadModelBatchOperation.Write("private",new JsonObject{["count"]=1},ReadModelBatchConcurrency.CreateOnly),
ReadModelBatchOperation.Write(scope.StateKey,entry.Value!,ReadModelBatchConcurrency.Match(entry.ETag!))]);
byte[]? opened=null;
bool raced=false;
client.BeforeTrySaveByteState=(key,bytes)=> {
 if(raced || key!=old.Scope.ComputeMarkerKey())return;
 var marker=ReadModelBatchMarker.ReadProof(bytes);
 if(marker?.Status!=ReadModelBatchMarkerStatus.Committed || marker.Fingerprint!=ReadModelBatchFingerprint.Compute(old))return;
 raced=true;client.BeforeTrySaveByteState=null;
 var replacement=new SharedProjectionEpochCoordinator(store,store);
 Console.WriteLine("recovery retry = "+replacement.TryWriteScratchAsync(scope,"legacy","private",new JsonObject{["count"]=1},"scratch",ReadModelBatchConcurrency.CreateOnly).GetAwaiter().GetResult());
 Console.WriteLine("replacement = "+replacement.TryWriteScratchAsync(scope,"legacy","private",new JsonObject{["count"]=1},"scratch",ReadModelBatchConcurrency.CreateOnly).GetAwaiter().GetResult());
 replacement.AbortAsync(scope,"legacy",(_,_,_)=>Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([])).GetAwaiter().GetResult();
 opened=client.ByteStoreValue(scope.StateKey);
 Console.WriteLine("before old CAS = "+store.GetAsync<SharedProjectionEpochState>(scope.StoreName,scope.StateKey).GetAwaiter().GetResult().Value!.Phase);
};
var protocol=new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client,scope.StoreName),new(),NullLogger.Instance);
var result=await protocol.ExecuteAsync(old,null,CancellationToken.None);
Console.WriteLine("old result = "+result.Status+" / "+result.ConflictKind+" / "+result.RecoveryReason);
Console.WriteLine("raced = "+raced+"; exact fence preserved = "+(opened is not null && client.ByteStoreValue(scope.StateKey).AsSpan().SequenceEqual(opened)));
Console.WriteLine("after old CAS = "+(await store.GetAsync<SharedProjectionEpochState>(scope.StoreName,scope.StateKey)).Value!.Phase);
